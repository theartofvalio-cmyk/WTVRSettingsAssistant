using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using Vortice.DirectInput;
using DirectInputState = Vortice.DirectInput.JoystickState;

namespace HOTASTrimUtility;

[DesignerCategory("Code")]
public partial class Form1 : Form
{
    private enum TrimAction
    {
        NoseDown,
        NoseUp,
        RollLeft,
        RollRight,
        RudderLeft,
        RudderRight,
        StoreCurrentTrim,
        ResetAll,
        ToggleInstructor
    }

    private enum InputKind
    {
        Button,
        Pov
    }

    private enum PovDirection
    {
        Center,
        Up,
        UpRight,
        Right,
        DownRight,
        Down,
        DownLeft,
        Left,
        UpLeft
    }

    private enum AxisTarget
    {
        Roll,
        Pitch,
        Rudder
    }

    private sealed class PhysicalInput
    {
        public Guid DeviceGuid { get; init; }
        public string DeviceName { get; init; } = string.Empty;
        public InputKind Kind { get; init; }
        public int Index { get; init; }
        public PovDirection PovDirection { get; init; }

        public string CompactName =>
            Kind == InputKind.Button
                ? $"Btn {Index + 1}"
                : $"POV{Index + 1} {FormatPovDirection(PovDirection)}";

        public string FullName => $"{DeviceName} - {CompactName}";
    }

    private sealed class ActionBinding
    {
        public required PhysicalInput Trigger { get; init; }
        public PhysicalInput? Modifier { get; init; }

        public string CompactDisplay =>
            Modifier is null
                ? Trigger.CompactName
                : $"{Modifier.CompactName} + {Trigger.CompactName}";

        public string FullDisplay =>
            Modifier is null
                ? Trigger.FullName
                : $"Hold {Modifier.FullName}, then press {Trigger.FullName}";
    }

    private sealed class SavedBindingsFile
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("D");
        public string? AircraftId { get; set; }
        public int Version { get; set; } = 10;
        public string ProfileName { get; set; } = string.Empty;
        public string AircraftType { get; set; } = "Prop Plane";

        public Dictionary<string, SavedActionBinding> Bindings { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);

        public SavedAxisSource? RollAxis { get; set; }
        public SavedAxisSource? PitchAxis { get; set; }
        public SavedAxisSource? RudderAxis { get; set; }

        public bool InvertRoll { get; set; }
        public bool InvertPitch { get; set; }
        public bool InvertRudder { get; set; }

        public decimal PitchStep { get; set; } = 0.50M;
        public decimal RollStep { get; set; } = 0.50M;
        public decimal RudderStep { get; set; } = 0.50M;

        // Legacy fields kept so older profile files remain readable. VTrim
        // now sends output whenever vJoy is connected and uses the explicit
        // Recenter controls in Physical Axis Routing.
        public bool SendToVJoy { get; set; } = true;
        public string SelectedDeviceGuid { get; set; } = string.Empty;
        public string SelectedDeviceName { get; set; } = string.Empty;

        // ON: unassigned directions still use their normal standalone trim
        // binding while a configured modifier is held.
        // OFF: any configured modifier blocks all standalone trim bindings.
        public bool UniversalTriggerPassthrough { get; set; }

        // ON enables chord-lock input context:
        // an unassigned game button pressed first protects the whole chord.
        // Trim triggers cannot override it. Only a configured VTrim modifier
        // can explicitly take VTrim context from an old latching switch.
        public bool BlockUnassignedInputChords { get; set; } = true;

        public bool AutoCenterAxesOnVJoyConnect { get; set; }
        public bool RepeatWhileHeld { get; set; } = true;
        public int HeldTrimRate { get; set; } = 7;

        // Pitch-only telemetry attitude hold; old trim-key fields are ignored on load.
        public bool InstructorModeEnabled { get; set; }
        public InstructorTuning Instructor { get; set; } = new();
        public AxisResponse RollResponse { get; set; } = new();
        public AxisResponse PitchResponse { get; set; } = new();
        public AxisResponse RudderResponse { get; set; } = new();

        // Adds calibrated Roll bias as Rudder trim increases.
        // The slider stores only the 0..100 gain magnitude. Direction is
        // learned during calibration so inverted game axes are supported.
        public bool HorizontalRudderAssist { get; set; }
        public int RudderRollCompensationPercent { get; set; } = 35;
        public int RudderRollCompensationDirection { get; set; } = -1;

