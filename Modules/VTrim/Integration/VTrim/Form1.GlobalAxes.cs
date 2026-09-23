namespace HOTASTrimUtility;

public partial class Form1
{
    private sealed class GlobalAxes
    {
        // Legacy only: v2.2 stores trim bindings in Default/aircraft controls.
        public Dictionary<string, SavedActionBinding>? Bindings { get; set; }
        public SavedAxisSource? Roll { get; set; }
        public SavedAxisSource? Pitch { get; set; }
        public SavedAxisSource? Rudder { get; set; }
        public bool InvertRoll { get; set; }
        public bool InvertPitch { get; set; }
        public bool InvertRudder { get; set; }
        public string DeviceGuid { get; set; } = "";
        public string DeviceName { get; set; } = "";
    }
    private GlobalAxes? _globalAxes;
    private HashSet<string> _favoriteAircraft = new();
    private void CaptureGlobalAxes()
    {
        _globalAxes = new GlobalAxes { Roll = SaveAxisSource(_rollAxis), Pitch = SaveAxisSource(_pitchAxis),
            Rudder = SaveAxisSource(_rudderAxis), InvertRoll = _invertRollBox.Checked,
            InvertPitch = _invertPitchBox.Checked, InvertRudder = _invertRudderBox.Checked,
            DeviceGuid = _selectedDeviceGuid?.ToString("D") ?? "", DeviceName = _selectedDeviceName };
    }
    private void ApplyGlobalAxes(SavedBindingsFile legacy)
    {
        // Migrate the first loaded legacy setup once; aircraft switches never replace hardware routing.
        _globalAxes ??= new GlobalAxes { Roll = legacy.RollAxis, Pitch = legacy.PitchAxis, Rudder = legacy.RudderAxis,
            InvertRoll = legacy.InvertRoll, InvertPitch = legacy.InvertPitch, InvertRudder = legacy.InvertRudder,
            DeviceGuid = legacy.SelectedDeviceGuid, DeviceName = legacy.SelectedDeviceName };
        _rollAxis = RestoreAxisSource(_globalAxes.Roll);
        _pitchAxis = RestoreAxisSource(_globalAxes.Pitch);
        _rudderAxis = RestoreAxisSource(_globalAxes.Rudder);
        _invertRollBox.Checked = _globalAxes.InvertRoll;
        _invertPitchBox.Checked = _globalAxes.InvertPitch;
        _invertRudderBox.Checked = _globalAxes.InvertRudder;
        _selectedDeviceName = _globalAxes.DeviceName;
        _selectedDeviceGuid = Guid.TryParse(_globalAxes.DeviceGuid, out var guid) ? guid : null;
    }
    private Action? _openProfiles;
    private Action? _openTrimDashboard;
    private Action? _openCurves;
    public void OpenAircraftProfiles() => _openProfiles?.Invoke();
    public void OpenAircraftProfileEditor(string profileName)
    {
        _openProfiles?.Invoke();
        OpenAircraftControlEditor(profileName);
    }
}
