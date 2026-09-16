using Microsoft.Win32;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace WTVRSettingsAssistant;

internal sealed class OpenXrNeckBackend : IDisposable
{
    private const string MappingName = "XRNeckSaferSHM";
    private const int MappingSize = 80;
    private const string LayerRegistryPath = @"SOFTWARE\Khronos\OpenXR\1\ApiLayers\Implicit";
    private const string ManifestFileName = "XR_APILAYER_NOVENDOR_XRNeckSafer.json";
    private const string LayerName = "XR_APILAYER_NOVENDOR_XRNeckSafer";
    private readonly string _manifestPath;
    private readonly bool _offlinePreview;
    private readonly MemoryMappedFile _mapping;
    private readonly MemoryMappedViewAccessor _accessor;
    private long _lastTick;
    private readonly object _motionLock = new();
    private readonly System.Threading.Timer _motionTimer;
    private NeckAssistSettings? _motionSettings;
    private bool _motionActive, _disposed;
    private long _lastCommand;
    private float _yawVelocity, _pitchVelocity;
    private bool _awaitingRecenter, _requireActivationRelease;
    private float _filteredYaw;
    private float _filteredPitch;
    private bool _yawEngaged;
    private bool _pitchEngaged;
    private float _pressDirection = 1;
    private bool _simpleDirectionLatched;
    private float _lastObservedYaw;
    private float _lastObservedPitch;
    public bool HasLiveTelemetry { get; private set; }
    public string RegistrationError { get; private set; } = "";

    public void UpdateMotion(NeckAssistSettings settings, bool runtimeActive)
    {
        // Own the snapshot: UI curve edits must not mutate the worker's settings.
        var snapshot = System.Text.Json.JsonSerializer.Deserialize<NeckAssistSettings>(
            System.Text.Json.JsonSerializer.Serialize(settings))!;
        lock (_motionLock)
        {
            if (_disposed) return;
            _motionSettings = snapshot;
            _motionActive = runtimeActive;
            _lastCommand = System.Diagnostics.Stopwatch.GetTimestamp();
        }
    }

    private void MotionTick(object? state)
    {
        lock (_motionLock)
        {
            if (_disposed || _motionSettings is null) return;
            double age = (System.Diagnostics.Stopwatch.GetTimestamp() - _lastCommand) /
                (double)System.Diagnostics.Stopwatch.Frequency;
            UpdateMotionCore(_motionSettings, _motionActive && age < 0.25);
        }
    }

    private void UpdateMotionCore(NeckAssistSettings settings, bool runtimeActive)
    {
        long now = System.Diagnostics.Stopwatch.GetTimestamp();
        float dt = _lastTick == 0 ? 0.016f : Math.Clamp((float)(now - _lastTick) / System.Diagnostics.Stopwatch.Frequency, 0.001f, 0.1f);
        _lastTick = now;
        // XRNeckSafer publishes no poses until it acknowledges a center request.
        // It clears this byte on each load, including a restarted VR game.
        if (settings.Enabled && !_accessor.ReadBoolean(61))
        {
            if (!_awaitingRecenter) ResetCenterState();
            _accessor.Write(56, true);
        }
        if (_awaitingRecenter)
        {
            if (_accessor.ReadBoolean(56) || !_accessor.ReadBoolean(61)) return;
            _awaitingRecenter = false;
            _lastTick = now;
        }
        if (_requireActivationRelease)
        {
            if (!runtimeActive) _requireActivationRelease = false;
            else return;
        }
        float yaw = _accessor.ReadSingle(0);
        float pitch = _accessor.ReadSingle(4);
        if (!float.IsFinite(yaw) || !float.IsFinite(pitch))
        {
            _filteredYaw = _filteredPitch = _yawVelocity = _pitchVelocity = 0;
            _yawEngaged = _pitchEngaged = _simpleDirectionLatched = false;
            _accessor.Write(8, 0f); _accessor.Write(12, 0f);
            return;
        }
        bool pressMode = settings.MovementMode == "Simple";
        if (pressMode && !runtimeActive) _simpleDirectionLatched = false;
        if (pressMode && runtimeActive && !_simpleDirectionLatched && Math.Abs(yaw) >= settings.SimpleDeadzoneAngle)
        {
            _pressDirection = Math.Sign(yaw == 0 ? 1 : yaw);
            _simpleDirectionLatched = true;
        }
        UpdateEngagement(ref _yawEngaged, yaw, settings.StartAngle, settings.ReturnAngle, settings.Enabled && runtimeActive && !pressMode);
        UpdateEngagement(ref _pitchEngaged, pitch, settings.PitchStartAngle, settings.PitchReturnAngle, settings.Enabled && runtimeActive && !pressMode && settings.PitchEnabled);
        float targetYaw = pressMode ? (runtimeActive && _simpleDirectionLatched ? _pressDirection * settings.PressRotationAngle : 0) :
            (_yawEngaged ? settings.YawBezier.Evaluate(yaw, settings.StartAngle, settings.MaximumViewAngle, 110, settings.YawCurvature, settings.NaturalRearView, settings.YawNaturalResumeAngle) - yaw : 0);
        float targetPitch = !pressMode && _pitchEngaged ? settings.PitchBezier.Evaluate(pitch, settings.PitchStartAngle, settings.PitchMaximumViewAngle, 80, settings.PitchCurvature, settings.NaturalRearView, settings.PitchNaturalResumeAngle) - pitch : 0;
        float transitionTau = 0.52f - settings.TransitionSpeed / 100f * 0.46f;
        _filteredYaw = Stabilize(_filteredYaw, targetYaw, ref _yawVelocity, dt, transitionTau);
        _filteredPitch = Stabilize(_filteredPitch, targetPitch, ref _pitchVelocity, dt, transitionTau);
        if (!settings.Enabled) _filteredYaw = _filteredPitch = _yawVelocity = _pitchVelocity = 0;
        // The native layer supports externally supplied offsets when its built-in
        // linear mapping is disabled. Only write our fields, preserving telemetry.
        _accessor.Write(8, _filteredYaw * MathF.PI / 180);
        _accessor.Write(12, _filteredPitch * MathF.PI / 180);
    }