        // Pitch correction is learned at the same time as Roll correction.
        // Old profiles default to 0%, so they remain behavior-compatible until
        // the user runs a new calibration.
        public int RudderPitchCompensationPercent { get; set; } = 0;
        public int RudderPitchCompensationDirection { get; set; } = 1;
    }

    private sealed class AppSettings
    {
        public GlobalAxes? PhysicalAxes { get; set; }
        public HashSet<string> FavoriteAircraft { get; set; } = new();
        public int Version { get; set; } = 2;
        public string OutputBackend { get; set; } = "vJoy";
        public bool AutoConnectToVJoy { get; set; } = true; // Legacy JSON name.
        public bool StartWithWindowsMinimized { get; set; } = false;

        // Used only to migrate settings written by VTrim 1.x.
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? StartWithWindows { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? StartMinimizedWithWindows { get; set; }
    }

    private sealed class SavedActionBinding
    {
        public SavedPhysicalInput? Trigger { get; set; }
        public SavedPhysicalInput? Modifier { get; set; }
    }

    private sealed class SavedPhysicalInput
    {
        public string DeviceGuid { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public int Index { get; set; }
        public string PovDirection { get; set; } = string.Empty;
    }

    private sealed class SavedAxisSource
    {
        public string DeviceGuid { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string AxisKey { get; set; } = string.Empty;
        public int CenterRaw { get; set; } = 32767;
        public bool SignedRange { get; set; }
    }

    private sealed class DetectedInput
    {
        public required InputDevice Device { get; init; }
        public required InputKind Kind { get; init; }
        public required int Index { get; init; }
        public PovDirection PovDirection { get; init; }

        public PhysicalInput ToPhysicalInput()
        {
            return new PhysicalInput
            {
                DeviceGuid = Device.Guid,
                DeviceName = Device.FriendlyName,
                Kind = Kind,
                Index = Index,
                PovDirection = PovDirection
            };
        }

        public string DisplayName => ToPhysicalInput().FullName;
    }

    private sealed class AxisSource
    {
        public Guid DeviceGuid { get; init; }
        public string DeviceName { get; init; } = string.Empty;
        public string AxisKey { get; init; } = string.Empty;
        public int CenterRaw { get; set; }
        public bool SignedRange { get; init; }

        public string DisplayName => $"{DeviceName} - {AxisKey}";
    }

    private sealed class AxisSample
    {
        public required string Key { get; init; }
        public required int RawValue { get; init; }
    }

    private sealed class InputDevice : IDisposable
    {
        public required Guid Guid { get; init; }
        public required string FriendlyName { get; init; }
        public required IDirectInputDevice8 Device { get; init; }

        public DirectInputState? PreviousState { get; set; }

        public override string ToString() => FriendlyName;

        public void Dispose()
        {
            try
            {
                Device.Unacquire();
            }
            catch
            {
                // The controller may already be disconnected.
            }

            Device.Dispose();
        }
    }

    private static readonly string[] ScalarAxisPropertyNames =
    {
        "X",
        "Y",
        "Z",
        "RotationX",
        "RotationY",
        "RotationZ"
    };

    private const int AxisDetectionThreshold = 3500;
    // Output selection is handled by IFlightOutput.
    private const string StartMinimizedArgument = "--minimized";
    private const string StartupRegistryPath =
        @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupRegistryValueName = "VTrimPreview";

    // Filters small physical sensor drift around the calibrated centre.

    private static readonly JsonSerializerOptions BindingsJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    // Portable profile storage.
    //
    // Profiles and keybinds are NOT embedded in VTrim and are NOT loaded
    // from older %LOCALAPPDATA% builds. A clean copy of the application
    // therefore starts with zero profiles and zero saved keybinds.
    private string SettingsDirectory =>
        _embeddedSettingsDirectory ?? (
        File.Exists(Path.Combine(AppContext.BaseDirectory, "installed.flag"))
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VTrimPreview")
            : AppContext.BaseDirectory);

    private string AppSettingsFilePath =>
        Path.Combine(
            SettingsDirectory,
            "VTrim.settings.json");

    private string ProfilesDirectory =>
        Path.Combine(
            SettingsDirectory,
            "Profiles");

    private string DefaultSetupFilePath => Path.Combine(SettingsDirectory, "VTrim.default-setup.json");

    private string ActiveProfileFilePath =>
        Path.Combine(
            ProfilesDirectory,
            "_active-profile.txt");

    private readonly Dictionary<Guid, InputDevice> _devices = new();
    private readonly Dictionary<TrimAction, ActionBinding> _bindings = new();

    private string _activeProfileName = string.Empty;
    private bool _switchingProfile;
    private string _selectedDeviceName = string.Empty;
    private readonly Dictionary<TrimAction, Button> _bindingButtons = new();
    private readonly Dictionary<TrimAction, DateTime> _trimHoldStartedTimes = new();
    private DateTime _lastTrimProcessingUtc = DateTime.UtcNow;

    // A trigger captured by a two-input combination remains exclusive until
    // that trigger is released. Example: Btn 4 + POV Right will not also
    // activate a single POV Right binding.
    private readonly Dictionary<string, PhysicalInput> _capturedCombinationTriggers = new();

    // Input context is locked by the FIRST non-trim button of a chord.
    //
    // Important distinction:
    // - A button used as a VTrim MODIFIER/lever may claim VTrim context.
    // - A button/POV used only as a trim TRIGGER can never steal context.
    // - An unassigned game button pressed first blocks VTrim triggers that
    //   follow it, so game chords such as Trigger 1 + POV Up remain native.
    //
    // A configured VTrim modifier is allowed to override an old latching
    // game context. This is the escape path for HOTAS toggle switches that
    // stay logically ON.
    private PhysicalInput? _inputContextButton;
    private bool _inputContextIsVTrimModifier;
    private bool _foreignChordSuppressed;

    private readonly Dictionary<(Guid DeviceGuid, string AxisKey), int> _axisDetectionBaseline = new();

    private readonly System.Windows.Forms.Timer _pollTimer = new()
    {
        Interval = 15
    };

    private readonly System.Windows.Forms.Timer _vJoyAutoConnectTimer = new()
    {
        Interval = 2500
    };

    private readonly System.Windows.Forms.Timer _warThunderTelemetryTimer = new()
    {
        Interval = 50
    };

    private readonly ToolTip _toolTip = new()
    {
        AutoPopDelay = 12000,
        InitialDelay = 250,
        ReshowDelay = 100
    };

    private readonly Dictionary<Control, float> _baseFontSizes = new();
    // Fonts inherited from a parent control are ambient objects and must never be
    // disposed by a child while responsive scaling is running. Keep ownership only
    // for fonts created by this scaler so resize/DPI changes remain stable.
    private readonly Dictionary<Control, Font> _responsiveOwnedFonts = new();
    private readonly Dictionary<TableLayoutPanel, (float[] Rows, float[] Columns)> _baseTableMetrics = new();
    private readonly Dictionary<Control, int> _baseTopDockHeights = new();
    private readonly Dictionary<Control, int> _responsiveScrollDesignHeights = new();
    private readonly Dictionary<Control, (Padding Margin, Padding Padding, Size Minimum, Rectangle LooseBounds)> _baseControlMetrics = new();
    private TableLayoutPanel _rootLayout = null!;
    private bool _applyingUiScale;
    private bool _loadingSavedSettings;
    private bool _loadingApplicationSettings;
    private readonly bool _startMinimizedRequested =
        Environment.GetCommandLineArgs().Any(
            argument =>
                string.Equals(
                    argument,
                    StartMinimizedArgument,
                    StringComparison.OrdinalIgnoreCase));

    private IDirectInput8? _directInput;
    private IFlightOutput? _vJoy; // Legacy name retained for UI regression tests.
    private bool _vJoyConnected;
    private bool _manualVJoyDisconnect;
    private int _remainingAutomaticVJoyConnectAttempts;

    private AxisSource? _rollAxis;
    private AxisSource? _pitchAxis;
    private AxisSource? _rudderAxis;

    private AxisTarget? _detectingAxis;
    private TrimAction? _captureAction;
    private PhysicalInput? _captureFirstInput;
    private bool _waitingForDeviceDetection;
    private Guid? _selectedDeviceGuid;

    private double _pitchTrim;
    private double _rollTrim;
    private double _rudderTrim;
    private double _automaticRudderRollCompensation;
    private double _automaticRudderPitchCompensation;

    private double _lastPhysicalRoll;
    private double _lastPhysicalPitch;
    private double _lastPhysicalRudder;

    private bool _instructorModeEnabled;
    private readonly StoreTrimController _storeTrimReturn = new();
    private AxisVector _currentManualCommand;
    private double _lastAxisSnapshotTime = double.NegativeInfinity;
    private bool _rollInputAvailable, _pitchInputAvailable, _rudderInputAvailable;
    private Button _storeTrimButton = null!;
    private bool _applicationClosing;
    private readonly bool _offlinePreview;

    private ComboBox _deviceBox = null!;
    private Button _autoDetectDeviceButton = null!;
    private Label _deviceStatusLabel = null!;

    private Button _rollAxisButton = null!;
    private Button _pitchAxisButton = null!;
    private Button _rudderAxisButton = null!;
    private Label _rollAxisLabel = null!;
    private Label _pitchAxisLabel = null!;
    private Label _rudderAxisLabel = null!;
    private CheckBox _invertRollBox = null!;
    private CheckBox _invertPitchBox = null!;
    private CheckBox _invertRudderBox = null!;

    private Label _vJoyStatusLabel = null!;
    private Button _vJoyConnectButton = null!;
    private CheckBox _autoConnectToVJoyBox = null!;
    private CheckBox _startWithWindowsMinimizedBox = null!;

    private Label _pitchValueLabel = null!;
    private Label _rollValueLabel = null!;
    private Label _rudderValueLabel = null!;
    private TrimBar _pitchBar = null!;
    private TrimBar _rollBar = null!;
    private TrimBar _rudderBar = null!;
    private StickAxisPreview _stickPreview = null!;
    private RudderAxisPreview _rudderPreview = null!;
    private Button _instructorModeButton = null!;

    private StepEditor _pitchStepBox = null!;
    private StepEditor _rollStepBox = null!;
    private StepEditor _rudderStepBox = null!;
    private CheckBox _repeatWhileHeldBox = null!;
    private RepeatSpeedSlider _repeatSpeedSlider = null!;
    private Label _repeatSpeedValueLabel = null!;
    private CheckBox _universalTriggerBox = null!;
    private CheckBox _unassignedInputGuardBox = null!;
    private CheckBox _horizontalRudderAssistBox = null!;
    private RepeatSpeedSlider _rudderRollCompensationSlider = null!;
    private Label _rudderRollCompensationValueLabel = null!;

    private RepeatSpeedSlider _rudderPitchCompensationSlider = null!;
    private Label _rudderPitchCompensationValueLabel = null!;

    private Button _horizontalRudderCalibrateButton = null!;
    private Label _horizontalRudderCalibrationStatusLabel = null!;

    private bool _horizontalRudderCalibrationArmed;
    private string _horizontalRudderCalibrationMessage = string.Empty;

    // +1 = Roll output follows Rudder trim sign.
    // -1 = Roll output uses the opposite sign.
    // Calibration learns this automatically from the actual stick/pedal
    // positions used by the pilot, so game-side axis inversion is OK.
    private int _rudderRollCompensationDirection = -1;
    private int _rudderPitchCompensationDirection = 1;

    private Label _lastInputLabel = null!;
    private Label _instructionLabel = null!;
    private Label _statusLabel = null!;

    private ComboBox _profileBox = null!;
    private ProfileTypeComboBox _profileTypeBox = null!;
    private Label _activeProfileLabel = null!;
    private Label _profileSummaryLabel = null!;
    private Button _deleteProfileButton = null!;
    private string _languageCode = "en";

    public Form1() : this(offlinePreview: false) { }

    // The UI regression harness can render the actual form without touching
    // saved profiles, Windows startup, hardware or game telemetry.
    internal Form1(bool offlinePreview) : this(offlinePreview, null, false) { }

    public Form1(string settingsDirectory, bool embedded) : this(false, settingsDirectory, embedded) { }

    private Form1(bool offlinePreview, string? settingsDirectory, bool embedded)
    {
        _embeddedMode = embedded;
        _embeddedSettingsDirectory = settingsDirectory;
        _offlinePreview = offlinePreview;
        // Suppress autosave callbacks until the runtime-built controls exist.
        _loadingSavedSettings = _loadingApplicationSettings = true;
        BuildInterface();
        if (offlinePreview) return;
        _loadingSavedSettings = _loadingApplicationSettings = false;
        LoadApplicationSettings();
        LoadBindings();
        if (_embeddedMode)
        {
            _manualVJoyDisconnect = !_autoConnectToVJoyBox.Checked;
            _startWithWindowsMinimizedBox.Visible = false;
            MinimumSize = Size.Empty;
        }

        _pollTimer.Tick += PollTimer_Tick;
        _vJoyAutoConnectTimer.Tick +=
            VJoyAutoConnectTimer_Tick;

        _warThunderTelemetryTimer.Tick +=
            WarThunderTelemetryTimer_Tick;

        Shown += (_, _) =>
        {
            RefreshDevices();
            _pollTimer.Start();
            _warThunderTelemetryTimer.Start();
            StartAutomaticVJoyConnection();

            if (!_embeddedMode && _startMinimizedRequested)
            {
                BeginInvoke(
                    () => WindowState =
                        FormWindowState.Minimized);
            }
        };

        FormClosed += Form1_FormClosed;
        KeyDown += Form1_KeyDown;
    }

    private void BuildInterface()
    {
        SuspendLayout();
        Controls.Clear();

        Text = VT("Header.WindowTitle");
        StartPosition = FormStartPosition.CenterScreen;
        ShowIcon = true;

        try
        {
            System.Drawing.Icon? applicationIcon =
                System.Drawing.Icon.ExtractAssociatedIcon(
                    Application.ExecutablePath);

            if (applicationIcon is not null)
            {
                Icon = applicationIcon;
            }
        }
        catch
        {
            // The executable icon may not be available while the designer loads.
        }

        Rectangle workingArea =
            Screen.PrimaryScreen?.WorkingArea ??
            new Rectangle(0, 0, 1920, 1080);

        int availableWidth = Math.Max(320, workingArea.Width - 32);
        int availableHeight = Math.Max(240, workingArea.Height - 32);
        Size frame = SizeFromClientSize(Size.Empty);
        ClientSize = new Size(Math.Max(280, Math.Min(1080, availableWidth - frame.Width)),
            Math.Max(200, Math.Min(1120, availableHeight - frame.Height)));
        MinimumSize = new Size(Math.Min(980, availableWidth), Math.Min(720, availableHeight));

        // The form remains fully resizable and can still be maximized.
        // Only the default proportions and the old height cap are changed.
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        KeyPreview = true;
        // When hosted inside WT Assistant the parent already owns DPI/layout.
        // Scaling the child form again caused cramped spacing and clipping.
        AutoScaleMode = _embeddedMode ? AutoScaleMode.None : AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);

        BackColor = Theme.Background;
        ForeColor = Theme.Text;
        Font = new Font("Segoe UI", 9.5F);
        DoubleBuffered = true;

        _rootLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Theme.Background
        };

        _rootLayout.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 100F));

        _rootLayout.RowStyles.Add(
            new RowStyle(SizeType.Absolute, 100F));

        _rootLayout.RowStyles.Add(
            new RowStyle(SizeType.Absolute, 46F));

        _rootLayout.RowStyles.Add(
            new RowStyle(SizeType.Percent, 100F));

        Control header = CreateHeader();

        var navigation = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = new Padding(18, 5, 18, 5),
            BackColor = Theme.Navigation
        };

        navigation.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 19F));
        navigation.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 22F));
        navigation.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 17F));
        navigation.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 18F));
        navigation.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 24F));
        navigation.RowStyles.Add(
            new RowStyle(SizeType.Percent, 100F));

        var pageHost = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = new Padding(12),
            BackColor = Theme.Background
        };

        var dashboardPage = new ScrollPagePanel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Theme.Background
        };

        var setupPage = new ScrollPagePanel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Theme.Background,
            Visible = false
        };

        var profilesPage = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Theme.Background,
            Visible = false
        };

        BuildTrimTab(dashboardPage);
        BuildSetupTab(setupPage);
        BuildProfilesTab(profilesPage);

        var curvesPage = new ScrollPagePanel { Dock = DockStyle.Fill, BackColor = Theme.Background, Visible = false };
        BuildCurvesTab(curvesPage);
        pageHost.Controls.Add(curvesPage);

        pageHost.Controls.Add(profilesPage);
        pageHost.Controls.Add(setupPage);
        pageHost.Controls.Add(dashboardPage);

        Button dashboardButton =
            I18n(CreateNavigationButton("Trim Dashboard"), "Nav.Trim");
        dashboardButton.Dock = DockStyle.Fill;
        dashboardButton.Margin = new Padding(0, 0, 5, 0);

        Button setupButton =
            I18n(CreateNavigationButton("Devices & Output"), "Nav.Devices");
        setupButton.Dock = DockStyle.Fill;
        setupButton.Margin = new Padding(5, 0, 5, 0);

        Button profilesButton =
            I18n(CreateNavigationButton("Profiles"), "Nav.Profiles");
        profilesButton.Dock = DockStyle.Fill;
        profilesButton.Margin = new Padding(5, 0, 5, 0);

        Button curvesButton = I18n(CreateNavigationButton("Axis Curves"), "Nav.Curves");
        curvesButton.Dock = DockStyle.Fill;
        curvesButton.Margin = new Padding(5, 0, 5, 0);

        _openProfiles = () => ShowPage(2);
        void ShowPage(int pageIndex)
        {
            dashboardPage.Visible = pageIndex == 0;
            setupPage.Visible = pageIndex == 1;
            profilesPage.Visible = pageIndex == 2;
            curvesPage.Visible = pageIndex == 3;

            if (pageIndex == 0)
            {
                dashboardPage.BringToFront();
            }
            else if (pageIndex == 1)
            {
                setupPage.BringToFront();
            }
            else if (pageIndex == 2)
            {
                profilesPage.BringToFront();
                UpdateProfileSummary();
            }
            else
            {
                curvesPage.BringToFront();
                RefreshCurveEditors();
            }

            SetNavigationState(dashboardButton, pageIndex == 0);
            SetNavigationState(setupButton, pageIndex == 1);
            SetNavigationState(profilesButton, pageIndex == 2);
            SetNavigationState(curvesButton, pageIndex == 3);
        }

        dashboardButton.Click += (_, _) => ShowPage(0);
        setupButton.Click += (_, _) => ShowPage(1);
        profilesButton.Click += (_, _) => ShowPage(2);
        curvesButton.Click += (_, _) => ShowPage(3);

        var nativeButtonsBadge = new Label
        {
            Text = VT("Nav.Native"),
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            BackColor = Theme.Navigation,
            ForeColor = Theme.Success,
            Font = new Font("Segoe UI Semibold", 8.5F),
            TextAlign = ContentAlignment.MiddleRight,
            AutoEllipsis = false,
            UseMnemonic = false
        };
        nativeButtonsBadge.Tag = "i18n:Nav.Native";

        navigation.Controls.Add(dashboardButton, 0, 0);
        navigation.Controls.Add(setupButton, 1, 0);
        navigation.Controls.Add(curvesButton, 2, 0);
        navigation.Controls.Add(profilesButton, 3, 0);
        navigation.Controls.Add(nativeButtonsBadge, 4, 0);

        _rootLayout.Controls.Add(header, 0, 0);
        _rootLayout.Controls.Add(navigation, 0, 1);
        _rootLayout.Controls.Add(pageHost, 0, 2);

        Controls.Add(_rootLayout);
        if (_embeddedMode) ApplyEmbeddedAviationTheme(_rootLayout);

        ShowPage(0);
        UpdateTrimDisplay(0, 0, 0, 0, 0, 0);

        CaptureBaseFontSizes(this);
        CaptureResponsiveMetrics(this);
        Resize += (_, _) => ApplyResponsiveUiScale();
        ApplyResponsiveUiScale();

        ResumeLayout(true);
    }

    public void ApplyLanguage(string languageCode)
    {
        _languageCode = NormalizeVTrimLanguage(languageCode);
        Text = VT("Header.WindowTitle");
        ApplyVTrimLanguage(this);
        if (_vJoyConnected)
        {
            _vJoyStatusLabel.Text = VT("VJoy.Connected");
            _vJoyConnectButton.Text = VT("Common.Disconnect");
        }
        RefreshCurveEditors();
        foreach (TrimAction action in _bindingButtons.Keys.ToArray()) UpdateBindingButton(action);
        UpdateProfileSummary();
    }

    private void ApplyVTrimLanguage(Control parent)
    {
        foreach (Control control in parent.Controls)
        {
            if (control.Tag is string tag && tag.StartsWith("i18n:", StringComparison.Ordinal))
                control.Text = VT(tag[5..]);
            if (control.HasChildren) ApplyVTrimLanguage(control);
        }
    }

    private T I18n<T>(T control, string key) where T : Control
    {
        control.Tag = "i18n:" + key;
        control.Text = VT(key);
        return control;
    }

    private string VT(string key) => VTrimText(_languageCode, key);

    private static string NormalizeVTrimLanguage(string? code)
    {
        if (string.Equals(code, "bg", StringComparison.OrdinalIgnoreCase)) return "bg";
        if (string.Equals(code, "el", StringComparison.OrdinalIgnoreCase)) return "el";
        if (string.Equals(code, "ro", StringComparison.OrdinalIgnoreCase)) return "ro";
        return "en";
    }

    private static string VTrimText(string languageCode, string key)
    {
        string English(string k) => k switch
        {
            "Devices.Invert" => "Invert",
            "Nav.Trim" => "Trim Dashboard", "Nav.Devices" => "Devices & Output", "Nav.Curves" => "Axis Curves", "Nav.Profiles" => "Profiles", "Nav.Native" => "BUTTONS STAY NATIVE",
            "Header.WindowTitle" => "VTrim - Virtual Trim for HOTAS & Rudder Pedals", "Header.Title" => "VTrim", "Header.Subtitle" => "Virtual Trim for HOTAS & Rudder Pedals", "Header.Description" => "Pitch  •  Roll  •  Rudder\r\nPhysical buttons stay native", "Header.Support" => "Buy me a Beer",
            "Profiles.GameProfiles" => "Game profiles", "Profiles.AircraftType" => "Aircraft type", "Profiles.New" => "New", "Profiles.Duplicate" => "Duplicate", "Profiles.Rename" => "Rename", "Profiles.Delete" => "Delete", "Profiles.NewProfile" => "New profile", "Profiles.ProfileName" => "Profile name", "Profiles.Create" => "Create", "Profiles.Contents" => "Profile Contents", "Profiles.Active" => "Active profile: {0}", "Profiles.Temporary" => "None (temporary session)", "Profiles.Aircraft" => "Aircraft type: {0}", "Profiles.TrimBinds" => "Trim keybinds: {0} of {1}", "Profiles.Axes" => "Physical axes: {0} of {1} configured", "Profiles.Roll" => "Roll: {0}", "Profiles.Pitch" => "Pitch: {0}", "Profiles.Rudder" => "Rudder: {0}", "Profiles.Saved" => "Profiles are normal JSON files inside the app's Profiles folder.", "Profiles.Unsaved" => "Nothing in this temporary session will be saved when VTrim closes.", "Profiles.Files" => "Profiles are normal JSON files inside the app's Profiles folder.",
            "Dashboard.LiveMonitor" => "Live monitor", "Dashboard.Legend" => "Move physical controls. Gold shows virtual trim added by VTrim.", "Dashboard.InstructorOff" => "Flight Assistant: OFF", "Dashboard.InstructorOn" => "Flight Assistant: ON", "Dashboard.StoreTrim" => "Store Trim", "Dashboard.CenterAllTrim" => "CENTER ALL", "Dashboard.Stick" => "STICK", "Dashboard.RudderYaw" => "RUDDER / YAW TRIM", "Dashboard.RudderTrim" => "RUDDER TRIM",
            "Devices.SetupTitle" => "Physical Input Devices", "Devices.AutoDetect" => "Auto-Detect", "Devices.Refresh" => "Refresh", "Devices.Input" => "Physical Input Devices", "Devices.NoneDetected" => "No physical DirectInput devices detected.", "Devices.ConnectThenRefresh" => "Connect a HOTAS, yoke, pedals or other controller, then click Refresh.", "Devices.CountDetected" => "{0} physical DirectInput device(s) detected.", "Devices.ReadyProfile" => "Device ready for profile: {0}", "Devices.InitFailed" => "DirectInput initialization failed: {0}", "Devices.InputFailed" => "DirectInput read failed: {0}", "Devices.LastDetected" => "Last detected input: {0}", "Devices.Routing" => "Physical Axis Routing", "Devices.RecenterAll" => "Recenter All", "Devices.Recenter" => "Recenter", "Devices.DetectRoll" => "Detect Roll", "Devices.DetectPitch" => "Detect Pitch", "Devices.DetectRudder" => "Detect Rudder", "Devices.SetupHelp" => "Start from neutral, click Detect, then move only the requested axis.",
            "Common.Listening" => "Listening...", "Common.Connect" => "Connect", "Common.Disconnect" => "Disconnect", "Common.Cancelled" => "Detection was cancelled.", "Common.Cancel" => "Cancel", "Common.NotConfigured" => "Not configured",
            "Sensitivity.Title" => "Trim sensitivity", "Sensitivity.Description" => "Adjust how fast virtual trim moves and how safely input is accepted.", "Sensitivity.HoldSpeed" => "Hold speed", "Sensitivity.UniversalTrigger" => "Universal trigger", "Sensitivity.InputGuard" => "Input guard", "Sensitivity.RateHelp" => "Higher values move trim faster while a command is held.", "Sensitivity.RepeatOn" => "Repeat: ON", "Sensitivity.RepeatOff" => "Repeat: OFF", "Sensitivity.GuardOn" => "Guard: ON", "Sensitivity.GuardOff" => "Guard: OFF", "Sensitivity.TriggerOn" => "Trigger: ON", "Sensitivity.TriggerOff" => "Trigger: OFF",
            "Status.LastInputNone" => "Last input: none", "Status.ConfigureAxes" => "Configure Roll, Pitch and Rudder physical axes to enable live routing.", "Status.DirectInputNotStarted" => "DirectInput not started", "Curves.Heading" => "Axis Curves\r\nThe graph shows physical input before trim and Flight Assistant correction.", "Curves.Roll" => "Roll", "Curves.Pitch" => "Pitch", "Curves.RudderYaw" => "Rudder / Yaw", "Curves.Curve" => "Curve (-100 to +100)", "Curves.CenterDeadzone" => "Center deadzone (%)", "Curves.InputRange" => "Input range (%)", "Curves.OutputRange" => "Output range (%)", "Curves.ResetAxis" => "Reset this axis", "Curves.CurveTooltip" => "0 = linear; positive softens the center; negative increases center response.", "Curves.OutputTooltip" => "Limits the shaped physical command. Trim and damping are added afterward.", "Curves.Input" => "Input", "Curves.Output" => "Output",
            "Trim.Bindings" => "Trim Bindings", "Trim.BindingsHelp" => "Click a control, then press one input. Hold a modifier first to create a two-button combination.", "Trim.ClickBind" => "Click to bind", "Trim.Center" => "CENTER ALL", "Trim.Store" => "STORE TRIM", "Trim.Instructor" => "FLIGHT ASSISTANT", "Trim.NoseDown" => "NOSE DOWN", "Trim.NoseUp" => "NOSE UP", "Trim.RollLeft" => "ROLL LEFT", "Trim.RollRight" => "ROLL RIGHT", "Trim.RudderLeft" => "RUDDER LEFT", "Trim.RudderRight" => "RUDDER RIGHT", "Trim.AlreadyStored" => "Trim is already stored. Return controls to center before capturing again.", "Trim.NeedsFreshInput" => "Store Trim needs fresh input from each configured controller. Check Devices & Output.", "Trim.RollValue" => "Roll trim: {0:+0.00;-0.00;0.00}%", "Trim.PitchValue" => "Pitch trim: {0:+0.00;-0.00;0.00}%", "Trim.RudderValue" => "Rudder trim: {0:+0.00;-0.00;0.00}%", "Trim.AssignTooltip" => "Assign an input for {0}",
            "VJoy.Connected" => "vJoy Device 1 connected: X, Y and Rz output is active.", "VJoy.OutputActive" => "vJoy Device 1 is receiving VTrim output.", "VJoy.NotReadyRetry" => "vJoy Device 1 is not ready. Auto-connect will retry.", "VJoy.ConnectFailed" => "Could not connect to vJoy Device 1: {0}", "VJoy.AutoFailed" => "Auto-connect failed. Open Devices & Output and use Connect.", "VJoy.Retrying" => "vJoy Device 1 is not ready — automatic retry ({0} tries remaining).", "VJoy.Disconnected" => "vJoy Device 1: disconnected", "VJoy.ClickReconnect" => "vJoy Device 1 is disconnected. Click Connect to reconnect.", "VJoy.Title" => "VTrim Output", "VJoy.Device1" => "vJoy DEVICE 1", "VJoy.StatusDisconnected" => "Virtual output: disconnected", "VJoy.SetupConnect" => "Setup / Connect", "VJoy.TestAxes" => "Test axes", "VJoy.Description" => "ONE-CLICK SETUP  ·  Installs bundled signed vJoy, configures Device 1 and connects output. Windows may ask for administrator access.\r\n\r\nIn War Thunder, bind Roll / Pitch / Rudder to vJoy X / Y / Rz. Physical HOTAS buttons stay native. Switch mappings use virtual buttons 1–32.", "VJoy.AutoConnect" => "Connect output automatically", "VJoy.StartWithWindows" => "Start VTrim with Windows, minimized",
            "Instructor.On" => "Flight Assistant enabled. Hold a stick position briefly, then release toward center to send game trim.", "Instructor.Off" => "Flight Assistant disabled. Manual trim is available.", "Instructor.PropOnly" => "Flight Assistant is available for Prop Plane profiles only.", "Instructor.BindGameTrim" => "Open Trim setup and bind the same keyboard command as Trim aircraft in War Thunder.", "Instructor.StateOff" => "Enable Flight Assistant to trim when the stick springs back toward center.", "Instructor.NoOutput" => "Set up and connect vJoy in Devices & Output.", "Instructor.GameNotFocused" => "Paused until War Thunder is the foreground window.", "Instructor.LowAirspeed" => "Standby below 90 km/h indicated airspeed.", "Instructor.Trimming" => "Captured the previous stick position; sending the game's trim command.", "Instructor.ReturnToCenter" => "Game trim sent. Let the stick finish returning to center.", "Instructor.ReleaseToTrim" => "Position ready. Let the stick spring back to capture game trim.", "Instructor.CheckHotas" => "A configured controller is disconnected or no flight axis is assigned.", "Instructor.TrimKeyBlocked" => "Trim key could not be sent. Release keyboard modifiers and check game focus / elevation.", "Instructor.DefaultDetail" => "Move the stick, hold briefly, then release toward center. Game trim: {0}", "Instructor.TrimSetup" => "Trim setup", "Instructor.DialogTitle" => "Flight Assistant · Spring-return trim", "Instructor.DialogHeading" => "PROP PLANE INSTRUCTOR", "Instructor.TrimOnReturn" => "Trim on spring return", "Instructor.GameTrimKey" => "Game trim key", "Instructor.ClickTrimKey" => "Click here and press your game trim key", "Instructor.NotAssigned" => "Not assigned", "Instructor.HoldBeforeRelease" => "Hold before release (ms)", "Instructor.ReturnMovement" => "Return movement (%)", "Instructor.Help" => "1. In War Thunder, bind Trim aircraft to the same keyboard key or chord shown above. If you use a HOTAS trim button, add this key as a second game binding.\r\n\r\n2. Bind Roll / Pitch / Rudder to vJoy X / Y / Rz. Move the stick, hold briefly, then let it spring back. Flight Assistant captures the position before the return and taps game trim once.\r\n\r\nWorks in a focused War Thunder flight above 90 km/h. The aircraft and selected game control mode must support trim. Esc clears the key.", "Instructor.SaveSetup" => "Save setup", "Instructor.AssignTrimFirst" => "Assign the game's trim key first", "Instructor.StoreConflict" => "Store Trim and Flight Assistant need different bindings. The existing binding was kept.", "Instructor.TrimStored" => "Trim stored. Flight Assistant ON/OFF is unchanged.", "Instructor.Centered" => "All manual trim centered. Flight Assistant Mode remains automatic.", "Instructor.TelemetryTimeout" => "Local telemetry timed out. Check that War Thunder is running in a flight.", "Instructor.TelemetryReadFailed" => "Cannot read 127.0.0.1:8111: {0}", "Instructor.Label.Off" => "OFF", "Instructor.Label.NoOutput" => "NO OUTPUT", "Instructor.Label.GameNotFocused" => "GAME NOT FOCUSED", "Instructor.Label.WaitingForFlight" => "WAITING FOR FLIGHT", "Instructor.Label.LowAirspeed" => "LOW AIRSPEED", "Instructor.Label.Trimming" => "TRIMMING", "Instructor.Label.ReturnToCenter" => "RETURN TO CENTER", "Instructor.Label.ReleaseToTrim" => "RELEASE TO TRIM", "Instructor.Label.CheckHotas" => "CHECK HOTAS", "Instructor.Label.TrimKeyBlocked" => "TRIM KEY BLOCKED", "Instructor.Label.PropOnly" => "PROP PLANES ONLY", "Instructor.Label.BindGameTrim" => "BIND GAME TRIM", "Instructor.Label.Configuring" => "CONFIGURING", "Instructor.Label.ManualTrim" => "MANUAL TRIM", "Instructor.Label.Waiting" => "WAITING", "Instructor.Label.MoveStick" => "MOVE STICK", "Instructor.Label.Paused" => "PAUSED",
            _ => k
        };

        string code = NormalizeVTrimLanguage(languageCode);
        return code switch
        {
            "el" => key switch
            {
                "Header.WindowTitle" => "VTrim - Virtual Trim για HOTAS και πεντάλ rudder",
                "Header.Title" => "VTrim",
                "Header.Support" => "Κέρασέ με μια μπίρα",
                "Nav.Native" => "ΤΑ ΚΟΥΜΠΙΑ ΜΕΝΟΥΝ NATIVE",
                "Common.Listening" => "Αναμονή...",
                "Common.Connect" => "Σύνδεση",
                "Common.Disconnect" => "Αποσύνδεση",
                "Common.Cancelled" => "Η ανίχνευση ακυρώθηκε.",
                "Dashboard.LiveMonitor" => "Ζωντανή παρακολούθηση",
                "Dashboard.Legend" => "Κίνησε τα φυσικά χειριστήρια. Το χρυσό δείχνει το virtual trim που προσθέτει το VTrim.",
                "Dashboard.InstructorOff" => "Flight Assistant: OFF",
                "Dashboard.InstructorOn" => "Flight Assistant: ON",
                "Dashboard.StoreTrim" => "ΑΠΟΘΗΚΕΥΣΗ TRIM",
                "Dashboard.CenterAllTrim" => "ΚΕΝΤΡΑΡΙΣΜΑ ΟΛΩΝ",
                "Dashboard.Stick" => "STICK",
                "Dashboard.RudderYaw" => "RUDDER / YAW TRIM",
                "Dashboard.RudderTrim" => "RUDDER TRIM",
                "Devices.SetupTitle" => "Φυσικές συσκευές εισόδου",
                "Devices.Input" => "Φυσικές συσκευές εισόδου",
                "Devices.NoneDetected" => "Δεν εντοπίστηκαν φυσικές DirectInput συσκευές.",
                "Devices.ConnectThenRefresh" => "Σύνδεσε HOTAS, yoke, pedals ή άλλο controller και πάτησε Ανανέωση.",
                "Devices.CountDetected" => "Εντοπίστηκαν {0} φυσικές DirectInput συσκευές.",
                "Devices.ReadyProfile" => "Η συσκευή είναι έτοιμη για προφίλ: {0}",
                "Devices.InitFailed" => "Η αρχικοποίηση DirectInput απέτυχε: {0}",
                "Devices.InputFailed" => "Η ανάγνωση DirectInput απέτυχε: {0}",
                "Devices.LastDetected" => "Τελευταίο input: {0}",
                "Devices.Routing" => "Δρομολόγηση φυσικών αξόνων",
                "Devices.RecenterAll" => "Κεντράρισμα όλων",
                "Devices.Recenter" => "Κεντράρισμα",
                "Devices.DetectRoll" => "Ανίχνευση Roll",
                "Devices.DetectPitch" => "Ανίχνευση Pitch",
                "Devices.DetectRudder" => "Ανίχνευση Rudder",
                "Devices.SetupHelp" => "Ξεκίνα από ουδέτερη θέση, πάτησε Ανίχνευση και κίνησε μόνο τον ζητούμενο άξονα.",
                "Status.LastInputNone" => "Τελευταίο input: κανένα",
                "Status.ConfigureAxes" => "Ρύθμισε φυσικούς άξονες Roll, Pitch και Rudder για να ενεργοποιηθεί το live routing.",
                "Status.DirectInputNotStarted" => "Το DirectInput δεν έχει ξεκινήσει",
                "Trim.AlreadyStored" => "Το trim έχει ήδη αποθηκευτεί. Επέστρεψε τα χειριστήρια στο κέντρο πριν ξαναγράψεις.",
                "Trim.NeedsFreshInput" => "Το Store Trim χρειάζεται νέο input από κάθε ρυθμισμένο controller. Έλεγξε το Devices & Output.",
                "Trim.NoseDown" => "ΜΥΤΗ ΚΑΤΩ",
                "Trim.NoseUp" => "ΜΥΤΗ ΠΑΝΩ",
                "Trim.RollLeft" => "ROLL ΑΡΙΣΤΕΡΑ",
                "Trim.RollRight" => "ROLL ΔΕΞΙΑ",
                "Trim.RudderLeft" => "RUDDER ΑΡΙΣΤΕΡΑ",
                "Trim.RudderRight" => "RUDDER ΔΕΞΙΑ",
                "Trim.RollValue" => "Roll trim: {0:+0.00;-0.00;0.00}%",
                "Trim.PitchValue" => "Pitch trim: {0:+0.00;-0.00;0.00}%",
                "Trim.RudderValue" => "Rudder trim: {0:+0.00;-0.00;0.00}%",
                "Trim.AssignTooltip" => "Ανάθεση input για {0}",
                "VJoy.Connected" => "vJoy Device 1 συνδεδεμένο: η έξοδος X, Y και Rz είναι ενεργή.",
                "VJoy.OutputActive" => "Το vJoy Device 1 λαμβάνει έξοδο VTrim.",
                "VJoy.NotReadyRetry" => "Το vJoy Device 1 δεν είναι έτοιμο. Η αυτόματη σύνδεση θα ξαναδοκιμάσει.",
                "VJoy.ConnectFailed" => "Δεν ήταν δυνατή η σύνδεση στο vJoy Device 1: {0}",
                "VJoy.AutoFailed" => "Η αυτόματη σύνδεση απέτυχε. Άνοιξε Devices & Output και πάτησε Σύνδεση.",
                "VJoy.Retrying" => "Το vJoy Device 1 δεν είναι έτοιμο — αυτόματη επανάληψη ({0} προσπάθειες απομένουν).",
                "VJoy.Disconnected" => "vJoy Device 1: αποσυνδεδεμένο",
                "VJoy.ClickReconnect" => "Το vJoy Device 1 είναι αποσυνδεδεμένο. Πάτησε Σύνδεση για επανασύνδεση.",
                "VJoy.Device1" => "vJoy DEVICE 1",
                "VJoy.StatusDisconnected" => "Virtual έξοδος: αποσυνδεδεμένη",
                "VJoy.AutoConnect" => "Αυτόματη σύνδεση εξόδου",
                "VJoy.StartWithWindows" => "Εκκίνηση VTrim με τα Windows, ελαχιστοποιημένο",
                "VJoy.Description" => "ΡΥΘΜΙΣΗ ΜΕ ΕΝΑ ΚΛΙΚ  ·  Εγκαθιστά το bundled signed vJoy, ρυθμίζει το Device 1 και συνδέει την έξοδο. Τα Windows μπορεί να ζητήσουν δικαιώματα administrator.\r\n\r\nΣτο War Thunder, δέσμευσε Roll / Pitch / Rudder σε vJoy X / Y / Rz. Τα φυσικά HOTAS buttons μένουν native. Τα switch mappings χρησιμοποιούν virtual buttons 1–32.",
                "Profiles.GameProfiles" => "Προφίλ παιχνιδιού",
                "Profiles.AircraftType" => "Τύπος αεροσκάφους",
                "Profiles.New" => "Νέο",
                "Profiles.Duplicate" => "Αντιγραφή",
                "Profiles.Rename" => "Μετονομασία",
                "Profiles.Delete" => "Διαγραφή",
                "Profiles.NewProfile" => "Νέο προφίλ",
                "Profiles.ProfileName" => "Όνομα προφίλ",
                "Profiles.Create" => "Δημιουργία",
                "Profiles.Contents" => "Περιεχόμενα προφίλ",
                "Profiles.Active" => "Ενεργό προφίλ: {0}",
                "Profiles.Temporary" => "Κανένα (προσωρινή συνεδρία)",
                "Profiles.Aircraft" => "Τύπος αεροσκάφους: {0}",
                "Profiles.TrimBinds" => "Trim keybinds: {0} από {1}",
                "Profiles.Axes" => "Φυσικοί άξονες: {0} από {1} ρυθμισμένοι",
                "Profiles.Roll" => "Roll: {0}",
                "Profiles.Pitch" => "Pitch: {0}",
                "Profiles.Rudder" => "Rudder: {0}",
                "Profiles.Saved" => "Τα προφίλ είναι κανονικά JSON αρχεία μέσα στον φάκελο Profiles της εφαρμογής.",
                "Profiles.Unsaved" => "Τίποτα σε αυτή την προσωρινή συνεδρία δεν θα αποθηκευτεί όταν κλείσει το VTrim.",
                "Profiles.Files" => "Τα προφίλ είναι κανονικά JSON αρχεία μέσα στον φάκελο Profiles της εφαρμογής.",
                "Sensitivity.GuardOn" => "Προστασία: ON",
                "Sensitivity.GuardOff" => "Προστασία: OFF",
                "Sensitivity.RepeatOn" => "Επανάληψη: ON",
                "Sensitivity.RepeatOff" => "Επανάληψη: OFF",
                "Sensitivity.TriggerOn" => "Trigger: ON",
                "Sensitivity.TriggerOff" => "Trigger: OFF",
                "Instructor.NoOutput" => "Ρύθμισε και σύνδεσε το vJoy στο Devices & Output.",
                "Instructor.GameNotFocused" => "Παύση μέχρι το War Thunder να είναι το ενεργό παράθυρο.",
                "Instructor.LowAirspeed" => "Αναμονή κάτω από 90 km/h indicated airspeed.",
                "Instructor.Trimming" => "Καταγράφηκε η προηγούμενη θέση stick· αποστέλλεται η trim εντολή του παιχνιδιού.",
                "Instructor.ReturnToCenter" => "Στάλθηκε game trim. Άφησε το stick να επιστρέψει πλήρως στο κέντρο.",
                "Instructor.ReleaseToTrim" => "Η θέση είναι έτοιμη. Άφησε το stick να επιστρέψει για καταγραφή game trim.",
                "Instructor.CheckHotas" => "Ένας ρυθμισμένος controller είναι αποσυνδεδεμένος ή δεν έχει οριστεί άξονας πτήσης.",
                "Instructor.TrimKeyBlocked" => "Το trim key δεν μπόρεσε να σταλεί. Άφησε τα keyboard modifiers και έλεγξε game focus / elevation.",
                "Instructor.DefaultDetail" => "Κίνησε το stick, κράτησε λίγο και άφησέ το προς το κέντρο. Game trim: {0}",
                "Instructor.TrimSetup" => "Ρύθμιση trim",
                "Instructor.DialogHeading" => "PROP PLANE INSTRUCTOR",
                "Instructor.GameTrimKey" => "Game trim key",
                "Instructor.HoldBeforeRelease" => "Κράτημα πριν την επιστροφή (ms)",
                "Instructor.ReturnMovement" => "Κίνηση επιστροφής (%)",
                "Instructor.StoreConflict" => "Store Trim και Flight Assistant χρειάζονται διαφορετικά bindings. Το υπάρχον binding διατηρήθηκε.",
                "Instructor.TrimStored" => "Το trim αποθηκεύτηκε. Το Flight Assistant ON/OFF δεν άλλαξε.",
                "Instructor.Centered" => "Όλο το manual trim κεντραρίστηκε. Το Flight Assistant Mode παραμένει αυτόματο.",
                "Instructor.TelemetryTimeout" => "Το τοπικό telemetry έκανε timeout. Έλεγξε ότι το War Thunder τρέχει σε πτήση.",
                "Instructor.TelemetryReadFailed" => "Δεν είναι δυνατή η ανάγνωση 127.0.0.1:8111: {0}", "Instructor.Label.Off" => "ΑΝΕΝΕΡΓΟ", "Instructor.Label.NoOutput" => "ΧΩΡΙΣ ΕΞΟΔΟ", "Instructor.Label.GameNotFocused" => "ΤΟ ΠΑΙΧΝΙΔΙ ΔΕΝ ΕΧΕΙ FOCUS", "Instructor.Label.WaitingForFlight" => "ΑΝΑΜΟΝΗ ΠΤΗΣΗΣ", "Instructor.Label.LowAirspeed" => "ΧΑΜΗΛΗ ΤΑΧΥΤΗΤΑ", "Instructor.Label.Trimming" => "TRIMMING", "Instructor.Label.ReturnToCenter" => "ΕΠΙΣΤΡΟΦΗ ΣΤΟ ΚΕΝΤΡΟ", "Instructor.Label.ReleaseToTrim" => "ΑΦΗΣΕ ΓΙΑ TRIM", "Instructor.Label.CheckHotas" => "ΕΛΕΓΞΕ HOTAS", "Instructor.Label.TrimKeyBlocked" => "TRIM KEY ΜΠΛΟΚΑΡΙΣΜΕΝΟ", "Instructor.Label.PropOnly" => "ΜΟΝΟ PROP PLANES", "Instructor.Label.BindGameTrim" => "ΔΕΣΜΕΥΣΗ GAME TRIM", "Instructor.Label.Configuring" => "ΡΥΘΜΙΣΗ", "Instructor.Label.ManualTrim" => "ΧΕΙΡΟΚΙΝΗΤΟ TRIM", "Instructor.Label.Waiting" => "ΑΝΑΜΟΝΗ", "Instructor.Label.MoveStick" => "ΚΙΝΗΣΕ ΤΟ STICK", "Instructor.Label.Paused" => "ΠΑΥΣΗ",
                "Nav.Trim" => "Πίνακας Trim", "Nav.Devices" => "Συσκευές και έξοδος", "Nav.Curves" => "Καμπύλες αξόνων", "Nav.Profiles" => "Προφίλ", "Header.Subtitle" => "Virtual Trim για HOTAS και rudder pedals", "Header.Description" => "Pitch  •  Roll  •  Rudder\r\nΤα φυσικά κουμπιά μένουν native", "Common.Cancel" => "Άκυρο", "Common.NotConfigured" => "Δεν έχει ρυθμιστεί", "Devices.AutoDetect" => "Αυτόματος εντοπισμός", "Devices.Refresh" => "Ανανέωση", "Curves.Heading" => "Καμπύλες αξόνων\r\nΤο γράφημα δείχνει τη φυσική είσοδο πριν από trim και διόρθωση Flight Assistant.", "Curves.Roll" => "Roll", "Curves.Pitch" => "Pitch", "Curves.RudderYaw" => "Rudder / Yaw", "Curves.Curve" => "Καμπύλη (-100 έως +100)", "Curves.CenterDeadzone" => "Νεκρή ζώνη κέντρου (%)", "Curves.InputRange" => "Εύρος εισόδου (%)", "Curves.OutputRange" => "Εύρος εξόδου (%)", "Curves.ResetAxis" => "Επαναφορά άξονα", "Curves.CurveTooltip" => "0 = γραμμικό· θετικό μαλακώνει το κέντρο· αρνητικό αυξάνει την απόκριση κέντρου.", "Curves.OutputTooltip" => "Περιορίζει τη διαμορφωμένη φυσική εντολή. Trim και damping προστίθενται μετά.", "Curves.Input" => "Είσοδος", "Curves.Output" => "Έξοδος", "Trim.Bindings" => "Trim bindings", "Trim.BindingsHelp" => "Κάνε κλικ σε control και μετά πάτησε ένα input. Κράτα πρώτα modifier για συνδυασμό δύο κουμπιών.", "Trim.ClickBind" => "Κλικ για binding", "Trim.Center" => "ΚΕΝΤΡΑΡΙΣΜΑ ΟΛΩΝ", "Trim.Store" => "ΑΠΟΘΗΚΕΥΣΗ TRIM", "Trim.Instructor" => "ΛΕΙΤΟΥΡΓΙΑ INSTRUCTOR", "VJoy.Title" => "Έξοδος VTrim", "VJoy.SetupConnect" => "Ρύθμιση / Σύνδεση", "VJoy.TestAxes" => "Δοκιμή αξόνων", "Instructor.On" => "Ο Flight Assistant είναι ενεργός. Κράτησε για λίγο θέση stick και μετά άφησε προς το κέντρο για αποστολή trim.", "Instructor.Off" => "Ο Flight Assistant είναι ανενεργός. Το χειροκίνητο trim είναι διαθέσιμο.", "Instructor.PropOnly" => "Ο Flight Assistant είναι διαθέσιμος μόνο για προφίλ Prop Plane.", "Instructor.BindGameTrim" => "Άνοιξε το Trim setup και δέσμευσε την ίδια εντολή πληκτρολογίου με το Trim aircraft στο War Thunder.", "Instructor.StateOff" => "Ενεργοποίησε τον Flight Assistant για trim όταν το stick επιστρέφει προς το κέντρο.", "Instructor.DialogTitle" => "Flight Assistant · spring-return trim", "Instructor.TrimOnReturn" => "Trim στην επιστροφή ελατηρίου", "Instructor.ClickTrimKey" => "Κάνε κλικ εδώ και πάτησε το game trim key", "Instructor.NotAssigned" => "Δεν έχει οριστεί", "Instructor.SaveSetup" => "Αποθήκευση setup", "Instructor.AssignTrimFirst" => "Πρώτα όρισε το game trim key", "Instructor.Help" => "1. Στο War Thunder, δέσμευσε το Trim aircraft στο ίδιο πλήκτρο ή chord που φαίνεται πάνω.\r\n\r\n2. Δέσμευσε Roll / Pitch / Rudder σε vJoy X / Y / Rz. Μετακίνησε το stick, κράτησε λίγο και άφησέ το να επιστρέψει.\r\n\r\nΛειτουργεί σε ενεργή πτήση War Thunder πάνω από 90 km/h. Το Esc καθαρίζει το πλήκτρο.", _ => English(key)
            },
            "ro" => key switch
            {
                "Header.WindowTitle" => "VTrim - Trim virtual pentru HOTAS și pedale rudder",
                "Header.Title" => "VTrim",
                "Header.Support" => "Cumpără-mi o bere",
                "Nav.Native" => "BUTOANELE RĂMÂN NATIVE",
                "Common.Listening" => "Ascult...",
                "Common.Connect" => "Conectează",
                "Common.Disconnect" => "Deconectează",
                "Common.Cancelled" => "Detectarea a fost anulată.",
                "Dashboard.LiveMonitor" => "Monitor live",
                "Dashboard.Legend" => "Mișcă comenzile fizice. Auriu arată trimul virtual adăugat de VTrim.",
                "Dashboard.InstructorOff" => "Flight Assistant: OFF",
                "Dashboard.InstructorOn" => "Flight Assistant: ON",
                "Dashboard.StoreTrim" => "SALVEAZĂ TRIM",
                "Dashboard.CenterAllTrim" => "CENTREAZĂ TOT",
                "Dashboard.Stick" => "STICK",
                "Dashboard.RudderYaw" => "TRIM RUDDER / YAW",
                "Dashboard.RudderTrim" => "TRIM RUDDER",
                "Devices.SetupTitle" => "Dispozitive fizice de intrare",
                "Devices.Input" => "Dispozitive fizice de intrare",
                "Devices.NoneDetected" => "Nu au fost detectate dispozitive DirectInput fizice.",
                "Devices.ConnectThenRefresh" => "Conectează HOTAS, yoke, pedale sau alt controller, apoi apasă Reîmprospătează.",
                "Devices.CountDetected" => "{0} dispozitiv(e) DirectInput fizice detectate.",
                "Devices.ReadyProfile" => "Dispozitiv pregătit pentru profil: {0}",
                "Devices.InitFailed" => "Inițializarea DirectInput a eșuat: {0}",
                "Devices.InputFailed" => "Citirea DirectInput a eșuat: {0}",
                "Devices.LastDetected" => "Ultimul input detectat: {0}",
                "Devices.Routing" => "Rutare axe fizice",
                "Devices.RecenterAll" => "Recentrează tot",
                "Devices.Recenter" => "Recentrează",
                "Devices.DetectRoll" => "Detectează Roll",
                "Devices.DetectPitch" => "Detectează Pitch",
                "Devices.DetectRudder" => "Detectează Rudder",
                "Devices.SetupHelp" => "Pornește din neutru, apasă Detectează, apoi mișcă doar axa cerută.",
                "Status.LastInputNone" => "Ultimul input: niciunul",
                "Status.ConfigureAxes" => "Configurează axele fizice Roll, Pitch și Rudder pentru rutare live.",
                "Status.DirectInputNotStarted" => "DirectInput nu a pornit",
                "Trim.AlreadyStored" => "Trimul este deja salvat. Revino cu comenzile la centru înainte de capturare din nou.",
                "Trim.NeedsFreshInput" => "Store Trim are nevoie de input nou de la fiecare controller configurat. Verifică Devices & Output.",
                "Trim.NoseDown" => "NAS JOS",
                "Trim.NoseUp" => "NAS SUS",
                "Trim.RollLeft" => "ROLL STÂNGA",
                "Trim.RollRight" => "ROLL DREAPTA",
                "Trim.RudderLeft" => "RUDDER STÂNGA",
                "Trim.RudderRight" => "RUDDER DREAPTA",
                "Trim.RollValue" => "Trim roll: {0:+0.00;-0.00;0.00}%",
                "Trim.PitchValue" => "Trim pitch: {0:+0.00;-0.00;0.00}%",
                "Trim.RudderValue" => "Trim rudder: {0:+0.00;-0.00;0.00}%",
                "Trim.AssignTooltip" => "Atribuie un input pentru {0}",
                "VJoy.Connected" => "vJoy Device 1 conectat: ieșirea X, Y și Rz este activă.",
                "VJoy.OutputActive" => "vJoy Device 1 primește ieșirea VTrim.",
                "VJoy.NotReadyRetry" => "vJoy Device 1 nu este pregătit. Auto-conectarea va reîncerca.",
                "VJoy.ConnectFailed" => "Nu s-a putut conecta la vJoy Device 1: {0}",
                "VJoy.AutoFailed" => "Auto-conectarea a eșuat. Deschide Devices & Output și folosește Conectează.",
                "VJoy.Retrying" => "vJoy Device 1 nu este pregătit — reîncercare automată ({0} încercări rămase).",
                "VJoy.Disconnected" => "vJoy Device 1: deconectat",
                "VJoy.ClickReconnect" => "vJoy Device 1 este deconectat. Apasă Conectează pentru reconectare.",
                "VJoy.Device1" => "vJoy DEVICE 1",
                "VJoy.StatusDisconnected" => "Ieșire virtuală: deconectată",
                "VJoy.AutoConnect" => "Conectează ieșirea automat",
                "VJoy.StartWithWindows" => "Pornește VTrim cu Windows, minimizat",
                "VJoy.Description" => "CONFIGURARE DINTR-UN CLIC  ·  Instalează vJoy semnat inclus, configurează Device 1 și conectează ieșirea. Windows poate cere acces de administrator.\r\n\r\nÎn War Thunder, leagă Roll / Pitch / Rudder la vJoy X / Y / Rz. Butoanele HOTAS fizice rămân native. Mapările de switch folosesc butoane virtuale 1–32.",
                "Profiles.GameProfiles" => "Profiluri joc",
                "Profiles.AircraftType" => "Tip aeronavă",
                "Profiles.New" => "Nou",
                "Profiles.Duplicate" => "Duplică",
                "Profiles.Rename" => "Redenumește",
                "Profiles.Delete" => "Șterge",
                "Profiles.NewProfile" => "Profil nou",
                "Profiles.ProfileName" => "Nume profil",
                "Profiles.Create" => "Creează",
                "Profiles.Contents" => "Conținut profil",
                "Profiles.Active" => "Profil activ: {0}",
                "Profiles.Temporary" => "Niciunul (sesiune temporară)",
                "Profiles.Aircraft" => "Tip aeronavă: {0}",
                "Profiles.TrimBinds" => "Bindinguri trim: {0} din {1}",
                "Profiles.Axes" => "Axe fizice: {0} din {1} configurate",
                "Profiles.Roll" => "Roll: {0}",
                "Profiles.Pitch" => "Pitch: {0}",
                "Profiles.Rudder" => "Rudder: {0}",
                "Profiles.Saved" => "Profilurile sunt fișiere JSON normale în folderul Profiles al aplicației.",
                "Profiles.Unsaved" => "Nimic din această sesiune temporară nu va fi salvat când VTrim se închide.",
                "Profiles.Files" => "Profilurile sunt fișiere JSON normale în folderul Profiles al aplicației.",
                "Sensitivity.GuardOn" => "Protecție: ON",
                "Sensitivity.GuardOff" => "Protecție: OFF",
                "Sensitivity.RepeatOn" => "Repetare: ON",
                "Sensitivity.RepeatOff" => "Repetare: OFF",
                "Sensitivity.TriggerOn" => "Trigger: ON",
                "Sensitivity.TriggerOff" => "Trigger: OFF",
                "Instructor.NoOutput" => "Configurează și conectează vJoy în Devices & Output.",
                "Instructor.GameNotFocused" => "Pauză până când War Thunder este fereastra activă.",
                "Instructor.LowAirspeed" => "Standby sub 90 km/h viteză indicată.",
                "Instructor.Trimming" => "Poziția anterioară a stickului a fost capturată; se trimite comanda de trim a jocului.",
                "Instructor.ReturnToCenter" => "Trimul jocului a fost trimis. Lasă stickul să revină complet la centru.",
                "Instructor.ReleaseToTrim" => "Poziția este gata. Lasă stickul să revină pentru capturarea trimului jocului.",
                "Instructor.CheckHotas" => "Un controller configurat este deconectat sau nu este atribuită nicio axă de zbor.",
                "Instructor.TrimKeyBlocked" => "Tasta de trim nu a putut fi trimisă. Eliberează modificatorii de tastatură și verifică focusul/elevarea jocului.",
                "Instructor.DefaultDetail" => "Mișcă stickul, ține scurt, apoi revino spre centru. Trim joc: {0}",
                "Instructor.TrimSetup" => "Configurare trim",
                "Instructor.DialogHeading" => "INSTRUCTOR AVION CU ELICE",
                "Instructor.GameTrimKey" => "Tastă trim joc",
                "Instructor.HoldBeforeRelease" => "Ținere înainte de revenire (ms)",
                "Instructor.ReturnMovement" => "Mișcare de revenire (%)",
                "Instructor.StoreConflict" => "Store Trim și Flight Assistant au nevoie de bindinguri diferite. Bindingul existent a fost păstrat.",
                "Instructor.TrimStored" => "Trim salvat. Flight Assistant ON/OFF este neschimbat.",
                "Instructor.Centered" => "Tot trimul manual a fost centrat. Flight Assistant Mode rămâne automat.",
                "Instructor.TelemetryTimeout" => "Telemetria locală a expirat. Verifică dacă War Thunder rulează într-un zbor.",
                "Instructor.TelemetryReadFailed" => "Nu se poate citi 127.0.0.1:8111: {0}", "Instructor.Label.Off" => "OPRIT", "Instructor.Label.NoOutput" => "FĂRĂ IEȘIRE", "Instructor.Label.GameNotFocused" => "JOCUL NU ARE FOCUS", "Instructor.Label.WaitingForFlight" => "AȘTEPT ZBOR", "Instructor.Label.LowAirspeed" => "VITEZĂ MICĂ", "Instructor.Label.Trimming" => "TRIMMING", "Instructor.Label.ReturnToCenter" => "REVENIRE LA CENTRU", "Instructor.Label.ReleaseToTrim" => "ELIBEREAZĂ PENTRU TRIM", "Instructor.Label.CheckHotas" => "VERIFICĂ HOTAS", "Instructor.Label.TrimKeyBlocked" => "TASTĂ TRIM BLOCATĂ", "Instructor.Label.PropOnly" => "DOAR PROP PLANES", "Instructor.Label.BindGameTrim" => "LEAGĂ GAME TRIM", "Instructor.Label.Configuring" => "CONFIGURARE", "Instructor.Label.ManualTrim" => "TRIM MANUAL", "Instructor.Label.Waiting" => "AȘTEPTARE", "Instructor.Label.MoveStick" => "MIȘCĂ STICKUL", "Instructor.Label.Paused" => "PAUZĂ",
                "Nav.Trim" => "Tablou Trim", "Nav.Devices" => "Dispozitive și ieșire", "Nav.Curves" => "Curbe axe", "Nav.Profiles" => "Profiluri", "Header.Subtitle" => "Trim virtual pentru HOTAS și pedale rudder", "Header.Description" => "Pitch  •  Roll  •  Rudder\r\nButoanele fizice rămân native", "Common.Cancel" => "Anulează", "Common.NotConfigured" => "Neconfigurat", "Devices.AutoDetect" => "Detectare automată", "Devices.Refresh" => "Reîmprospătează", "Curves.Heading" => "Curbe axe\r\nGraficul arată intrarea fizică înainte de trim și corecția Flight Assistant.", "Curves.Roll" => "Roll", "Curves.Pitch" => "Pitch", "Curves.RudderYaw" => "Rudder / Yaw", "Curves.Curve" => "Curbă (-100 la +100)", "Curves.CenterDeadzone" => "Zonă moartă centru (%)", "Curves.InputRange" => "Interval intrare (%)", "Curves.OutputRange" => "Interval ieșire (%)", "Curves.ResetAxis" => "Resetează această axă", "Curves.CurveTooltip" => "0 = liniar; pozitiv înmoaie centrul; negativ crește răspunsul în centru.", "Curves.OutputTooltip" => "Limitează comanda fizică formată. Trimul și dampingul se adaugă după.", "Curves.Input" => "Intrare", "Curves.Output" => "Ieșire", "Trim.Bindings" => "Bindings trim", "Trim.BindingsHelp" => "Clic pe un control, apoi apasă un input. Ține mai întâi un modifier pentru combinație cu două butoane.", "Trim.ClickBind" => "Clic pentru binding", "Trim.Center" => "CENTREAZĂ TOT", "Trim.Store" => "SALVEAZĂ TRIM", "Trim.Instructor" => "MOD INSTRUCTOR", "VJoy.Title" => "Ieșire VTrim", "VJoy.SetupConnect" => "Configurează / Conectează", "VJoy.TestAxes" => "Testează axele", "Instructor.On" => "Flight Assistant activat. Ține scurt o poziție a stickului, apoi revino spre centru pentru a trimite trimul jocului.", "Instructor.Off" => "Flight Assistant dezactivat. Trimul manual este disponibil.", "Instructor.PropOnly" => "Instructorul este disponibil doar pentru profiluri Prop Plane.", "Instructor.BindGameTrim" => "Deschide Trim setup și leagă aceeași comandă de tastatură ca Trim aircraft în War Thunder.", "Instructor.StateOff" => "Activează Flight Assistant pentru trim când stickul revine spre centru.", "Instructor.DialogTitle" => "Flight Assistant · spring-return trim", "Instructor.TrimOnReturn" => "Trim la revenirea pe arc", "Instructor.ClickTrimKey" => "Clic aici și apasă game trim key", "Instructor.NotAssigned" => "Neatribuit", "Instructor.SaveSetup" => "Salvează setup", "Instructor.AssignTrimFirst" => "Atribuie mai întâi game trim key", "Instructor.Help" => "1. În War Thunder, leagă Trim aircraft la aceeași tastă sau combinație afișată mai sus.\r\n\r\n2. Leagă Roll / Pitch / Rudder la vJoy X / Y / Rz. Mișcă stickul, ține scurt, apoi lasă-l să revină.\r\n\r\nFuncționează într-un zbor War Thunder focalizat peste 90 km/h. Esc șterge tasta.", _ => English(key)
            },
            _ => English(key)
        };
    }

    private Control CreateHeader()
    {
        if (_embeddedMode)
        {
            return CreateEmbeddedHeader();
        }

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = new Padding(20, 10, 18, 10),
            BackColor = Theme.Header
        };

        // Reserve a real title column instead of letting it compete with
        // the description for percentage width. This prevents "VTrim" from
        // clipping at different DPI/window sizes.
        header.ColumnStyles.Add(
            new ColumnStyle(SizeType.Absolute, 72F));

        header.ColumnStyles.Add(
            new ColumnStyle(SizeType.Absolute, 360F));

        header.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 100F));

        header.ColumnStyles.Add(
            new ColumnStyle(SizeType.Absolute, 190F));

        header.RowStyles.Add(
            new RowStyle(SizeType.Percent, 56F));

        header.RowStyles.Add(
            new RowStyle(SizeType.Percent, 44F));

        var logo = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 14, 0),
            BackColor = Theme.Accent
        };

        var logoText = new Label
        {
            Text = "VT",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI Semibold", 16F),
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter,
            UseMnemonic = false
        };

        logo.Controls.Add(logoText);

        var title = I18n(new Label
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = new Padding(2, 0, 0, 0),
            Font = new Font("Segoe UI Semibold", 20F),
            ForeColor = Theme.Text,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = false,
            UseMnemonic = false
        }, "Header.Title");

        var subtitle = I18n(new Label
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Font = new Font("Segoe UI Semibold", 12F),
            ForeColor = Theme.AccentLight,
            Padding = new Padding(2, 0, 0, 0),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = false,
            UseMnemonic = false
        }, "Header.Subtitle");

        var description = I18n(new Label
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(14, 4, 14, 4),
            Font = new Font("Segoe UI", 8.4F),
            ForeColor = Theme.Muted,
            TextAlign = ContentAlignment.MiddleRight,
            AutoEllipsis = false,
            UseMnemonic = false
        }, "Header.Description");

        var beerButton = I18n(CreateBeerButton("Buy me a Beer"), "Header.Support");
        beerButton.Dock = DockStyle.Fill;
        beerButton.Margin = new Padding(8, 13, 0, 13);
        beerButton.Click += (_, _) =>
            OpenWebLink(
                "https://www.paypal.com/donate/?business=SLS9FP9VALFV4&no_recurring=1&item_name=Thank+you+for+supporting+what+I+do%21&currency_code=EUR");

        header.Controls.Add(logo, 0, 0);
        header.SetRowSpan(logo, 2);
        header.Controls.Add(title, 1, 0);
        header.Controls.Add(subtitle, 1, 1);
        header.Controls.Add(description, 2, 0);
        header.SetRowSpan(description, 2);
        header.Controls.Add(beerButton, 3, 0);
        header.SetRowSpan(beerButton, 2);

        return header;
    }

    private void CaptureBaseFontSizes(Control parent)
    {
        if (parent is AircraftBrowserPanel) return;
        foreach (Control control in parent.Controls)
        {
            if (!_baseFontSizes.ContainsKey(control))
            {
                _baseFontSizes[control] = control.Font.Size;
                if (IsResponsiveTextControl(control))
                    control.TextChanged += ResponsiveTextControl_TextChanged;
            }

            if (control.HasChildren)
            {
                CaptureBaseFontSizes(control);
            }
        }
    }

    private void CaptureResponsiveMetrics(Control parent)
    {
        if (parent is AircraftBrowserPanel) return;
        foreach (Control control in parent.Controls)
        {
            if (!_baseControlMetrics.ContainsKey(control))
            {
                Rectangle loose = control.Parent is TableLayoutPanel || control.Dock != DockStyle.None
                    ? Rectangle.Empty
                    : control.Bounds;
                _baseControlMetrics[control] = (control.Margin, control.Padding, control.MinimumSize, loose);
            }

            if (control is TableLayoutPanel table && !_baseTableMetrics.ContainsKey(table))
            {
                _baseTableMetrics[table] = (
                    table.RowStyles.Cast<RowStyle>().Select(r => r.Height).ToArray(),
                    table.ColumnStyles.Cast<ColumnStyle>().Select(c => c.Width).ToArray());
            }

            if (control.Dock == DockStyle.Top && control.Height > 0 &&
                !_responsiveScrollDesignHeights.ContainsKey(control) &&
                !_baseTopDockHeights.ContainsKey(control))
                _baseTopDockHeights[control] = control.Height;

            if (control.HasChildren) CaptureResponsiveMetrics(control);
        }
    }

    private static Padding ScalePadding(Padding value, float scale) => new(
        (int)Math.Round(value.Left * scale),
        (int)Math.Round(value.Top * scale),
        (int)Math.Round(value.Right * scale),
        (int)Math.Round(value.Bottom * scale));

    private float GetResponsiveUiScale()
    {
        if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return 1F;
        float widthScale = ClientSize.Width / (_embeddedMode ? 1240F : 1080F);
        float heightScale = ClientSize.Height / (_embeddedMode ? 760F : 980F);
        return _embeddedMode
            ? Math.Clamp(Math.Min(widthScale, heightScale), 0.82F, 1.55F)
            : Math.Clamp(Math.Min(widthScale, heightScale), 0.88F, 1.30F);
    }

    private void RegisterResponsiveScrollSurface(Control parent, Control surface, int designHeight)
    {
        if (parent is ScrollableControl scroll) scroll.AutoScroll = true;
        surface.Dock = DockStyle.Top;
        int dpiAdjusted = _embeddedMode
            ? designHeight
            : (int)Math.Round(designHeight * parent.DeviceDpi / 96F);
        _responsiveScrollDesignHeights[surface] = Math.Max(1, dpiAdjusted);
        parent.SizeChanged += (_, _) => SizeResponsiveScrollSurface(parent, surface);
        parent.Controls.Add(surface);
        SizeResponsiveScrollSurface(parent, surface);
    }

    private void SizeResponsiveScrollSurface(Control parent, Control surface)
    {
        if (!_responsiveScrollDesignHeights.TryGetValue(surface, out int designHeight)) return;
        int scaledDesignHeight = Math.Max(1, (int)Math.Round(designHeight * GetResponsiveUiScale()));
        surface.Height = Math.Max(parent.ClientSize.Height, scaledDesignHeight);
    }

    private static bool IsResponsiveTextControl(Control control) =>
        control is Button ||
        control is CheckBox { Appearance: Appearance.Button } ||
        control is Label { AutoSize: false };

    private void ResponsiveTextControl_TextChanged(object? sender, EventArgs eventArgs)
    {
        if (_applyingUiScale || sender is not Control control || control.IsDisposed) return;
        FitOneResponsiveTextControl(control);
    }

    private static bool UsesLimitedHeaderFontScale(Control control) =>
        control.Text == "VTrim" ||
        control.Text == "VT" ||
        control.Text == "VTRIM ASSISTANT" ||
        control.Text == "Virtual Trim for HOTAS & Rudder Pedals" ||
        control.Text == "Buy me a Beer" ||
        control.Text.Contains("Adjustable pitch, roll and yaw trim", StringComparison.Ordinal);

    private float ResponsiveFontSize(Control control, float baseSize, float scale)
    {
        float controlScale = UsesLimitedHeaderFontScale(control)
            ? Math.Clamp(scale, 0.94F, 1.10F)
            : scale;
        return Math.Max(7F, baseSize * controlScale);
    }

    private void FitOneResponsiveTextControl(Control control)
    {
        if (!_baseFontSizes.TryGetValue(control, out float baseSize)) return;
        float desiredSize = ResponsiveFontSize(control, baseSize, GetResponsiveUiScale());
        if (Math.Abs(control.Font.Size - desiredSize) > 0.05F)
            SetResponsiveFont(control, desiredSize);

        FitTextControl(control, 8.75F);
    }

    private void SetResponsiveFont(Control control, float size)
    {
        if (control.IsDisposed) return;
        Font current = control.Font;
        Font replacement = new(current.FontFamily, size, current.Style, current.Unit);
        control.Font = replacement;
        if (_responsiveOwnedFonts.Remove(control, out Font? previous)) previous.Dispose();
        _responsiveOwnedFonts[control] = replacement;
    }

    private void FitTextControl(Control control, float minimumPointSize)
    {
        if (control.IsDisposed || control.ClientSize.Width < 12 || control.ClientSize.Height < 12 || string.IsNullOrWhiteSpace(control.Text)) return;
        int availableWidth = Math.Max(1, control.ClientSize.Width - control.Padding.Horizontal - 12);
        int availableHeight = Math.Max(1, control.ClientSize.Height - control.Padding.Vertical - 8);
        if (availableWidth < 8 || availableHeight < 8) return;

        TextFormatFlags flags = TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl;
        float candidateSize = control.Font.Size;
        FontStyle style = control.Font.Style;
        string family = control.Font.FontFamily.Name;
        GraphicsUnit unit = control.Font.Unit;
        while (candidateSize > minimumPointSize)
        {
            using Font candidate = new(family, candidateSize, style, unit);
            Size measured = TextRenderer.MeasureText(control.Text, candidate, new Size(availableWidth, int.MaxValue), flags);
            if (measured.Width <= availableWidth && measured.Height <= availableHeight) break;
            candidateSize -= 0.5F;
        }

        candidateSize = Math.Max(minimumPointSize, candidateSize);
        if (Math.Abs(control.Font.Size - candidateSize) < 0.05F) return;
        SetResponsiveFont(control, candidateSize);
    }

    private void FitResponsiveText(Control parent)
    {
        if (parent is AircraftBrowserPanel) return;
        foreach (Control control in parent.Controls)
        {
            if (IsResponsiveTextControl(control))
                // Long help/status labels are just as important as short captions.
                // Reset to the current window-scaled size, then shrink only when
                // the actual post-layout cell cannot contain the current text.
                FitOneResponsiveTextControl(control);

            if (control.HasChildren) FitResponsiveText(control);
        }
    }

    private void ApplyResponsiveUiScale()
    {
        if (_applyingUiScale ||
            _rootLayout is null ||
            ClientSize.Width <= 0 ||
            ClientSize.Height <= 0)
        {
            return;
        }

        _applyingUiScale = true;

        try
        {
            // Embedded VTrim uses the same design-space principle as the main app.
            // The entire visual hierarchy grows together when the host is maximized.
            float scale = GetResponsiveUiScale();

            SuspendLayout();

            foreach ((TableLayoutPanel table, var metrics) in _baseTableMetrics)
            {
                if (table.IsDisposed) continue;
                for (int i = 0; i < Math.Min(table.RowStyles.Count, metrics.Rows.Length); i++)
                    if (table.RowStyles[i].SizeType == SizeType.Absolute) table.RowStyles[i].Height = metrics.Rows[i] * scale;
                for (int i = 0; i < Math.Min(table.ColumnStyles.Count, metrics.Columns.Length); i++)
                    if (table.ColumnStyles[i].SizeType == SizeType.Absolute) table.ColumnStyles[i].Width = metrics.Columns[i] * scale;
            }

            foreach ((Control control, var metrics) in _baseControlMetrics)
            {
                if (control.IsDisposed) continue;
                control.Margin = ScalePadding(metrics.Margin, scale);
                control.Padding = ScalePadding(metrics.Padding, scale);
                if (!metrics.Minimum.IsEmpty)
                    control.MinimumSize = new Size((int)Math.Round(metrics.Minimum.Width * scale), (int)Math.Round(metrics.Minimum.Height * scale));
                if (!metrics.LooseBounds.IsEmpty)
                    control.Bounds = new Rectangle(
                        (int)Math.Round(metrics.LooseBounds.X * scale),
                        (int)Math.Round(metrics.LooseBounds.Y * scale),
                        (int)Math.Round(metrics.LooseBounds.Width * scale),
                        (int)Math.Round(metrics.LooseBounds.Height * scale));
            }

            foreach ((Control control, int baseHeight) in _baseTopDockHeights)
            {
                if (control.IsDisposed || control.Parent is null) continue;
                int scaledHeight = Math.Max(1, (int)Math.Round(baseHeight * scale));
                if (control.Parent is ScrollableControl)
                    scaledHeight = Math.Max(control.Parent.ClientSize.Height, scaledHeight);
                control.Height = scaledHeight;
            }

            foreach ((Control surface, _) in _responsiveScrollDesignHeights)
            {
                if (!surface.IsDisposed && surface.Parent is Control parent)
                    SizeResponsiveScrollSurface(parent, surface);
            }

            foreach ((Control control, float baseSize) in _baseFontSizes)
            {
                if (control.IsDisposed)
                {
                    continue;
                }

                float scaledSize = ResponsiveFontSize(control, baseSize, scale);

                if (Math.Abs(control.Font.Size - scaledSize) < 0.05F)
                {
                    continue;
                }

                SetResponsiveFont(control, scaledSize);
            }

        }
        finally
        {
            ResumeLayout(true);
            PerformLayout();
            // Run the anti-crop pass only after TableLayout has calculated the new
            // scaled cell rectangles; otherwise the fit test sees stale bounds.
            FitResponsiveText(this);
            _applyingUiScale = false;
        }
    }

    private static Button CreateNavigationButton(string text)
    {
        var button = new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Navigation,
            ForeColor = Theme.Muted,
            Font = new Font("Segoe UI Semibold", 9F),
            Cursor = Cursors.Hand,
            TextAlign = ContentAlignment.MiddleCenter,
            UseMnemonic = false,
            AutoEllipsis = false
        };

        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor =
            Theme.SelectedNavigation;

        button.FlatAppearance.MouseDownBackColor =
            Theme.ControlPressed;

        return button;
    }

    private static void SetNavigationState(
        Button button,
        bool active)
    {
        button.BackColor = active
            ? Theme.SelectedNavigation
            : Theme.Navigation;

        button.ForeColor = active
            ? Theme.Text
            : Theme.Muted;

        button.FlatAppearance.BorderSize = active
            ? 1
            : 0;

        button.FlatAppearance.BorderColor =
            active
                ? Theme.Accent
                : Theme.Navigation;
    }


    private void BuildProfilesTab(Control parent)
    {
        _profileBox = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Theme.Control, ForeColor = Theme.Text, FlatStyle = FlatStyle.Flat, FormattingEnabled = true };
        _profileBox.Format += (_, e) => e.Value = AircraftSearchService.CleanName(e.ListItem?.ToString());
        _profileBox.SelectedIndexChanged += (_, _) =>
        {
            if (!_switchingProfile && !_loadingSavedSettings && _profileBox.SelectedItem is string name &&
                !string.Equals(name, _activeProfileName, StringComparison.OrdinalIgnoreCase)) SwitchProfile(name);
        };
        // Retain flight-type compatibility in saved setups, without the retired type selector.
        _profileTypeBox = new ProfileTypeComboBox();
        _activeProfileLabel = new Label();
        _profileSummaryLabel = new Label();
        Disposed += (_, _) => { _profileTypeBox.Dispose(); _activeProfileLabel.Dispose(); _profileSummaryLabel.Dispose(); };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3,
            Padding = new Padding(14), BackColor = Theme.Panel };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Label { Text = "Game profiles", Dock = DockStyle.Fill, ForeColor = Theme.Text }, 0, 0);
        layout.Controls.Add(_profileBox, 0, 1);
        layout.Controls.Add(CreateAircraftProfileBrowser(false), 0, 2);
        parent.Controls.Add(layout);
    }

    private void CreateProfile()
    {
        if (!ChooseAircraftProfile("", null, out string requestedName, out string? aircraftId))
        {
            return;
        }

        if (!TryNormalizeNewProfileName(
                requestedName,
                out string profileName))
        {
            return;
        }

        _profileAircraftId = aircraftId;
        _profileId = Guid.NewGuid().ToString("D");

        // Save the CURRENT session into the new profile. This is especially
        // useful on a fresh install: configure VTrim first, then create the
        // profile when you are ready to keep those settings.
        SavedBindingsFile profile =
            CaptureCurrentProfile();

        profile.ProfileName =
            profileName;

        _activeProfileName =
            profileName;

        WriteProfile(
            profileName,
            profile);

        WriteActiveProfileName();
        RefreshProfileList();

        SetInstruction(
            $"Created and saved profile: {profileName}.",
            Theme.Success);
    }

    private void DuplicateProfile()
    {
        SavedBindingsFile currentProfile =
            CaptureCurrentProfile();

        string baseName =
            string.IsNullOrWhiteSpace(
                _activeProfileName)
                ? "New Profile"
                : $"{_activeProfileName} Copy";

        string? requestedName =
            PromptForProfileName(
                "Duplicate Profile",
                "Name for the duplicate:",
                GetUniqueProfileName(
                    baseName));

        if (!TryNormalizeNewProfileName(
                requestedName,
                out string profileName))
        {
            return;
        }

        currentProfile.ProfileName =
            profileName;
        currentProfile.Id = Guid.NewGuid().ToString("D");
        _profileId = currentProfile.Id;

        WriteProfile(
            profileName,
            currentProfile);

        _activeProfileName =
            profileName;

        WriteActiveProfileName();
        RefreshProfileList();

        SetInstruction(
            $"Saved current setup as: {profileName}.",
            Theme.Success);
    }

    private void RenameProfile()
    {
        if (string.IsNullOrWhiteSpace(
                _activeProfileName))
        {
            MessageBox.Show(
                this,
                "There is no saved profile to rename. Create a profile first.",
                "VTrim Profiles",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            return;
        }

        string? requestedName =
            PromptForProfileName(
                "Rename Profile",
                "New profile name:",
                _activeProfileName);

        if (string.IsNullOrWhiteSpace(
                requestedName))
        {
            return;
        }

        string profileName =
            NormalizeProfileName(
                requestedName);

        if (string.Equals(
                profileName,
                _activeProfileName,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (ProfileExists(profileName))
        {
            ShowProfileExistsMessage(
                profileName);

            return;
        }

        SaveBindings();

        string oldPath =
            GetProfileFilePath(
                _activeProfileName);

        SavedBindingsFile profile =
            CaptureCurrentProfile();

        profile.ProfileName =
            profileName;

        WriteProfile(
            profileName,
            profile);

        if (File.Exists(oldPath))
        {
            File.Delete(oldPath);
        }

        _activeProfileName =
            profileName;

        WriteActiveProfileName();
        RefreshProfileList();

        SetInstruction(
            $"Profile renamed to: {profileName}.",
            Theme.Success);
    }

    private void DeleteProfile()
    {
        if (string.IsNullOrWhiteSpace(
                _activeProfileName))
        {
            return;
        }

        DialogResult result =
            MessageBox.Show(
                this,
                $"Delete profile \"{_activeProfileName}\"?",
                "Delete Profile",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

        if (result != DialogResult.Yes)
        {
            return;
        }

        string deletedName =
            _activeProfileName;

        string profilePath =
            GetProfileFilePath(
                deletedName);

        if (File.Exists(profilePath))
        {
            File.Delete(profilePath);
        }

        List<string> remainingProfiles =
            GetProfileNames();

        if (remainingProfiles.Count == 0)
        {
            // Deleting the last profile is valid. Return to a completely
            // unsaved/clean temporary session.
            _activeProfileName =
                string.Empty;

            ApplySavedProfile(
                new SavedBindingsFile
                {
                    ProfileName =
                        string.Empty
                });

            DeleteActiveProfilePointer();
        }
        else
        {
            _activeProfileName =
                remainingProfiles[0];

            SavedBindingsFile profile =
                ReadProfile(
                    _activeProfileName);

            ApplySavedProfile(
                profile);

            WriteActiveProfileName();
        }

        RefreshProfileList();

        SetInstruction(
            $"Deleted profile: {deletedName}.",
            Theme.Warning);
    }

    private void SwitchProfile(string profileName)
    {
        if (string.IsNullOrWhiteSpace(
                profileName))
        {
            return;
        }

        SaveBindings();

        _activeProfileName =
            profileName;

        SavedBindingsFile profile =
            ReadProfile(
                profileName);

        ApplySavedProfile(
            profile);

        WriteActiveProfileName();
        RefreshProfileList();

        SetInstruction(
            $"Active profile: {profileName}.",
            Theme.Success);
    }

    private void RefreshProfileList()
    {
        AircraftProfilesChanged?.Invoke();
        if (_profileBox is null)
        {
            return;
        }

        List<string> profileNames =
            GetProfileNames();

        _switchingProfile = true;

        try
        {
            _profileBox.BeginUpdate();
            _profileBox.Items.Clear();

            foreach (string profileName in profileNames)
            {
                _profileBox.Items.Add(
                    profileName);
            }

            if (!string.IsNullOrWhiteSpace(
                    _activeProfileName) &&
                profileNames.Any(
                    name =>
                        string.Equals(
                            name,
                            _activeProfileName,
                            StringComparison.OrdinalIgnoreCase)))
            {
                _profileBox.SelectedItem =
                    _activeProfileName;
            }
            else
            {
                _profileBox.SelectedIndex =
                    -1;
            }

            _profileBox.EndUpdate();
        }
        finally
        {
            _switchingProfile = false;
        }

        UpdateProfileSummary();
    }

    private void UpdateProfileSummary()
    {
        if (_activeProfileLabel is null ||
            _profileSummaryLabel is null)
        {
            return;
        }

        int configuredAxes =
            (_rollAxis is null ? 0 : 1) +
            (_pitchAxis is null ? 0 : 1) +
            (_rudderAxis is null ? 0 : 1);

        bool hasActiveProfile =
            !string.IsNullOrWhiteSpace(
                _activeProfileName);

        _activeProfileLabel.Text =
            hasActiveProfile
                ? string.Format(CultureInfo.CurrentCulture, VT("Profiles.Active"), _activeProfileName)
                : "Default setup";

        _activeProfileLabel.ForeColor =
            hasActiveProfile
                ? Theme.Success
                : Theme.Warning;

        string aircraftType = NormalizeAircraftType(_profileTypeBox?.SelectedItem?.ToString());
        var aircraft = _aircraftDatabase?.GetById(_profileAircraftId);
        if (aircraft is not null) aircraftType = $"{aircraft.DisplayName} ({aircraft.Nation}, {aircraft.VehicleType})";
        else if (_profileAircraftId is not null) aircraftType = _profileAircraftId;
        string notConfigured = VT("Common.NotConfigured");

        _profileSummaryLabel.Text =
            string.Format(CultureInfo.CurrentCulture, VT("Profiles.Aircraft"), aircraftType) + "\r\n" +
            string.Format(CultureInfo.CurrentCulture, VT("Profiles.TrimBinds"), _bindings.Count, Enum.GetValues<TrimAction>().Length) + "\r\n" +
            string.Format(CultureInfo.CurrentCulture, VT("Profiles.Axes"), configuredAxes, 3) + "\r\n\r\n" +
            string.Format(CultureInfo.CurrentCulture, VT("Profiles.Roll"), _rollAxis?.DisplayName ?? notConfigured) + "\r\n" +
            string.Format(CultureInfo.CurrentCulture, VT("Profiles.Pitch"), _pitchAxis?.DisplayName ?? notConfigured) + "\r\n" +
            string.Format(CultureInfo.CurrentCulture, VT("Profiles.Rudder"), _rudderAxis?.DisplayName ?? notConfigured) + "\r\n\r\n" +
            (hasActiveProfile
                ? VT("Profiles.Saved") + "\r\n"
                : "Default setup saved automatically." + "\r\n") +
            VT("Profiles.Files");

        if (_deleteProfileButton is not null)
        {
            _deleteProfileButton.Enabled =
                hasActiveProfile && !_offlinePreview &&
                GetProfileNames().Count > 0;
        }
    }

    private static string NormalizeAircraftType(string? value)
    {
        return value switch
        {
            "Jet Plane" => "Jet Plane",
            "Helicopter" => "Helicopter",
            _ => "Prop Plane"
        };
    }

    private bool TryNormalizeNewProfileName(
        string? requestedName,
        out string profileName)
    {
        profileName =
            string.IsNullOrWhiteSpace(
                requestedName)
                ? string.Empty
                : NormalizeProfileName(
                    requestedName);

        if (string.IsNullOrWhiteSpace(
                profileName))
        {
            return false;
        }

        if (ProfileExists(profileName))
        {
            ShowProfileExistsMessage(
                profileName);

            return false;
        }

        return true;
    }

    private void ShowProfileExistsMessage(
        string profileName)
    {
        MessageBox.Show(
            this,
            $"A profile named \"{profileName}\" already exists.",
            "VTrim Profiles",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private bool PromptForNewProfile(string initialName, out string? profileName, out string aircraftType)
    {
        profileName = null;
        aircraftType = NormalizeAircraftType(_profileTypeBox?.SelectedItem?.ToString());
        using var dialog = new Form
        {
            Text = VT("Profiles.NewProfile"),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            ClientSize = new Size(560, 270),
            MinimizeBox = false, MaximizeBox = false, ShowInTaskbar = false,
            AutoScaleMode = AutoScaleMode.Dpi, BackColor = Theme.Background, ForeColor = Theme.Text,
            Font = new Font("Segoe UI", 9.5F)
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Margin = Padding.Empty,
            Padding = new Padding(20, 16, 20, 16), BackColor = Theme.Background
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        Label nameLabel = new() { Text = VT("Profiles.ProfileName"), Dock = DockStyle.Fill, ForeColor = Theme.Text, TextAlign = ContentAlignment.MiddleLeft };
        TextBox input = new() { Text = UpgradeLegacyProfileDisplayName(initialName), Dock = DockStyle.Fill, Margin = new Padding(0, 5, 0, 7), BackColor = Theme.Control, ForeColor = Theme.Text, Font = new Font("Segoe UI", 10F), MaxLength = 48 };
        Label typeLabel = new() { Text = VT("Profiles.AircraftType"), Dock = DockStyle.Fill, ForeColor = Theme.Text, TextAlign = ContentAlignment.MiddleLeft };
        ProfileTypeComboBox typeBox = new() { Dock = DockStyle.Fill, Margin = new Padding(0, 5, 0, 7), BackColor = Theme.Control, ForeColor = Theme.Text, Font = new Font("Segoe UI Semibold", 10F) };
        typeBox.SelectedItem = aircraftType;
        FlowLayoutPanel buttons = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Padding = new Padding(0, 8, 0, 0), BackColor = Theme.Background };
        Button ok = CreatePrimaryButton(VT("Profiles.Create")); ok.Size = new Size(128, 40); ok.DialogResult = DialogResult.OK;
        Button cancel = CreateSecondaryButton(VT("Common.Cancel")); cancel.Size = new Size(112, 40); cancel.Margin = new Padding(0, 0, 10, 0); cancel.DialogResult = DialogResult.Cancel;
        buttons.Controls.Add(ok); buttons.Controls.Add(cancel);
        layout.Controls.Add(nameLabel, 0, 0); layout.Controls.Add(input, 0, 1); layout.Controls.Add(typeLabel, 0, 2); layout.Controls.Add(typeBox, 0, 3); layout.Controls.Add(buttons, 0, 4);
        dialog.Controls.Add(layout); dialog.AcceptButton = ok; dialog.CancelButton = cancel;
        dialog.Shown += (_, _) => { input.SelectAll(); input.Focus(); };
        if (dialog.ShowDialog(this) != DialogResult.OK) return false;
        profileName = input.Text; aircraftType = NormalizeAircraftType(typeBox.SelectedItem?.ToString()); return true;
    }

    private string? PromptForProfileName(
        string title,
        string prompt,
        string initialValue)
    {
        using var dialog = new Form
        {
            Text = title,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            ClientSize = new Size(520, 178),
            MinimumSize = new Size(536, 217),
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            AutoScaleMode = AutoScaleMode.Dpi,
            BackColor = Theme.Background,
            ForeColor = Theme.Text,
            Font = new Font("Segoe UI", 9.5F)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = new Padding(18, 16, 18, 16),
            BackColor = Theme.Background
        };

        layout.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                100F));

        layout.RowStyles.Add(
            new RowStyle(
                SizeType.Absolute,
                36F));

        layout.RowStyles.Add(
            new RowStyle(
                SizeType.Absolute,
                48F));

        layout.RowStyles.Add(
            new RowStyle(
                SizeType.Percent,
                100F));

        var promptLabel = new Label
        {
            Text = prompt,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            ForeColor = Theme.Text,
            TextAlign = ContentAlignment.MiddleLeft,
            UseMnemonic = false
        };

        var inputBox = new TextBox
        {
            Text = UpgradeLegacyProfileDisplayName(
                initialValue),
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 5, 0, 7),
            BackColor = Theme.Control,
            ForeColor = Theme.Text,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 10F),
            MaxLength = 48
        };

        Button cancelButton =
            CreateSecondaryButton("Cancel");

        cancelButton.Size =
            new Size(112, 38);

        cancelButton.Margin =
            new Padding(0, 0, 10, 0);

        cancelButton.DialogResult =
            DialogResult.Cancel;

        Button okButton =
            CreatePrimaryButton("OK");

        okButton.Size =
            new Size(112, 38);

        okButton.Margin =
            Padding.Empty;

        okButton.DialogResult =
            DialogResult.OK;

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = new Padding(0, 7, 0, 0),
            BackColor = Theme.Background,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };

        // RightToLeft flow means add OK first, then Cancel.
        buttonPanel.Controls.Add(okButton);
        buttonPanel.Controls.Add(cancelButton);

        layout.Controls.Add(
            promptLabel,
            0,
            0);

        layout.Controls.Add(
            inputBox,
            0,
            1);

        layout.Controls.Add(
            buttonPanel,
            0,
            2);

        dialog.Controls.Add(layout);
        dialog.AcceptButton = okButton;
        dialog.CancelButton = cancelButton;

        dialog.Shown +=
            (_, _) =>
            {
                inputBox.SelectAll();
                inputBox.Focus();
            };

        return dialog.ShowDialog(this) == DialogResult.OK
            ? inputBox.Text
            : null;
    }

    private void BuildTrimTab(Control parent)
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Theme.Background
        };

        layout.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                100F));

        layout.RowStyles.Add(
            new RowStyle(
                SizeType.Percent,
                40F));

        layout.RowStyles.Add(
            new RowStyle(
                SizeType.Percent,
                52F));

        layout.RowStyles.Add(
            new RowStyle(
                SizeType.Percent,
                8F));

        Control liveMonitor = CreateLiveMonitorCard();
        // Reserve space for the heading, rudder axis, assist controls and live
        // indicators; percentage sizing could collapse the rudder axis row.
        layout.RowStyles[0] = new RowStyle(SizeType.Absolute, 500F);

        var middleLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 10, 0, 10),
            Padding = Padding.Empty,
            BackColor = Theme.Background
        };

        middleLayout.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                61F));

        middleLayout.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                39F));

        middleLayout.RowStyles.Add(
            new RowStyle(
                SizeType.Percent,
                100F));

        Control bindings = CreateBindingsCard();
        bindings.Margin = new Padding(0, 0, 5, 0);

        Control sensitivity = CreateSensitivityCard();
        sensitivity.Margin = new Padding(5, 0, 0, 0);

        middleLayout.Controls.Add(bindings, 0, 0);
        middleLayout.Controls.Add(sensitivity, 1, 0);

        Control activity = CreateActivityCard();

        layout.Controls.Add(liveMonitor, 0, 0);
        layout.Controls.Add(middleLayout, 0, 1);
        layout.Controls.Add(activity, 0, 2);

        // Keep the monitor and bindings readable on short screens without making
        // the first viewport look like the lower panels were accidentally cut off.
        RegisterResponsiveScrollSurface(parent, layout, 1100);
    }

    private Control CreateLiveMonitorCard()
    {
        Panel panel = CreateSectionPanel(Point.Empty, Size.Empty);
        panel.Dock = DockStyle.Fill;
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3,
            Margin = Padding.Empty, Padding = new Padding(16, 12, 16, 14),
            BackColor = Theme.Panel
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 84F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
        var heading = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2,
            Margin = new Padding(0, 2, 0, 2), BackColor = Theme.Panel
        };
        for (int i = 0; i < 3; i++)
            heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
        heading.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
        heading.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        var title = I18n(new Label
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty, Font = new Font("Segoe UI Semibold", 11F),
            ForeColor = Theme.Text, TextAlign = ContentAlignment.MiddleLeft,
            UseMnemonic = false
        }, "Dashboard.LiveMonitor");
        var legend = I18n(new Label
        {
            Dock = DockStyle.Fill, Margin = Padding.Empty,
            Font = new Font("Segoe UI", 8F), ForeColor = Theme.Muted,
            TextAlign = ContentAlignment.MiddleRight, UseMnemonic = false
        }, "Dashboard.Legend");
        heading.Controls.Add(title, 0, 0);
        heading.Controls.Add(legend, 1, 0);
        heading.SetColumnSpan(legend, 2);
        _instructorModeButton = CreateSecondaryButton(VT("Dashboard.InstructorOff"));
        _instructorModeButton.Click += (_, _) => ApplyTrimAction(TrimAction.ToggleInstructor);
        _storeTrimButton = I18n(CreateSecondaryButton("Store Trim"), "Dashboard.StoreTrim");
        _storeTrimButton.BackColor = Theme.RudderControl;
        _storeTrimButton.FlatAppearance.BorderColor = Theme.RudderBorder;
        _storeTrimButton.Click += (_, _) => ApplyTrimAction(TrimAction.StoreCurrentTrim);
        Button reset = I18n(CreateResetButton("Center All Trim"), "Dashboard.CenterAllTrim");
        reset.Click += (_, _) => ApplyTrimAction(TrimAction.ResetAll);
        Button[] actions = { _instructorModeButton, _storeTrimButton, reset };
        for (int i = 0; i < actions.Length; i++)
        {
            actions[i].Dock = DockStyle.Fill;
            actions[i].Margin = new Padding(i == 0 ? 0 : 5, 4, i == 2 ? 0 : 5, 6);
            heading.Controls.Add(actions[i], i, 1);
        }
        _toolTip.SetToolTip(_instructorModeButton,
            "Toggle pitch attitude hold and pitch-rate dampening.");
        _toolTip.SetToolTip(_storeTrimButton,
            "Capture the current curved stick/pedal command as trim, then return controls to center. During armed Horizontal Assist calibration this captures calibration only.");
        _toolTip.SetToolTip(reset,
            VT("Instructor.TrimStored"));
        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1,
            Margin = Padding.Empty, BackColor = Theme.Panel
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36F));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32F));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32F));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        Control values = CreateTrimValuesCard();
        Control stick = CreateStickPreviewCard();
        Control rudder = CreateRudderPreviewCard();
        values.Margin = new Padding(0, 0, 5, 0);
        stick.Margin = new Padding(5, 0, 5, 0);
        rudder.Margin = new Padding(0, 6, 0, 0);
        var axes = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2,
            Margin = new Padding(5, 0, 5, 0), BackColor = Theme.Panel
        };
        axes.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        axes.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        axes.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
        stick.Margin = Padding.Empty;
        axes.Controls.Add(stick, 0, 0);
        axes.Controls.Add(rudder, 0, 1);
        Control instructorStrip = CreateInstructorStrip();
        content.Controls.Add(values, 0, 0);
        content.Controls.Add(axes, 1, 0);
        var instructorControls = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3,
            Margin = new Padding(5, 0, 0, 0), BackColor = Theme.Panel
        };
        instructorControls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        instructorControls.RowStyles.Add(new RowStyle(SizeType.Absolute, Math.Max(28, Font.Height + 12) * 5 + 8));
        instructorControls.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        instructorControls.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        instructorControls.Controls.Add(_instructorLive!, 0, 0);
        instructorControls.Controls.Add(_instructorSettingsButton!, 0, 1);
        content.Controls.Add(instructorControls, 2, 0);
        layout.Controls.Add(heading, 0, 0);
        layout.Controls.Add(content, 0, 1);
        layout.Controls.Add(instructorStrip, 0, 2);
        panel.Controls.Add(layout);
        UpdateInstructorModeUi();
        return panel;
    }

    private Control CreateTrimValuesCard()
    {
        Panel card = CreateInnerPanel(
            Point.Empty,
            Size.Empty);

        card.Dock = DockStyle.Fill;

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = new Padding(14, 10, 14, 10),
            BackColor = Theme.Inner
        };

        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

        grid.RowStyles.Add(
            new RowStyle(
                SizeType.Percent,
                33.333F));

        grid.RowStyles.Add(
            new RowStyle(
                SizeType.Percent,
                33.333F));

        grid.RowStyles.Add(
            new RowStyle(
                SizeType.Percent,
                33.333F));

        _pitchValueLabel =
            CreateAxisValueLabel(
                "Pitch Trim  0.00%",
                Point.Empty);

        _rollValueLabel =
            CreateAxisValueLabel(
                "Roll Trim  0.00%",
                Point.Empty);

        _rudderValueLabel =
            CreateAxisValueLabel(
                "Rudder Trim  0.00%",
                Point.Empty);

        _pitchBar = CreateTrimBar(
            Point.Empty,
            Size.Empty);

        _rollBar = CreateTrimBar(
            Point.Empty,
            Size.Empty);

        _rudderBar = CreateTrimBar(
            Point.Empty,
            Size.Empty);

        AddTrimValueRow(
            grid,
            0,
            _pitchValueLabel,
            _pitchBar);

        AddTrimValueRow(
            grid,
            1,
            _rollValueLabel,
            _rollBar);

        AddTrimValueRow(
            grid,
            2,
            _rudderValueLabel,
            _rudderBar);

        card.Controls.Add(grid);

        return card;
    }

    private static void AddTrimValueRow(
        TableLayoutPanel grid,
        int row,
        Label label,
        TrimBar bar)
    {
        label.Dock = DockStyle.Fill;
        label.Margin = new Padding(0, 3, 8, 3);
        label.TextAlign = ContentAlignment.MiddleLeft;

        bar.Dock = DockStyle.Fill;
        bar.Margin = new Padding(0, 8, 0, 8);

        grid.Controls.Add(label, 0, row);
        grid.Controls.Add(bar, 1, row);
    }

    private Control CreateStickPreviewCard()
    {
        Panel card = CreateInnerPanel(
            Point.Empty,
            Size.Empty);

        card.Dock = DockStyle.Fill;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = new Padding(10, 8, 10, 10),
            BackColor = Theme.Inner
        };

        layout.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                100F));

        layout.RowStyles.Add(
            new RowStyle(
                SizeType.Absolute,
                25F));

        layout.RowStyles.Add(
            new RowStyle(
                SizeType.Percent,
                100F));

        var title = I18n(new Label
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Font = new Font("Segoe UI Semibold", 8.5F),
            ForeColor = Theme.Muted,
            TextAlign = ContentAlignment.MiddleCenter
        }, "Dashboard.Stick");

        _stickPreview = new StickAxisPreview
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty
        };

        layout.Controls.Add(title, 0, 0);
        layout.Controls.Add(_stickPreview, 0, 1);

        card.Controls.Add(layout);

        return card;
    }

    private Control CreateRudderPreviewCard()
    {
        Panel card = CreateInnerPanel(
            Point.Empty,
            Size.Empty);

        card.Dock = DockStyle.Fill;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = new Padding(10, 8, 10, 8),
            BackColor = Theme.Inner
        };

        layout.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                100F));

        layout.RowStyles.Add(
            new RowStyle(
                SizeType.Absolute,
                25F));

        layout.RowStyles.Add(
            new RowStyle(
                SizeType.Percent,
                100F));

        layout.RowStyles.Add(
            new RowStyle(
                SizeType.Absolute,
                180F));

        var title = I18n(new Label
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Font = new Font("Segoe UI Semibold", 8.5F),
            ForeColor = Theme.Muted,
            TextAlign = ContentAlignment.MiddleCenter,
            UseMnemonic = false
        }, "Dashboard.RudderYaw");

        _rudderPreview = new RudderAxisPreview
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty
        };

        Control assistPanel =
            CreateHorizontalRudderAssistPanel();

        assistPanel.Margin =
            new Padding(0, 4, 0, 0);

        layout.Controls.Add(title, 0, 0);
        layout.Controls.Add(_rudderPreview, 0, 1);
        layout.Controls.Add(assistPanel, 0, 2);
        assistPanel.Visible = false;
        layout.RowStyles[2].Height = 0;

        card.Controls.Add(layout);

        return card;
    }

    private Control CreateHorizontalRudderAssistPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 4,
            Margin = Padding.Empty,
            Padding = new Padding(6, 3, 6, 3),
            BackColor = Theme.Panel
        };

        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34F));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44F));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22F));

        panel.RowStyles.Add(
            new RowStyle(
                SizeType.Absolute,
                42F));

        panel.RowStyles.Add(
            new RowStyle(
                SizeType.Absolute,
                42F));

        panel.RowStyles.Add(
            new RowStyle(
                SizeType.Absolute,
                42F));

        panel.RowStyles.Add(
            new RowStyle(
                SizeType.Percent,
                100F));

        var assistTitle = new Label
        {
            Text = "Horizontal Assist",
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Font = new Font(
                "Segoe UI Semibold",
                10.5F),
            ForeColor = Theme.Text,
            TextAlign =
                ContentAlignment.MiddleLeft,
            UseMnemonic = false
        };

        _horizontalRudderAssistBox = new CheckBox
        {
            Appearance = Appearance.Button,
            Dock = DockStyle.Fill,
            Margin = new Padding(4, 1, 0, 1),
            Checked = false,
            FlatStyle = FlatStyle.Flat,
            Font = new Font(
                "Segoe UI Semibold",
                10F),
            ForeColor = Theme.Text,
            TextAlign =
                ContentAlignment.MiddleCenter,
            Cursor = Cursors.Hand,
            UseMnemonic = false
        };

        _horizontalRudderAssistBox
            .FlatAppearance.BorderColor =
                Theme.Border;

        _horizontalRudderAssistBox.CheckedChanged +=
            (_, _) =>
            {
                if (!_horizontalRudderAssistBox.Checked)
                {
                    _automaticRudderRollCompensation = 0.0;
                    _automaticRudderPitchCompensation = 0.0;
                }

                UpdateHorizontalRudderAssistUi();

                if (!_loadingSavedSettings)
                {
                    SaveBindings();
                }
            };

        var compensationTitle = new Label
        {
            Text = "Roll Gain",
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Font = new Font(
                "Segoe UI",
                9.2F),
            ForeColor = Theme.Muted,
            TextAlign =
                ContentAlignment.MiddleLeft,
            UseMnemonic = false
        };

        _rudderRollCompensationSlider =
            new RepeatSpeedSlider
            {
                Dock = DockStyle.Fill,
                Margin =
                    new Padding(
                        2,
                        2,
                        4,
                        2),
                Minimum = 0,
                Maximum = 100,
                Value = 35,
                BackColor = Theme.Panel
            };

        _rudderRollCompensationSlider
            .ValueChanged +=
            (_, _) =>
            {
                UpdateHorizontalRudderAssistUi();

                if (!_loadingSavedSettings)
                {
                    SaveBindings();
                }
            };

        _rudderRollCompensationValueLabel =
            new Label
            {
                Text = "35%",
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                Font = new Font(
                    "Segoe UI Semibold",
                    9.2F),
                ForeColor =
                    Theme.AccentLight,
                TextAlign =
                    ContentAlignment.MiddleRight,
                UseMnemonic = false
            };

        var pitchCompensationTitle = new Label
        {
            Text = "Pitch Gain",
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Font = new Font(
                "Segoe UI",
                9.2F),
            ForeColor = Theme.Muted,
            TextAlign =
                ContentAlignment.MiddleLeft,
            UseMnemonic = false
        };

        _rudderPitchCompensationSlider =
            new RepeatSpeedSlider
            {
                Dock = DockStyle.Fill,
                Margin =
                    new Padding(
                        2,
                        2,
                        4,
                        2),
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                BackColor = Theme.Panel
            };

        _rudderPitchCompensationSlider
            .ValueChanged +=
            (_, _) =>
            {
                UpdateHorizontalRudderAssistUi();

                if (!_loadingSavedSettings)
                {
                    SaveBindings();
                }
            };

        _rudderPitchCompensationValueLabel =
            new Label
            {
                Text = "0%",
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                Font = new Font(
                    "Segoe UI Semibold",
                    9.2F),
                ForeColor =
                    Theme.AccentLight,
                TextAlign =
                    ContentAlignment.MiddleRight,
                UseMnemonic = false
            };

        _horizontalRudderCalibrateButton =
            CreateSecondaryButton(
                "CALIB");

        _horizontalRudderCalibrateButton.Dock =
            DockStyle.Fill;

        _horizontalRudderCalibrateButton.Margin =
            new Padding(0, 3, 5, 0);

        _horizontalRudderCalibrateButton.Font =
            new Font(
                "Segoe UI Semibold",
                8.8F);

        _horizontalRudderCalibrateButton.Click +=
            (_, _) =>
                ToggleHorizontalRudderCalibration();

        _horizontalRudderCalibrationStatusLabel =
            new Label
            {
                Text =
                    "READY",
                Dock = DockStyle.Fill,
                Margin = new Padding(4, 3, 0, 0),
                Font = new Font(
                    "Segoe UI",
                    8.7F),
                ForeColor = Theme.Muted,
                TextAlign =
                    ContentAlignment.MiddleLeft,
                AutoEllipsis = false,
                UseMnemonic = false
            };

        panel.Controls.Add(
            assistTitle,
            0,
            0);

        panel.SetColumnSpan(
            assistTitle,
            2);

        panel.Controls.Add(
            _horizontalRudderAssistBox,
            2,
            0);

        panel.Controls.Add(
            compensationTitle,
            0,
            1);

        panel.Controls.Add(
            _rudderRollCompensationSlider,
            1,
            1);

        panel.Controls.Add(
            _rudderRollCompensationValueLabel,
            2,
            1);

        panel.Controls.Add(
            pitchCompensationTitle,
            0,
            2);

        panel.Controls.Add(
            _rudderPitchCompensationSlider,
            1,
            2);

        panel.Controls.Add(
            _rudderPitchCompensationValueLabel,
            2,
            2);

        panel.Controls.Add(
            _horizontalRudderCalibrateButton,
            0,
            3);

        panel.Controls.Add(
            _horizontalRudderCalibrationStatusLabel,
            1,
            3);

        panel.SetColumnSpan(
            _horizontalRudderCalibrationStatusLabel,
            2);

        _toolTip.SetToolTip(
            assistTitle,
            "Adds calibrated Roll and Pitch correction as Rudder trim increases.");

        _toolTip.SetToolTip(
            _horizontalRudderAssistBox,
            "Helps hold the aircraft attitude while Rudder trim moves the aircraft sideways.");

        _toolTip.SetToolTip(
            _rudderRollCompensationSlider,
            "0% = no automatic Roll. 100% = one-for-one Roll gain. Calibration learns the required Roll direction.");

        _toolTip.SetToolTip(
            _rudderPitchCompensationSlider,
            "0% = no automatic Pitch. 100% = one-for-one Pitch gain. Calibration learns the required Pitch direction.");

        _toolTip.SetToolTip(
            _horizontalRudderCalibrateButton,
            "Arm one-shot calibration. Hold Rudder and hold the stick at the exact Roll + Pitch position that keeps the aircraft steady, then press STORE TRIM once.");

        return panel;
    }

    private Control CreateBindingsCard()
    {
        Panel panel = CreateSectionPanel(Point.Empty, Size.Empty);
        panel.Dock = DockStyle.Fill;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = new Padding(16, 12, 16, 14),
            BackColor = Theme.Panel
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 88F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var heading = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            BackColor = Theme.Panel
        };

        var title = new Label
        {
            Text = VT("Trim.Bindings"),
            Tag = "i18n:Trim.Bindings",
            Dock = DockStyle.Top,
            Height = 30,
            Margin = Padding.Empty,
            Font = new Font("Segoe UI Semibold", 12.5F),
            ForeColor = Theme.Text,
            AutoSize = false,
            UseMnemonic = false
        };

        var description = new Label
        {
            Text = VT("Trim.BindingsHelp"),
            Tag = "i18n:Trim.BindingsHelp",
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Font = new Font("Segoe UI", 10.5F),
            ForeColor = Theme.Muted,
            AutoSize = false,
            AutoEllipsis = false,
            UseMnemonic = false
        };

        heading.Controls.Add(description);
        heading.Controls.Add(title);
        heading.Resize += (_, _) =>
        {
            title.Height = Math.Max(22, heading.Height / 3);
            FitTextControl(description, 9.5F);
        };

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 5,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Theme.Panel
        };

        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
        // Five clear command bands: pitch down; roll/center; store/instructor;
        // pitch up; and a dedicated rudder band. This avoids the old crowded
        // layout and prevents the rudder row from being created outside RowCount.
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 18F));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 24F));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 18F));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));

        Button noseDown = CreateBindingButton(TrimAction.NoseDown);
        Button rollLeft = CreateBindingButton(TrimAction.RollLeft);
        Button reset = CreateBindingButton(TrimAction.ResetAll);
        Button instructorMode = CreateBindingButton(TrimAction.ToggleInstructor);
        _instructorBindingButton = instructorMode;
        Button storeTrim = CreateBindingButton(TrimAction.StoreCurrentTrim);
        Button rollRight = CreateBindingButton(TrimAction.RollRight);
        Button noseUp = CreateBindingButton(TrimAction.NoseUp);
        Button rudderLeft = CreateBindingButton(TrimAction.RudderLeft);
        Button rudderRight = CreateBindingButton(TrimAction.RudderRight);

        foreach (Button button in new[]
                 {
                     noseDown,
                     rollLeft,
                     reset,
                     instructorMode,
                     storeTrim,
                     rollRight,
                     noseUp
                 })
        {
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(7, 5, 7, 5);
            button.MinimumSize = new Size(0, 58);
        }

        grid.Controls.Add(noseDown, 1, 0);
        grid.Controls.Add(rollLeft, 0, 1);
        grid.Controls.Add(reset, 1, 1);
        grid.Controls.Add(rollRight, 2, 1);
        grid.Controls.Add(storeTrim, 1, 2);
        grid.Controls.Add(instructorMode, 2, 2);
        grid.Controls.Add(noseUp, 1, 3);

        var rudderSection = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 2,
            Margin = new Padding(5, 4, 5, 0),
            Padding = Padding.Empty,
            BackColor = Theme.Panel
        };

        rudderSection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10F));
        rudderSection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
        rudderSection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
        rudderSection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10F));
        rudderSection.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
        rudderSection.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var rudderLabel = I18n(new Label
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Font = new Font("Segoe UI Semibold", 8F),
            ForeColor = Theme.RudderAccent,
            TextAlign = ContentAlignment.MiddleCenter,
            UseMnemonic = false
        }, "Dashboard.RudderTrim");

        rudderSection.Controls.Add(rudderLabel, 0, 0);
        rudderSection.SetColumnSpan(rudderLabel, 4);

        rudderLeft.Dock = DockStyle.Fill;
        rudderLeft.Margin = new Padding(4, 2, 4, 2);
        rudderLeft.MinimumSize = new Size(0, 36);

        rudderRight.Dock = DockStyle.Fill;
        rudderRight.Margin = new Padding(4, 2, 4, 2);
        rudderRight.MinimumSize = new Size(0, 36);

        rudderSection.Controls.Add(rudderLeft, 1, 1);
        rudderSection.Controls.Add(rudderRight, 2, 1);

        grid.Controls.Add(rudderSection, 0, 4);
        grid.SetColumnSpan(rudderSection, 3);

        layout.Controls.Add(heading, 0, 0);
        layout.Controls.Add(grid, 0, 1);
        panel.Controls.Add(layout);

        return panel;
    }

    private Control CreateSensitivityCard()
    {
        Panel panel = CreateSectionPanel(Point.Empty, Size.Empty);
        panel.Dock = DockStyle.Fill;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Margin = Padding.Empty,
            Padding = new Padding(16, 12, 16, 14),
            BackColor = Theme.Panel
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 78F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 104F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68F));

        var heading = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            BackColor = Theme.Panel
        };

        var title = I18n(new Label
        {
            Dock = DockStyle.Top,
            Height = 32,
            Margin = Padding.Empty,
            Font = new Font("Segoe UI Semibold", 12.5F),
            ForeColor = Theme.Text,
            AutoSize = false,
            UseMnemonic = false
        }, "Sensitivity.Title");

        var description = I18n(new Label
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Font = new Font("Segoe UI", 10.2F),
            ForeColor = Theme.Muted,
            AutoSize = false,
            UseMnemonic = false
        }, "Sensitivity.Description");

        heading.Controls.Add(description);
        heading.Controls.Add(title);
        heading.Resize += (_, _) =>
        {
            title.Height = Math.Max(22, heading.Height / 3);
            FitTextControl(description, 9.5F);
        };

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Theme.Panel
        };

        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 26F));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64F));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10F));

        for (int row = 0; row < 3; row++)
        {
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 33.333F));
        }

        _pitchStepBox = AddStepRow(grid, 0, "Pitch", 0.50M);
        _rollStepBox = AddStepRow(grid, 1, "Roll", 0.50M);
        _rudderStepBox = AddStepRow(grid, 2, "Rudder", 0.50M);

        var speedPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Theme.Panel
        };

        speedPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 72F));
        speedPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28F));
        speedPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
        speedPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var speedTitle = I18n(new Label
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Font = new Font("Segoe UI Semibold", 8.5F),
            ForeColor = Theme.Text,
            TextAlign = ContentAlignment.MiddleLeft,
            UseMnemonic = false
        }, "Sensitivity.HoldSpeed");

        _repeatSpeedValueLabel = new Label
        {
            Text = "35%/s",
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Font = new Font("Segoe UI Semibold", 9F),
            ForeColor = Theme.AccentLight,
            TextAlign = ContentAlignment.MiddleRight,
            UseMnemonic = false
        };

        _repeatSpeedSlider = new RepeatSpeedSlider
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Minimum = 1,
            Maximum = 12,
            Value = 7,
            BackColor = Theme.Panel
        };

        _repeatSpeedSlider.ValueChanged +=
            (_, _) =>
            {
                UpdateRepeatSpeedLabel();

                if (!_loadingSavedSettings)
                {
                    SaveBindings();
                }
            };

        _toolTip.SetToolTip(
            speedTitle,
            "Controls how quickly trim changes while a trim control is held.");

        _toolTip.SetToolTip(
            _repeatSpeedSlider,
            "Left = slower trim movement. Right = faster trim movement.");

        speedPanel.Controls.Add(speedTitle, 0, 0);
        speedPanel.Controls.Add(_repeatSpeedValueLabel, 1, 0);
        speedPanel.Controls.Add(_repeatSpeedSlider, 0, 1);
        speedPanel.SetColumnSpan(_repeatSpeedSlider, 2);

        var triggerModePanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = new Padding(0, 3, 0, 3),
            BackColor = Theme.Panel
        };

        triggerModePanel.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 54F));

        triggerModePanel.ColumnStyles.Add(
            new ColumnStyle(SizeType.Percent, 46F));

        triggerModePanel.RowStyles.Add(
            new RowStyle(SizeType.Percent, 50F));

        triggerModePanel.RowStyles.Add(
            new RowStyle(SizeType.Percent, 50F));

        var triggerModeDescription = I18n(new Label
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Font = new Font("Segoe UI Semibold", 8.5F),
            ForeColor = Theme.Text,
            TextAlign = ContentAlignment.MiddleLeft,
            UseMnemonic = false
        }, "Sensitivity.UniversalTrigger");

        _universalTriggerBox = new CheckBox
        {
            Appearance = Appearance.Button,
            Dock = DockStyle.Fill,
            Margin = new Padding(6, 2, 0, 2),
            Checked = false,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Semibold", 8F),
            ForeColor = Theme.Text,
            TextAlign = ContentAlignment.MiddleCenter,
            Cursor = Cursors.Hand,
            UseMnemonic = false
        };

        _universalTriggerBox.FlatAppearance.BorderColor =
            Theme.Border;

        _universalTriggerBox.CheckedChanged +=
            (_, _) =>
            {
                UpdateUniversalTriggerToggle();

                if (!_loadingSavedSettings)
                {
                    _capturedCombinationTriggers.Clear();
                    SaveBindings();
                }
            };

        triggerModePanel.Controls.Add(
            triggerModeDescription,
            0,
            0);

        triggerModePanel.Controls.Add(
            _universalTriggerBox,
            1,
            0);

        var unassignedGuardDescription = I18n(new Label
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Font = new Font("Segoe UI Semibold", 8.5F),
            ForeColor = Theme.Text,
            TextAlign = ContentAlignment.MiddleLeft,
            UseMnemonic = false
        }, "Sensitivity.InputGuard");

        _unassignedInputGuardBox = new CheckBox
        {
            Appearance = Appearance.Button,
            Dock = DockStyle.Fill,
            Margin = new Padding(6, 2, 0, 2),
            Checked = true,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Semibold", 8F),
            ForeColor = Theme.Text,
            TextAlign = ContentAlignment.MiddleCenter,
            Cursor = Cursors.Hand,
            UseMnemonic = false
        };

        _unassignedInputGuardBox.FlatAppearance.BorderColor =
            Theme.Border;

        _unassignedInputGuardBox.CheckedChanged +=
            (_, _) =>
            {
                ResetForeignChordSuppression();
                UpdateUnassignedInputGuardToggle();

                if (!_loadingSavedSettings)
                {
                    SaveBindings();
                }
            };

        triggerModePanel.Controls.Add(
            unassignedGuardDescription,
            0,
            1);

        triggerModePanel.Controls.Add(
            _unassignedInputGuardBox,
            1,
            1);

        _toolTip.SetToolTip(
            unassignedGuardDescription,
            "ON: the first game/modifier button locks the chord context.\r\n" +
            "Trim triggers such as POV Up cannot override a game button that was pressed first.\r\n" +
            "A configured VTrim modifier/lever can override an old latching game switch.\r\n" +
            "OFF: unassigned buttons are ignored completely.");

        _toolTip.SetToolTip(
            _unassignedInputGuardBox,
            "Chord Lock keeps Trigger 1 + POV Up native to the game, while a configured VTrim modifier can still take control from a latched switch.");

        _toolTip.SetToolTip(
            triggerModeDescription,
            "ON: unassigned directions can still activate normal single trim bindings while a configured modifier is held.\r\n" +
            "OFF: holding any configured modifier blocks all standalone trim bindings.");

        _toolTip.SetToolTip(
            _universalTriggerBox,
            "OFF is isolated mode: a held modifier reserves the POV/buttons for combinations only.");

        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Theme.Panel
        };

        footer.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                100F));

        footer.RowStyles.Add(
            new RowStyle(
                SizeType.Absolute,
                36F));

        footer.RowStyles.Add(
            new RowStyle(
                SizeType.Percent,
                100F));

        _repeatWhileHeldBox = new CheckBox
        {
            Appearance = Appearance.Button,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Checked = true,
            FlatStyle = FlatStyle.Flat,
            Font = new Font(
                "Segoe UI Semibold",
                8.5F),
            ForeColor = Theme.Text,
            TextAlign =
                ContentAlignment.MiddleCenter,
            Cursor = Cursors.Hand,
            UseMnemonic = false
        };

        _repeatWhileHeldBox.FlatAppearance.BorderColor =
            Theme.Border;

        _repeatWhileHeldBox.CheckedChanged +=
            (_, _) =>
            {
                UpdateRepeatWhileHeldToggle();

                if (!_loadingSavedSettings)
                {
                    SaveBindings();
                }
            };

        _toolTip.SetToolTip(
            _repeatWhileHeldBox,
            "ON: holding a trim input moves trim smoothly at the selected % per second.\r\n" +
            "OFF: each press changes trim exactly once by the configured tap step.");

        var hint = I18n(new Label
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Font = new Font("Segoe UI", 7.6F),
            ForeColor = Theme.Muted,
            TextAlign = ContentAlignment.BottomLeft,
            UseMnemonic = false
        }, "Sensitivity.RateHelp");

        footer.Controls.Add(
            _repeatWhileHeldBox,
            0,
            0);

        footer.Controls.Add(
            hint,
            0,
            1);

        layout.Controls.Add(heading, 0, 0);
        layout.Controls.Add(grid, 0, 1);
        layout.Controls.Add(speedPanel, 0, 2);
        layout.Controls.Add(triggerModePanel, 0, 3);
        layout.Controls.Add(footer, 0, 4);

        panel.Controls.Add(layout);
        UpdateRepeatSpeedLabel();
        UpdateHorizontalRudderAssistUi();
        UpdateUniversalTriggerToggle();
        UpdateUnassignedInputGuardToggle();
        UpdateRepeatWhileHeldToggle();

        return panel;
    }

    private void ToggleHorizontalRudderCalibration()
    {
        if (_horizontalRudderCalibrationArmed)
        {
            _horizontalRudderCalibrationArmed = false;
            _horizontalRudderCalibrationMessage =
                "READY";

            UpdateHorizontalRudderAssistUi();

            SetInstruction(
                "Horizontal Assist calibration cancelled.",
                Theme.Muted);

            return;
        }

        if (_rollAxis is null ||
            _pitchAxis is null ||
            _rudderAxis is null)
        {
            _horizontalRudderCalibrationMessage =
                "CHECK AXES";

            UpdateHorizontalRudderAssistUi();

            SetInstruction(
                "Configure Roll, Pitch and Rudder axes before calibrating Horizontal Assist.",
                Theme.Error);

            return;
        }

        if (_storeTrimReturn.AnyWaiting)
        {
            SetInstruction("Return the stick and pedals to center before arming calibration.", Theme.Warning);
            return;
        }
        ResetAutomaticInstructorHold();

        _horizontalRudderCalibrationArmed = true;
        _horizontalRudderCalibrationMessage =
            "ARMED";

        // Calibration must measure the aircraft's natural Rudder coupling,
        // so existing automatic Roll/Pitch compensation is bypassed.
        _automaticRudderRollCompensation = 0.0;
        _automaticRudderPitchCompensation = 0.0;

        UpdateHorizontalRudderAssistUi();

        SetInstruction(
            "Horizontal Assist calibration armed. In-game, hold the Rudder amount you want to test and hold the stick at the exact Roll AND Pitch position that keeps the aircraft steady. Press STORE TRIM once to teach VTrim both corrections.",
            Theme.Warning);
    }

    private bool TryCaptureHorizontalRudderCalibration()
    {
        double rudder =
            _lastPhysicalRudder;

        double roll =
            _lastPhysicalRoll;

        double pitch =
            _lastPhysicalPitch;

        double rudderMagnitude =
            Math.Abs(rudder);

        double rollMagnitude =
            Math.Abs(roll);

        double pitchMagnitude =
            Math.Abs(pitch);

        if (rudderMagnitude < 0.08)
        {
            _horizontalRudderCalibrationMessage =
                "MORE RUDDER";

            UpdateHorizontalRudderAssistUi();

            SetInstruction(
                "Calibration not captured: use at least about 8% Rudder, hold the aircraft steady with Roll and Pitch, then press STORE TRIM again.",
                Theme.Warning);

            return false;
        }

        // Learn signed Roll and Pitch ratios against the SAME Rudder sample.
        //
        // Example:
        //   Rudder 50%
        //   Roll   20%  -> Roll Gain 40%
        //   Pitch  10%  -> Pitch Gain 20%
        //
        // Direction is learned independently for Roll and Pitch. No sign is
        // assumed, so inverted axes inside VTrim or the game are supported.
        double signedRollRatio =
            Math.Abs(rudder) <= 0.000001
                ? 0.0
                : roll / rudder;

        double signedPitchRatio =
            Math.Abs(rudder) <= 0.000001
                ? 0.0
                : pitch / rudder;

        double measuredRollPercent =
            Math.Abs(signedRollRatio) *
            100.0;

        double measuredPitchPercent =
            Math.Abs(signedPitchRatio) *
            100.0;

        int calibratedRollPercent =
            Math.Clamp(
                (int)Math.Round(
                    measuredRollPercent,
                    MidpointRounding.AwayFromZero),
                0,
                100);

        int calibratedPitchPercent =
            Math.Clamp(
                (int)Math.Round(
                    measuredPitchPercent,
                    MidpointRounding.AwayFromZero),
                0,
                100);

        if (rollMagnitude > 0.005)
        {
            _rudderRollCompensationDirection =
                signedRollRatio >= 0.0
                    ? 1
                    : -1;
        }

        if (pitchMagnitude > 0.005)
        {
            _rudderPitchCompensationDirection =
                signedPitchRatio >= 0.0
                    ? 1
                    : -1;
        }

        _rudderRollCompensationSlider.Value =
            calibratedRollPercent;

        _rudderPitchCompensationSlider.Value =
            calibratedPitchPercent;

        _horizontalRudderAssistBox.Checked =
            true;

        _horizontalRudderCalibrationArmed =
            false;

        _horizontalRudderCalibrationMessage =
            "CALIBRATED";

        UpdateHorizontalRudderAssistUi();

        if (!_loadingSavedSettings)
        {
            SaveBindings();
        }

        string rollDirection =
            _rudderRollCompensationDirection > 0
                ? "same"
                : "opposite";

        string pitchDirection =
            _rudderPitchCompensationDirection > 0
                ? "same"
                : "opposite";

        SetInstruction(
            $"Horizontal Assist calibrated: Roll {calibratedRollPercent}% ({rollDirection} direction), Pitch {calibratedPitchPercent}% ({pitchDirection} direction). More Rudder trim now adds both corrections proportionally. STORE TRIM did not change aircraft trim.",
            Theme.Success);

        return true;
    }

    private double CalculateHorizontalRudderRollCompensation(
        double rudderTrimPercent)
    {
        if (_horizontalRudderAssistBox is null ||
            _rudderRollCompensationSlider is null ||
            !_horizontalRudderAssistBox.Checked ||
            _horizontalRudderCalibrationArmed)
        {
            return 0.0;
        }

        double gain =
            Math.Clamp(
                _rudderRollCompensationSlider.Value,
                0,
                100) /
            100.0;

        // Linear proportional assist:
        // the farther Rudder trim moves from center, the more Roll is added.
        //
        // Example at 40% gain:
        // 25% Rudder trim -> 10% Roll
        // 50% Rudder trim -> 20% Roll
        // 75% Rudder trim -> 30% Roll
        return Math.Clamp(
            rudderTrimPercent *
            gain *
            _rudderRollCompensationDirection,
            -95.0,
            95.0);
    }

    private double CalculateHorizontalRudderPitchCompensation(
        double rudderTrimPercent)
    {
        if (_horizontalRudderAssistBox is null ||
            _rudderPitchCompensationSlider is null ||
            !_horizontalRudderAssistBox.Checked ||
            _horizontalRudderCalibrationArmed)
        {
            return 0.0;
        }

        double gain =
            Math.Clamp(
                _rudderPitchCompensationSlider.Value,
                0,
                100) /
            100.0;

        // Same proportional model as Roll:
        // more Rudder trim = more of the learned Pitch correction.
        return Math.Clamp(
            rudderTrimPercent *
            gain *
            _rudderPitchCompensationDirection,
            -95.0,
            95.0);
    }

    private void UpdateHorizontalRudderAssistUi()
    {
        if (_horizontalRudderAssistBox is null ||
            _rudderRollCompensationSlider is null ||
            _rudderRollCompensationValueLabel is null ||
            _rudderPitchCompensationSlider is null ||
            _rudderPitchCompensationValueLabel is null)
        {
            return;
        }

        bool enabled =
            _horizontalRudderAssistBox.Checked;

        _horizontalRudderAssistBox.Text =
            enabled
                ? "ON"
                : "OFF";

        _horizontalRudderAssistBox.BackColor =
            enabled
                ? Theme.RudderControl
                : Theme.TrimControl;

        _horizontalRudderAssistBox.ForeColor =
            Theme.Text;

        _horizontalRudderAssistBox.FlatAppearance.BorderColor =
            enabled
                ? Theme.RudderBorder
                : Theme.TrimBorder;

        _horizontalRudderAssistBox.FlatAppearance.MouseOverBackColor =
            enabled
                ? Theme.RudderControlHover
                : Theme.TrimControlHover;

        _horizontalRudderAssistBox.FlatAppearance.MouseDownBackColor =
            enabled
                ? Theme.RudderControlPressed
                : Theme.TrimControlPressed;

        _rudderRollCompensationSlider.Enabled =
            enabled;

        _rudderRollCompensationValueLabel.Text =
            $"{_rudderRollCompensationSlider.Value}%";

        _rudderRollCompensationValueLabel.ForeColor =
            enabled
                ? Theme.AccentLight
                : Theme.Muted;

        _rudderPitchCompensationSlider.Enabled =
            enabled;

        _rudderPitchCompensationValueLabel.Text =
            $"{_rudderPitchCompensationSlider.Value}%";

        _rudderPitchCompensationValueLabel.ForeColor =
            enabled
                ? Theme.AccentLight
                : Theme.Muted;

        if (_horizontalRudderCalibrateButton is not null)
        {
            _horizontalRudderCalibrateButton.Text =
                _horizontalRudderCalibrationArmed
                    ? "CANCEL"
                    : "CALIB";

            _horizontalRudderCalibrateButton.BackColor =
                _horizontalRudderCalibrationArmed
                    ? Theme.WarningControl
                    : Theme.Control;

            _horizontalRudderCalibrateButton.FlatAppearance.BorderColor =
                _horizontalRudderCalibrationArmed
                    ? Theme.WarningBorder
                    : Theme.Border;

            _horizontalRudderCalibrateButton.FlatAppearance.MouseOverBackColor =
                _horizontalRudderCalibrationArmed
                    ? Theme.WarningControlHover
                    : Theme.ControlHover;

            _horizontalRudderCalibrateButton.FlatAppearance.MouseDownBackColor =
                _horizontalRudderCalibrationArmed
                    ? Theme.WarningControlPressed
                    : Theme.ControlPressed;
        }

        if (_horizontalRudderCalibrationStatusLabel is not null)
        {
            _horizontalRudderCalibrationStatusLabel.Text =
                !string.IsNullOrWhiteSpace(
                    _horizontalRudderCalibrationMessage)
                    ? _horizontalRudderCalibrationMessage
                    : "READY";

            _horizontalRudderCalibrationStatusLabel.ForeColor =
                _horizontalRudderCalibrationArmed
                    ? Theme.Warning
                    : Theme.Muted;
        }
    }

    private void UpdateRepeatWhileHeldToggle()
    {
        if (_repeatWhileHeldBox is null)
        {
            return;
        }

        bool repeatEnabled =
            _repeatWhileHeldBox.Checked;

        _repeatWhileHeldBox.Text =
            repeatEnabled
                ? VT("Sensitivity.RepeatOn")
                : VT("Sensitivity.RepeatOff");

        _repeatWhileHeldBox.BackColor =
            repeatEnabled
                ? Theme.RudderControl
                : Theme.TrimControl;

        _repeatWhileHeldBox.ForeColor =
            Theme.Text;

        _repeatWhileHeldBox.FlatAppearance.BorderColor =
            repeatEnabled
                ? Theme.RudderBorder
                : Theme.TrimBorder;

        _repeatWhileHeldBox.FlatAppearance.MouseOverBackColor =
            repeatEnabled
                ? Theme.RudderControlHover
                : Theme.TrimControlHover;

        _repeatWhileHeldBox.FlatAppearance.MouseDownBackColor =
            repeatEnabled
                ? Theme.RudderControlPressed
                : Theme.TrimControlPressed;
    }

    private void UpdateUnassignedInputGuardToggle()
    {
        if (_unassignedInputGuardBox is null)
        {
            return;
        }

        bool guardEnabled =
            _unassignedInputGuardBox.Checked;

        _unassignedInputGuardBox.Text =
            guardEnabled
                ? VT("Sensitivity.GuardOn")
                : VT("Sensitivity.GuardOff");

        _unassignedInputGuardBox.BackColor =
            guardEnabled
                ? Theme.WarningControl
                : Theme.RudderControl;

        _unassignedInputGuardBox.ForeColor =
            Theme.Text;

        _unassignedInputGuardBox.FlatAppearance.BorderColor =
            guardEnabled
                ? Theme.WarningBorder
                : Theme.RudderBorder;

        _unassignedInputGuardBox.FlatAppearance.MouseOverBackColor =
            guardEnabled
                ? Theme.WarningControlHover
                : Theme.RudderControlHover;

        _unassignedInputGuardBox.FlatAppearance.MouseDownBackColor =
            guardEnabled
                ? Theme.WarningControlPressed
                : Theme.RudderControlPressed;
    }

    private void UpdateUniversalTriggerToggle()
    {
        if (_universalTriggerBox is null)
        {
            return;
        }

        bool passthroughEnabled =
            _universalTriggerBox.Checked;

        _universalTriggerBox.Text =
            passthroughEnabled
                ? VT("Sensitivity.TriggerOn")
                : VT("Sensitivity.TriggerOff");

        _universalTriggerBox.BackColor =
            passthroughEnabled
                ? Theme.WarningControl
                : Theme.RudderControl;

        _universalTriggerBox.ForeColor =
            Theme.Text;

        _universalTriggerBox.FlatAppearance.BorderColor =
            passthroughEnabled
                ? Theme.WarningBorder
                : Theme.RudderBorder;

        _universalTriggerBox.FlatAppearance.MouseOverBackColor =
            passthroughEnabled
                ? Theme.WarningControlHover
                : Theme.RudderControlHover;

        _universalTriggerBox.FlatAppearance.MouseDownBackColor =
            passthroughEnabled
                ? Theme.WarningControlPressed
                : Theme.RudderControlPressed;
    }

    private void UpdateRepeatSpeedLabel()
    {
        if (_repeatSpeedSlider is null ||
            _repeatSpeedValueLabel is null)
        {
            return;
        }

        double rate =
            GetHeldTrimRatePercentPerSecond();

        _repeatSpeedValueLabel.Text =
            $"{rate:0.#}%/s";
    }

    private double GetHeldTrimRatePercentPerSecond()
    {
        int speed =
            Math.Clamp(
                _repeatSpeedSlider?.Value ?? 7,
                1,
                12);

        double[] rates =
        {
            0.0,
            0.75,
            1.5,
            3.0,
            5.0,
            8.0,
            12.0,
            20.0,
            35.0,
            60.0,
            90.0,
            130.0,
            180.0
        };

        return rates[speed];
    }

    private static double GetHeldTrimStartDelaySeconds()
    {
        // A short delay keeps quick taps precise. Once the switch is held,
        // trim begins moving continuously instead of repeating in bursts.
        return 0.16;
    }

    private StepEditor AddStepRow(
        TableLayoutPanel grid,
        int row,
        string axisName,
        decimal defaultValue)
    {
        var label = new Label
        {
            Text = axisName,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 3, 8, 3),
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.Text
        };

        var editor = new StepEditor(
            defaultValue,
            minimum: 0.01M,
            maximum: 10.00M,
            increment: 0.05M)
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 6, 8, 6)
        };

        var unit = new Label
        {
            Text = "%",
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 3, 0, 3),
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Theme.Muted
        };

        grid.Controls.Add(label, 0, row);
        grid.Controls.Add(editor, 1, row);
        grid.Controls.Add(unit, 2, row);

        return editor;
    }

    private Control CreateActivityCard()
    {
        Panel panel = CreateSectionPanel(Point.Empty, Size.Empty);
        panel.Dock = DockStyle.Fill;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = new Padding(14, 6, 14, 6),
            BackColor = Theme.Panel
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 33.333F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 33.333F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 33.333F));

        _lastInputLabel = new Label
        {
            Text = VT("Status.LastInputNone"),
            Tag = "i18n:Status.LastInputNone",
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            AutoEllipsis = false,
            ForeColor = Theme.AccentLight,
            TextAlign = ContentAlignment.MiddleLeft,
            UseMnemonic = false
        };

        _instructionLabel = new Label
        {
            Text = VT("Status.ConfigureAxes"),
            Tag = "i18n:Status.ConfigureAxes",
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            AutoEllipsis = false,
            ForeColor = Theme.Muted,
            Font = new Font("Segoe UI", 11F),
            TextAlign = ContentAlignment.MiddleLeft,
            UseMnemonic = false
        };

        _statusLabel = new Label
        {
            Text = VT("Status.DirectInputNotStarted"),
            Tag = "i18n:Status.DirectInputNotStarted",
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            AutoEllipsis = false,
            ForeColor = Theme.Success,
            TextAlign = ContentAlignment.MiddleLeft,
            UseMnemonic = false
        };

        layout.Controls.Add(_lastInputLabel, 0, 0);
        layout.Controls.Add(_instructionLabel, 0, 1);
        layout.Controls.Add(_statusLabel, 0, 2);
        panel.Controls.Add(layout);

        return panel;
    }

    private void BuildSetupTab(Control parent)
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Theme.Background
        };

        layout.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                100F));

        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 262F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 156F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 130F));

        Control deviceCard = CreateDeviceCard();
        deviceCard.Margin = new Padding(0, 0, 0, 8);

        Control axisCard = CreateAxisMappingCard();
        axisCard.Margin = new Padding(0, 0, 0, 8);

        Control vJoyCard = CreateVJoyCard();
        vJoyCard.Margin = new Padding(0, 0, 0, 8);

        Control instructionsCard =
            CreateWarThunderCard();

        layout.Controls.Add(vJoyCard, 0, 0);
        layout.Controls.Add(deviceCard, 0, 1);
        layout.Controls.Add(axisCard, 0, 2);
        layout.Controls.Add(instructionsCard, 0, 3);

        // Embedded hosts can be shorter than the setup page. Scroll vertically
        // instead of compressing device/axis controls until labels are clipped.
        RegisterResponsiveScrollSurface(parent, layout, 1002);
    }

    private Control CreateDeviceCard()
    {
        Panel panel = CreateSectionPanel(
            Point.Empty,
            Size.Empty);

        panel.Dock = DockStyle.Fill;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = new Padding(16, 10, 16, 10),
            BackColor = Theme.Panel
        };

        layout.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                100F));

        layout.RowStyles.Add(
            new RowStyle(
                SizeType.Absolute,
                34F));

        layout.RowStyles.Add(
            new RowStyle(
                SizeType.Percent,
                100F));

        layout.RowStyles.Add(
            new RowStyle(
                SizeType.Absolute,
                36F));

        var title = new Label
        {
            Text = VT("Devices.Input"),
            Tag = "i18n:Devices.Input",
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Font = new Font("Segoe UI Semibold", 11F),
            ForeColor = Theme.Text,
            TextAlign = ContentAlignment.MiddleLeft
        };

        var controls = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Theme.Panel
        };

        controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54F));
        controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 27F));
        controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 19F));

        controls.RowStyles.Add(
            new RowStyle(
                SizeType.Percent,
                100F));

        _deviceBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 6, 8, 6),
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Control,
            ForeColor = Theme.Text,
            Font = new Font("Segoe UI", 11.5F)
        };

        _deviceBox.SelectedIndexChanged +=
            (_, _) =>
            {
                if (_deviceBox.SelectedItem
                    is not InputDevice device)
                {
                    return;
                }

                _selectedDeviceGuid =
                    device.Guid;

                _selectedDeviceName =
                    device.FriendlyName;

                _deviceStatusLabel.Text =
                    $"Selected: {device.FriendlyName}";

                _deviceStatusLabel.ForeColor =
                    Theme.Success;
            };

        _autoDetectDeviceButton =
            CreatePrimaryButton(
                VT("Devices.AutoDetect"));

        _autoDetectDeviceButton.Tag = "i18n:Devices.AutoDetect";

        _autoDetectDeviceButton.Dock =
            DockStyle.Fill;

        _autoDetectDeviceButton.Margin =
            new Padding(4, 5, 4, 5);

        _autoDetectDeviceButton.Click +=
            (_, _) =>
                BeginDeviceAutoDetection();

        var refreshButton =
            CreateSecondaryButton(
                VT("Devices.Refresh"));

        refreshButton.Tag = "i18n:Devices.Refresh";

        refreshButton.Dock =
            DockStyle.Fill;

        refreshButton.Margin =
            new Padding(4, 5, 0, 5);

        refreshButton.Click +=
            (_, _) =>
                RefreshDevices();

        controls.Controls.Add(_deviceBox, 0, 0);
        controls.Controls.Add(
            _autoDetectDeviceButton,
            1,
            0);

        controls.Controls.Add(
            refreshButton,
            2,
            0);

        _deviceStatusLabel = new Label
        {
            Text =
                "Searching for DirectInput devices...",
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            AutoEllipsis = false,
            Font = new Font("Segoe UI", 10.5F),
            ForeColor = Theme.Muted,
            TextAlign =
                ContentAlignment.MiddleLeft
        };

        layout.Controls.Add(title, 0, 0);
        layout.Controls.Add(controls, 0, 1);
        layout.Controls.Add(
            _deviceStatusLabel,
            0,
            2);

        panel.Controls.Add(layout);

        return panel;
    }

    private Control CreateAxisMappingCard()
    {
        Panel panel = CreateSectionPanel(
            Point.Empty,
            Size.Empty);

        panel.Dock = DockStyle.Fill;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = new Padding(16, 10, 16, 12),
            BackColor = Theme.Panel
        };

        layout.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                100F));

        layout.RowStyles.Add(
            new RowStyle(
                SizeType.Absolute,
                34F));

        layout.RowStyles.Add(
            new RowStyle(
                SizeType.Percent,
                100F));

        layout.RowStyles.Add(
            new RowStyle(
                SizeType.Absolute,
                38F));

        var heading = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Theme.Panel
        };

        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 80F));
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));

        var title = new Label
        {
            Text = VT("Devices.Routing"),
            Tag = "i18n:Devices.Routing",
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Font = new Font("Segoe UI Semibold", 13F),
            ForeColor = Theme.Text,
            TextAlign = ContentAlignment.MiddleLeft
        };

        var recenterAllButton =
            CreatePrimaryButton(
                VT("Devices.RecenterAll"));

        recenterAllButton.Tag = "i18n:Devices.RecenterAll";

        recenterAllButton.Dock =
            DockStyle.Fill;

        recenterAllButton.Margin =
            new Padding(4, 0, 0, 0);

        recenterAllButton.Click +=
            (_, _) =>
                RecenterAllConfiguredAxes(
                    showStatus: true);

        heading.Controls.Add(title, 0, 0);
        heading.Controls.Add(
            recenterAllButton,
            1,
            0);

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            RowCount = 4,
            Margin = new Padding(0, 8, 0, 8),
            Padding = Padding.Empty,
            BackColor = Theme.Panel
        };

        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 9F));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33F));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 23F));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 14F));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 21F));

        grid.RowStyles.Add(
            new RowStyle(
                SizeType.Absolute,
                0F));

        for (int row = 1; row < 4; row++)
        {
            grid.RowStyles.Add(
                new RowStyle(
                    SizeType.Percent,
                    33.333F));
        }

        _rollAxisLabel =
            CreateAxisBindingLabel(
                VT("Common.NotConfigured"));

        _pitchAxisLabel =
            CreateAxisBindingLabel(
                VT("Common.NotConfigured"));

        _rudderAxisLabel =
            CreateAxisBindingLabel(
                VT("Common.NotConfigured"));

        _rollAxisButton =
            CreateSecondaryButton(
                VT("Devices.DetectRoll"));

        _pitchAxisButton =
            CreateSecondaryButton(
                VT("Devices.DetectPitch"));

        _rudderAxisButton =
            CreateSecondaryButton(
                VT("Devices.DetectRudder"));

        _rollAxisButton.Tag = "i18n:Devices.DetectRoll";

        _rollAxisButton.Click +=
            (_, _) =>
                BeginAxisDetection(
                    AxisTarget.Roll);

        _pitchAxisButton.Tag = "i18n:Devices.DetectPitch";

        _pitchAxisButton.Click +=
            (_, _) =>
                BeginAxisDetection(
                    AxisTarget.Pitch);

        _rudderAxisButton.Tag = "i18n:Devices.DetectRudder";

        _rudderAxisButton.Click +=
            (_, _) =>
                BeginAxisDetection(
                    AxisTarget.Rudder);

        _invertRollBox =
            CreateInvertBox();

        _invertPitchBox =
            CreateInvertBox();

        _invertRudderBox =
            CreateInvertBox();

        var centerRollButton =
            CreateSmallButton(
                VT("Devices.Recenter"));
        centerRollButton.Tag = "i18n:Devices.Recenter";

        var centerPitchButton =
            CreateSmallButton(
                VT("Devices.Recenter"));
        centerPitchButton.Tag = "i18n:Devices.Recenter";

        var centerRudderButton =
            CreateSmallButton(
                VT("Devices.Recenter"));
        centerRudderButton.Tag = "i18n:Devices.Recenter";

        centerRollButton.Click +=
            (_, _) =>
                RecenterAxis(
                    AxisTarget.Roll);

        centerPitchButton.Click +=
            (_, _) =>
                RecenterAxis(
                    AxisTarget.Pitch);

        centerRudderButton.Click +=
            (_, _) =>
                RecenterAxis(
                    AxisTarget.Rudder);

        AddAxisMappingRow(
            grid,
            1,
            "Roll",
            _rollAxisLabel,
            _rollAxisButton,
            _invertRollBox,
            centerRollButton);

        AddAxisMappingRow(
            grid,
            2,
            "Pitch",
            _pitchAxisLabel,
            _pitchAxisButton,
            _invertPitchBox,
            centerPitchButton);

        AddAxisMappingRow(
            grid,
            3,
            "Rudder",
            _rudderAxisLabel,
            _rudderAxisButton,
            _invertRudderBox,
            centerRudderButton);

        var help = new Label
        {
            Text =
                "Start from neutral, click Detect, then move only the requested axis.",
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Font = new Font("Segoe UI", 11F),
            ForeColor = Theme.Muted,
            TextAlign = ContentAlignment.MiddleLeft
        };

        layout.Controls.Add(heading, 0, 0);
        layout.Controls.Add(grid, 0, 1);
        layout.Controls.Add(help, 0, 2);

        panel.Controls.Add(layout);

        return panel;
    }

    private static void AddAxisHeader(
        TableLayoutPanel grid,
        int column,
        string text)
    {
        var label = new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            Margin = new Padding(4, 0, 4, 0),
            Font = new Font("Segoe UI Semibold", 9.2F),
            ForeColor = Theme.Muted,
            TextAlign = ContentAlignment.MiddleLeft
        };

        grid.Controls.Add(label, column, 0);
    }

    private void AddAxisMappingRow(
        TableLayoutPanel grid,
        int row,
        string axisName,
        Label bindingLabel,
        Button detectButton,
        CheckBox invertBox,
        Button centerButton)
    {
        var nameLabel = new Label
        {
            Text = axisName,
            Dock = DockStyle.Fill,
            Margin = new Padding(4),
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI Semibold", 12F),
            ForeColor = Theme.Text
        };

        bindingLabel.Dock = DockStyle.Fill;
        bindingLabel.Margin = new Padding(4);

        detectButton.Dock = DockStyle.Fill;
        detectButton.Margin = new Padding(4);

        invertBox.Dock = DockStyle.Fill;
        invertBox.Margin = new Padding(8, 4, 4, 4);

        centerButton.Dock = DockStyle.Fill;
        centerButton.Margin = new Padding(4);

        grid.Controls.Add(nameLabel, 0, row);
        grid.Controls.Add(bindingLabel, 1, row);
        grid.Controls.Add(detectButton, 2, row);
        grid.Controls.Add(invertBox, 3, row);
        grid.Controls.Add(centerButton, 4, row);
    }

    private Control CreateVJoyCard() => CreateVirtualOutputCard();

    private Control CreateWarThunderCard()
    {
        Panel panel = CreateSectionPanel(Point.Empty, Size.Empty);
        panel.Dock = DockStyle.Fill;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = new Padding(16, 10, 16, 10),
            BackColor = Theme.Panel
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 78F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var title = new Label
        {
            Text = VT("Devices.SetupTitle"),
            Tag = "i18n:Devices.SetupTitle",
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Font = new Font("Segoe UI Semibold", 10F),
            ForeColor = Theme.Text,
            TextAlign = ContentAlignment.MiddleLeft,
            UseMnemonic = false
        };

        var instructions = new Label
        {
            Text = VT("Devices.SetupHelp"),
            Tag = "i18n:Devices.SetupHelp",
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            ForeColor = Theme.Muted,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = false,
            UseMnemonic = false
        };

        layout.Controls.Add(title, 0, 0);
        layout.Controls.Add(instructions, 1, 0);
        panel.Controls.Add(layout);

        return panel;
    }

    private Button CreateBindingButton(TrimAction action)
    {
        var button = CreateSecondaryButton(string.Empty);
        button.Font = new Font("Segoe UI Semibold", 9.8F);
        button.TextAlign = ContentAlignment.MiddleCenter;
        button.Padding = new Padding(3);
        button.UseMnemonic = false;
        button.AutoEllipsis = false;

        switch (action)
        {
            case TrimAction.ToggleInstructor:
                button.BackColor = Theme.Control;
                button.FlatAppearance.BorderColor = Theme.Accent;
                button.FlatAppearance.MouseOverBackColor = Theme.ControlHover;
                break;

            case TrimAction.StoreCurrentTrim:
                button.BackColor = Theme.RudderControl;
                button.FlatAppearance.BorderColor = Theme.RudderBorder;
                button.FlatAppearance.MouseOverBackColor = Theme.RudderControlHover;
                button.FlatAppearance.MouseDownBackColor = Theme.RudderControlPressed;
                break;

            case TrimAction.ResetAll:
                button.BackColor = Theme.ResetControl;
                button.FlatAppearance.BorderColor = Theme.ResetBorder;
                button.FlatAppearance.MouseOverBackColor = Theme.ResetControlHover;
                button.FlatAppearance.MouseDownBackColor = Theme.ResetControlPressed;
                break;

            case TrimAction.RudderLeft:
            case TrimAction.RudderRight:
                button.BackColor = Theme.RudderControl;
                button.FlatAppearance.BorderColor = Theme.RudderBorder;
                button.FlatAppearance.MouseOverBackColor = Theme.RudderControlHover;
                button.FlatAppearance.MouseDownBackColor = Theme.RudderControlPressed;
                break;

            default:
                button.BackColor = Theme.TrimControl;
                button.FlatAppearance.BorderColor = Theme.TrimBorder;
                button.FlatAppearance.MouseOverBackColor = Theme.TrimControlHover;
                button.FlatAppearance.MouseDownBackColor = Theme.TrimControlPressed;
                break;
        }

        button.Click += (_, _) => BeginBinding(action);
        button.MouseEnter += (_, _) =>
        {
            if (!_captureAction.HasValue)
                button.Text = $"{GetActionSymbol(action)}  {GetActionName(action)}";
        };
        button.MouseLeave += (_, _) =>
        {
            if (!_captureAction.HasValue) UpdateBindingButton(action);
        };

        button.MouseUp += (_, eventArgs) =>
        {
            if (eventArgs.Button != MouseButtons.Right)
            {
                return;
            }

            _bindings.Remove(action);
            _trimHoldStartedTimes.Remove(action);
            _capturedCombinationTriggers.Clear();
            ResetForeignChordSuppression();
            UpdateBindingButton(action);
            SaveBindings();
            SetInstruction($"{GetActionName(action)} binding cleared and saved.", Theme.Warning);
        };

        _bindingButtons[action] = button;
        UpdateBindingButton(action);

        return button;
    }

    private void BeginBinding(TrimAction action)
    {
        if (_devices.Count == 0)
        {
            SetStatus("No physical DirectInput devices are available.", Theme.Error);
            return;
        }

        _detectingAxis = null;
        _waitingForDeviceDetection = false;
        ResetAutoDetectDeviceButton();
        ResetForeignChordSuppression();

        _captureAction = action;
        _captureFirstInput = null;

        SetInstruction(
            $"Binding {GetActionName(action)}: press one input and release it, or hold it and press a second input.",
            Theme.AccentLight);
    }

    private void BeginDeviceAutoDetection()
    {
        if (_devices.Count == 0)
        {
            SetStatus("No physical DirectInput devices are available.", Theme.Error);
            return;
        }

        CancelBindingCapture();
        ResetForeignChordSuppression();
        _detectingAxis = null;
        _waitingForDeviceDetection = true;

        _autoDetectDeviceButton.Text = VT("Common.Listening");
        _autoDetectDeviceButton.BackColor = Theme.Warning;

        SetInstruction(
            "Press a button or POV direction on the controller you want to select. Press Escape to cancel.",
            Theme.AccentLight);
    }

    private void BeginAxisDetection(AxisTarget target)
    {
        if (_devices.Count == 0)
        {
            SetStatus("No physical DirectInput devices are available.", Theme.Error);
            return;
        }

        CancelBindingCapture();
        ResetForeignChordSuppression();
        _waitingForDeviceDetection = false;
        ResetAutoDetectDeviceButton();

        _detectingAxis = target;
        _axisDetectionBaseline.Clear();

        foreach (InputDevice device in _devices.Values)
        {
            if (!TryReadState(device, out DirectInputState state))
            {
                continue;
            }

            foreach (AxisSample sample in EnumerateAxisSamples(state))
            {
                _axisDetectionBaseline[(device.Guid, sample.Key)] = sample.RawValue;
            }
        }

        SetAxisDetectButtonState(target, listening: true);

        SetInstruction(
            $"Detecting {target}: start from neutral, then move only that axis through a clear movement.",
            Theme.AccentLight);
    }

    private void RefreshDevices()
    {
        ResetAutomaticInstructorHold();
        _pollTimer.Stop();

        Guid? previousSelection = _selectedDeviceGuid;

        DisposeInputSystem();

        _deviceBox.Items.Clear();
        _selectedDeviceGuid = null;

        try
        {
            _directInput = DInput.DirectInput8Create();

            List<DeviceInstance> instances = _directInput
                .GetDevices(
                    DeviceClass.GameControl,
                    DeviceEnumerationFlags.AttachedOnly)
                .Where(instance => !IsVirtualOutputDevice(instance))
                .ToList();

            Dictionary<string, int> duplicateCounts = instances
                .GroupBy(
                    instance => CleanDeviceName(instance.ProductName),
                    StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.Count(),
                    StringComparer.OrdinalIgnoreCase);

            Dictionary<string, int> duplicateNumbers =
                new(StringComparer.OrdinalIgnoreCase);

            foreach (DeviceInstance instance in instances)
            {
                string baseName = CleanDeviceName(instance.ProductName);

                if (string.IsNullOrWhiteSpace(baseName))
                {
                    baseName = "Unnamed DirectInput Controller";
                }

                string friendlyName = baseName;

                if (duplicateCounts.TryGetValue(baseName, out int count) &&
                    count > 1)
                {
                    duplicateNumbers.TryGetValue(baseName, out int number);
                    number++;
                    duplicateNumbers[baseName] = number;
                    friendlyName = $"{baseName} ({number})";
                }

                try
                {
                    IDirectInputDevice8 inputDevice =
                        _directInput.CreateDevice(instance.InstanceGuid);

                    inputDevice.SetCooperativeLevel(
                        Handle,
                        CooperativeLevel.NonExclusive |
                        CooperativeLevel.Background);

                    var formatResult =
                        inputDevice.SetDataFormat<RawJoystickState>();

                    if (formatResult.Failure)
                    {
                        inputDevice.Dispose();
                        continue;
                    }

                    inputDevice.Acquire();

                    var item = new InputDevice
                    {
                        Guid = instance.InstanceGuid,
                        FriendlyName = friendlyName,
                        Device = inputDevice
                    };

                    if (TryReadState(item, out DirectInputState initialState))
                    {
                        item.PreviousState = initialState;
                    }

                    _devices[item.Guid] = item;
                    _deviceBox.Items.Add(item);
                }
                catch
                {
                    // Ignore controllers that cannot be initialized.
                }
            }

            if (_deviceBox.Items.Count == 0)
            {
                _deviceStatusLabel.Text = VT("Devices.NoneDetected");

                _deviceStatusLabel.ForeColor = Theme.Error;

                SetStatus(VT("Devices.ConnectThenRefresh"), Theme.Error);
            }
            else
            {
                ReconnectSavedDeviceReferences();

                InputDevice? selection = null;

                if (previousSelection.HasValue &&
                    _devices.TryGetValue(
                        previousSelection.Value,
                        out InputDevice? previousDevice))
                {
                    selection = previousDevice;
                }

                if (selection is null &&
                    _selectedDeviceGuid.HasValue &&
                    _devices.TryGetValue(
                        _selectedDeviceGuid.Value,
                        out InputDevice? savedDevice))
                {
                    selection = savedDevice;
                }

                if (selection is null &&
                    !string.IsNullOrWhiteSpace(
                        _selectedDeviceName))
                {
                    selection =
                        _devices.Values.FirstOrDefault(
                            device =>
                                string.Equals(
                                    device.FriendlyName,
                                    _selectedDeviceName,
                                    StringComparison.OrdinalIgnoreCase));
                }

                selection ??= _deviceBox.Items[0] as InputDevice;
                _deviceBox.SelectedItem = selection;

                _deviceStatusLabel.Text = string.Format(CultureInfo.CurrentCulture, VT("Devices.CountDetected"), _deviceBox.Items.Count);

                _deviceStatusLabel.ForeColor = Theme.Success;

                UpdateAxisMappingLabels();
                UpdateProfileSummary();

                SetStatus(string.Format(CultureInfo.CurrentCulture, VT("Devices.ReadyProfile"), _activeProfileName), Theme.Success);
            }
        }
        catch (Exception exception)
        {
            _deviceStatusLabel.Text = string.Format(CultureInfo.CurrentCulture, VT("Devices.InitFailed"), exception.Message);

            _deviceStatusLabel.ForeColor = Theme.Error;
            SetStatus(VT("Devices.InputFailed"), Theme.Error);
        }
        finally
        {
            _pollTimer.Start();
        }
    }

    private void PollTimer_Tick(object? sender, EventArgs e)
    {
        Dictionary<Guid, DirectInputState> currentStates = new();

        foreach (InputDevice device in _devices.Values)
        {
            if (TryReadState(device, out DirectInputState state))
            {
                currentStates[device.Guid] = state;
            }
        }

        if (_detectingAxis.HasValue)
        {
            TryCompleteAxisDetection(currentStates);
        }

        List<DetectedInput> newInputs = FindNewInputs(currentStates);

        if (newInputs.Count > 0)
        {
            DetectedInput displayedInput =
                newInputs.LastOrDefault(
                    input =>
                        input.Kind == InputKind.Button)
                ?? newInputs[^1];

            _lastInputLabel.Text = string.Format(CultureInfo.CurrentCulture, VT("Devices.LastDetected"), displayedInput.DisplayName);
        }

        if (!_captureAction.HasValue &&
            !_waitingForDeviceDetection &&
            !_detectingAxis.HasValue)
        {
            UpdateForeignChordSuppression(
                newInputs,
                currentStates);
        }

        bool consumeButtonInput = false;

        if (_captureAction.HasValue)
        {
            HandleBindingCapture(newInputs, currentStates);
            consumeButtonInput = true;
        }
        else if (_waitingForDeviceDetection &&
                 newInputs.Count > 0)
        {
            DetectedInput detected = newInputs[0];

            _waitingForDeviceDetection = false;
            ResetAutoDetectDeviceButton();
            SelectDevice(detected.Device.Guid);

            SetInstruction(
                $"Selected device: {detected.Device.FriendlyName}.",
                Theme.Success);

            consumeButtonInput = true;
        }

        if (!consumeButtonInput)
        {
            UpdatePhysicalAxisSnapshot(
                currentStates);

            ProcessTrimBindings(currentStates);
        }

        ProcessLiveAxes(currentStates);

        foreach ((Guid guid, DirectInputState state) in currentStates)
        {
            if (_devices.TryGetValue(guid, out InputDevice? device))
            {
                device.PreviousState = state;
            }
        }
    }

    private void TryCompleteAxisDetection(
        IReadOnlyDictionary<Guid, DirectInputState> currentStates)
    {
        if (!_detectingAxis.HasValue)
        {
            return;
        }

        AxisTarget target = _detectingAxis.Value;

        InputDevice? bestDevice = null;
        AxisSample? bestSample = null;
        int bestDelta = 0;
        int bestCenter = 32767;

        foreach ((Guid deviceGuid, DirectInputState state) in currentStates)
        {
            if (!_devices.TryGetValue(deviceGuid, out InputDevice? device))
            {
                continue;
            }

            foreach (AxisSample sample in EnumerateAxisSamples(state))
            {
                if (!_axisDetectionBaseline.TryGetValue(
                        (deviceGuid, sample.Key),
                        out int baseline))
                {
                    continue;
                }

                int delta = Math.Abs(sample.RawValue - baseline);

                if (delta <= bestDelta)
                {
                    continue;
                }

                bestDelta = delta;
                bestDevice = device;
                bestSample = sample;
                bestCenter = baseline;
            }
        }

        if (bestDevice is null ||
            bestSample is null ||
            bestDelta < AxisDetectionThreshold)
        {
            return;
        }

        var source = new AxisSource
        {
            DeviceGuid = bestDevice.Guid,
            DeviceName = bestDevice.FriendlyName,
            AxisKey = bestSample.Key,
            CenterRaw = bestCenter,
            SignedRange = LooksLikeSignedRange(bestCenter)
        };

        switch (target)
        {
            case AxisTarget.Roll:
                _rollAxis = source;
                break;

            case AxisTarget.Pitch:
                _pitchAxis = source;
                break;

            case AxisTarget.Rudder:
                _rudderAxis = source;
                break;
        }

        _detectingAxis = null;
        _axisDetectionBaseline.Clear();

        SetAxisDetectButtonState(target, listening: false);
        UpdateAxisMappingLabels();

        SaveBindings();
        UpdateProfileSummary();

        SetInstruction(
            $"{target} axis assigned to {source.DisplayName} and saved in {_activeProfileName}.",
            Theme.Success);
    }

    private void UpdatePhysicalAxisSnapshot(
        IReadOnlyDictionary<Guid, DirectInputState> currentStates)
    {
        bool Available(AxisSource? source) => source is not null &&
            currentStates.TryGetValue(source.DeviceGuid, out var state) &&
            TryReadAxisRaw(state, source.AxisKey, out _);
        _rollInputAvailable = Available(_rollAxis);
        _pitchInputAvailable = Available(_pitchAxis);
        _rudderInputAvailable = Available(_rudderAxis);
        _lastPhysicalRoll = ReadNormalizedAxis(_rollAxis, currentStates, _invertRollBox.Checked);
        _lastPhysicalPitch = ReadNormalizedAxis(_pitchAxis, currentStates, _invertPitchBox.Checked);
        _lastPhysicalRudder = ReadNormalizedAxis(_rudderAxis, currentStates, _invertRudderBox.Checked);
        _lastAxisSnapshotTime = MonotonicSeconds;
        _currentManualCommand = ComposeManualCommand();
    }

    private AxisVector ComposeManualCommand()
    {
        _automaticRudderRollCompensation = 0;
        _automaticRudderPitchCompensation = 0;
        var neutral = new AxisVector(
            Math.Clamp(_rollTrim + _automaticRudderRollCompensation, -95, 95) / 100.0,
            Math.Clamp(_pitchTrim + _automaticRudderPitchCompensation, -95, 95) / 100.0,
            _rudderTrim / 100.0);
        var raw = new AxisVector(_lastPhysicalRoll, _lastPhysicalPitch, _lastPhysicalRudder);
        var normal = new AxisVector(
            ApplyIndependentTrimBias(_rollResponse.Apply(raw.Roll), neutral.Roll),
            ApplyIndependentTrimBias(_pitchResponse.Apply(raw.Pitch), neutral.Pitch),
            ApplyIndependentTrimBias(_rudderResponse.Apply(raw.Rudder), neutral.Rudder));
        AxisVector result = _storeTrimReturn.Apply(raw, normal, neutral,
            _rollInputAvailable, _pitchInputAvailable, _rudderInputAvailable);
        // A disconnected physical device must not leave a saved offset stuck on.
        return new(_rollInputAvailable ? result.Roll : 0,
            _pitchInputAvailable ? result.Pitch : 0,
            _rudderInputAvailable ? result.Rudder : 0);
    }

    private void ProcessLiveAxes(
        IReadOnlyDictionary<Guid, DirectInputState> currentStates)
    {
        UpdatePhysicalAxisSnapshot(currentStates);
        double outputRoll = _currentManualCommand.Roll;
        double outputPitch = _currentManualCommand.Pitch;
        double outputRudder = _currentManualCommand.Rudder;
        ApplyAutomaticInstructorMode(_lastPhysicalRoll, _lastPhysicalPitch, _lastPhysicalRudder,
            _rollInputAvailable, _pitchInputAvailable, _rudderInputAvailable,
            ref outputRoll, ref outputPitch, ref outputRudder);
        UpdateCurvePreview(AxisTarget.Roll, _lastPhysicalRoll);
        UpdateCurvePreview(AxisTarget.Pitch, _lastPhysicalPitch);
        UpdateCurvePreview(AxisTarget.Rudder, _lastPhysicalRudder);
        UpdateTrimDisplay(_lastPhysicalRoll, _lastPhysicalPitch, _lastPhysicalRudder,
            outputRoll, outputPitch, outputRudder);
        if (!_vJoyConnected) return;
        try
        {
            _vJoy!.SendFlightAxes(outputRoll, outputPitch, outputRudder);
        }
        catch (Exception exception)
        {
            HandleVJoyConnectionLost($"Virtual controller output failed: {exception.Message}");
        }
    }

    private double ReadNormalizedAxis(
        AxisSource? source,
        IReadOnlyDictionary<Guid, DirectInputState> currentStates,
        bool inverted)
    {
        if (source is null ||
            !currentStates.TryGetValue(
                source.DeviceGuid,
                out DirectInputState? state) ||
            !TryReadAxisRaw(
                state,
                source.AxisKey,
                out int raw))
        {
            return 0;
        }

        double normalized = NormalizeAxis(
            raw,
            source.CenterRaw,
            source.SignedRange);

        return inverted ? -normalized : normalized;
    }

    private static double ApplyIndependentTrimBias(
        double physical,
        double trimBias)
    {
        physical =
            Math.Clamp(
                physical,
                -1.0,
                1.0);

        trimBias =
            Math.Clamp(
                trimBias,
                -0.95,
                0.95);

        // This generic trim model applies an independent command bias on
        // each axis. It does not change another axis or dynamically rescale
        // physical stick sensitivity.
        return Math.Clamp(
            physical + trimBias,
            -1.0,
            1.0);
    }

    private static int ToVJoyValue(double normalized)
    {
        normalized = Math.Clamp(
            normalized,
            -1.0,
            1.0);

        // Preserve the exact vJoy centre:
        // -1.0 = 1
        //  0.0 = 16384
        // +1.0 = 32768
        if (normalized >= 0.0)
        {
            return 16384 +
                   (int)Math.Round(
                       normalized * 16384.0);
        }

        return 16384 +
               (int)Math.Round(
                   normalized * 16383.0);
    }

    private async void ToggleVJoyConnection()
    {
        _vJoyAutoConnectTimer.Stop();

        if (_vJoyConnected)
        {
            SetOutputEnabled(false);
            return;
        }

        _manualVJoyDisconnect = false;
        await SetupAndConnectOutputAsync();
    }

    private bool ConnectVJoy(bool automatic)
    {
        if (_vJoyConnected)
        {
            return true;
        }

        try
        {
            DisposeVJoyInstance();
            _vJoy = FlightOutputFactory.Create(_selectedOutputBackend);
            _vJoy.Connect();

            _vJoyConnected = true;
            _vJoyStatusLabel.Text = VT("VJoy.Connected");

            _vJoyStatusLabel.ForeColor = Theme.Success;
            _vJoyConnectButton.Text = VT("Common.Disconnect");
            _toolTip.SetToolTip(
                _vJoyStatusLabel,
                VT("VJoy.OutputActive"));

            return true;
        }
        catch (Exception exception)
        {
            DisposeVJoyInstance();
            _vJoyConnected = false;
            _vJoyStatusLabel.Text =
                automatic
                    ? VT("VJoy.NotReadyRetry")
                    : string.Format(CultureInfo.CurrentCulture, VT("VJoy.ConnectFailed"), exception.Message);

            _vJoyStatusLabel.ForeColor = Theme.Error;
            _vJoyConnectButton.Text = VT("Common.Connect");
            _toolTip.SetToolTip(
                _vJoyStatusLabel,
                exception.Message);

            return false;
        }
    }

    private void StartAutomaticVJoyConnection()
    {
        _vJoyAutoConnectTimer.Stop();

        if (!_autoConnectToVJoyBox.Checked ||
            _vJoyConnected)
        {
            return;
        }

        _manualVJoyDisconnect = false;
        _remainingAutomaticVJoyConnectAttempts = 8;
        TryAutomaticVJoyConnection();
    }

    private void VJoyAutoConnectTimer_Tick(
        object? sender,
        EventArgs eventArgs)
    {
        TryAutomaticVJoyConnection();
    }

    private void TryAutomaticVJoyConnection()
    {
        _vJoyAutoConnectTimer.Stop();

        if (_vJoyConnected ||
            !_autoConnectToVJoyBox.Checked ||
            _manualVJoyDisconnect)
        {
            return;
        }

        if (ConnectVJoy(automatic: true))
        {
            _remainingAutomaticVJoyConnectAttempts = 0;
            return;
        }

        _remainingAutomaticVJoyConnectAttempts--;

        if (_remainingAutomaticVJoyConnectAttempts <= 0 && !_embeddedMode)
        {
            _vJoyStatusLabel.Text = VT("VJoy.AutoFailed");
            _vJoyStatusLabel.ForeColor = Theme.Error;
            return;
        }

        if (_embeddedMode && _remainingAutomaticVJoyConnectAttempts <= 0) _remainingAutomaticVJoyConnectAttempts = 8;
        _vJoyStatusLabel.Text = string.Format(CultureInfo.CurrentCulture, VT("VJoy.Retrying"), _remainingAutomaticVJoyConnectAttempts);
        _vJoyStatusLabel.ForeColor = Theme.Warning;
        _vJoyAutoConnectTimer.Start();
    }

    private void HandleVJoyConnectionLost(
        string message)
    {
        _instructorController.Invalidate();
        _lastPitchHold = default;
        DisposeVJoyInstance();
        _vJoyConnected = false;
        _vJoyStatusLabel.Text = message;
        _vJoyStatusLabel.ForeColor = Theme.Error;
        _vJoyConnectButton.Text = VT("Common.Connect");
        _toolTip.SetToolTip(
            _vJoyStatusLabel,
            message);

        if (_autoConnectToVJoyBox.Checked &&
            !_manualVJoyDisconnect)
        {
            _remainingAutomaticVJoyConnectAttempts = 8;
            _vJoyStatusLabel.Text =
                "vJoy connection was lost — reconnecting automatically.";
            _vJoyStatusLabel.ForeColor = Theme.Warning;
            _vJoyAutoConnectTimer.Start();
        }
    }

    private void DisconnectVJoy()
    {
        ResetAutomaticInstructorHold();
        DisposeVJoyInstance();
        _vJoyConnected = false;
        _vJoyStatusLabel.Text = VT("VJoy.Disconnected");
        _vJoyStatusLabel.ForeColor = Theme.Warning;
        _vJoyConnectButton.Text = VT("Common.Connect");
        _toolTip.SetToolTip(
            _vJoyStatusLabel,
            VT("VJoy.ClickReconnect"));
    }

    private void DisposeVJoyInstance()
    {
        try
        {
            _vJoy?.Release();
        }
        catch
        {
            // The vJoy device may already have been released by the driver.
        }

        try
        {
            _vJoy?.Dispose();
        }
        catch
        {
            // Ignore wrapper shutdown errors.
        }

        _vJoy = null;
    }

    private void RecenterAxis(AxisTarget target)
    {
        AxisSource? source = target switch
        {
            AxisTarget.Roll => _rollAxis,
            AxisTarget.Pitch => _pitchAxis,
            AxisTarget.Rudder => _rudderAxis,
            _ => null
        };

        if (source is null ||
            !TryCaptureStableAxisCenter(
                source,
                out int stableCenter))
        {
            SetInstruction(
                $"{target} could not be recentered. Keep the control still and try again.",
                Theme.Error);

            return;
        }

        source.CenterRaw = stableCenter;

        SetInstruction(
            $"{target} neutral calibrated from a stable sample.",
            Theme.Success);
    }

    private void RecenterAllConfiguredAxes(
        bool showStatus)
    {
        int centeredCount = 0;

        foreach ((AxisTarget target, AxisSource? source)
                 in new[]
                 {
                     (AxisTarget.Roll, _rollAxis),
                     (AxisTarget.Pitch, _pitchAxis),
                     (AxisTarget.Rudder, _rudderAxis)
                 })
        {
            if (source is null ||
                !TryCaptureStableAxisCenter(
                    source,
                    out int stableCenter))
            {
                continue;
            }

            source.CenterRaw = stableCenter;
            centeredCount++;
        }

        if (showStatus)
        {
            SetInstruction(
                centeredCount > 0
                    ? $"Calibrated the neutral centre of {centeredCount} configured axis/axes."
                    : "No configured physical axes could be centered.",
                centeredCount > 0
                    ? Theme.Success
                    : Theme.Error);
        }
    }

    private bool TryCaptureStableAxisCenter(
        AxisSource source,
        out int stableCenter)
    {
        stableCenter = source.CenterRaw;

        if (!_devices.TryGetValue(
                source.DeviceGuid,
                out InputDevice? device))
        {
            return false;
        }

        var samples = new List<int>(25);

        for (int sampleIndex = 0;
             sampleIndex < 25;
             sampleIndex++)
        {
            if (TryReadState(
                    device,
                    out DirectInputState state) &&
                TryReadAxisRaw(
                    state,
                    source.AxisKey,
                    out int raw))
            {
                samples.Add(raw);
            }

            System.Threading.Thread.Sleep(4);
        }

        if (samples.Count < 10)
        {
            return false;
        }

        samples.Sort();

        // Median is resistant to small spikes and noisy pedal sensors.
        stableCenter =
            samples[samples.Count / 2];

        int spread =
            samples[^1] -
            samples[0];

        // Reject calibration while the physical control is moving.
        int allowedSpread =
            source.SignedRange
                ? 900
                : 1400;

        return spread <= allowedSpread;
    }

    private void HandleBindingCapture(
        IReadOnlyList<DetectedInput> newInputs,
        IReadOnlyDictionary<Guid, DirectInputState> currentStates)
    {
        if (!_captureAction.HasValue)
        {
            return;
        }

        TrimAction action = _captureAction.Value;

        if (_captureFirstInput is null)
        {
            if (newInputs.Count == 0)
            {
                return;
            }

            PhysicalInput first = newInputs[0].ToPhysicalInput();
            _captureFirstInput = first;
            SelectDevice(first.DeviceGuid);

            SetInstruction(
                $"Detected {first.FullName}. Release it for a single binding, or hold it and press another input.",
                Theme.AccentLight);

            return;
        }

        PhysicalInput candidateModifier = _captureFirstInput;

        DetectedInput? secondInput = newInputs.FirstOrDefault(
            item => !InputsAreEqual(
                candidateModifier,
                item.ToPhysicalInput()));

        bool firstStillHeld =
            IsPhysicalInputActive(
                candidateModifier,
                currentStates);

        if (firstStillHeld &&
            secondInput is not null)
        {
            FinishBinding(
                action,
                secondInput.ToPhysicalInput(),
                candidateModifier);

            return;
        }

        if (!firstStillHeld)
        {
            FinishBinding(
                action,
                candidateModifier,
                modifier: null);
        }
    }

    private void FinishBinding(
        TrimAction action,
        PhysicalInput trigger,
        PhysicalInput? modifier)
    {
        TrimAction other = action == TrimAction.StoreCurrentTrim
            ? TrimAction.ToggleInstructor : TrimAction.StoreCurrentTrim;
        if ((action == TrimAction.StoreCurrentTrim || action == TrimAction.ToggleInstructor) &&
            _bindings.TryGetValue(other, out ActionBinding? existing) &&
            InputsAreEqual(existing.Trigger, trigger) &&
            ((existing.Modifier is null && modifier is null) ||
             (existing.Modifier is not null && modifier is not null && InputsAreEqual(existing.Modifier, modifier))))
        {
            CancelBindingCapture();
            SetInstruction(VT("Instructor.StoreConflict"), Theme.Warning);
            return;
        }
        _bindings[action] = new ActionBinding
        {
            Trigger = trigger,
            Modifier = modifier
        };

        _captureAction = null;
        _captureFirstInput = null;
        ResetForeignChordSuppression();

        SelectDevice(trigger.DeviceGuid);
        UpdateBindingButton(action);
        SaveBindings();

        SetInstruction(
            $"{GetActionName(action)} assigned and saved: {_bindings[action].FullDisplay}.",
            Theme.Success);
    }

    private List<DetectedInput> FindNewInputs(
        IReadOnlyDictionary<Guid, DirectInputState> currentStates)
    {
        var results = new List<DetectedInput>();

        foreach ((Guid guid, DirectInputState current) in currentStates)
        {
            if (!_devices.TryGetValue(
                    guid,
                    out InputDevice? device) ||
                device.PreviousState is null)
            {
                continue;
            }

            DirectInputState previous = device.PreviousState;

            int buttonCount =
                Math.Min(
                    current.Buttons.Length,
                    previous.Buttons.Length);

            for (int index = 0; index < buttonCount; index++)
            {
                if (current.Buttons[index] &&
                    !previous.Buttons[index])
                {
                    results.Add(new DetectedInput
                    {
                        Device = device,
                        Kind = InputKind.Button,
                        Index = index
                    });
                }
            }

            int povCount = Math.Min(
                current.PointOfViewControllers.Length,
                previous.PointOfViewControllers.Length);

            for (int index = 0; index < povCount; index++)
            {
                PovDirection currentDirection =
                    GetPovDirection(
                        current.PointOfViewControllers[index]);

                PovDirection previousDirection =
                    GetPovDirection(
                        previous.PointOfViewControllers[index]);

                if (currentDirection != PovDirection.Center &&
                    currentDirection != previousDirection)
                {
                    results.Add(new DetectedInput
                    {
                        Device = device,
                        Kind = InputKind.Pov,
                        Index = index,
                        PovDirection = currentDirection
                    });
                }
            }
        }

        return results;
    }

    private void ProcessTrimBindings(
        IReadOnlyDictionary<Guid, DirectInputState> currentStates)
    {
        DateTime now =
            DateTime.UtcNow;

        double frameSeconds =
            Math.Clamp(
                (now - _lastTrimProcessingUtc).TotalSeconds,
                0.0,
                0.10);

        _lastTrimProcessingUtc = now;

        if (_foreignChordSuppressed)
        {
            _trimHoldStartedTimes.Clear();
            _capturedCombinationTriggers.Clear();
            return;
        }

        UpdateCapturedCombinationTriggers(currentStates);

        bool blockStandaloneBindings =
            _universalTriggerBox is not null &&
            !_universalTriggerBox.Checked &&
            IsAnyConfiguredModifierActive(currentStates);

        foreach (TrimAction action in Enum.GetValues<TrimAction>())
        {
            if (!_bindings.TryGetValue(
                    action,
                    out ActionBinding? binding))
            {
                continue;
            }

            bool active = IsActionBindingActive(
                binding,
                currentStates,
                usePreviousState: false);

            bool wasActive = IsActionBindingActive(
                binding,
                currentStates,
                usePreviousState: true);

            // Exact combinations always take priority over a shared
            // single-input binding.
            //
            // With Universal Trigger OFF, holding any configured modifier
            // blocks every standalone trim binding.
            if (binding.Modifier is null &&
                (IsTriggerCapturedByCombination(binding.Trigger) ||
                 blockStandaloneBindings))
            {
                active = false;
                wasActive = false;
            }

            if (active && !wasActive)
            {
                // A short tap always applies exactly one configured step.
                // Instructor Mode and Center All are one-shot commands.
                ApplyTrimAction(action);

                if (action != TrimAction.ResetAll &&
                    action != TrimAction.StoreCurrentTrim &&
                    action != TrimAction.ToggleInstructor)
                {
                    _trimHoldStartedTimes[action] =
                        now;
                }

                continue;
            }

            if (!active)
            {
                _trimHoldStartedTimes.Remove(action);
                continue;
            }

            if (action == TrimAction.ResetAll ||
                action == TrimAction.StoreCurrentTrim ||
                action == TrimAction.ToggleInstructor ||
                !_repeatWhileHeldBox.Checked ||
                !_trimHoldStartedTimes.TryGetValue(
                    action,
                    out DateTime holdStarted) ||
                (now - holdStarted).TotalSeconds <
                    GetHeldTrimStartDelaySeconds())
            {
                continue;
            }

            double trimDelta =
                GetHeldTrimRatePercentPerSecond() *
                frameSeconds;

            ApplyTrimDelta(
                action,
                trimDelta);
        }
    }

    private void UpdateForeignChordSuppression(
        IReadOnlyList<DetectedInput> newInputs,
        IReadOnlyDictionary<Guid, DirectInputState> currentStates)
    {
        // OFF means unassigned game buttons do not participate in VTrim's
        // context logic at all.
        if (_unassignedInputGuardBox is null ||
            !_unassignedInputGuardBox.Checked)
        {
            ResetForeignChordSuppression();
            return;
        }

        // Release the current context only when its owner is physically
        // released. A latching switch can therefore remain the game context,
        // but a configured VTrim modifier may explicitly override it below.
        if (_inputContextButton is not null &&
            !IsPhysicalInputActive(
                _inputContextButton,
                currentStates,
                usePreviousState: false))
        {
            bool wasForeign =
                _foreignChordSuppressed;

            bool wasVTrimModifier =
                _inputContextIsVTrimModifier;

            _inputContextButton =
                null;

            _inputContextIsVTrimModifier =
                false;

            _foreignChordSuppressed =
                false;

            if (wasForeign)
            {
                SetInstruction(
                    "Game chord released. VTrim input restored.",
                    Theme.Success);
            }
            else if (wasVTrimModifier)
            {
                SetInstruction(
                    "VTrim modifier released.",
                    Theme.Muted);
            }
        }

        if (newInputs.Count == 0)
        {
            return;
        }

        // A configured VTrim MODIFIER is the only input allowed to take VTrim
        // context away from an existing foreign/latching game context.
        //
        // This is deliberately NOT true for normal trim triggers. So if the
        // user presses Trigger 1 first and then POV Up, POV Up stays a trigger
        // inside Trigger 1's game chord instead of becoming the new context.
        PhysicalInput? newlyPressedVTrimModifier =
            newInputs
                .Select(
                    detected =>
                        detected.ToPhysicalInput())
                .FirstOrDefault(
                    IsInputConfiguredAsModifier);

        if (newlyPressedVTrimModifier is not null)
        {
            bool changedContext =
                _inputContextButton is null ||
                !_inputContextIsVTrimModifier ||
                !InputsAreEqual(
                    _inputContextButton,
                    newlyPressedVTrimModifier);

            _inputContextButton =
                newlyPressedVTrimModifier;

            _inputContextIsVTrimModifier =
                true;

            _foreignChordSuppressed =
                false;

            _trimHoldStartedTimes.Clear();
            _capturedCombinationTriggers.Clear();

            if (changedContext)
            {
                SetInstruction(
                    $"VTrim modifier context: {newlyPressedVTrimModifier.FullName}.",
                    Theme.Success);
            }

            return;
        }

        // Once a VTrim modifier owns the chord, ordinary game buttons and trim
        // triggers cannot steal that context until the modifier is released.
        if (_inputContextIsVTrimModifier &&
            _inputContextButton is not null)
        {
            return;
        }

        // Once a game button owns the chord, trim triggers cannot steal that
        // context. This is the key fix for:
        //
        //   Trigger 1 (game-only) -> POV Up (trim trigger)
        //
        // POV Up remains ignored by VTrim for that chord.
        if (_foreignChordSuppressed &&
            _inputContextButton is not null)
        {
            return;
        }

        // No context exists yet. Only a newly pressed physical BUTTON that is
        // not configured as a VTrim modifier can start a foreign game chord.
        //
        // A standalone trim trigger does not become a context owner:
        // it simply executes normally through ProcessTrimBindings().
        DetectedInput? firstNewButton =
            newInputs.FirstOrDefault(
                detected =>
                    detected.Kind == InputKind.Button);

        if (firstNewButton is null)
        {
            return;
        }

        PhysicalInput candidate =
            firstNewButton.ToPhysicalInput();

        if (IsInputConfiguredAsModifier(candidate))
        {
            // Normally handled above, kept here for completeness.
            _inputContextButton =
                candidate;

            _inputContextIsVTrimModifier =
                true;

            _foreignChordSuppressed =
                false;

            return;
        }

        if (IsInputConfiguredOnlyAsTrimTrigger(candidate))
        {
            // A trim trigger pressed by itself is not a modifier/context key.
            // It must remain free to perform its normal VTrim action.
            return;
        }

        // FIRST GAME BUTTON WINS until it is released or a real VTrim
        // modifier/lever explicitly overrides it.
        _inputContextButton =
            candidate;

        _inputContextIsVTrimModifier =
            false;

        _foreignChordSuppressed =
            true;

        _trimHoldStartedTimes.Clear();
        _capturedCombinationTriggers.Clear();

        SetInstruction(
            $"Game chord locked by {candidate.FullName}. " +
            "Trim triggers will be ignored until this button is released or a VTrim modifier is pressed.",
            Theme.Warning);
    }

    private bool IsInputConfiguredAsModifier(
        PhysicalInput input)
    {
        foreach (ActionBinding binding in _bindings.Values)
        {
            if (binding.Modifier is not null &&
                InputsAreEqual(
                    binding.Modifier,
                    input))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsInputConfiguredOnlyAsTrimTrigger(
        PhysicalInput input)
    {
        bool usedAsTrigger =
            false;

        foreach (ActionBinding binding in _bindings.Values)
        {
            if (binding.Modifier is not null &&
                InputsAreEqual(
                    binding.Modifier,
                    input))
            {
                return false;
            }

            if (InputsAreEqual(
                    binding.Trigger,
                    input))
            {
                usedAsTrigger =
                    true;
            }
        }

        return usedAsTrigger;
    }

    private bool IsInputUsedByVTrim(
        PhysicalInput input)
    {
        foreach (ActionBinding binding in _bindings.Values)
        {
            if (InputsAreEqual(
                    binding.Trigger,
                    input))
            {
                return true;
            }

            if (binding.Modifier is not null &&
                InputsAreEqual(
                    binding.Modifier,
                    input))
            {
                return true;
            }
        }

        return false;
    }

    private void ResetForeignChordSuppression()
    {
        _inputContextButton = null;
        _inputContextIsVTrimModifier = false;
        _foreignChordSuppressed = false;
    }

    private void UpdateCapturedCombinationTriggers(
        IReadOnlyDictionary<Guid, DirectInputState> currentStates)
    {
        // Release a captured trigger only after the physical trigger itself
        // has returned to neutral. Releasing the modifier first therefore
        // cannot accidentally activate the shared single-input binding.
        foreach (string key in _capturedCombinationTriggers.Keys.ToList())
        {
            PhysicalInput trigger =
                _capturedCombinationTriggers[key];

            if (!IsPhysicalInputActive(
                    trigger,
                    currentStates,
                    usePreviousState: false))
            {
                _capturedCombinationTriggers.Remove(key);
            }
        }

        foreach (ActionBinding binding in _bindings.Values)
        {
            if (binding.Modifier is null)
            {
                continue;
            }

            if (!IsActionBindingActive(
                    binding,
                    currentStates,
                    usePreviousState: false))
            {
                continue;
            }

            string triggerKey =
                GetPhysicalInputKey(binding.Trigger);

            _capturedCombinationTriggers[triggerKey] =
                binding.Trigger;
        }
    }

    private bool IsAnyConfiguredModifierActive(
        IReadOnlyDictionary<Guid, DirectInputState> currentStates)
    {
        var checkedModifiers =
            new HashSet<string>(
                StringComparer.Ordinal);

        foreach (ActionBinding binding in _bindings.Values)
        {
            if (binding.Modifier is null)
            {
                continue;
            }

            string modifierKey =
                GetPhysicalInputKey(
                    binding.Modifier);

            if (!checkedModifiers.Add(modifierKey))
            {
                continue;
            }

            if (IsPhysicalInputActive(
                    binding.Modifier,
                    currentStates,
                    usePreviousState: false))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsTriggerCapturedByCombination(
        PhysicalInput trigger)
    {
        return _capturedCombinationTriggers.ContainsKey(
            GetPhysicalInputKey(trigger));
    }

    private static string GetPhysicalInputKey(
        PhysicalInput input)
    {
        return string.Join(
            "|",
            input.DeviceGuid,
            input.Kind,
            input.Index,
            input.PovDirection);
    }

    private bool IsActionBindingActive(
        ActionBinding binding,
        IReadOnlyDictionary<Guid, DirectInputState> currentStates,
        bool usePreviousState)
    {
        bool triggerActive = IsPhysicalInputActive(
            binding.Trigger,
            currentStates,
            usePreviousState);

        if (!triggerActive)
        {
            return false;
        }

        return binding.Modifier is null ||
               IsPhysicalInputActive(
                   binding.Modifier,
                   currentStates,
                   usePreviousState);
    }

    private bool IsPhysicalInputActive(
        PhysicalInput input,
        IReadOnlyDictionary<Guid, DirectInputState> currentStates,
        bool usePreviousState = false)
    {
        if (!_devices.TryGetValue(
                input.DeviceGuid,
                out InputDevice? device))
        {
            return false;
        }

        DirectInputState? state = usePreviousState
            ? device.PreviousState
            : currentStates.TryGetValue(
                input.DeviceGuid,
                out DirectInputState? current)
                ? current
                : null;

        if (state is null)
        {
            return false;
        }

        if (input.Kind == InputKind.Button)
        {
            return input.Index >= 0 &&
                   input.Index < state.Buttons.Length &&
                   state.Buttons[input.Index];
        }

        if (input.Index < 0 ||
            input.Index >=
            state.PointOfViewControllers.Length)
        {
            return false;
        }

        PovDirection currentDirection =
            GetPovDirection(
                state.PointOfViewControllers[input.Index]);

        return PovMatches(
            input.PovDirection,
            currentDirection);
    }

    private void ApplyTrimAction(TrimAction action)
    {
        if (action == TrimAction.StoreCurrentTrim)
        {
            if (_horizontalRudderCalibrationArmed)
            {
                TryCaptureHorizontalRudderCalibration();
                return;
            }

            StoreCurrentControlPositionAsTrim();
            return;
        }

        if (action == TrimAction.ToggleInstructor)
        {
            ToggleInstructorMode();
            return;
        }

        double tapAmount = action switch
        {
            TrimAction.NoseDown or
            TrimAction.NoseUp =>
                (double)_pitchStepBox.Value,

            TrimAction.RollLeft or
            TrimAction.RollRight =>
                (double)_rollStepBox.Value,

            TrimAction.RudderLeft or
            TrimAction.RudderRight =>
                (double)_rudderStepBox.Value,

            _ => 0.0
        };

        ApplyTrimDelta(
            action,
            tapAmount);
    }

    private void ToggleInstructorMode()
    {
        if (!IsPropProfile) return;
        _instructorModeEnabled = !_instructorModeEnabled;
        ResetAutomaticInstructorHold(); UpdateInstructorModeUi();
        if (!_loadingSavedSettings) SaveBindings();
        SetInstruction(_instructorModeEnabled
            ? "Flight Assistant enabled: pitch attitude hold and pitch-rate dampening."
            : VT("Instructor.Off"), _instructorModeEnabled ? Theme.Success : Theme.Muted);
    }

    private void StoreCurrentControlPositionAsTrim()
    {
        if (MonotonicSeconds - _lastAxisSnapshotTime > 0.25 ||
            !(_rollInputAvailable || _pitchInputAvailable || _rudderInputAvailable) ||
            (_rollAxis is not null && !_rollInputAvailable) ||
            (_pitchAxis is not null && !_pitchInputAvailable) ||
            (_rudderAxis is not null && !_rudderInputAvailable))
        {
            SetInstruction(VT("Trim.NeedsFreshInput"), Theme.Warning);
            return;
        }
        // Rebase from the full current command: shaped physical controls plus
        // any roll/pitch/rudder trim the pilot has already dialed in.
        // A new manual capture also replaces an unfinished spring-return capture.
        var currentTrim = new AxisVector(
            Math.Clamp(_rollTrim + _automaticRudderRollCompensation, -95, 95) / 100.0,
            Math.Clamp(_pitchTrim + _automaticRudderPitchCompensation, -95, 95) / 100.0,
            _rudderTrim / 100.0);
        var desired = new AxisVector(
            _rollInputAvailable
                ? ApplyIndependentTrimBias(_rollResponse.Apply(_lastPhysicalRoll), currentTrim.Roll)
                : 0,
            _pitchInputAvailable
                ? ApplyIndependentTrimBias(_pitchResponse.Apply(_lastPhysicalPitch), currentTrim.Pitch)
                : 0,
            _rudderInputAvailable
                ? ApplyIndependentTrimBias(_rudderResponse.Apply(_lastPhysicalRudder), currentTrim.Rudder)
                : 0);
        TrimCapture capture = StoreTrimController.SolveCapture(desired, 0, 0);
        _rollTrim = capture.ManualTrimPercent.Roll;
        _pitchTrim = capture.ManualTrimPercent.Pitch;
        _rudderTrim = capture.ManualTrimPercent.Rudder;
        _storeTrimReturn.Begin(new(_lastPhysicalRoll, _lastPhysicalPitch, _lastPhysicalRudder),
            new(Math.Max(0.002, _rollResponse.Deadzone / 100),
                Math.Max(0.002, _pitchResponse.Deadzone / 100),
                Math.Max(0.002, _rudderResponse.Deadzone / 100)));
        ResetAutomaticInstructorHold();
        SetInstruction(capture.Limited
            ? "Trim stored at the available trim limit. Return controls to center; the full requested offset exceeded the limit."
            : _storeTrimReturn.AnyWaiting
                ? "Trim stored. Return stick/pedals to center; then fine-tune with your normal trim buttons."
                : VT("Instructor.TrimStored"),
            capture.Limited ? Theme.Warning : Theme.Success);
        UpdateInstructorModeUi();
    }

    private void ApplyTrimDelta(
        TrimAction action,
        double positiveAmount)
    {
        MarkManualTrimOverride(action);
        positiveAmount =
            Math.Max(
                0.0,
                positiveAmount);

        switch (action)
        {
            case TrimAction.NoseDown:
                _pitchTrim -= positiveAmount;
                break;

            case TrimAction.NoseUp:
                _pitchTrim += positiveAmount;
                break;

            case TrimAction.RollLeft:
                _rollTrim -= positiveAmount;
                break;

            case TrimAction.RollRight:
                _rollTrim += positiveAmount;
                break;

            case TrimAction.RudderLeft:
                _rudderTrim -= positiveAmount;
                break;

            case TrimAction.RudderRight:
                _rudderTrim += positiveAmount;
                break;

            case TrimAction.ResetAll:
                _pitchTrim = 0.0;
                _rollTrim = 0.0;
                _rudderTrim = 0.0;
                _automaticRudderRollCompensation = 0.0;
                _automaticRudderPitchCompensation = 0.0;
                _storeTrimReturn.Cancel();
                ResetAutomaticInstructorHold();
                UpdateInstructorModeUi();
                SetInstruction(
                    _instructorModeEnabled
                        ? VT("Instructor.Centered")
                        : "All trim is centered.",
                    Theme.Success);
                break;
        }

        _pitchTrim =
            Math.Clamp(
                _pitchTrim,
                -95.0,
                95.0);

        _rollTrim =
            Math.Clamp(
                _rollTrim,
                -95.0,
                95.0);

        _rudderTrim =
            Math.Clamp(
                _rudderTrim,
                -95.0,
                95.0);
    }

    private void UpdateTrimDisplay(
        double physicalRoll,
        double physicalPitch,
        double physicalRudder,
        double outputRoll,
        double outputPitch,
        double outputRudder)
    {
        _pitchValueLabel.Text = string.Format(CultureInfo.CurrentCulture, VT("Trim.PitchValue"), Math.Clamp(_pitchTrim + _automaticRudderPitchCompensation, -95, 95));

        _rollValueLabel.Text = string.Format(CultureInfo.CurrentCulture, VT("Trim.RollValue"), Math.Clamp(_rollTrim + _automaticRudderRollCompensation, -95, 95));

        _rudderValueLabel.Text =
            string.Format(CultureInfo.CurrentCulture, VT("Trim.RudderValue"), _rudderTrim);

        _pitchBar.Value =
            Math.Clamp(
                _pitchTrim +
                _automaticRudderPitchCompensation,
                -95.0,
                95.0);

        _rollBar.Value =
            Math.Clamp(
                _rollTrim +
                _automaticRudderRollCompensation,
                -95.0,
                95.0);
        _rudderBar.Value = _rudderTrim;

        _stickPreview.SetValues(
            physicalRoll,
            physicalPitch,
            outputRoll,
            outputPitch);
        _stickPreview.SetInstructor(_instructorModeEnabled && _vJoyConnected,
            _currentManualCommand.Roll, _currentManualCommand.Pitch);

        _rudderPreview.SetValues(
            physicalRudder,
            outputRudder);
    }

    private void UpdateBindingButton(TrimAction action)
    {
        if (!_bindingButtons.TryGetValue(
                action,
                out Button? button))
        {
            return;
        }

        string bindingText = _bindings.TryGetValue(
            action,
            out ActionBinding? binding)
                ? binding.CompactDisplay
                : VT("Trim.ClickBind");

        button.Text = $"{GetActionSymbol(action)}  {bindingText}";
        button.TextAlign = ContentAlignment.MiddleCenter;

        string toolTipText = _bindings.TryGetValue(
            action,
            out binding)
                ? binding.FullDisplay
                : VT("Trim.AssignTooltip");

        _toolTip.SetToolTip(button, GetActionName(action) + "\r\n" + toolTipText);
    }

    private void UpdateAxisMappingLabels()
    {
        UpdateAxisMappingLabel(
            _rollAxisLabel,
            _rollAxis);

        UpdateAxisMappingLabel(
            _pitchAxisLabel,
            _pitchAxis);

        UpdateAxisMappingLabel(
            _rudderAxisLabel,
            _rudderAxis);
    }

    private void UpdateAxisMappingLabel(
        Label label,
        AxisSource? source)
    {
        if (source is null)
        {
            label.Text =
                VT("Common.NotConfigured");

            label.ForeColor =
                Theme.Warning;

            return;
        }

        bool connected =
            _devices.ContainsKey(
                source.DeviceGuid);

        label.Text =
            connected
                ? source.DisplayName
                : $"{source.DisplayName} (waiting)";

        label.ForeColor =
            connected
                ? Theme.Success
                : Theme.Warning;
    }

    private void SetAxisDetectButtonState(
        AxisTarget target,
        bool listening)
    {
        Button button = target switch
        {
            AxisTarget.Roll => _rollAxisButton,
            AxisTarget.Pitch => _pitchAxisButton,
            AxisTarget.Rudder => _rudderAxisButton,
            _ => throw new ArgumentOutOfRangeException(
                nameof(target))
        };

        button.Text = listening
            ? "Move axis..."
            : $"Detect {target}";

        button.BackColor = listening
            ? Theme.Warning
            : Theme.Control;
    }

    private void SelectDevice(Guid deviceGuid)
    {
        if (!_devices.TryGetValue(
                deviceGuid,
                out InputDevice? device))
        {
            return;
        }

        _selectedDeviceGuid = deviceGuid;
        _selectedDeviceName = device.FriendlyName;
        _deviceBox.SelectedItem = device;

        _deviceStatusLabel.Text =
            $"Selected device: {device.FriendlyName}";

        _deviceStatusLabel.ForeColor = Theme.Success;
    }

    private void CancelBindingCapture()
    {
        _captureAction = null;
        _captureFirstInput = null;
        ResetForeignChordSuppression();
    }

    private void ResetAutoDetectDeviceButton()
    {
        _autoDetectDeviceButton.Text =
            "Auto-Detect Device";

        _autoDetectDeviceButton.BackColor =
            Theme.Accent;
    }

    private void LoadApplicationSettings()
    {
        _loadingApplicationSettings = true;

        try
        {
            AppSettings settings =
                File.Exists(AppSettingsFilePath)
                    ? JsonSerializer.Deserialize<AppSettings>(
                          File.ReadAllText(AppSettingsFilePath),
                          BindingsJsonOptions)
                      ?? new AppSettings()
                    : new AppSettings();

            _globalAxes = settings.PhysicalAxes;
            _favoriteAircraft = settings.FavoriteAircraft ?? new();
            _selectedOutputBackend = "vJoy";
            _autoConnectToVJoyBox.Checked =
                settings.AutoConnectToVJoy;

            _startWithWindowsMinimizedBox.Checked =
                settings.Version >= 2
                    ? settings.StartWithWindowsMinimized
                    : (settings.StartWithWindows ?? true) &&
                      (settings.StartMinimizedWithWindows ?? true);
        }
        catch (Exception exception)
        {
            _autoConnectToVJoyBox.Checked = true;
            _startWithWindowsMinimizedBox.Checked = false;

            SetStatus(
                $"Startup preferences were reset: {exception.Message}",
                Theme.Warning);
        }
        finally
        {
            _loadingApplicationSettings = false;
        }

        // Refresh the Run entry on every launch so it continues to point to
        // the current executable if the portable VTrim folder was moved.
        SaveApplicationSettings(showConfirmation: false);
    }

    private void SaveApplicationSettings(
        bool showConfirmation = true)
    {
        if (_loadingApplicationSettings)
        {
            return;
        }

        try
        {
            var settings = new AppSettings
            {
                PhysicalAxes = _globalAxes,
                FavoriteAircraft = _favoriteAircraft,
                OutputBackend = "vJoy",
                AutoConnectToVJoy =
                    _autoConnectToVJoyBox.Checked,
                StartWithWindowsMinimized =
                    _startWithWindowsMinimizedBox.Checked
            };

            Directory.CreateDirectory(
                SettingsDirectory);

            File.WriteAllText(
                AppSettingsFilePath,
                JsonSerializer.Serialize(
                    settings,
                    BindingsJsonOptions));

            if (!_embeddedMode) UpdateWindowsStartupRegistration(
                settings.StartWithWindowsMinimized);

            if (showConfirmation)
            {
                SetStatus(
                    "Automatic connection and Windows startup preferences saved.",
                    Theme.Success);
            }
        }
        catch (Exception exception)
        {
            SetStatus(
                $"Startup preferences could not be saved: {exception.Message}",
                Theme.Error);
        }
    }

    private static void UpdateWindowsStartupRegistration(
        bool startWithWindowsMinimized)
    {
        using RegistryKey runKey =
            Registry.CurrentUser.CreateSubKey(
                StartupRegistryPath,
                writable: true)
            ?? throw new InvalidOperationException(
                "The Windows startup registry key is unavailable.");

        if (!startWithWindowsMinimized)
        {
            runKey.DeleteValue(
                StartupRegistryValueName,
                throwOnMissingValue: false);
            return;
        }

        string executablePath =
            GetApplicationExecutablePath();

        string startupCommand =
            $"\"{executablePath}\" {StartMinimizedArgument}";

        runKey.SetValue(
            StartupRegistryValueName,
            startupCommand,
            RegistryValueKind.String);
    }

    private static string GetApplicationExecutablePath()
    {
        string executablePath =
            Application.ExecutablePath;

        // Application.ExecutablePath is dotnet.exe under `dotnet run`.
        // Prefer this application's app host when it is available so a
        // development launch cannot register the shared .NET host itself.
        if (string.Equals(
                Path.GetFileName(executablePath),
                "dotnet.exe",
                StringComparison.OrdinalIgnoreCase))
        {
            string appHostPath =
                Path.Combine(
                    AppContext.BaseDirectory,
                    Assembly.GetExecutingAssembly().GetName().Name + ".exe");

            if (File.Exists(appHostPath))
            {
                executablePath = appHostPath;
            }
        }

        return Path.GetFullPath(
            executablePath);
    }

    private void LoadBindings()
    {
        StartAircraftDatabase();
        _loadingSavedSettings = true;

        try
        {
            Directory.CreateDirectory(
                ProfilesDirectory);

            List<string> profileNames =
                GetProfileNames();

            if (profileNames.Count == 0 ||
                (!File.Exists(ActiveProfileFilePath) && File.Exists(DefaultSetupFilePath)))
            {
                _activeProfileName =
                    string.Empty;

                var defaultSetup = File.Exists(DefaultSetupFilePath)
                    ? JsonSerializer.Deserialize<SavedBindingsFile>(File.ReadAllText(DefaultSetupFilePath), BindingsJsonOptions)
                    : null;
                ApplySavedProfile(defaultSetup ?? new SavedBindingsFile { ProfileName = string.Empty });

                DeleteActiveProfilePointer();
                RefreshProfileList();

                SetInstruction(
                    "Default setup: axes and bindings are saved automatically.",
                    Theme.Success);

                return;
            }

            string requestedProfile =
                File.Exists(
                    ActiveProfileFilePath)
                    ? File.ReadAllText(
                        ActiveProfileFilePath).Trim()
                    : string.Empty;

            _activeProfileName =
                UpgradeLegacyProfileDisplayName(
                    profileNames.FirstOrDefault(
                        name =>
                            string.Equals(
                                name,
                                requestedProfile,
                                StringComparison.OrdinalIgnoreCase))
                    ?? profileNames[0]);

            SavedBindingsFile profile =
                ReadProfile(
                    _activeProfileName);

            ApplySavedProfile(
                profile);

            RefreshProfileList();

            SetInstruction(
                $"Loaded profile: {_activeProfileName}.",
                Theme.Success);
        }
        catch (Exception exception)
        {
            _activeProfileName =
                string.Empty;

            SetInstruction(
                $"Profiles could not be loaded: {exception.Message}",
                Theme.Error);
        }
        finally
        {
            _loadingSavedSettings = false;
        }
    }

    private void SaveBindings()
    {
        if (_offlinePreview) return;
        try
        {
            if (_loadingSavedSettings) return;
            CaptureGlobalAxes();
            SaveApplicationSettings(showConfirmation: false);
            if (string.IsNullOrWhiteSpace(_activeProfileName))
            {
                Directory.CreateDirectory(SettingsDirectory);
                string temporary = DefaultSetupFilePath + ".tmp";
                File.WriteAllText(temporary, JsonSerializer.Serialize(CaptureCurrentProfile(), BindingsJsonOptions));
                File.Move(temporary, DefaultSetupFilePath, overwrite: true);
                DeleteActiveProfilePointer();
                UpdateProfileSummary();
                return;
            }

            Directory.CreateDirectory(
                ProfilesDirectory);

            SavedBindingsFile profile =
                CaptureCurrentProfile();

            WriteProfile(
                _activeProfileName,
                profile);

            WriteActiveProfileName();
            UpdateProfileSummary();
        }
        catch (Exception exception)
        {
            if (!IsDisposed &&
                IsHandleCreated)
            {
                SetStatus(
                    $"Profile could not be saved: {exception.Message}",
                    Theme.Error);
            }
        }
    }

    private SavedBindingsFile CaptureCurrentProfile()
    {
        var profile =
            new SavedBindingsFile
            {
                Id = _profileId,
                AircraftId = _profileAircraftId,
                Version = 11,
                ProfileName =
                    _activeProfileName,
                AircraftType = NormalizeAircraftType(_profileTypeBox?.SelectedItem?.ToString()),

                RollAxis = null,

                PitchAxis = null,

                RudderAxis = null,

                InvertRoll =
                    _invertRollBox?.Checked ?? false,

                InvertPitch =
                    _invertPitchBox?.Checked ?? false,

                InvertRudder =
                    _invertRudderBox?.Checked ?? false,

                PitchStep =
                    _pitchStepBox?.Value ?? 0.50M,

                RollStep =
                    _rollStepBox?.Value ?? 0.50M,

                RudderStep =
                    _rudderStepBox?.Value ?? 0.50M,

                SendToVJoy = true,

                SelectedDeviceGuid =
                    _selectedDeviceGuid?.ToString("D") ??
                    string.Empty,

                SelectedDeviceName =
                    _selectedDeviceName,

                UniversalTriggerPassthrough =
                    _universalTriggerBox?.Checked ?? false,

                BlockUnassignedInputChords =
                    _unassignedInputGuardBox?.Checked ?? true,

                AutoCenterAxesOnVJoyConnect = false,

                RepeatWhileHeld =
                    _repeatWhileHeldBox?.Checked ?? true,

                HeldTrimRate =
                    _repeatSpeedSlider?.Value ?? 7,

                InstructorModeEnabled =
                    _instructorModeEnabled,
                Instructor = _instructorTuning,
                RollResponse = _rollResponse,
                PitchResponse = _pitchResponse,
                RudderResponse = _rudderResponse,

                HorizontalRudderAssist =
                    _horizontalRudderAssistBox?.Checked ?? false,

                RudderRollCompensationPercent =
                    _rudderRollCompensationSlider?.Value ?? 35,

                RudderRollCompensationDirection =
                    _rudderRollCompensationDirection >= 0
                        ? 1
                        : -1,

                RudderPitchCompensationPercent =
                    _rudderPitchCompensationSlider?.Value ?? 0,

                RudderPitchCompensationDirection =
                    _rudderPitchCompensationDirection >= 0
                        ? 1
                        : -1
            };

        foreach ((TrimAction action, ActionBinding binding)
                 in _bindings)
        {
            profile.Bindings[action.ToString()] =
                new SavedActionBinding
                {
                    Trigger =
                        SavePhysicalInput(
                            binding.Trigger),

                    Modifier =
                        binding.Modifier is null
                            ? null
                            : SavePhysicalInput(
                                binding.Modifier)
                };
        }

        return profile;
    }

    private void ApplySavedProfile(
        SavedBindingsFile profile)
    {
        _profileId = Guid.TryParse(profile.Id, out _) ? profile.Id : Guid.NewGuid().ToString("D");
        _profileAircraftId = profile.AircraftId;
        bool oldLoadingState =
            _loadingSavedSettings;

        _loadingSavedSettings = true;

        try
        {
            CancelBindingCapture();
            ApplyGlobalAxes(profile);
            _globalAxes!.Bindings ??= profile.Bindings;
            profile.Bindings = _globalAxes.Bindings;
            ResetForeignChordSuppression();
            _trimHoldStartedTimes.Clear();
            _capturedCombinationTriggers.Clear();

            _pitchTrim = 0.0;
            _rollTrim = 0.0;
            _rudderTrim = 0.0;
            _automaticRudderRollCompensation = 0.0;
            _automaticRudderPitchCompensation = 0.0;
            _instructorModeEnabled =
                profile.Version >= 10 && profile.InstructorModeEnabled && NormalizeAircraftType(profile.AircraftType) != "Helicopter";
            _instructorTuning = profile.Version >= 11 ? profile.Instructor ?? new() : new();
            _instructorTuning.Normalize();
            _rollResponse = profile.RollResponse ?? new();
            _pitchResponse = profile.PitchResponse ?? new();
            _rudderResponse = profile.RudderResponse ?? new();
            _rollResponse.Normalize();
            _pitchResponse.Normalize();
            _rudderResponse.Normalize();
            RefreshCurveEditors();

            ResetAutomaticInstructorHold();
            _storeTrimReturn.Cancel();
            _currentManualCommand = default;
            _lastAxisSnapshotTime = double.NegativeInfinity;
            _horizontalRudderCalibrationArmed = false;
            _horizontalRudderCalibrationMessage = string.Empty;

            _bindings.Clear();

            foreach ((string actionName, SavedActionBinding savedBinding)
                     in profile.Bindings)
            {
                if (!Enum.TryParse(
                        actionName,
                        ignoreCase: true,
                        out TrimAction action) ||
                    savedBinding.Trigger is null)
                {
                    continue;
                }

                PhysicalInput? trigger =
                    RestorePhysicalInput(
                        savedBinding.Trigger);

                if (trigger is null)
                {
                    continue;
                }

                PhysicalInput? modifier =
                    savedBinding.Modifier is null
                        ? null
                        : RestorePhysicalInput(
                            savedBinding.Modifier);

                if (savedBinding.Modifier is not null &&
                    modifier is null)
                {
                    continue;
                }

                // 1.3.0 used the StoreCurrentTrim key for Instructor Mode.
                // Preserve that HOTAS assignment as Instructor, not as BOTH actions.
                if (profile.Version == 7 && action == TrimAction.StoreCurrentTrim &&
                    !profile.Bindings.Keys.Any(k => string.Equals(k, nameof(TrimAction.ToggleInstructor), StringComparison.OrdinalIgnoreCase)))
                    action = TrimAction.ToggleInstructor;

                _bindings[action] =
                    new ActionBinding
                    {
                        Trigger = trigger,
                        Modifier = modifier
                    };
            }

            _rollAxis =
                RestoreAxisSource(
                    profile.RollAxis);

            _pitchAxis =
                RestoreAxisSource(
                    profile.PitchAxis);

            _rudderAxis =
                RestoreAxisSource(
                    profile.RudderAxis);

            _invertRollBox.Checked =
                profile.InvertRoll;

            _invertPitchBox.Checked =
                profile.InvertPitch;

            _invertRudderBox.Checked =
                profile.InvertRudder;

            _pitchStepBox.Value =
                profile.PitchStep;

            _rollStepBox.Value =
                profile.RollStep;

            _rudderStepBox.Value =
                profile.RudderStep;

            _universalTriggerBox.Checked =
                profile.UniversalTriggerPassthrough;

            _unassignedInputGuardBox.Checked =
                profile.BlockUnassignedInputChords;

            _repeatWhileHeldBox.Checked =
                profile.RepeatWhileHeld;

            _repeatSpeedSlider.Value =
                Math.Clamp(
                    profile.HeldTrimRate,
                    _repeatSpeedSlider.Minimum,
                    _repeatSpeedSlider.Maximum);

            _horizontalRudderAssistBox.Checked =
                false;

            _rudderRollCompensationSlider.Value =
                Math.Clamp(
                    profile.RudderRollCompensationPercent,
                    _rudderRollCompensationSlider.Minimum,
                    _rudderRollCompensationSlider.Maximum);

            _rudderRollCompensationDirection =
                profile.RudderRollCompensationDirection >= 0
                    ? 1
                    : -1;

            _rudderPitchCompensationSlider.Value =
                Math.Clamp(
                    profile.RudderPitchCompensationPercent,
                    _rudderPitchCompensationSlider.Minimum,
                    _rudderPitchCompensationSlider.Maximum);

            _rudderPitchCompensationDirection =
                profile.RudderPitchCompensationDirection >= 0
                    ? 1
                    : -1;

            _selectedDeviceName =
                profile.SelectedDeviceName;

            _selectedDeviceGuid =
                Guid.TryParse(
                    profile.SelectedDeviceGuid,
                    out Guid selectedGuid)
                        ? selectedGuid
                        : null;

            if (_profileTypeBox is not null)
                _profileTypeBox.SelectedItem = NormalizeAircraftType(profile.AircraftType);

            ApplyGlobalAxes(profile);
            ReconnectSavedDeviceReferences();
            UpdateAxisMappingLabels();

            foreach (TrimAction action
                     in Enum.GetValues<TrimAction>())
            {
                UpdateBindingButton(action);
            }

            UpdateHorizontalRudderAssistUi();
            UpdateInstructorModeUi();
            UpdateUniversalTriggerToggle();
            UpdateUnassignedInputGuardToggle();
            UpdateRepeatSpeedLabel();
            UpdateRepeatWhileHeldToggle();
            UpdateTrimDisplay(0, 0, 0, 0, 0, 0);
            UpdateProfileSummary();
        }
        finally
        {
            _loadingSavedSettings =
                oldLoadingState;
        }
    }

    private static SavedAxisSource? SaveAxisSource(
        AxisSource? source)
    {
        if (source is null)
        {
            return null;
        }

        return new SavedAxisSource
        {
            DeviceGuid =
                source.DeviceGuid.ToString("D"),

            DeviceName =
                source.DeviceName,

            AxisKey =
                source.AxisKey,

            CenterRaw =
                source.CenterRaw,

            SignedRange =
                source.SignedRange
        };
    }

    private static AxisSource? RestoreAxisSource(
        SavedAxisSource? source)
    {
        if (source is null ||
            string.IsNullOrWhiteSpace(
                source.AxisKey))
        {
            return null;
        }

        Guid.TryParse(
            source.DeviceGuid,
            out Guid deviceGuid);

        return new AxisSource
        {
            DeviceGuid = deviceGuid,
            DeviceName = source.DeviceName,
            AxisKey = source.AxisKey,
            CenterRaw = source.CenterRaw,
            SignedRange = source.SignedRange
        };
    }

    private List<string> GetProfileNames()
    {
        Directory.CreateDirectory(
            ProfilesDirectory);

        var names =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        foreach (string path in Directory.EnumerateFiles(
                     ProfilesDirectory,
                     "*.json",
                     SearchOption.TopDirectoryOnly))
        {
            string displayName =
                string.Empty;

            try
            {
                string json =
                    File.ReadAllText(
                        path);

                SavedBindingsFile? profile =
                    JsonSerializer.Deserialize<SavedBindingsFile>(
                        json,
                        BindingsJsonOptions);

                displayName =
                    UpgradeLegacyProfileDisplayName(
                        profile?.ProfileName?.Trim() ??
                        string.Empty);
            }
            catch
            {
                // Fall back to the filename for an older or damaged file.
            }

            if (string.IsNullOrWhiteSpace(
                    displayName))
            {
                string encodedName =
                    Path.GetFileNameWithoutExtension(
                        path);

                displayName =
                    DecodeProfileFileName(
                        encodedName);
            }

            if (!string.IsNullOrWhiteSpace(
                    displayName))
            {
                names.Add(
                    displayName);
            }
        }

        return names
            .OrderBy(
                name => name,
                StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private bool ProfileExists(
        string profileName)
    {
        return File.Exists(
            GetProfileFilePath(
                profileName));
    }

    private SavedBindingsFile ReadProfile(
        string profileName)
    {
        string profilePath =
            GetProfileFilePath(
                profileName);

        if (!File.Exists(profilePath))
        {
            return new SavedBindingsFile
            {
                ProfileName =
                    profileName
            };
        }

        string json =
            File.ReadAllText(
                profilePath);

        SavedBindingsFile? profile =
            JsonSerializer.Deserialize<SavedBindingsFile>(
                json,
                BindingsJsonOptions);

        profile ??=
            new SavedBindingsFile();

        profile.ProfileName =
            UpgradeLegacyProfileDisplayName(
                profileName);

        return profile;
    }

    private void WriteProfile(
        string profileName,
        SavedBindingsFile profile)
    {
        Directory.CreateDirectory(
            ProfilesDirectory);

        profile.ProfileName =
            profileName;

        string profilePath =
            GetProfileFilePath(
                profileName);

        string temporaryPath =
            profilePath + ".tmp";

        string json =
            JsonSerializer.Serialize(
                profile,
                BindingsJsonOptions);

        File.WriteAllText(
            temporaryPath,
            json);
        using (JsonDocument.Parse(File.ReadAllText(temporaryPath))) { }

        if (File.Exists(profilePath) && !File.Exists(profilePath + ".pre-1.3.1.bak"))
            File.Copy(profilePath, profilePath + ".pre-1.3.1.bak", overwrite: false);
        File.Move(
            temporaryPath,
            profilePath,
            overwrite: true);
    }

    private void WriteActiveProfileName()
    {
        Directory.CreateDirectory(
            ProfilesDirectory);

        if (string.IsNullOrWhiteSpace(
                _activeProfileName))
        {
            DeleteActiveProfilePointer();
            return;
        }

        File.WriteAllText(
            ActiveProfileFilePath,
            _activeProfileName);
    }

    private void DeleteActiveProfilePointer()
    {
        try
        {
            if (File.Exists(
                    ActiveProfileFilePath))
            {
                File.Delete(
                    ActiveProfileFilePath);
            }
        }
        catch
        {
            // A stale active-profile pointer must never stop VTrim.
        }
    }

    private string GetProfileFilePath(
        string profileName)
    {
        Directory.CreateDirectory(
            ProfilesDirectory);

        // First support existing profile files, including the earlier
        // underscore-based filenames.
        foreach (string path in Directory.EnumerateFiles(
                     ProfilesDirectory,
                     "*.json",
                     SearchOption.TopDirectoryOnly))
        {
            try
            {
                string json =
                    File.ReadAllText(
                        path);

                SavedBindingsFile? profile =
                    JsonSerializer.Deserialize<SavedBindingsFile>(
                        json,
                        BindingsJsonOptions);

                if (profile is not null &&
                    string.Equals(
                        profile.ProfileName,
                        profileName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return path;
                }
            }
            catch
            {
                // Ignore an unreadable unrelated profile and continue.
            }
        }

        return Path.Combine(
            ProfilesDirectory,
            EncodeProfileFileName(
                profileName) + ".json");
    }

    private static string EncodeProfileFileName(
        string profileName)
    {
        // Windows cannot place ':' in a filename. URI escaping keeps the
        // visible profile name unchanged while producing a safe filename.
        return Uri.EscapeDataString(
            NormalizeProfileName(
                profileName));
    }

    private static string DecodeProfileFileName(
        string encodedName)
    {
        try
        {
            return Uri.UnescapeDataString(
                encodedName);
        }
        catch
        {
            return encodedName;
        }
    }

    private static string UpgradeLegacyProfileDisplayName(
        string profileName)
    {
        if (string.IsNullOrWhiteSpace(
                profileName))
        {
            return profileName;
        }

        // Older VTrim builds replaced Windows-invalid filename characters
        // inside the visible profile name. The most common result was:
        // "War Thunder_ A-10C" instead of "War Thunder: A-10C".
        //
        // Only convert underscore + space, which is the legacy separator
        // pattern. Normal underscores inside words remain unchanged.
        return profileName.Replace(
            "_ ",
            ": ",
            StringComparison.Ordinal);
    }

    private static string NormalizeProfileName(
        string profileName)
    {
        // This normalizes only the visible name. It no longer replaces ':'
        // or other punctuation with underscores.
        string cleaned =
            new string(
                profileName
                    .Trim()
                    .Where(
                        character =>
                            !char.IsControl(
                                character))
                    .ToArray());

        cleaned =
            string.Join(
                " ",
                cleaned.Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries));

        if (cleaned.Length > 48)
        {
            cleaned =
                cleaned[..48];
        }

        return cleaned;
    }

    private string GetUniqueProfileName(
        string baseName)
    {
        string candidate =
            NormalizeProfileName(
                baseName);

        int suffix = 2;

        while (ProfileExists(candidate))
        {
            candidate =
                NormalizeProfileName(
                    $"{baseName} {suffix}");

            suffix++;
        }

        return candidate;
    }

    private void ReconnectSavedDeviceReferences()
    {
        _rollAxis =
            ReconnectAxisSource(
                _rollAxis);

        _pitchAxis =
            ReconnectAxisSource(
                _pitchAxis);

        _rudderAxis =
            ReconnectAxisSource(
                _rudderAxis);

        foreach (TrimAction action in _bindings.Keys.ToList())
        {
            ActionBinding binding =
                _bindings[action];

            _bindings[action] =
                new ActionBinding
                {
                    Trigger =
                        ReconnectPhysicalInput(
                            binding.Trigger),

                    Modifier =
                        binding.Modifier is null
                            ? null
                            : ReconnectPhysicalInput(
                                binding.Modifier)
                };
        }
    }

    private AxisSource? ReconnectAxisSource(
        AxisSource? source)
    {
        if (source is null ||
            _devices.ContainsKey(
                source.DeviceGuid))
        {
            return source;
        }

        InputDevice? replacement =
            _devices.Values.FirstOrDefault(
                device =>
                    string.Equals(
                        device.FriendlyName,
                        source.DeviceName,
                        StringComparison.OrdinalIgnoreCase));

        if (replacement is null)
        {
            return source;
        }

        return new AxisSource
        {
            DeviceGuid = replacement.Guid,
            DeviceName = replacement.FriendlyName,
            AxisKey = source.AxisKey,
            CenterRaw = source.CenterRaw,
            SignedRange = source.SignedRange
        };
    }

    private PhysicalInput ReconnectPhysicalInput(
        PhysicalInput input)
    {
        if (_devices.ContainsKey(
                input.DeviceGuid))
        {
            return input;
        }

        InputDevice? replacement =
            _devices.Values.FirstOrDefault(
                device =>
                    string.Equals(
                        device.FriendlyName,
                        input.DeviceName,
                        StringComparison.OrdinalIgnoreCase));

        if (replacement is null)
        {
            return input;
        }

        return new PhysicalInput
        {
            DeviceGuid = replacement.Guid,
            DeviceName = replacement.FriendlyName,
            Kind = input.Kind,
            Index = input.Index,
            PovDirection = input.PovDirection
        };
    }

    private static SavedPhysicalInput SavePhysicalInput(
        PhysicalInput input)
    {
        return new SavedPhysicalInput
        {
            DeviceGuid =
                input.DeviceGuid.ToString("D"),

            DeviceName =
                input.DeviceName,

            Kind =
                input.Kind.ToString(),

            Index =
                input.Index,

            PovDirection =
                input.PovDirection.ToString()
        };
    }

    private static PhysicalInput? RestorePhysicalInput(
        SavedPhysicalInput savedInput)
    {
        if (!Guid.TryParse(
                savedInput.DeviceGuid,
                out Guid deviceGuid) ||
            !Enum.TryParse(
                savedInput.Kind,
                ignoreCase: true,
                out InputKind kind) ||
            !Enum.TryParse(
                savedInput.PovDirection,
                ignoreCase: true,
                out PovDirection povDirection) ||
            savedInput.Index < 0)
        {
            return null;
        }

        return new PhysicalInput
        {
            DeviceGuid = deviceGuid,
            DeviceName =
                string.IsNullOrWhiteSpace(
                    savedInput.DeviceName)
                    ? "Saved Controller"
                    : savedInput.DeviceName,

            Kind = kind,
            Index = savedInput.Index,
            PovDirection = povDirection
        };
    }

    private void Form1_KeyDown(
        object? sender,
        KeyEventArgs eventArgs)
    {
        if (eventArgs.KeyCode != Keys.Escape)
        {
            return;
        }

        bool cancelled = false;

        if (_captureAction.HasValue)
        {
            CancelBindingCapture();
            cancelled = true;
        }

        if (_waitingForDeviceDetection)
        {
            _waitingForDeviceDetection = false;
            ResetAutoDetectDeviceButton();
            cancelled = true;
        }

        if (_detectingAxis.HasValue)
        {
            SetAxisDetectButtonState(
                _detectingAxis.Value,
                listening: false);

            _detectingAxis = null;
            _axisDetectionBaseline.Clear();
            cancelled = true;
        }

        if (cancelled)
        {
            SetInstruction(
                VT("Common.Cancelled"),
                Theme.Warning);

            eventArgs.Handled = true;
        }
    }

    private void Form1_FormClosed(
        object? sender,
        FormClosedEventArgs eventArgs)
    {
        if (_applicationClosing) return;
        _applicationClosing = true;
        _instructorController.Invalidate();
        _telemetryShutdown.Cancel();
        _pollTimer.Stop();
        _vJoyAutoConnectTimer.Stop();
        _warThunderTelemetryTimer.Stop();
        SaveBindings();
        DisconnectVJoy();
        DisposeInputSystem();
        _warThunderTelemetryClient.Dispose();
        _pollTimer.Dispose();
        _vJoyAutoConnectTimer.Dispose();
        _warThunderTelemetryTimer.Dispose();
    }

    private void DisposeInputSystem()
    {
        foreach (InputDevice device in _devices.Values)
        {
            device.Dispose();
        }

        _storeTrimReturn.Cancel();
        _lastAxisSnapshotTime = double.NegativeInfinity;
        _devices.Clear();
        _capturedCombinationTriggers.Clear();
        ResetForeignChordSuppression();

        _directInput?.Dispose();
        _directInput = null;
    }

    private static bool TryReadState(
        InputDevice device,
        out DirectInputState state)
    {
        state = new DirectInputState();

        try
        {
            var pollResult = device.Device.Poll();

            if (pollResult.Failure)
            {
                var acquireResult = device.Device.Acquire();

                if (acquireResult.Failure)
                {
                    return false;
                }

                pollResult = device.Device.Poll();

                if (pollResult.Failure)
                {
                    return false;
                }
            }

            state = device.Device.GetCurrentJoystickState();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static IEnumerable<AxisSample> EnumerateAxisSamples(
        DirectInputState state)
    {
        Type stateType = typeof(DirectInputState);

        foreach (string propertyName in ScalarAxisPropertyNames)
        {
            PropertyInfo? property =
                stateType.GetProperty(
                    propertyName,
                    BindingFlags.Instance |
                    BindingFlags.Public);

            if (property?.GetValue(state) is int rawValue)
            {
                yield return new AxisSample
                {
                    Key = propertyName,
                    RawValue = rawValue
                };
            }
        }

        PropertyInfo? slidersProperty =
            stateType.GetProperty(
                "Sliders",
                BindingFlags.Instance |
                BindingFlags.Public);

        if (slidersProperty?.GetValue(state) is int[] sliders)
        {
            for (int index = 0; index < sliders.Length; index++)
            {
                yield return new AxisSample
                {
                    Key = $"Sliders[{index}]",
                    RawValue = sliders[index]
                };
            }
        }
    }

    private static bool TryReadAxisRaw(
        DirectInputState state,
        string axisKey,
        out int rawValue)
    {
        rawValue = 0;

        if (axisKey.StartsWith(
                "Sliders[",
                StringComparison.Ordinal))
        {
            int start = axisKey.IndexOf('[') + 1;
            int end = axisKey.IndexOf(']');

            if (start <= 0 ||
                end <= start ||
                !int.TryParse(
                    axisKey[start..end],
                    out int sliderIndex))
            {
                return false;
            }

            PropertyInfo? slidersProperty =
                typeof(DirectInputState).GetProperty(
                    "Sliders",
                    BindingFlags.Instance |
                    BindingFlags.Public);

            if (slidersProperty?.GetValue(state) is not int[] sliders ||
                sliderIndex < 0 ||
                sliderIndex >= sliders.Length)
            {
                return false;
            }

            rawValue = sliders[sliderIndex];
            return true;
        }

        PropertyInfo? property =
            typeof(DirectInputState).GetProperty(
                axisKey,
                BindingFlags.Instance |
                BindingFlags.Public);

        if (property?.GetValue(state) is not int scalarValue)
        {
            return false;
        }

        rawValue = scalarValue;
        return true;
    }

    private static double NormalizeAxis(
        int raw,
        int center,
        bool signedRange)
    {
        int minimum = signedRange
            ? -32768
            : 0;

        int maximum = signedRange
            ? 32767
            : 65535;

        center = Math.Clamp(
            center,
            minimum + 1,
            maximum - 1);

        double normalized = raw >= center
            ? (raw - center) /
              (double)(maximum - center)
            : (raw - center) /
              (double)(center - minimum);

        return Math.Clamp(
            normalized,
            -1.0,
            1.0);
    }

    private static bool LooksLikeSignedRange(int center)
    {
        return center >= -4096 &&
               center <= 4096;
    }

    private static bool InputsAreEqual(
        PhysicalInput first,
        PhysicalInput second)
    {
        return first.DeviceGuid == second.DeviceGuid &&
               first.Kind == second.Kind &&
               first.Index == second.Index &&
               first.PovDirection ==
               second.PovDirection;
    }

    private static PovDirection GetPovDirection(int value)
    {
        if (value < 0 ||
            value == 65535)
        {
            return PovDirection.Center;
        }

        int angle = value % 36000;

        if (angle < 2250 ||
            angle >= 33750)
        {
            return PovDirection.Up;
        }

        if (angle < 6750)
        {
            return PovDirection.UpRight;
        }

        if (angle < 11250)
        {
            return PovDirection.Right;
        }

        if (angle < 15750)
        {
            return PovDirection.DownRight;
        }

        if (angle < 20250)
        {
            return PovDirection.Down;
        }

        if (angle < 24750)
        {
            return PovDirection.DownLeft;
        }

        if (angle < 29250)
        {
            return PovDirection.Left;
        }

        return PovDirection.UpLeft;
    }

    private static bool PovMatches(
        PovDirection binding,
        PovDirection current)
    {
        return binding switch
        {
            PovDirection.Up =>
                current is PovDirection.Up or
                    PovDirection.UpLeft or
                    PovDirection.UpRight,

            PovDirection.Down =>
                current is PovDirection.Down or
                    PovDirection.DownLeft or
                    PovDirection.DownRight,

            PovDirection.Left =>
                current is PovDirection.Left or
                    PovDirection.UpLeft or
                    PovDirection.DownLeft,

            PovDirection.Right =>
                current is PovDirection.Right or
                    PovDirection.UpRight or
                    PovDirection.DownRight,

            _ => current == binding
        };
    }

    private static string FormatPovDirection(
        PovDirection direction)
    {
        return direction switch
        {
            PovDirection.Up => "Up",
            PovDirection.UpRight => "Up Right",
            PovDirection.Right => "Right",
            PovDirection.DownRight => "Down Right",
            PovDirection.Down => "Down",
            PovDirection.DownLeft => "Down Left",
            PovDirection.Left => "Left",
            PovDirection.UpLeft => "Up Left",
            _ => "Centered"
        };
    }

    private string GetActionName(
        TrimAction action)
    {
        return action switch
        {
            TrimAction.NoseDown => VT("Trim.NoseDown"),
            TrimAction.NoseUp => VT("Trim.NoseUp"),
            TrimAction.RollLeft => VT("Trim.RollLeft"),
            TrimAction.RollRight => VT("Trim.RollRight"),
            TrimAction.RudderLeft => VT("Trim.RudderLeft"),
            TrimAction.RudderRight => VT("Trim.RudderRight"),
            TrimAction.StoreCurrentTrim => VT("Trim.Store"),
            TrimAction.ToggleInstructor => VT("Trim.Instructor"),
            TrimAction.ResetAll => VT("Trim.Center"),
            _ => action.ToString().ToUpperInvariant()
        };
    }

    private static string GetActionSymbol(
        TrimAction action)
    {
        return action switch
        {
            TrimAction.NoseDown => "▲",
            TrimAction.NoseUp => "▼",
            TrimAction.RollLeft => "◀",
            TrimAction.RollRight => "▶",
            TrimAction.RudderLeft => "↶",
            TrimAction.RudderRight => "↷",
            TrimAction.StoreCurrentTrim => "◆",
            TrimAction.ToggleInstructor => "○",
            TrimAction.ResetAll => "●",
            _ => string.Empty
        };
    }

    private static string CleanDeviceName(
        string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        string result = name.Trim();

        if (result.Equals(
                "HID-compliant game controller",
                StringComparison.OrdinalIgnoreCase))
        {
            return "Generic HID Game Controller";
        }

        return result;
    }

    private static void OpenWebLink(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch
        {
            // Browser launch failure is non-fatal.
        }
    }

    private static void OpenJoyControlPanel()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "control.exe",
                Arguments = "joy.cpl",
                UseShellExecute = true
            });
        }
        catch
        {
            // Control panel launch failure is non-fatal.
        }
    }

    private static Label CreateAxisBindingLabel(
        string text)
    {
        return new Label
        {
            Text = text,
            BackColor = Theme.Inner,
            BorderStyle = BorderStyle.FixedSingle,
            ForeColor = Theme.Warning,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI Semibold", 11F),
            Padding = new Padding(8, 0, 8, 0),
            AutoEllipsis = false
        };
    }

    private static CheckBox CreateInvertBox()
    {
        return new CheckBox
        {
            Text = "Invert",
            Tag = "i18n:Devices.Invert",
            ForeColor = Theme.Text,
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI Semibold", 11F),
            TextAlign = ContentAlignment.MiddleLeft
        };
    }

    private static Button CreateSmallButton(
        string text)
    {
        var button = CreateSecondaryButton(text);
        button.Font = new Font(
            "Segoe UI Semibold",
            10F);

        return button;
    }

    private static Label CreateAxisValueLabel(
        string text,
        Point location)
    {
        return new Label
        {
            Text = text,
            Location = location,
            Size = new Size(150, 30),
            Font = new Font("Segoe UI Semibold", 11F),
            ForeColor = Theme.Text
        };
    }

    private static TrimBar CreateTrimBar(
        Point location,
        Size size)
    {
        return new TrimBar
        {
            Location = location,
            Size = size
        };
    }

    private static Panel CreateInnerPanel(
        Point location,
        Size size)
    {
        return new CardPanel
        {
            Location = location,
            Size = size,
            BackColor = Theme.Inner,
            BorderColor = Theme.Border
        };
    }

    private static Panel CreateSectionPanel(
        Point location,
        Size size)
    {
        return new CardPanel
        {
            Location = location,
            Size = size,
            BackColor = Theme.Panel,
            BorderColor = Theme.Border
        };
    }

    private static Label CreateSectionHeading(
        string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Font = new Font(
                "Segoe UI Semibold",
                9.5F),
            ForeColor = Theme.Text
        };
    }

    private static Button CreatePrimaryButton(
        string text)
    {
        var button = new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Accent,
            ForeColor = Theme.Background,
            Font = new Font(
                "Segoe UI Semibold",
                9F),
            Cursor = Cursors.Hand,
            UseMnemonic = false,
            AutoEllipsis = false
        };

        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor =
            Theme.AccentHover;

        button.FlatAppearance.MouseDownBackColor =
            Theme.AccentPressed;

        return button;
    }

    private static Button CreateBeerButton(
        string text)
    {
        var button = new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.BeerControl,
            ForeColor = Theme.BeerText,
            Font = new Font("Segoe UI Semibold", 9F),
            Cursor = Cursors.Hand,
            UseMnemonic = false,
            AutoEllipsis = false
        };

        button.FlatAppearance.BorderColor = Theme.BeerBorder;
        button.FlatAppearance.MouseOverBackColor = Theme.BeerControlHover;
        button.FlatAppearance.MouseDownBackColor = Theme.BeerControlPressed;

        return button;
    }

    private static Button CreateSecondaryButton(
        string text)
    {
        var button = new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Control,
            ForeColor = Theme.Text,
            Font = new Font(
                "Segoe UI Semibold",
                9F),
            Cursor = Cursors.Hand,
            UseMnemonic = false,
            AutoEllipsis = false
        };

        button.FlatAppearance.BorderColor =
            Theme.Border;

        button.FlatAppearance.MouseOverBackColor =
            Theme.ControlHover;

        button.FlatAppearance.MouseDownBackColor =
            Theme.ControlPressed;

        return button;
    }

    private static Button CreateResetButton(
        string text)
    {
        var button = new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.ResetControl,
            ForeColor = Theme.Text,
            Font = new Font(
                "Segoe UI Semibold",
                9F),
            Cursor = Cursors.Hand,
            UseMnemonic = false,
            AutoEllipsis = false
        };

        button.FlatAppearance.BorderColor =
            Theme.ResetBorder;

        button.FlatAppearance.MouseOverBackColor =
            Theme.ResetControlHover;

        button.FlatAppearance.MouseDownBackColor =
            Theme.ResetControlPressed;

        return button;
    }

    private void SetInstruction(
        string text,
        Color color)
    {
        _instructionLabel.Text = text;
        _instructionLabel.ForeColor = color;
    }

    private void SetStatus(
        string text,
        Color color)
    {
        _statusLabel.Text = text;
        _statusLabel.ForeColor = color;
    }
}


