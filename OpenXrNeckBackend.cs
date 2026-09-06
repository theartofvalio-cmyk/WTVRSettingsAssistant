using Microsoft.Win32;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace WTVRSettingsAssistant;

internal sealed class OpenXrNeckBackend : IDisposable
{
    private const string MappingName = "XRNeckSaferSHM";
    private const int MappingSize = 80;
    private readonly string _manifestPath;
    private readonly MemoryMappedFile _mapping;
    private readonly MemoryMappedViewAccessor _accessor;
    private long _lastTick;
    private float _filteredYaw;
    private float _filteredPitch;
    private bool _yawEngaged;
    private bool _pitchEngaged;
    private float _lastObservedYaw;
    private float _lastObservedPitch;
    public bool HasLiveTelemetry { get; private set; }
    public string RegistrationError { get; private set; } = "";

    public void UpdateMotion(NeckAssistSettings settings)
    {
        long now = System.Diagnostics.Stopwatch.GetTimestamp();
        float dt = _lastTick == 0 ? 0.016f : Math.Clamp((float)(now - _lastTick) / System.Diagnostics.Stopwatch.Frequency, 0.001f, 0.1f);
        _lastTick = now;
        float yaw = _accessor.ReadSingle(0);
        float pitch = _accessor.ReadSingle(4);
        UpdateEngagement(ref _yawEngaged, yaw, settings.StartAngle, settings.ReturnAngle, settings.Enabled);
        UpdateEngagement(ref _pitchEngaged, pitch, settings.PitchStartAngle, settings.PitchReturnAngle, settings.Enabled && settings.PitchEnabled);
        float targetYaw = _yawEngaged ? settings.YawBezier.Evaluate(yaw, settings.StartAngle, settings.MaximumViewAngle, 110, settings.YawCurvature, settings.NaturalRearView, settings.YawNaturalResumeAngle) - yaw : 0;
        float targetPitch = _pitchEngaged ? settings.PitchBezier.Evaluate(pitch, settings.PitchStartAngle, settings.PitchMaximumViewAngle, 80, settings.PitchCurvature, settings.NaturalRearView, settings.PitchNaturalResumeAngle) - pitch : 0;
        _filteredYaw = Stabilize(_filteredYaw, targetYaw, dt);
        _filteredPitch = Stabilize(_filteredPitch, targetPitch, dt);
        if (!settings.Enabled) _filteredYaw = _filteredPitch = 0;
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

    private static float Stabilize(float previous, float target, float dt)
    {
        if (Math.Abs(target - previous) < 0.04f) return previous;
        float smoothed = previous + (target - previous) * (1 - MathF.Exp(-dt / 0.045f));
        float maxStep = 540f * dt;
        return previous + Math.Clamp(smoothed - previous, -maxStep, maxStep);
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

    public OpenXrNeckBackend(string appFolder)
    {
        _manifestPath = Path.Combine(appFolder, "NeckAssist", "OpenXR", "XR_APILAYER_NOVENDOR_XRNeckSafer.json");
        _mapping = MemoryMappedFile.CreateOrOpen(MappingName, MappingSize);
        _accessor = _mapping.CreateViewAccessor(0, MappingSize, MemoryMappedFileAccess.ReadWrite);
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
                using RegistryKey? key = root.OpenSubKey(@"SOFTWARE\Khronos\OpenXR\1\ApiLayers\Implicit");
                return key?.GetValue(_manifestPath) is int value && value == 0;
            }
            catch { return false; }
        }
    }

    public void Apply(NeckAssistSettings settings)
    {
        SetRegistered(settings.Enabled);
        _accessor.Read(0, out SharedData data);
        data.LeftStartAt = settings.StartAngle;
        data.RightStartAt = settings.StartAngle;
        data.UpStartAt = settings.PitchStartAngle;
        data.DownStartAt = settings.PitchStartAngle;
        data.LeftMultiplier = CalculateMultiplier(settings.StartAngle, settings.MaximumViewAngle);
        data.RightMultiplier = data.LeftMultiplier;
        data.UpMultiplier = CalculateMultiplier(settings.PitchStartAngle, settings.PitchMaximumViewAngle, 80);
        data.DownMultiplier = data.UpMultiplier;
        data.UseLinearRotation = false;
        data.UseLinearPitchRotation = false;
        data.HoldLinearRotation = !settings.Enabled;
        data.HoldLinearPitchRotation = !settings.Enabled || !settings.PitchEnabled;
        _accessor.Write(0, ref data);
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
        _filteredYaw = _filteredPitch = 0;
        _accessor.Read(0, out SharedData data);
        data.ResetHmdOrientation = true;
        _accessor.Write(0, ref data);
    }

    public void SetHeld(bool held, bool pitchEnabled)
    {
        _accessor.Read(0, out SharedData data);
        data.HoldLinearRotation = held;
        data.HoldLinearPitchRotation = held || !pitchEnabled;
        _accessor.Write(0, ref data);
    }

    private void SetRegistered(bool enabled)
    {
        if (!FilesAvailable) return;
        try
        {
            using RegistryKey root = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
            using RegistryKey key = root.CreateSubKey(@"SOFTWARE\Khronos\OpenXR\1\ApiLayers\Implicit", true);
            key.SetValue(_manifestPath, enabled ? 0 : 1, RegistryValueKind.DWord);
            key.Flush();
            RegistrationError = "";
        }
        catch (Exception ex) { RegistrationError = ex.Message; }
    }

    private static float CalculateMultiplier(int startAngle, int maximumViewAngle, int assumedPhysicalLimit = 110)
    {
        int physicalSpan = Math.Max(1, assumedPhysicalLimit - startAngle);
        int extraRotation = Math.Max(0, maximumViewAngle - assumedPhysicalLimit);
        return extraRotation / (float)physicalSpan;
    }

    public void Dispose()
    {
        _accessor.Dispose();
        _mapping.Dispose();
    }
}