    private static void UpdateEngagement(ref bool engaged, float angle, int start, int release, bool enabled)
    {
        if (!enabled) { engaged = false; return; }
        float absolute = Math.Abs(angle);
        if (!engaged && absolute >= start) engaged = true;
        else if (engaged && absolute <= Math.Min(release, start - 2)) engaged = false;
    }

    private static float Stabilize(float previous, float target, ref float velocity, float dt, float tau)
    {
        // Exact critically damped response: continuous velocity at activation and
        // reversal, independent of update cadence, with no angle quantization.
        float omega = 2f / Math.Max(0.06f, tau);
        float error = previous - target;
        float combined = velocity + omega * error;
        float decay = MathF.Exp(-omega * dt);
        velocity = (velocity - omega * combined * dt) * decay;
        return target + (error + combined * dt) * decay;
    }

    private static float Extra(float angle, int start, int maximum, int limit) =>
        MathF.Sign(angle) * Math.Max(0, Math.Abs(angle) - start) * CalculateMultiplier(start, maximum, limit);

    private static float Filter(float previous, float target, int amount, float dt)
    {
        if (amount <= 0) return target;
        float tau = 0.015f + amount / 100f * 0.22f;
        return previous + (target - previous) * (1 - MathF.Exp(-dt / tau));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SharedData
    {
        public float HmdYawAngle;
        public float HmdPitchAngle;
        public float YawOffset;
        public float PitchOffset;
        public float LateralOffset;
        public float LongitudinalOffset;
        public float RightMultiplier;
        public float LeftMultiplier;
        public float UpMultiplier;
        public float DownMultiplier;
        public int LeftStartAt;
        public int RightStartAt;
        public int UpStartAt;
        public int DownStartAt;
        [MarshalAs(UnmanagedType.Bool)] public bool ResetHmdOrientation;
        [MarshalAs(UnmanagedType.Bool)] public bool UseLinearRotation;
        [MarshalAs(UnmanagedType.Bool)] public bool UseLinearPitchRotation;
        [MarshalAs(UnmanagedType.Bool)] public bool HoldLinearRotation;
        [MarshalAs(UnmanagedType.Bool)] public bool HoldLinearPitchRotation;
        [MarshalAs(UnmanagedType.Bool)] public bool HasBeenCentered;
    }

    public OpenXrNeckBackend(string appFolder, bool offlinePreview = false, string? mappingName = null)
    {
        _offlinePreview = offlinePreview;
        _manifestPath = Path.GetFullPath(Path.Combine(appFolder, "NeckAssist", "OpenXR", ManifestFileName));
        _mapping = MemoryMappedFile.CreateOrOpen(mappingName ?? (offlinePreview ? "NeckPreview-" + Guid.NewGuid() : MappingName), MappingSize);
        _accessor = _mapping.CreateViewAccessor(0, MappingSize, MemoryMappedFileAccess.ReadWrite);
        _motionTimer = new System.Threading.Timer(MotionTick, null, 4, 4);
    }

    public bool FilesAvailable => File.Exists(_manifestPath) &&
                                  File.Exists(Path.Combine(Path.GetDirectoryName(_manifestPath)!, "XR_APILAYER_NOVENDOR_XRNeckSafer.dll"));

    public bool IsRegistered
    {
        get
        {
            try
            {
                using RegistryKey root = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
                using RegistryKey? key = root.OpenSubKey(LayerRegistryPath);
                return key?.GetValue(_manifestPath) is int value && value == 0 && !HasOtherEnabledLayer(key);
            }
            catch { return false; }
        }
    }

    public void Apply(NeckAssistSettings settings)
    {
        SetRegistered(settings.Enabled);
        _accessor.Write(40, settings.StartAngle);
        _accessor.Write(44, settings.StartAngle);
        _accessor.Write(48, settings.PitchStartAngle);
        _accessor.Write(52, settings.PitchStartAngle);
        float yawMultiplier = CalculateMultiplier(settings.StartAngle, settings.MaximumViewAngle);
        float pitchMultiplier = CalculateMultiplier(settings.PitchStartAngle, settings.PitchMaximumViewAngle, 80);
        _accessor.Write(24, yawMultiplier);
        _accessor.Write(28, yawMultiplier);
        _accessor.Write(32, pitchMultiplier);
        _accessor.Write(36, pitchMultiplier);
        // Native C++ bool fields occupy one byte. Never rewrite native-owned poses
        // or its centering acknowledgement with a shared-structure snapshot.
        _accessor.Write(57, false);
        _accessor.Write(58, false);
        SetHeld(!settings.Enabled, settings.PitchEnabled);
    }

    public void DisableLayer()
    {
        lock (_motionLock)
        {
        _motionSettings = null;
        try
        {
            _filteredYaw = _filteredPitch = _yawVelocity = _pitchVelocity = 0;
            _yawEngaged = _pitchEngaged = false;
            _simpleDirectionLatched = false;
            _accessor.Write(8, 0f);
            _accessor.Write(12, 0f);
            SetHeld(true, false);
        }
        catch
        {
            // Shutdown must still unregister the API layer even if the shared
            // memory region is unavailable or already being torn down.
        }

        SetRegistered(false);
        }
    }

    public (float Yaw, float Pitch, float VirtualYaw) ReadTelemetry()
    {
        _accessor.Read(0, out SharedData data);
        if (float.IsFinite(data.HmdYawAngle) && float.IsFinite(data.HmdPitchAngle) &&
            (Math.Abs(data.HmdYawAngle - _lastObservedYaw) > 0.02f || Math.Abs(data.HmdPitchAngle - _lastObservedPitch) > 0.02f))
            HasLiveTelemetry = true;
        _lastObservedYaw = data.HmdYawAngle;
        _lastObservedPitch = data.HmdPitchAngle;
        return (data.HmdYawAngle, data.HmdPitchAngle, data.HmdYawAngle + data.YawOffset * 180f / MathF.PI);
    }

    public float ReadVirtualPitch() => _accessor.ReadSingle(4) + _accessor.ReadSingle(12) * 180f / MathF.PI;

    public void Recenter()
    {
        lock (_motionLock)
        {
        if (_disposed) return;
        ResetCenterState();
        _accessor.Write(56, true);
        }
    }

    private void ResetCenterState()
    {
        _filteredYaw = _filteredPitch = _yawVelocity = _pitchVelocity = 0;
        _yawEngaged = _pitchEngaged = _simpleDirectionLatched = false;
        _pressDirection = 1;
        _lastTick = 0;
        _awaitingRecenter = _requireActivationRelease = true;
        _accessor.Write(8, 0f);
        _accessor.Write(12, 0f);
    }

    public void SetHeld(bool held, bool pitchEnabled)
    {
        _accessor.Write(59, held);
        _accessor.Write(60, held || !pitchEnabled);
    }

    private void SetRegistered(bool enabled)
    {
        if (_offlinePreview) return;
        if (!FilesAvailable) return;
        try
        {
            SetRegisteredInView(RegistryView.Registry64, enabled);
            SetRegisteredInView(RegistryView.Registry32, enabled);
            RegistrationError = "";
        }
        catch (Exception ex) { RegistrationError = ex.Message; }
    }

    private void SetRegisteredInView(RegistryView view, bool enabled)
    {
        using RegistryKey root = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, view);
        using RegistryKey key = root.CreateSubKey(LayerRegistryPath, true);
        RemoveOtherRegisteredLayers(key);
        key.SetValue(_manifestPath, enabled ? 0 : 1, RegistryValueKind.DWord);
        key.Flush();
    }