internal sealed class ProfileTypeComboBox : ComboBox
{
    public ProfileTypeComboBox()
    {
        DropDownStyle = ComboBoxStyle.DropDownList;
        DrawMode = DrawMode.OwnerDrawFixed;
        ItemHeight = 42;
        FlatStyle = FlatStyle.Flat;
        Items.AddRange(new object[] { "Prop Plane", "Jet Plane", "Helicopter" });
        SelectedIndex = 0;
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= Items.Count) return;
        e.DrawBackground();
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        string text = Convert.ToString(Items[e.Index], CultureInfo.InvariantCulture) ?? "Prop Plane";
        Color color = (e.State & DrawItemState.Selected) != 0 ? Color.White : Theme.Text;
        Rectangle icon = new(e.Bounds.X + 8, e.Bounds.Y + 2, 46, Math.Max(18, e.Bounds.Height - 4));
        DrawAircraftIcon(e.Graphics, icon, text, color);
        Rectangle textBounds = new(icon.Right + 9, e.Bounds.Y, Math.Max(20, e.Bounds.Right - icon.Right - 12), e.Bounds.Height);
        float drawSize = Font.Size;
        while (drawSize > 7.5F)
        {
            using Font test = new(Font.FontFamily, drawSize, FontStyle.Bold, Font.Unit);
            if (TextRenderer.MeasureText(e.Graphics, text, test, Size.Empty, TextFormatFlags.NoPadding).Width <= textBounds.Width) break;
            drawSize -= 0.5F;
        }
        using Font font = new(Font.FontFamily, drawSize, FontStyle.Bold, Font.Unit);
        TextRenderer.DrawText(e.Graphics, text, font, textBounds,
            color, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        e.DrawFocusRectangle();
    }

    private static void DrawAircraftIcon(Graphics g, Rectangle bounds, string type, Color color)
    {
        AircraftIcons.Draw(g, bounds, type, color);
    }
}


internal sealed class CardPanel : Panel
{
    public CardPanel()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.UserPaint,
            true);
    }

    [DefaultValue(typeof(Color), "58, 71, 92")]
    public Color BorderColor { get; set; } =
        Theme.Border;

    protected override void OnPaint(
        PaintEventArgs eventArgs)
    {
        base.OnPaint(eventArgs);

        using var borderPen =
            new Pen(BorderColor, 1F);

        eventArgs.Graphics.DrawRectangle(
            borderPen,
            0,
            0,
            Math.Max(0, Width - 1),
            Math.Max(0, Height - 1));
    }
}

internal static class Theme
{
    public static readonly Color Background = Color.FromArgb(29, 31, 33);
    public static readonly Color Header = Color.FromArgb(29, 31, 33);
    public static readonly Color Navigation = Color.FromArgb(25, 25, 28);
    public static readonly Color SelectedNavigation = Color.FromArgb(46, 46, 51);
    public static readonly Color Surface = Color.FromArgb(38, 38, 42);
    public static readonly Color Panel = Color.FromArgb(36, 38, 40);
    public static readonly Color Inner = Color.FromArgb(25, 25, 28);
    public static readonly Color Control = Color.FromArgb(53, 53, 58);
    public static readonly Color ControlHover = Color.FromArgb(66, 66, 72);
    public static readonly Color ControlPressed = Color.FromArgb(77, 77, 84);
    public static readonly Color Border = Color.FromArgb(75, 75, 82);
    public static readonly Color Text = Color.FromArgb(235, 238, 240);
    public static readonly Color Muted = Color.FromArgb(168, 168, 176);
    public static readonly Color Accent = Color.FromArgb(226, 180, 85);
    public static readonly Color AccentHover = Color.FromArgb(241, 198, 103);
    public static readonly Color AccentPressed = Color.FromArgb(188, 145, 63);
    public static readonly Color AccentLight = Color.FromArgb(235, 211, 157);
    public static readonly Color Success = Color.FromArgb(111, 211, 151);
    public static readonly Color Warning = Color.FromArgb(241, 183, 91);
    public static readonly Color Error = Color.FromArgb(242, 116, 124);