    private void RemoveOtherRegisteredLayers(RegistryKey key)
    {
        foreach (string valueName in key.GetValueNames())
        {
            if (SamePath(valueName, _manifestPath) || !LooksLikeNeckSaferManifest(valueName)) continue;
            key.DeleteValue(valueName, throwOnMissingValue: false);
        }
    }

    private bool HasOtherEnabledLayer(RegistryKey key)
    {
        foreach (string valueName in key.GetValueNames())
        {
            if (SamePath(valueName, _manifestPath) || !LooksLikeNeckSaferManifest(valueName)) continue;
            if (key.GetValue(valueName) is int value && value == 0) return true;
        }
        return false;
    }

    private static bool LooksLikeNeckSaferManifest(string valueName)
    {
        if (valueName.EndsWith(ManifestFileName, StringComparison.OrdinalIgnoreCase)) return true;
        try
        {
            return File.Exists(valueName) && File.ReadAllText(valueName).Contains(LayerName, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool SamePath(string left, string right)
    {
        try
        {
            return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static float CalculateMultiplier(int startAngle, int maximumViewAngle, int assumedPhysicalLimit = 110)
    {
        int physicalSpan = Math.Max(1, assumedPhysicalLimit - startAngle);
        int extraRotation = Math.Max(0, maximumViewAngle - assumedPhysicalLimit);
        return extraRotation / (float)physicalSpan;
    }

    public void Dispose()
    {
        lock (_motionLock)
        {
        _disposed = true;
        _motionTimer.Dispose();
        _accessor.Dispose();
        _mapping.Dispose();
        }
    }
}