    public static readonly Color WarningControl = Color.FromArgb(92, 68, 35);
    public static readonly Color WarningControlHover = Color.FromArgb(112, 82, 41);
    public static readonly Color WarningControlPressed = Color.FromArgb(128, 92, 46);
    public static readonly Color WarningBorder = Color.FromArgb(164, 119, 59);

    public static readonly Color TrimControl = Color.FromArgb(47, 47, 52);
    public static readonly Color TrimControlHover = Color.FromArgb(61, 61, 67);
    public static readonly Color TrimControlPressed = Color.FromArgb(72, 72, 79);
    public static readonly Color TrimBorder = Color.FromArgb(82, 82, 90);

    public static readonly Color ResetControl = Color.FromArgb(86, 55, 43);
    public static readonly Color ResetControlHover = Color.FromArgb(108, 67, 50);
    public static readonly Color ResetControlPressed = Color.FromArgb(124, 75, 56);
    public static readonly Color ResetBorder = Color.FromArgb(158, 100, 72);

    public static readonly Color RudderControl = Color.FromArgb(40, 63, 61);
    public static readonly Color RudderControlHover = Color.FromArgb(49, 79, 76);
    public static readonly Color RudderControlPressed = Color.FromArgb(58, 92, 88);
    public static readonly Color RudderBorder = Color.FromArgb(77, 123, 117);
    public static readonly Color RudderAccent = Color.FromArgb(117, 190, 181);

    public static readonly Color BeerControl = Color.FromArgb(94, 67, 30);
    public static readonly Color BeerControlHover = Color.FromArgb(119, 83, 35);
    public static readonly Color BeerControlPressed = Color.FromArgb(136, 94, 40);
    public static readonly Color BeerBorder = Color.FromArgb(183, 129, 52);
    public static readonly Color BeerText = Color.FromArgb(255, 232, 184);
}

internal sealed class DarkTabControl : TabControl
{
    public DarkTabControl()
    {
        DrawMode = TabDrawMode.OwnerDrawFixed;
        SizeMode = TabSizeMode.Fixed;
        ItemSize = new Size(190, 34);
        Padding = new Point(12, 6);

        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
    }

    protected override void OnDrawItem(
        DrawItemEventArgs eventArgs)
    {
        Rectangle bounds = GetTabRect(eventArgs.Index);
        bool selected = SelectedIndex == eventArgs.Index;

        using var backgroundBrush =
            new SolidBrush(
                selected
                    ? Theme.Panel
                    : Theme.Header);

        using var textBrush =
            new SolidBrush(
                selected
                    ? Theme.Text
                    : Theme.Muted);

        eventArgs.Graphics.FillRectangle(
            backgroundBrush,
            bounds);

        string tabText = TabPages[eventArgs.Index].Text;
        float drawSize = Font.Size;
        int textBudget = Math.Max(20, bounds.Width - 12);
        while (drawSize > 7.5F)
        {
            using Font test = new(Font.FontFamily, drawSize, Font.Style, Font.Unit);
            if (TextRenderer.MeasureText(eventArgs.Graphics, tabText, test, Size.Empty, TextFormatFlags.NoPadding).Width <= textBudget) break;
            drawSize -= 0.5F;
        }
        using Font drawFont = new(Font.FontFamily, drawSize, Font.Style, Font.Unit);
        TextRenderer.DrawText(
            eventArgs.Graphics,
            tabText,
            drawFont,
            bounds,
            textBrush.Color,
            TextFormatFlags.HorizontalCenter |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.SingleLine);

        if (selected)
        {
            using var accentPen =
                new Pen(Theme.Accent, 3F);

            eventArgs.Graphics.DrawLine(
                accentPen,
                bounds.Left + 2,
                bounds.Bottom - 2,
                bounds.Right - 2,
                bounds.Bottom - 2);
        }
    }
}

internal sealed class RepeatSpeedSlider : Control
{
    private int _minimum = 1;
    private int _maximum = 10;
    private int _value = 5;
    private bool _dragging;

    public RepeatSpeedSlider()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable,
            true);

        TabStop = true;
        MinimumSize = new Size(64, 24);
        Cursor = Cursors.Hand;
    }

    public event EventHandler? ValueChanged;

    [DefaultValue(1)]
    public int Minimum
    {
        get => _minimum;
        set
        {
            _minimum = value;

            if (_maximum <= _minimum)
            {
                _maximum = _minimum + 1;
            }

            Value = _value;
            Invalidate();
        }
    }

    [DefaultValue(10)]
    public int Maximum
    {
        get => _maximum;
        set
        {
            _maximum = Math.Max(value, _minimum + 1);
            Value = _value;
            Invalidate();
        }
    }

    [DefaultValue(5)]
    public int Value
    {
        get => _value;
        set
        {
            int clamped = Math.Clamp(value, _minimum, _maximum);

            if (_value == clamped)
            {
                return;
            }

            _value = clamped;
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnMouseDown(MouseEventArgs eventArgs)
    {
        base.OnMouseDown(eventArgs);

        if (eventArgs.Button != MouseButtons.Left)
        {
            return;
        }

        Focus();
        _dragging = true;
        Capture = true;
        SetValueFromX(eventArgs.X);
    }

    protected override void OnMouseMove(MouseEventArgs eventArgs)
    {
        base.OnMouseMove(eventArgs);

        if (_dragging)
        {
            SetValueFromX(eventArgs.X);
        }
    }

    protected override void OnMouseUp(MouseEventArgs eventArgs)
    {
        base.OnMouseUp(eventArgs);
        _dragging = false;
        Capture = false;
    }

    protected override void OnKeyDown(KeyEventArgs eventArgs)
    {
        base.OnKeyDown(eventArgs);

        switch (eventArgs.KeyCode)
        {
            case Keys.Left:
            case Keys.Down:
                Value--;
                eventArgs.Handled = true;
                break;

            case Keys.Right:
            case Keys.Up:
                Value++;
                eventArgs.Handled = true;
                break;

            case Keys.Home:
                Value = Minimum;
                eventArgs.Handled = true;
                break;

            case Keys.End:
                Value = Maximum;
                eventArgs.Handled = true;
                break;
        }
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        base.OnPaint(eventArgs);

        eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        eventArgs.Graphics.Clear(BackColor);

        int thumbDiameter = Math.Clamp((int)Math.Round(Height * 0.48), 12, 24);
        int trackThickness = Math.Clamp((int)Math.Round(Height * 0.18), 4, 9);
        int horizontalPadding = Math.Max(thumbDiameter / 2 + 2, Math.Clamp(Width / 45, 8, 18));
        int trackY = Height / 2;
        int trackWidth = Math.Max(1, Width - horizontalPadding * 2);

        var trackRectangle = new Rectangle(
            horizontalPadding,
            trackY - trackThickness / 2,
            trackWidth,
            trackThickness);

        using var trackBrush = new SolidBrush(Theme.Control);
        eventArgs.Graphics.FillRectangle(trackBrush, trackRectangle);

        double fraction =
            (Value - Minimum) /
            (double)(Maximum - Minimum);

        int thumbX =
            horizontalPadding +
            (int)Math.Round(fraction * trackWidth);

        if (thumbX > horizontalPadding)
        {
            using var fillBrush = new SolidBrush(Theme.Accent);
            eventArgs.Graphics.FillRectangle(
                fillBrush,
                new Rectangle(
                    horizontalPadding,
                    trackY - trackThickness / 2,
                    thumbX - horizontalPadding,
                    trackThickness));
        }

        using var thumbBrush = new SolidBrush(Theme.AccentLight);
        using var thumbBorder = new Pen(Theme.Text, 1F);

        eventArgs.Graphics.FillEllipse(
            thumbBrush,
            thumbX - thumbDiameter / 2,
            trackY - thumbDiameter / 2,
            thumbDiameter,
            thumbDiameter);

        eventArgs.Graphics.DrawEllipse(
            thumbBorder,
            thumbX - thumbDiameter / 2,
            trackY - thumbDiameter / 2,
            thumbDiameter,
            thumbDiameter);

        if (Focused)
        {
            using var focusPen = new Pen(Theme.Accent, 1F)
            {
                DashStyle = DashStyle.Dot
            };

            eventArgs.Graphics.DrawRectangle(
                focusPen,
                1,
                1,
                Math.Max(0, Width - 3),
                Math.Max(0, Height - 3));
        }
    }

    private void SetValueFromX(int x)
    {
        int thumbDiameter = Math.Clamp((int)Math.Round(Height * 0.48), 12, 24);
        int horizontalPadding = Math.Max(thumbDiameter / 2 + 2, Math.Clamp(Width / 45, 8, 18));
        int trackWidth = Math.Max(1, Width - horizontalPadding * 2);
        double fraction = Math.Clamp(
            (x - horizontalPadding) / (double)trackWidth,
            0.0,
            1.0);

        Value =
            Minimum +
            (int)Math.Round(
                fraction *
                (Maximum - Minimum));
    }
}

internal sealed class StepEditor : UserControl
{
    private readonly Button _decreaseButton;
    private readonly Button _increaseButton;
    private readonly TextBox _valueBox;

    private readonly decimal _minimum;
    private readonly decimal _maximum;
    private readonly decimal _increment;

    private decimal _value;

    public StepEditor(
        decimal initialValue,
        decimal minimum,
        decimal maximum,
        decimal increment)
    {
        _minimum = minimum;
        _maximum = maximum;
        _increment = increment;
        _value = Math.Clamp(
            initialValue,
            minimum,
            maximum);

        BackColor = Theme.Control;
        MinimumSize = new Size(82, 32);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Theme.Control
        };

        layout.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Absolute,
                36F));

        layout.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Percent,
                100F));

        layout.ColumnStyles.Add(
            new ColumnStyle(
                SizeType.Absolute,
                36F));

        layout.RowStyles.Add(
            new RowStyle(
                SizeType.Percent,
                100F));

        _decreaseButton =
            CreateEditorButton("-");

        _increaseButton =
            CreateEditorButton("+");

        _valueBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(1, 5, 1, 5),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(248, 248, 248),
            ForeColor = Color.Black,
            Font = new Font(
                "Segoe UI Semibold",
                10F),
            TextAlign = HorizontalAlignment.Center
        };

        _decreaseButton.Click +=
            (_, _) => ChangeValue(-_increment);

        _increaseButton.Click +=
            (_, _) => ChangeValue(_increment);

        _valueBox.Leave +=
            (_, _) => CommitTypedValue();

        _valueBox.KeyDown +=
            (_, eventArgs) =>
            {
                if (eventArgs.KeyCode != Keys.Enter)
                {
                    return;
                }

                CommitTypedValue();
                eventArgs.SuppressKeyPress = true;
            };

        layout.Controls.Add(
            _decreaseButton,
            0,
            0);

        layout.Controls.Add(
            _valueBox,
            1,
            0);

        layout.Controls.Add(
            _increaseButton,
            2,
            0);

        Controls.Add(layout);
        UpdateText();
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(
        DesignerSerializationVisibility.Hidden)]
    internal decimal Value
    {
        get
        {
            CommitTypedValue();
            return _value;
        }
        set
        {
            _value = Math.Clamp(
                value,
                _minimum,
                _maximum);

            UpdateText();
        }
    }

    private static Button CreateEditorButton(
        string text)
    {
        var button = new Button
        {
            Text = text,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.Control,
            ForeColor = Theme.Text,
            Font = new Font(
                "Segoe UI Semibold",
                13F),
            Cursor = Cursors.Hand,
            TabStop = false
        };

        button.FlatAppearance.BorderColor =
            Theme.Border;

        button.FlatAppearance.MouseOverBackColor =
            Theme.ControlHover;

        button.FlatAppearance.MouseDownBackColor =
            Theme.ControlPressed;

        return button;
    }

    private void ChangeValue(decimal amount)
    {
        _value = Math.Clamp(
            _value + amount,
            _minimum,
            _maximum);

        UpdateText();
    }

    private void CommitTypedValue()
    {
        string typedText = _valueBox.Text.Trim();

        bool parsed =
            decimal.TryParse(
                typedText,
                NumberStyles.Number,
                CultureInfo.CurrentCulture,
                out decimal parsedValue) ||
            decimal.TryParse(
                typedText.Replace(',', '.'),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out parsedValue);

        if (parsed)
        {
            _value = Math.Clamp(
                parsedValue,
                _minimum,
                _maximum);
        }

        UpdateText();
    }

    private void UpdateText()
    {
        string formatted =
            _value.ToString(
                "0.00",
                CultureInfo.CurrentCulture);

        if (_valueBox.Text == formatted)
        {
            return;
        }

        _valueBox.Text = formatted;
        _valueBox.SelectionStart =
            _valueBox.Text.Length;
    }
}

internal sealed class TrimBar : Control
{
    private double _value;

    public TrimBar()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw,
            true);

        BackColor = Theme.Inner;
    }

    [DefaultValue(0d)]
    public double Value
    {
        get => _value;
        set
        {
            _value = Math.Clamp(
                value,
                -100,
                100);

            Invalidate();
        }
    }

    protected override void OnPaint(
        PaintEventArgs eventArgs)
    {
        base.OnPaint(eventArgs);

        eventArgs.Graphics.SmoothingMode =
            SmoothingMode.AntiAlias;

        eventArgs.Graphics.Clear(BackColor);

        int markerDiameter = Math.Clamp((int)Math.Round(Height * 0.34), 10, 20);
        // Keep the marker fully inside its control even when the bar is narrow or
        // the row becomes taller at high DPI. This avoids the half-clipped end
        // markers visible in the earlier VTrim layout.
        int padding = Math.Max(markerDiameter / 2 + 2, Math.Clamp(Width / 35, 8, 18));
        int trackHeight = Math.Clamp((int)Math.Round(Height * 0.18), 5, 12);

        int centerY =
            Height / 2;

        int left =
            padding;

        int right =
            Math.Max(
                left + 1,
                Width - padding);

        // IMPORTANT:
        // Both the zero line and the dot now use the exact same conversion.
        // Previously the line used Width / 2 while the dot used the track
        // width, which could differ by one pixel because of rounding.
        int centerX =
            ValueToX(
                left,
                right,
                0.0);

        var track = new Rectangle(
            left,
            centerY - trackHeight / 2,
            Math.Max(
                1,
                right - left),
            trackHeight);

        using var trackBrush =
            new SolidBrush(
                Color.FromArgb(
                    67,
                    78,
                    96));

        eventArgs.Graphics.FillRectangle(
            trackBrush,
            track);

        using var centerPen =
            new Pen(
                Theme.Muted,
                2F);

        eventArgs.Graphics.DrawLine(
            centerPen,
            centerX,
            3,
            centerX,
            Height - 3);

        int markerX =
            Math.Abs(_value) < 0.000001
                ? centerX
                : ValueToX(
                    left,
                    right,
                    _value);

        int fillLeft =
            Math.Min(
                centerX,
                markerX);

        int fillWidth =
            Math.Abs(
                markerX -
                centerX);

        if (fillWidth > 0)
        {
            using var fillBrush =
                new SolidBrush(
                    Theme.Accent);

            eventArgs.Graphics.FillRectangle(
                fillBrush,
                new Rectangle(
                    fillLeft,
                    centerY - trackHeight / 2,
                    fillWidth,
                    trackHeight));
        }

        using var markerBrush =
            new SolidBrush(
                Theme.AccentLight);

        eventArgs.Graphics.FillEllipse(
            markerBrush,
            markerX - markerDiameter / 2,
            centerY - markerDiameter / 2,
            markerDiameter,
            markerDiameter);
    }

    private static int ValueToX(
        int left,
        int right,
        double value)
    {
        double normalized =
            (Math.Clamp(
                value,
                -100.0,
                100.0) +
             100.0) /
            200.0;

        return left +
               (int)Math.Round(
                   normalized *
                   (right - left),
                   MidpointRounding.AwayFromZero);
    }
}

internal sealed class StickAxisPreview : Control
{
    private bool _showInstructor;
    private double _manualRoll, _manualPitch;
    internal void SetInstructor(bool enabled, double roll, double pitch)
    {
        _showInstructor = enabled; _manualRoll = roll; _manualPitch = pitch;
        Invalidate();
    }
    private double _physicalRoll;
    private double _physicalPitch;
    private double _outputRoll;
    private double _outputPitch;

    public StickAxisPreview()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw,
            true);

        BackColor = Color.FromArgb(19, 23, 29);
    }

    internal void SetValues(
        double physicalRoll,
        double physicalPitch,
        double outputRoll,
        double outputPitch)
    {
        _physicalRoll =
            Math.Clamp(
                physicalRoll,
                -1.0,
                1.0);

        _physicalPitch =
            Math.Clamp(
                physicalPitch,
                -1.0,
                1.0);

        _outputRoll =
            Math.Clamp(
                outputRoll,
                -1.0,
                1.0);

        _outputPitch =
            Math.Clamp(
                outputPitch,
                -1.0,
                1.0);

        Invalidate();
    }

    protected override void OnPaint(
        PaintEventArgs eventArgs)
    {
        base.OnPaint(eventArgs);

        eventArgs.Graphics.SmoothingMode =
            SmoothingMode.AntiAlias;

        eventArgs.Graphics.Clear(BackColor);

        var bounds = new Rectangle(
            12,
            8,
            Math.Max(20, Width - 24),
            Math.Max(20, Height - 16));

        using var borderPen =
            new Pen(Theme.Border, 1F);

        using var gridPen =
            new Pen(
                Color.FromArgb(
                    52,
                    61,
                    75),
                1F);

        using var centerPen =
            new Pen(
                Theme.Muted,
                1.4F);

        eventArgs.Graphics.DrawRectangle(
            borderPen,
            bounds);

        int centerX =
            bounds.Left +
            bounds.Width / 2;

        int centerY =
            bounds.Top +
            bounds.Height / 2;

        for (int division = 1; division < 4; division++)
        {
            int x =
                bounds.Left +
                bounds.Width *
                division / 4;

            int y =
                bounds.Top +
                bounds.Height *
                division / 4;

            eventArgs.Graphics.DrawLine(
                gridPen,
                x,
                bounds.Top,
                x,
                bounds.Bottom);

            eventArgs.Graphics.DrawLine(
                gridPen,
                bounds.Left,
                y,
                bounds.Right,
                y);
        }

        eventArgs.Graphics.DrawLine(
            centerPen,
            centerX,
            bounds.Top,
            centerX,
            bounds.Bottom);

        eventArgs.Graphics.DrawLine(
            centerPen,
            bounds.Left,
            centerY,
            bounds.Right,
            centerY);

        Point physicalPoint = ToPoint(
            bounds,
            _physicalRoll,
            _physicalPitch);

        Point outputPoint = ToPoint(
            bounds,
            _outputRoll,
            _outputPitch);
        if (_showInstructor)
        {
            Point manual = ToPoint(bounds, _manualRoll, _manualPitch);
            using var correctionPen = new Pen(Color.LightGreen, 3F);
            eventArgs.Graphics.DrawLine(correctionPen, manual, outputPoint);
            eventArgs.Graphics.DrawRectangle(correctionPen, manual.X - 5, manual.Y - 5, 10, 10);
            eventArgs.Graphics.DrawEllipse(correctionPen, outputPoint.X - 14, outputPoint.Y - 14, 28, 28);
        }

        using var physicalBrush =
            new SolidBrush(
                Theme.Text);

        using var outputBrush =
            new SolidBrush(
                Theme.Accent);

        using var outputRing =
            new Pen(
                Theme.AccentLight,
                2F);

        eventArgs.Graphics.FillEllipse(
            physicalBrush,
            physicalPoint.X - 4,
            physicalPoint.Y - 4,
            8,
            8);

        eventArgs.Graphics.FillEllipse(
            outputBrush,
            outputPoint.X - 7,
            outputPoint.Y - 7,
            14,
            14);

        eventArgs.Graphics.DrawEllipse(
            outputRing,
            outputPoint.X - 10,
            outputPoint.Y - 10,
            20,
            20);
    }

    private static Point ToPoint(
        Rectangle bounds,
        double roll,
        double pitch)
    {
        int x =
            bounds.Left +
            bounds.Width / 2 +
            (int)Math.Round(
                Math.Clamp(
                    roll,
                    -1.0,
                    1.0) *
                bounds.Width / 2);

        int y =
            bounds.Top +
            bounds.Height / 2 -
            (int)Math.Round(
                Math.Clamp(
                    pitch,
                    -1.0,
                    1.0) *
                bounds.Height / 2);

        return new Point(
            Math.Clamp(
                x,
                bounds.Left,
                bounds.Right),
            Math.Clamp(
                y,
                bounds.Top,
                bounds.Bottom));
    }
}

internal sealed class RudderAxisPreview : Control
{
    private double _physicalRudder;
    private double _outputRudder;

    public RudderAxisPreview()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw,
            true);

        BackColor = Color.FromArgb(19, 23, 29);
    }

    internal void SetValues(
        double physicalRudder,
        double outputRudder)
    {
        _physicalRudder =
            Math.Clamp(
                physicalRudder,
                -1.0,
                1.0);

        _outputRudder =
            Math.Clamp(
                outputRudder,
                -1.0,
                1.0);

        Invalidate();
    }

    protected override void OnPaint(
        PaintEventArgs eventArgs)
    {
        base.OnPaint(eventArgs);

        eventArgs.Graphics.SmoothingMode =
            SmoothingMode.AntiAlias;

        eventArgs.Graphics.Clear(BackColor);

        int left = 18;
        int right =
            Math.Max(
                left + 20,
                Width - 18);

        // Use the same value-to-pixel conversion as the markers. Integer
        // midpoint rounding could otherwise place the zero line one pixel
        // away from a marker whose value is exactly 0.0.
        int centerX =
            ValueToX(
                left,
                right,
                0.0);

        int trackY =
            Height / 2;

        using var trackPen =
            new Pen(
                Color.FromArgb(
                    75,
                    88,
                    108),
                8F);

        using var centerPen =
            new Pen(
                Theme.Muted,
                2F);

        eventArgs.Graphics.DrawLine(
            trackPen,
            left,
            trackY,
            right,
            trackY);

        eventArgs.Graphics.DrawLine(
            centerPen,
            centerX,
            trackY - 20,
            centerX,
            trackY + 20);

        int physicalX = ValueToX(
            left,
            right,
            _physicalRudder);

        int outputX = ValueToX(
            left,
            right,
            _outputRudder);

        using var physicalBrush =
            new SolidBrush(
                Theme.Text);

        using var physicalRing =
            new Pen(
                Color.FromArgb(
                    25,
                    25,
                    28),
                2F);

        using var outputBrush =
            new SolidBrush(
                Theme.Accent);

        using var outputRing =
            new Pen(
                Theme.AccentLight,
                2F);

        // Draw output first and the physical marker last. When both values
        // are centered, the small white physical dot remains visibly aligned
        // with the exact zero line instead of being hidden under the blue dot.
        eventArgs.Graphics.FillEllipse(
            outputBrush,
            outputX - 7,
            trackY - 7,
            14,
            14);

        eventArgs.Graphics.DrawEllipse(
            outputRing,
            outputX - 10,
            trackY - 10,
            20,
            20);

        eventArgs.Graphics.FillEllipse(
            physicalBrush,
            physicalX - 4,
            trackY - 4,
            8,
            8);

        eventArgs.Graphics.DrawEllipse(
            physicalRing,
            physicalX - 5,
            trackY - 5,
            10,
            10);

        using var labelFont =
            new Font(
                "Segoe UI Semibold",
                8F);

        using var labelBrush =
            new SolidBrush(
                Theme.Muted);

        eventArgs.Graphics.DrawString(
            "LEFT",
            labelFont,
            labelBrush,
            left,
            Math.Max(0, Math.Min(trackY + 18, Height - labelFont.GetHeight(eventArgs.Graphics) - 2)));

        SizeF rightTextSize =
            eventArgs.Graphics.MeasureString(
                "RIGHT",
                labelFont);

        eventArgs.Graphics.DrawString(
            "RIGHT",
            labelFont,
            labelBrush,
            right - rightTextSize.Width,
            Math.Max(0, Math.Min(trackY + 18, Height - labelFont.GetHeight(eventArgs.Graphics) - 2)));
    }

    private static int ValueToX(
        int left,
        int right,
        double value)
    {
        double normalized =
            (Math.Clamp(
                value,
                -1.0,
                1.0) +
             1.0) /
            2.0;

        return left +
               (int)Math.Round(
                   normalized *
                   (right - left));
    }
}


