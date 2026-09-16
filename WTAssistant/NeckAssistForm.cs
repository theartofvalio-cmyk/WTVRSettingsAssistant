using Microsoft.Win32;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WTVRSettingsAssistant;

internal sealed class AccentTrackBar : TrackBar
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color Accent { get; set; } = ThemePalette.FromArgb(255, 190, 70);
    public AccentTrackBar()
    {
        // Native TrackBar AutoSize overrides SetBounds at higher DPI and covers
        // adjacent labels, although our custom-painted rail looks much shorter.
        AutoSize = false;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        ValueChanged += (_, _) => Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? BackColor);
        int left = 12, right = Math.Max(13, Width - 13), y = Height / 2;
        int x = left + (int)((Value - Minimum) / (float)Math.Max(1, Maximum - Minimum) * (right - left));
        using Pen rail = new(ThemePalette.FromArgb(70, 85, 88), 5);
        using Pen filled = new(Accent, 5);
        using Brush thumb = new SolidBrush(Accent);
        e.Graphics.DrawLine(rail, left, y, right, y);
        e.Graphics.DrawLine(filled, left, y, x, y);
        e.Graphics.FillEllipse(thumb, x - 8, y - 9, 16, 18);
        if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, ClientRectangle);
    }
    private void SetFromMouse(int x) => Value = Math.Clamp(Minimum + (int)Math.Round((x - 12) / (double)Math.Max(1, Width - 25) * (Maximum - Minimum)), Minimum, Maximum);
    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { Focus(); Capture = true; SetFromMouse(e.X); } }
    protected override void OnMouseMove(MouseEventArgs e) { if (Capture && e.Button == MouseButtons.Left) SetFromMouse(e.X); }
    protected override void OnMouseUp(MouseEventArgs e) { Capture = false; }
}

internal sealed class NeckCurvePoint
{
    public float Input { get; set; }
    public float Output { get; set; }
}

internal static class NeckCurveMapping
{
    public static float Evaluate(float physical, int start, int maximum, List<NeckCurvePoint> points, int curvature = 100, int limit = 110)
    {
        float angle = Math.Abs(physical);
        if (angle <= start) return physical;
        float t = Math.Clamp((angle - start) / Math.Max(1, limit - start), 0, 1);
        float x0 = 0, y0 = 0, output = 1;
        foreach (NeckCurvePoint point in points.OrderBy(p => p.Input).Append(new NeckCurvePoint { Input = 1, Output = 1 }))
        {
            if (t <= point.Input)
            {
                float blend = (t - x0) / Math.Max(0.001f, point.Input - x0);
                float smooth = blend * blend * (3 - 2 * blend);
                blend += (smooth - blend) * Math.Clamp(curvature / 100f, 0, 1);
                output = y0 + (point.Output - y0) * blend;
                break;
            }
            x0 = point.Input; y0 = point.Output;
        }
        return Math.Sign(physical) * (angle + Math.Max(0, maximum - limit) * output);
    }
}

internal sealed class NeckAssistSettings
{
    public bool Enabled { get; set; }
    public int StartAngle { get; set; } = 21;
    public int MaximumViewAngle { get; set; } = 180;
    public int Smoothing { get; set; } = 35;
    public int YawCurvature { get; set; } = 98;
    public int PitchCurvature { get; set; } = 98;
    public int ReturnAngle { get; set; } = 12;
    public int TransitionWidth { get; set; } = 15;
    public bool PositionCompensation { get; set; } = true;
    public string Curve { get; set; } = "Linear";
    public string RecenterBinding { get; set; } = "Not assigned";
    public string ActivationBinding { get; set; } = "Not assigned";
    public string SimpleActivationBinding { get; set; } = "Not assigned";
    public string ActivationMode { get; set; } = "Hold";
    public bool PitchEnabled { get; set; }
    public int PitchStartAngle { get; set; } = 15;
    public int PitchReturnAngle { get; set; } = 9;
    public int PitchMaximumViewAngle { get; set; } = 115;
    public int PitchTransitionWidth { get; set; } = 10;
    public int PitchSmoothing { get; set; } = 45;
    public List<NeckCurvePoint> YawPoints { get; set; } = new();
    public BezierNeckCurve YawBezier { get; set; } = new();
    public BezierNeckCurve PitchBezier { get; set; } = new();
    public bool LinkAxes { get; set; } = true;
    public bool NaturalRearView { get; set; } = true;
    public int YawNaturalResumeAngle { get; set; } = 28;
    public int PitchNaturalResumeAngle { get; set; } = 20;
    public string MovementMode { get; set; } = "Simple";
    public int PressRotationAngle { get; set; } = 160;
    public int TransitionSpeed { get; set; } = 55;
    public string AdvancedActivationBehavior { get; set; } = "Toggle";
    public int SimpleDeadzoneAngle { get; set; } = 8;
}

internal sealed class NeckAssistForm : Form
{
    public event EventHandler? CloseRequested;
    public event EventHandler? AssistanceStateChanged;
    public bool AssistanceEnabled => _settings.Enabled;
    public void ToggleAssistance() => _enabled.Checked = !_enabled.Checked;
    private Panel? _navigation;
    public void ConfigureNavigation(Image home, Image info, Action showInfo)
    {
        // v2.0 uses the persistent application sidebar exclusively.
        // Legacy Home/Info overlays are intentionally removed.
    }
    private readonly string _settingsPath;
    private readonly NeckAssistSettings _settings;
    private readonly OpenXrNeckBackend _openXrBackend;
    private readonly CheckBox _enabled;
    private readonly Label _runtimeStatus;
    private readonly Label _backendStatus;
    private readonly TrackBar _startAngle;
    private readonly TrackBar _maximumAngle;
    private readonly TrackBar _smoothing;
    private readonly TrackBar _returnAngle;
    private readonly ComboBox _curve;
    private readonly CheckBox _positionCompensation;
    private readonly Label _startValue;
    private readonly Label _maximumValue;
    private readonly Label _smoothingValue;
    private readonly Label _returnValue;
    private readonly Label _transitionValue;
    private readonly NeckCurvePreview _curvePreview;
    private NeckCurvePreview? _pitchPreview;
    private readonly Button _recenterBind;
    private readonly Button _activationBind;
    private readonly Button _simpleActivationBind;
    private readonly ComboBox _activationMode;
    private readonly System.Windows.Forms.Timer _statusTimer = new() { Interval = 1500 };
    private bool _lastActivationPressed;
    private bool _lastRecenterPressed;
    private bool _toggleActive;
    private bool _runtimeAssistanceActive;
    private int _statusTicks;
    private bool _capturingBinding;
    private readonly Dictionary<Control, float> _themeBaseFontSizes = new();
    private bool _applyingThemeFontScale;
    private string _languageCode = "en";
    private static readonly IReadOnlyDictionary<string, string> LocalizedTextKeys = new Dictionary<string, string>
    {
        ["Neck Assistant"] = "Nav.Neck",
        ["NECK ASSISTANT"] = "Nav.Neck",
        ["BACK"] = "Assistant.Back",
        ["Enable Neck Assistant"] = "Assistant.EnableNeck",
        ["Enable"] = "Assistant.Enable",
        ["Enable up/down"] = "Assistant.EnableUpDown",
        ["● ACTIVE"] = "Assistant.Active",
        ["○ OFF"] = "Assistant.Off",
        ["LEFT / RIGHT (YAW)"] = "Assistant.Yaw",
        ["UP / DOWN (PITCH)"] = "Assistant.Pitch",
        ["Compensate around seated head position"] = "Assistant.PositionComp",
        ["RECENTER COMBO"] = "Assistant.RecenterCombo",
        ["ADVANCED TOGGLE"] = "Assistant.AdvancedToggle",
        ["SIMPLE HOLD"] = "Assistant.SimpleHold",
        ["CLEAR"] = "Assistant.Clear",
        ["HOLD"] = "Assistant.Hold",
        ["TOGGLE"] = "Assistant.Toggle",
        ["ADVANCED"] = "Assistant.Advanced",
        ["SIMPLE"] = "Assistant.Simple",
        ["Link axes: ON"] = "Assistant.LinkOn",
        ["Link axes: OFF"] = "Assistant.LinkOff",
        ["Rear-view boost: ON"] = "Assistant.RearOn",
        ["Rear-view boost: OFF"] = "Assistant.RearOff",
        ["Gradually increases virtual yaw after the activation angle. Start conservatively—strong amplification can cause discomfort."] = "Neck.Warning",
        ["Use the ON / OFF switch or your toggle combo. Press the combo once to enable assistance, again to disable it."] = "Neck.ActivationHint",
        ["Drag yellow to activate, orange to release, and green to set maximum view. Sliders stay synchronized."] = "Neck.GraphHelp",
        ["Yellow: activate • Orange: release • Green: maximum view • Red: headset • transition controls the easing"] = "Neck.GraphLegend",
        ["REAR-VIEW BOOST: extra rotation is added early; after the boost, your head and view continue together at 1:1."] = "Neck.RearBoostHelp",
        ["STANDARD: extra rotation increases throughout the turn. Drag yellow/orange/green markers or use the matching sliders."] = "Neck.StandardHelp",
        ["Left/right: boost starts"] = "Neck.Slider.YawStart",
        ["Left/right: release"] = "Neck.Slider.YawRelease",
        ["Natural motion resumes"] = "Neck.Slider.NaturalResume",
        ["Left/right: maximum view"] = "Neck.Slider.YawMax",
        ["Boost softness"] = "Neck.Slider.BoostSoftness",
        ["Up/down: boost starts"] = "Neck.Slider.PitchStart",
        ["Up/down: release"] = "Neck.Slider.PitchRelease",
        ["Natural pitch resumes"] = "Neck.Slider.PitchResume",
        ["Up/down: maximum view"] = "Neck.Slider.PitchMax",
        ["Pitch boost softness"] = "Neck.Slider.PitchSoftness",
        ["SIMPLE MODE - HOLD THE BOUND INPUT TO LOOK BACK"] = "Neck.SimpleModeTitle",
        ["Use War Thunder's normal recenter. Hold your Simple binding and turn past the deadzone: the selected rear rotation is added smoothly. Release the binding to return smoothly to your natural view."] = "Neck.SimpleModeDescription",
        ["Camera rear rotation"] = "Neck.CameraRearRotation",
        ["Head-direction deadzone"] = "Neck.HeadDeadzone",
        ["Toggle: press once ON, again OFF. Recenter remains available."] = "Neck.BindingHelp.Toggle",
        ["Hold: assistance stays active only while the combo is held. Recenter remains available."] = "Neck.BindingHelp.Hold",
        ["CAMERA TRANSITION SPEED"] = "Neck.TransitionSpeed",
        ["Lower = slower and softer  •  Higher = faster response."] = "Neck.TransitionHelp",
        ["RESTORE DEFAULTS"] = "Neck.RestoreDefaults",
        ["SIMPLE MODE  •  Hold the assigned input and turn beyond the deadzone to look behind you."] = "Neck.PressOverview",
        ["Horizontal"] = "Neck.Card.Horizontal",
        ["Vertical"] = "Neck.Card.Vertical",
        ["Activation"] = "Neck.Card.Activation",
        ["Release"] = "Neck.Card.Release",
        ["Natural motion"] = "Neck.Card.NaturalMotion",
        ["Maximum view"] = "Neck.Card.MaximumView",
        ["Softness"] = "Neck.Card.Softness",
    };

    public NeckAssistForm(string appFolder) : this(appFolder, false) { }

    public NeckAssistForm(string appFolder, bool offlinePreview)
    {
        _settingsPath = Path.Combine(appFolder, "Settings", "neck_assist.json");
        _settings = LoadSettings();
        _settings.YawBezier ??= new();
        _settings.PitchBezier ??= new();
        _settings.YawBezier.Validate();
        _settings.PitchBezier.Validate();
        // Always start each application session in Simple mode. Users may switch to Advanced explicitly.
        _settings.MovementMode = "Simple";
        _settings.PressRotationAngle = Math.Clamp(_settings.PressRotationAngle, 30, 180);
        _settings.TransitionSpeed = Math.Clamp(_settings.TransitionSpeed, 1, 100);
        _settings.AdvancedActivationBehavior = _settings.AdvancedActivationBehavior == "Hold" ? "Hold" : "Toggle";
        _settings.SimpleDeadzoneAngle = Math.Clamp(_settings.SimpleDeadzoneAngle, 0, 45);
        _toggleActive = _settings.Enabled;
        _runtimeAssistanceActive = _settings.Enabled;
        _openXrBackend = new OpenXrNeckBackend(appFolder, offlinePreview);

        Text = "Neck Assistant";
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ClientSize = new Size(460, Math.Min(880, Screen.PrimaryScreen?.WorkingArea.Height - 90 ?? 800));
        AutoScroll = true;
        AutoScrollMinSize = new Size(0, 1510);
        BackColor = ThemePalette.FromArgb(5, 12, 14);
        ForeColor = ThemePalette.FromArgb(235, 238, 240);
        ShowInTaskbar = false;
        TopMost = false;

        Button closeButton = MakeButton("×");
        closeButton.Font = new Font("Segoe UI", 15, FontStyle.Bold);
        closeButton.SetBounds(414, 12, 34, 34);
        closeButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        closeButton.Click += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);
        Controls.Add(closeButton);

        Label title = MakeLabel("NECK ASSISTANT", 24, FontStyle.Bold);
        title.SetBounds(20, 14, 410, 58);
        Controls.Add(title);

        Label warning = MakeLabel("Gradually increases virtual yaw after the activation angle. Start conservatively—strong amplification can cause discomfort.", 10, FontStyle.Italic);
        warning.SetBounds(20, 74, 410, 68);
        Controls.Add(warning);

        _enabled = new ThemeCheckBox
        {
            Text = T("Assistant.EnableNeck"),
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            Checked = _settings.Enabled,
            Location = new Point(20, 148)
        };
        Label enabledIndicator = MakeLabel(_settings.Enabled ? "● ACTIVE" : "○ OFF", 11, FontStyle.Bold);
        enabledIndicator.ForeColor = _settings.Enabled ? ThemePalette.FromArgb(70, 235, 125) : ThemePalette.FromArgb(145, 155, 158);
        enabledIndicator.TextAlign = ContentAlignment.MiddleLeft;
        enabledIndicator.SetBounds(330, 144, 150, 34);
        void RefreshEnabledIndicator()
        {
            _enabled.Text = T("Assistant.EnableNeck");
            enabledIndicator.Text = _enabled.Checked ? T("Assistant.Active") : T("Assistant.Off");
            enabledIndicator.ForeColor = _enabled.Checked ? ThemePalette.FromArgb(70, 235, 125) : ThemePalette.FromArgb(145, 155, 158);
        }
        _enabled.CheckedChanged += (_, _) =>
        {
            _settings.Enabled = _enabled.Checked;
            _toggleActive = _enabled.Checked;
            _runtimeAssistanceActive = _enabled.Checked;
            SaveSettings();
            if (_settings.Enabled)
                _openXrBackend.SetHeld(false, _settings.PitchEnabled);
            else
                _openXrBackend.DisableLayer();
            RefreshEnabledIndicator();
            RefreshStatus();
            AssistanceStateChanged?.Invoke(this, EventArgs.Empty);
        };
        Controls.AddRange(new Control[] { _enabled, enabledIndicator });

        _runtimeStatus = MakeLabel("Runtime: detecting…", 10, FontStyle.Bold);
        _runtimeStatus.SetBounds(20, 190, 410, 32);
        Controls.Add(_runtimeStatus);

        _backendStatus = MakeLabel("Backend: not installed", 10, FontStyle.Regular);
        _backendStatus.ForeColor = ThemePalette.FromArgb(255, 196, 80);
        _backendStatus.SetBounds(20, 222, 410, 52);
        Controls.Add(_backendStatus);

        _curvePreview = new NeckCurvePreview
        {
            AxisTitleKey = "Neck.HorizontalAxis",
            LanguageCode = _languageCode,
            BackColor = ThemePalette.FromArgb(8, 18, 21),
            ForeColor = Color.White
        };
        _curvePreview.SetBounds(20, 280, 410, 150);
        Controls.Add(_curvePreview);

        AddDivider(444);
        int y = 460;
        Label yawHeading = MakeLabel("LEFT / RIGHT (YAW)", 12, FontStyle.Bold);
        yawHeading.ForeColor = ThemePalette.FromArgb(125, 225, 240);
        yawHeading.SetBounds(20, y, 300, 26);
        Controls.Add(yawHeading);
        y += 32;
        (_startAngle, _startValue) = AddSlider("Yaw activation angle", 10, 100, _settings.StartAngle, y, "°");
        y += 94;
        (_maximumAngle, _maximumValue) = AddSlider("Maximum virtual yaw", 90, 220, _settings.MaximumViewAngle, y, "°");
        y += 94;
        (TrackBar transition, _transitionValue) = AddSlider("Yaw transition width", 0, 45, _settings.TransitionWidth, y, "°");
        y += 94;
        (_smoothing, _smoothingValue) = AddSlider("Yaw transition curve", 0, 100, _settings.YawCurvature, y, "%");
        y += 94;
        (_returnAngle, _returnValue) = AddSlider("Return threshold", 0, 90, _settings.ReturnAngle, y, "°");
        y += 94;
        (TrackBar yawResume, Label yawResumeValue) = AddSlider("Natural movement resumes", 15, 110, Math.Clamp(_settings.YawNaturalResumeAngle, 15, 110), y, "°");
        y += 94;

        Label curveLabel = MakeLabel("Amplification curve", 10, FontStyle.Bold);
        curveLabel.SetBounds(20, y, 220, 34);
        Controls.Add(curveLabel);
        _curve = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 10),
            BackColor = ThemePalette.FromArgb(16, 29, 33),
            ForeColor = Color.White
        };
        _curve.Items.AddRange(new object[] { "Linear", "Smoothstep" });
        _curve.SelectedItem = _curve.Items.Contains(_settings.Curve) ? _settings.Curve : "Linear";
        _curve.SetBounds(265, y, 165, 34);
        _curve.SelectedIndexChanged += (_, _) => { _settings.Curve = _curve.SelectedItem?.ToString() ?? "Linear"; SaveSettings(); };
        Controls.Add(_curve);

        y += 52;
        AddDivider(y);
        Label pitchHeading = MakeLabel("UP / DOWN (PITCH)", 12, FontStyle.Bold);
        pitchHeading.ForeColor = ThemePalette.FromArgb(125, 225, 240);
        pitchHeading.SetBounds(20, y + 8, 280, 36);
        Controls.Add(pitchHeading);
        ThemeCheckBox pitchEnabled = new()
        {
            Text = T("Assistant.EnableUpDown"),
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            Checked = _settings.PitchEnabled,
            Location = new Point(345, y + 12)
        };
        void RefreshPitchEnabledText() => pitchEnabled.Text = T("Assistant.EnableUpDown");
        pitchEnabled.CheckedChanged += (_, _) => { _settings.PitchEnabled = pitchEnabled.Checked; RefreshPitchEnabledText(); SaveSettings(); };
        Controls.Add(pitchEnabled);
        y += 52;
        (TrackBar pitchStart, Label pitchStartValue) = AddSlider("Pitch activation angle", 5, 80, _settings.PitchStartAngle, y, "°");
        y += 94;
        (TrackBar pitchReturn, Label pitchReturnValue) = AddSlider("Pitch release angle", 0, 75, Math.Min(_settings.PitchReturnAngle, _settings.PitchStartAngle - 2), y, "°");
        y += 94;
        (TrackBar pitchResume, Label pitchResumeValue) = AddSlider("Natural pitch resumes", 10, 80, Math.Clamp(_settings.PitchNaturalResumeAngle, 10, 80), y, "°");
        y += 94;
        (TrackBar pitchMaximum, Label pitchMaximumValue) = AddSlider("Maximum virtual pitch", 45, 140, _settings.PitchMaximumViewAngle, y, "°");
        y += 94;
        (TrackBar pitchTransition, Label pitchTransitionValue) = AddSlider("Pitch transition width", 0, 35, _settings.PitchTransitionWidth, y, "°");
        y += 94;
        (TrackBar pitchSmoothing, Label pitchSmoothingValue) = AddSlider("Pitch transition curve", 0, 100, _settings.PitchCurvature, y, "%");
        y += 94;
        HookSlider(pitchStart, pitchStartValue, "°", value => _settings.PitchStartAngle = value);
        HookSlider(pitchReturn, pitchReturnValue, "°", value => _settings.PitchReturnAngle = value);
        HookSlider(pitchResume, pitchResumeValue, "°", value => _settings.PitchNaturalResumeAngle = value);
        HookSlider(pitchMaximum, pitchMaximumValue, "°", value => _settings.PitchMaximumViewAngle = value);
        HookSlider(pitchTransition, pitchTransitionValue, "°", value => _settings.PitchTransitionWidth = value);
        HookSlider(pitchSmoothing, pitchSmoothingValue, "%", value => _settings.PitchCurvature = value);

        _positionCompensation = new ThemeCheckBox
        {
            Text = "Compensate around seated head position",
            Font = new Font("Segoe UI", 10),
            ForeColor = Color.White,
            AutoSize = true,
            Checked = _settings.PositionCompensation,
            Location = new Point(20, y + 48)
        };
        _positionCompensation.CheckedChanged += (_, _) => { _settings.PositionCompensation = _positionCompensation.Checked; SaveSettings(); };
        Controls.Add(_positionCompensation);

        int bindingY = y + 88;
        AddDivider(bindingY);
        Label recenterLabel = MakeLabel("RECENTER COMBO", 10, FontStyle.Bold);
        recenterLabel.SetBounds(20, bindingY + 12, 180, 24);
        Controls.Add(recenterLabel);
        _recenterBind = MakeButton(FormatBinding(_settings.RecenterBinding));
        _recenterBind.SetBounds(265, bindingY + 7, 165, 34);
        _recenterBind.Click += (_, _) => CaptureBinding(0);
        Controls.Add(_recenterBind);

        Label recenterHint = MakeLabel("Set this exact same HOTAS combo as War Thunder's VR recenter command.", 8.5f, FontStyle.Italic);
        recenterHint.ForeColor = ThemePalette.FromArgb(255, 205, 110);
        recenterHint.SetBounds(20, bindingY + 44, 410, 38);
        Controls.Add(recenterHint);

        // Activation is controlled by the main ON/OFF switch. Keep these objects only
        // for settings-file compatibility with earlier test builds.
        _activationBind = MakeButton(FormatBinding(_settings.ActivationBinding));
        _simpleActivationBind = MakeButton(FormatBinding(_settings.SimpleActivationBinding));
        _activationMode = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 10),
            BackColor = ThemePalette.FromArgb(16, 29, 33),
            ForeColor = Color.White
        };
        _activationMode.Items.Add("Main ON / OFF switch");
        _activationMode.SelectedIndex = 0;

        Label activationHint = MakeLabel("Use the ON / OFF switch above to activate or completely pause assistance. Only the recenter HOTAS combo needs to be bound.", 9, FontStyle.Italic);
        activationHint.ForeColor = ThemePalette.FromArgb(125, 225, 240);
        activationHint.SetBounds(20, bindingY + 91, 410, 58);
        Controls.Add(activationHint);

        Label hint = MakeLabel("Example: 45° activation and 180° maximum maps the remaining physical turn progressively toward a rear view.", 9, FontStyle.Italic);
        hint.ForeColor = ThemePalette.FromArgb(180, 195, 200);
        hint.SetBounds(20, bindingY + 150, 410, 45);
        Controls.Add(hint);

        Button clearRecenter = MakeButton("CLEAR");
        clearRecenter.SetBounds(340, bindingY + 7, 90, 34);
        _recenterBind.SetBounds(190, bindingY + 7, 140, 34);
        clearRecenter.Click += (_, _) =>
        {
            _settings.RecenterBinding = "Not assigned";
            _recenterBind.Text = FormatBinding("Not assigned");
            _lastRecenterPressed = false;
            _capturingBinding = false;
            SaveSettings();
        };
        Controls.Add(clearRecenter);

        // Reuse the existing controls in a full-window, wrapping page.
        Control[] pageControls = Controls.Cast<Control>().ToArray();
        Controls.Clear();
        AutoScroll = false;
        AutoScrollMinSize = Size.Empty;
        Panel header = new() { Dock = DockStyle.Top, Height = 66, BackColor = BackColor };
        closeButton.Text = "BACK";
        closeButton.Font = new Font("Segoe UI", 11, FontStyle.Bold);
        closeButton.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        closeButton.SetBounds(20, 14, 90, 38);
        title.Text = "NECK ASSISTANT";
        title.Font = new Font("Segoe UI", 18, FontStyle.Bold);
        title.SetBounds(130, 12, 650, 44);
        header.Controls.AddRange(new Control[] { closeButton, title });
        FlowLayoutPanel content = new() { Dock = DockStyle.Fill, AutoScroll = true, WrapContents = true, Padding = new Padding(16), BackColor = BackColor };
        Panel overview = new() { Width = 650, Height = 780, Margin = new Padding(0, 0, 20, 20) };
        Panel adjustments = new() { Width = 450, Height = 1160, Margin = new Padding(0, 0, 20, 20) };
        foreach (Control control in pageControls)
        {
            if (control == closeButton || control == title) continue;
            if (control.Top < 444)
            {
                control.Top -= 65;
                overview.Controls.Add(control);
            }
            else if (control.Top >= bindingY)
            {
                control.Top = control.Top - bindingY + 545;
                overview.Controls.Add(control);
            }
            else
            {
                control.Top -= 444;
                adjustments.Controls.Add(control);
            }
        }
        warning.SetBounds(20, 8, 600, 55);
        _enabled.Top = 70;
        _runtimeStatus.SetBounds(20, 110, 600, 32);
        _backendStatus.SetBounds(20, 145, 600, 50);
        _curvePreview.SetBounds(20, 200, 600, 280);
        Label graphHelp = MakeLabel("Drag the amber handle to set when assistance starts. Drag the green handle to set the view angle reached at 110° of head turn. Left and right are mirrored.", 10, FontStyle.Regular);
        graphHelp.SetBounds(20, 482, 600, 60);
        overview.Controls.Add(graphHelp);
        _curvePreview.MappingChanged += (_, _) =>
        {
            int editedStart = _curvePreview.StartAngle;
            int editedReturn = _curvePreview.ReturnAngle;
            int editedResume = _curvePreview.NaturalResumeAngle;
            int editedMaximum = _curvePreview.MaximumViewAngle;
            _settings.YawBezier = _curvePreview.Bezier;
            _settings.YawCurvature = _curvePreview.Curvature;
            _settings.StartAngle = editedStart;
            _settings.ReturnAngle = editedReturn;
            _settings.YawNaturalResumeAngle = editedResume;
            _settings.MaximumViewAngle = editedMaximum;
            _smoothing.Value = _curvePreview.Curvature;
            _settings.YawPoints = _curvePreview.Points.Select(p => new NeckCurvePoint { Input = p.Input, Output = p.Output }).ToList();
            _startAngle.Value = Math.Clamp(editedStart, _startAngle.Minimum, _startAngle.Maximum);
            _returnAngle.Value = Math.Clamp(editedReturn, _returnAngle.Minimum, _returnAngle.Maximum);
            yawResume.Value = Math.Clamp(editedResume, yawResume.Minimum, yawResume.Maximum);
            _maximumAngle.Value = Math.Clamp(editedMaximum, _maximumAngle.Minimum, _maximumAngle.Maximum);
            SaveSettings();
        };
        content.Controls.AddRange(new Control[] { overview, adjustments });
        Controls.Add(content);
        Controls.Add(header);
        _navigation = header;
        header.Height = 80;
        closeButton.Visible = false;
        header.Height = 96;
        PictureBox headerIcon = new()
        {
            Image = AssetManager.LoadImage("IllustratedHead.png"),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent,
            Bounds = new Rectangle(14, 8, 78, 78)
        };
        title.Font = new Font("Segoe UI", 27, FontStyle.Bold, GraphicsUnit.Pixel);
        title.SetBounds(108, 8, 560, 42);
        Label runtimeSupport = MakeLabel("Works automatically with SteamVR, OpenXR and VDXR.", 12.5f, FontStyle.Regular);
        UiLanguage.Bind(runtimeSupport, "Neck.RuntimeSupport", _languageCode);
        runtimeSupport.ForeColor = IllustratedTheme.Muted;
        runtimeSupport.SetBounds(110, 49, 650, 32);
        header.Controls.AddRange(new Control[] { headerIcon, runtimeSupport });

        // Wide graph first; each slider occupies a card in wrapping horizontal rows.
        content.Controls.Clear();
        content.FlowDirection = FlowDirection.TopDown;
        content.WrapContents = false;
        overview.Height = 560;
        FlowLayoutPanel sliderRows = new() { AutoSize = true, WrapContents = true, Margin = new Padding(0, 0, 0, 16) };
        Control[] adjustmentControls = adjustments.Controls.Cast<Control>().ToArray();
        foreach (TrackBar slider in adjustmentControls.OfType<TrackBar>().OrderBy(c => c.Top))
        {
            int rowTop = slider.Top - 38;
            Panel card = new() { Width = 445, Height = 94, Margin = new Padding(0, 0, 12, 8) };
            foreach (Control label in adjustmentControls.Where(c => c is Label && c.Top == rowTop))
            {
                label.Top = 0;
                card.Controls.Add(label);
            }
            slider.Top = 38;
            card.Controls.Add(slider);
            sliderRows.Controls.Add(card);
        }
        Panel bindings = new() { Height = 310, Margin = new Padding(0, 0, 0, 12) };
        foreach (Control control in overview.Controls.Cast<Control>().Where(c => c.Top >= 545).ToArray())
        {
            control.Top -= 545;
            bindings.Controls.Add(control);
        }
        FlowLayoutPanel otherSettings = new() { AutoSize = true, WrapContents = true, Margin = new Padding(0, 0, 0, 12) };
        foreach (Control control in adjustments.Controls.Cast<Control>().Where(c => c is ComboBox || c is CheckBox).ToArray())
        {
            control.Margin = new Padding(12);
            otherSettings.Controls.Add(control);
        }
        content.Controls.AddRange(new Control[] { overview, sliderRows, otherSettings, bindings });
        activationHint.Text = T("Neck.ActivationHint");
        Label toggleLabel = MakeLabel("NECK ASSISTANT TOGGLE", 11, FontStyle.Bold);
        toggleLabel.SetBounds(20, 215, 220, 36);
        _activationBind.SetBounds(250, 215, 260, 38);
        _activationBind.Click += (_, _) => CaptureBinding(1);
        Button clearToggle = MakeButton("CLEAR");
        clearToggle.SetBounds(522, 215, 100, 38);
        clearToggle.Click += (_, _) => { _settings.ActivationBinding = "Not assigned"; _activationBind.Text = FormatBinding("Not assigned"); _lastActivationPressed = false; _toggleActive = false; _capturingBinding = false; SaveSettings(); };
        bindings.Controls.AddRange(new Control[] { toggleLabel, _activationBind, clearToggle });
        void FitPage()
        {
            int availableWidth = Math.Max(460, content.ClientSize.Width - 55);
            overview.Width = sliderRows.Width = otherSettings.Width = bindings.Width = availableWidth;
            sliderRows.MaximumSize = otherSettings.MaximumSize = new Size(availableWidth, 0);
            int columns = availableWidth >= 1000 ? 2 : 1;
            int cardWidth = (availableWidth - 12 * columns) / columns;
            foreach (Panel card in sliderRows.Controls.OfType<Panel>())
            {
                card.Width = cardWidth;
                foreach (TrackBar bar in card.Controls.OfType<TrackBar>()) bar.Width = cardWidth - 30;
                foreach (Label label in card.Controls.OfType<Label>())
                {
                    label.Font = new Font("Segoe UI", 11, FontStyle.Bold);
                    if (label.TextAlign == ContentAlignment.MiddleRight) label.Left = cardWidth - 115;
                    else label.Width = cardWidth - 135;
                }
            }
            _curvePreview.Width = Math.Max(410, availableWidth - 40);
            _curvePreview.Height = 310;
            graphHelp.SetBounds(20, 516, availableWidth - 40, 44);
        }
        content.SizeChanged += (_, _) => FitPage();
        FitPage();

        // Compact dashboard: graph, two rows of four controls, then bindings.
        content.Visible = false;
        TableLayoutPanel dashboard = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(16, 0, 16, 8), BackColor = BackColor };
        dashboard.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        dashboard.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        // Keep both live graphs fully visible at the normal window size.
        // The former fixed lower rows starved the vertical graph of height.
        dashboard.RowStyles.Add(new RowStyle(SizeType.Absolute, 220));
        dashboard.RowStyles.Add(new RowStyle(SizeType.Absolute, 140));
        overview.Dock = DockStyle.Fill;
        overview.Margin = Padding.Empty;
        warning.Visible = false;
        _enabled.AutoSize = false;
        _enabled.SetBounds(12, 0, 320, 34);
        enabledIndicator.SetBounds(338, 0, 120, 34);
        _runtimeStatus.Visible = false;
        // Keep the status line clear of the taller top-row buttons at high DPI.
        _backendStatus.SetBounds(12, 42, 1000, 30);
        graphHelp.Text = T("Neck.GraphHelp");
        overview.SizeChanged += (_, _) =>
        {
            _backendStatus.Width = Math.Max(200, overview.Width - 24);
            _curvePreview.SetBounds(12, 70, Math.Max(200, overview.Width - 24), Math.Max(80, overview.Height - 124));
            graphHelp.SetBounds(12, overview.Height - 50, overview.Width - 24, 50);
        };
        TableLayoutPanel compactSliders = new() { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 2, Margin = Padding.Empty, Padding = new Padding(0, 10, 0, 0) };
        for (int i = 0; i < 5; i++) compactSliders.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        for (int i = 0; i < 2; i++) compactSliders.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        TrackBar[] visibleSliders = { _startAngle, _returnAngle, yawResume, _maximumAngle, _smoothing, pitchStart, pitchReturn, pitchResume, pitchMaximum, pitchSmoothing };
        string[] names = { T("Neck.Slider.YawStart"), T("Neck.Slider.YawRelease"), T("Neck.Slider.NaturalResume"), T("Neck.Slider.YawMax"), T("Neck.Slider.BoostSoftness"), T("Neck.Slider.PitchStart"), T("Neck.Slider.PitchRelease"), T("Neck.Slider.PitchResume"), T("Neck.Slider.PitchMax"), T("Neck.Slider.PitchSoftness") };
        ToolTip help = new() { AutoPopDelay = 20000, InitialDelay = 300 };
        Disposed += (_, _) => help.Dispose();
        string[] descriptionKeys = { "YawStart", "YawRelease", "YawResume", "YawMaximum", "YawSoftness", "PitchStart", "PitchRelease", "PitchResume", "PitchMaximum", "PitchSoftness" };
        for (int i = 0; i < visibleSliders.Length; i++)
        {
            TrackBar bar = visibleSliders[i];
            Panel card = (Panel)bar.Parent!;
            card.Dock = DockStyle.Fill;
            card.Margin = new Padding(4);
            Label caption = card.Controls.OfType<Label>().First(l => l.TextAlign != ContentAlignment.MiddleRight);
            Label value = card.Controls.OfType<Label>().First(l => l.TextAlign == ContentAlignment.MiddleRight);
            caption.Text = names[i];
            Color accent = i % 5 == 3 ? ThemePalette.FromArgb(75, 235, 125) : i % 5 == 2 ? ThemePalette.FromArgb(70, 220, 240) : i % 5 == 1 ? Color.DarkOrange : ThemePalette.FromArgb(255, 190, 70);
            caption.ForeColor = value.ForeColor = accent;
            if (bar is AccentTrackBar colored) colored.Accent = accent;
            caption.Font = value.Font = new Font("Segoe UI", 12, FontStyle.Bold);
            void FitCard() { caption.SetBounds(6, 0, Math.Max(120, card.Width - 75), 30); value.SetBounds(card.Width - 65, 0, 60, 30); bar.SetBounds(0, 30, card.Width, 48); }
            card.SizeChanged += (_, _) => FitCard();
            UiLanguage.BindToolTip(help, bar, "Neck.Tooltip." + descriptionKeys[i], _languageCode);
            UiLanguage.BindToolTip(help, caption, "Neck.Tooltip." + descriptionKeys[i], _languageCode);
            compactSliders.Controls.Add(card, i % 5, i / 5);
        }
        Panel pressControls = new() { Dock = DockStyle.Fill, BackColor = BackColor, Visible = false };
        Label pressTitle = MakeLabel(T("Neck.SimpleModeTitle"), 16, FontStyle.Bold);
        pressTitle.ForeColor = ThemePalette.FromArgb(70, 220, 240); pressTitle.SetBounds(28, 28, 900, 38);
        Label pressDescription = MakeLabel(T("Neck.SimpleModeDescription"), 12, FontStyle.Regular);
        pressDescription.SetBounds(28, 70, 1500, 52);
        Label pressAngleLabel = MakeLabel(T("Neck.CameraRearRotation"), 12, FontStyle.Bold); pressAngleLabel.ForeColor = ThemePalette.FromArgb(255, 190, 70); pressAngleLabel.SetBounds(28, 135, 330, 34);
        Label pressAngleValue = MakeLabel(_settings.PressRotationAngle + "°", 12, FontStyle.Bold); pressAngleValue.ForeColor = ThemePalette.FromArgb(255, 190, 70); pressAngleValue.TextAlign = ContentAlignment.MiddleRight; pressAngleValue.SetBounds(620, 135, 85, 34);
        AccentTrackBar pressRotation = new() { Minimum = 30, Maximum = 180, Value = _settings.PressRotationAngle, TickFrequency = 10, Accent = ThemePalette.FromArgb(255, 190, 70) };
        pressRotation.SetBounds(28, 170, 680, 46);
        pressRotation.ValueChanged += (_, _) => { _settings.PressRotationAngle = pressRotation.Value; pressAngleValue.Text = pressRotation.Value + "°"; SaveSettings(); };
        Label deadzoneLabel = MakeLabel(T("Neck.HeadDeadzone"), 12, FontStyle.Bold); deadzoneLabel.ForeColor = ThemePalette.FromArgb(70, 220, 240); deadzoneLabel.SetBounds(785, 135, 360, 34);
        Label deadzoneValue = MakeLabel(_settings.SimpleDeadzoneAngle + "°", 12, FontStyle.Bold); deadzoneValue.ForeColor = ThemePalette.FromArgb(70, 220, 240); deadzoneValue.TextAlign = ContentAlignment.MiddleRight; deadzoneValue.SetBounds(1380, 135, 85, 34);
        AccentTrackBar simpleDeadzone = new() { Minimum = 0, Maximum = 45, Value = _settings.SimpleDeadzoneAngle, TickFrequency = 5, Accent = ThemePalette.FromArgb(70, 220, 240) };
        simpleDeadzone.SetBounds(785, 170, 680, 46);
        simpleDeadzone.ValueChanged += (_, _) => { _settings.SimpleDeadzoneAngle = simpleDeadzone.Value; deadzoneValue.Text = simpleDeadzone.Value + "°"; SaveSettings(); };
        pressControls.Controls.AddRange(new Control[] { pressTitle, pressDescription, pressAngleLabel, pressAngleValue, pressRotation, deadzoneLabel, deadzoneValue, simpleDeadzone });
        bindings.Dock = DockStyle.Fill;
        foreach (Control c in bindings.Controls) c.Visible = false;
        recenterLabel.Visible = _recenterBind.Visible = clearRecenter.Visible = toggleLabel.Visible = _activationBind.Visible = clearToggle.Visible = true;
        toggleLabel.Text = "ADVANCED TOGGLE";
        Label simpleLabel = MakeLabel("SIMPLE HOLD", 11, FontStyle.Bold);
        Button clearSimple = MakeButton("CLEAR");
        ThemeCheckBox advancedBehavior = new() { Appearance = Appearance.Button, AutoSize = false, Checked = _settings.AdvancedActivationBehavior == "Hold", Text = _settings.AdvancedActivationBehavior == "Hold" ? T("Assistant.Hold") : T("Assistant.Toggle"), ForeColor = Color.White, BackColor = ThemePalette.FromArgb(35, 55, 60), TextAlign = ContentAlignment.MiddleCenter };
        _simpleActivationBind.Click += (_, _) => CaptureBinding(2);
        clearSimple.Click += (_, _) => { _settings.SimpleActivationBinding = "Not assigned"; _simpleActivationBind.Text = FormatBinding("Not assigned"); _lastActivationPressed = false; _runtimeAssistanceActive = false; _capturingBinding = false; SaveSettings(); };
        advancedBehavior.CheckedChanged += (_, _) => { _settings.AdvancedActivationBehavior = advancedBehavior.Checked ? "Hold" : "Toggle"; advancedBehavior.Text = advancedBehavior.Checked ? T("Assistant.Hold") : T("Assistant.Toggle"); advancedBehavior.BackColor = advancedBehavior.Checked ? ThemePalette.FromArgb(25, 95, 55) : ThemePalette.FromArgb(35, 55, 60); _toggleActive = false; SaveSettings(); };
        bindings.Controls.AddRange(new Control[] { simpleLabel, _simpleActivationBind, clearSimple, advancedBehavior });
        Button recenterNow = MakeButton("Recenter Neck Assistant");
        bindings.Controls.Add(recenterNow);
        recenterNow.Click += (_, _) => RecenterNeck();
        recenterLabel.SetBounds(12, 4, 235, 36); _recenterBind.SetBounds(250, 4, 250, 38); clearRecenter.SetBounds(515, 4, 100, 38);
        toggleLabel.SetBounds(12, 50, 235, 36); _activationBind.SetBounds(250, 50, 250, 38); clearToggle.SetBounds(515, 50, 100, 38);
        advancedBehavior.SetBounds(630, 50, 130, 38);
        simpleLabel.SetBounds(12, 96, 235, 36); _simpleActivationBind.SetBounds(250, 96, 250, 38); clearSimple.SetBounds(515, 96, 100, 38);
        RefreshPitchEnabledText();
        bindings.Controls.Add(pitchEnabled);
        pitchEnabled.AutoSize = false;
        pitchEnabled.SetBounds(640, 8, 320, 38);
        Label bindingHelp = MakeLabel(T("Neck.BindingHelp.Toggle"), 10.5f, FontStyle.Regular);
        UiLanguage.Bind(bindingHelp, "Neck.BindingHelp.Toggle", _languageCode);
        bindingHelp.SetBounds(12, 94, 710, 32); bindings.Controls.Add(bindingHelp);
        Label speedLabel = MakeLabel(T("Neck.TransitionSpeed"), 10, FontStyle.Bold); speedLabel.ForeColor = ThemePalette.FromArgb(70, 220, 240); speedLabel.SetBounds(790, 2, 360, 28);
        Label speedValue = MakeLabel(_settings.TransitionSpeed + "%", 10, FontStyle.Bold); speedValue.TextAlign = ContentAlignment.MiddleRight; speedValue.ForeColor = ThemePalette.FromArgb(70, 220, 240); speedValue.SetBounds(1450, 2, 90, 28);
        AccentTrackBar transitionSpeed = new() { Minimum = 1, Maximum = 100, Value = _settings.TransitionSpeed, TickFrequency = 10, Accent = ThemePalette.FromArgb(70, 220, 240) };
        transitionSpeed.SetBounds(790, 32, 750, 46);
        Label speedHelp = MakeLabel(T("Neck.TransitionHelp"), 10.5f, FontStyle.Regular); speedHelp.SetBounds(790, 82, 760, 32);
        transitionSpeed.ValueChanged += (_, _) => { _settings.TransitionSpeed = transitionSpeed.Value; speedValue.Text = transitionSpeed.Value + "%"; SaveSettings(); };
        bindings.Controls.AddRange(new Control[] { speedLabel, speedValue, transitionSpeed, speedHelp });
        UiLanguage.BindToolTip(help, _recenterBind, "Neck.Tooltip.Recenter", _languageCode);
        UiLanguage.BindToolTip(help, _activationBind, "Neck.Tooltip.Toggle", _languageCode);
        UiLanguage.BindToolTip(help, advancedBehavior, "Neck.Tooltip.AdvancedBehavior", _languageCode);
        UiLanguage.BindToolTip(help, _simpleActivationBind, "Neck.Tooltip.Simple", _languageCode);
        UiLanguage.BindToolTip(help, clearRecenter, "Neck.Tooltip.ClearRecenter", _languageCode);
        UiLanguage.BindToolTip(help, clearToggle, "Neck.Tooltip.ClearToggle", _languageCode);
        dashboard.Controls.Add(overview, 0, 0); dashboard.Controls.Add(compactSliders, 0, 1); dashboard.Controls.Add(bindings, 0, 2);
        dashboard.Controls.Add(pressControls, 0, 1);
        Controls.Add(dashboard); dashboard.BringToFront(); header.BringToFront();
        dashboard.Dock = DockStyle.None;
        void FitDashboard() { if (!IllustratedTheme.Enabled) dashboard.SetBounds(0, header.Height, ClientSize.Width, Math.Max(480, ClientSize.Height - header.Height)); }
        ClientSizeChanged += (_, _) => FitDashboard();
        FitDashboard();

        _pitchPreview = new NeckCurvePreview
        {
            AxisTitleKey = "Neck.VerticalAxis", LanguageCode = _languageCode, PhysicalLimit = 80, ViewLimit = 140,
            Bezier = _settings.PitchBezier, StartAngle = _settings.PitchStartAngle, ReturnAngle = _settings.PitchReturnAngle, NaturalResumeAngle = _settings.PitchNaturalResumeAngle,
            MaximumViewAngle = _settings.PitchMaximumViewAngle, Curvature = _settings.PitchCurvature,
            BackColor = _curvePreview.BackColor, ForeColor = _curvePreview.ForeColor,
            Enabled = _settings.PitchEnabled
        };
        overview.Controls.Add(_pitchPreview);
        overview.Controls.Add(pitchEnabled);
        void LayoutGraphs()
        {
            // Place the up/down toggle after the active-state label so its text never overlaps.
            pitchEnabled.SetBounds(470, 0, 300, 38);
            int width = Math.Max(240, overview.Width - 24);
            const int graphTop = 78;
            const int graphGap = 8;
            const int helpHeight = 42;
            int graphHeight = Math.Max(70, (overview.Height - graphTop - graphGap - helpHeight - 8) / 2);
            _curvePreview.SetBounds(12, graphTop, width, graphHeight);
            _pitchPreview.SetBounds(12, graphTop + graphHeight + graphGap, width, graphHeight);
            graphHelp.SetBounds(12, _pitchPreview.Bottom + 4, width, helpHeight);
        }
        overview.SizeChanged += (_, _) => LayoutGraphs();
        LayoutGraphs();
        graphHelp.Text = T("Neck.GraphLegend");
        pitchEnabled.CheckedChanged += (_, _) =>
        {
            _pitchPreview.Enabled = pitchEnabled.Checked;
            pitchStart.Enabled = pitchReturn.Enabled = pitchMaximum.Enabled = pitchSmoothing.Enabled = pitchEnabled.Checked;
            pitchResume.Enabled = pitchEnabled.Checked && _settings.NaturalRearView;
            _pitchPreview.Invalidate();
        };
        pitchStart.Enabled = pitchReturn.Enabled = pitchMaximum.Enabled = pitchSmoothing.Enabled = _settings.PitchEnabled;
        _pitchPreview.MappingChanged += (_, _) =>
        {
            int editedStart = _pitchPreview.StartAngle;
            int editedReturn = _pitchPreview.ReturnAngle;
            int editedResume = _pitchPreview.NaturalResumeAngle;
            int editedMaximum = _pitchPreview.MaximumViewAngle;
            _settings.PitchBezier = _pitchPreview.Bezier;
            _settings.PitchCurvature = _pitchPreview.Curvature;
            _settings.PitchStartAngle = editedStart;
            _settings.PitchReturnAngle = editedReturn;
            _settings.PitchNaturalResumeAngle = editedResume;
            _settings.PitchMaximumViewAngle = editedMaximum;
            pitchSmoothing.Value = _pitchPreview.Curvature;
            pitchStart.Value = Math.Clamp(editedStart, pitchStart.Minimum, pitchStart.Maximum);
            pitchReturn.Value = Math.Clamp(editedReturn, pitchReturn.Minimum, pitchReturn.Maximum);
            pitchResume.Value = Math.Clamp(editedResume, pitchResume.Minimum, pitchResume.Maximum);
            pitchMaximum.Value = Math.Clamp(editedMaximum, pitchMaximum.Minimum, pitchMaximum.Maximum);
            SaveSettings();
        };
        ThemeCheckBox linkAxes = new() { Text = _settings.LinkAxes ? T("Assistant.LinkOn") : T("Assistant.LinkOff"), Appearance = Appearance.Button, AutoSize = false, Checked = _settings.LinkAxes, ForeColor = Color.White, BackColor = ThemePalette.FromArgb(35, 55, 60), TextAlign = ContentAlignment.MiddleCenter };
        linkAxes.SetBounds(735, 0, 185, 38);
        overview.Controls.Add(linkAxes);
        ThemeCheckBox naturalRear = new() { Text = _settings.NaturalRearView ? T("Assistant.RearOn") : T("Assistant.RearOff"), Appearance = Appearance.Button, AutoSize = false, Checked = _settings.NaturalRearView, ForeColor = Color.White, BackColor = _settings.NaturalRearView ? ThemePalette.FromArgb(25, 95, 55) : ThemePalette.FromArgb(35, 55, 60), TextAlign = ContentAlignment.MiddleCenter };
        naturalRear.SetBounds(930, 0, 185, 38); overview.Controls.Add(naturalRear);
        void ExplainMovementMode() => graphHelp.Text = naturalRear.Checked
            ? T("Neck.RearBoostHelp")
            : T("Neck.StandardHelp");
        void SetRearControls() { yawResume.Enabled = naturalRear.Checked; pitchResume.Enabled = naturalRear.Checked && pitchEnabled.Checked; }
        naturalRear.CheckedChanged += (_, _) => { _settings.NaturalRearView = naturalRear.Checked; naturalRear.Text = naturalRear.Checked ? T("Assistant.RearOn") : T("Assistant.RearOff"); naturalRear.BackColor = naturalRear.Checked ? ThemePalette.FromArgb(25, 95, 55) : ThemePalette.FromArgb(35, 55, 60); SetRearControls(); ExplainMovementMode(); SaveSettings(); };
        UiLanguage.BindToolTip(help, naturalRear, "Neck.Tooltip.NaturalRear", _languageCode);
        ExplainMovementMode();
        SetRearControls();
        ThemeRadioButton normalMode = new() { Text = T("Assistant.Advanced"), Appearance = Appearance.Button, Checked = _settings.MovementMode == "Advanced", AutoSize = false, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleCenter, BackColor = ThemePalette.FromArgb(35, 55, 60) };
        ThemeRadioButton pressMode = new() { Text = T("Assistant.Simple"), Appearance = Appearance.Button, Checked = _settings.MovementMode == "Simple", AutoSize = false, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleCenter, BackColor = ThemePalette.FromArgb(35, 55, 60) };
        // Keep all three controls inside the 1625px design canvas, even at the normal minimum window width.
        pressMode.SetBounds(1125, 0, 125, 38);
        normalMode.SetBounds(1260, 0, 125, 38);
        overview.Controls.AddRange(new Control[] { pressMode, normalMode });
        Button restoreAdvancedDefaults = MakeButton(T("Neck.RestoreDefaults"));
        restoreAdvancedDefaults.SetBounds(1395, 0, 180, 38);
        overview.Controls.Add(restoreAdvancedDefaults);
        Label pressOverview = MakeLabel(T("Neck.PressOverview"), 14, FontStyle.Bold);
        pressOverview.TextAlign = ContentAlignment.MiddleCenter; pressOverview.ForeColor = ThemePalette.FromArgb(180, 225, 235); pressOverview.Visible = false; overview.Controls.Add(pressOverview);
        void LayoutPressOverview()
        {
            pressOverview.SetBounds(40, 82, Math.Max(300, overview.ClientSize.Width - 80), 72);
            pressOverview.TextAlign = ContentAlignment.MiddleCenter;
        }
        void QueuePressOverviewLayout()
        {
            if (!IsHandleCreated) return;
            BeginInvoke((Action)LayoutPressOverview);
        }
        void ApplyMovementModeUi()
        {
            bool press = pressMode.Checked;
            _settings.MovementMode = press ? "Simple" : "Advanced";
            // Simple mode does not need the graph-sized overview. Give that space to
            // its controls and binding help so nothing is pushed below the window.
            dashboard.RowStyles[0].SizeType = press ? SizeType.Absolute : SizeType.Percent;
            dashboard.RowStyles[0].Height = press ? 150 : 100;
            dashboard.RowStyles[1].SizeType = SizeType.Absolute;
            dashboard.RowStyles[1].Height = press ? 220 : 220;
            dashboard.RowStyles[2].SizeType = SizeType.Absolute;
            dashboard.RowStyles[2].Height = press ? 260 : 290;
            _curvePreview.Visible = _pitchPreview.Visible = graphHelp.Visible = pitchEnabled.Visible = linkAxes.Visible = naturalRear.Visible = !press;
            compactSliders.Visible = !press; pressControls.Visible = press; pressOverview.Visible = press;
            restoreAdvancedDefaults.Visible = !press;
            recenterLabel.Visible = false;
            _recenterBind.Visible = clearRecenter.Visible = true;
            toggleLabel.Visible = _activationBind.Visible = clearToggle.Visible = advancedBehavior.Visible = !press;
            simpleLabel.Visible = _simpleActivationBind.Visible = clearSimple.Visible = press;
            UiLanguage.Bind(bindingHelp, press ? "Neck.BindingHelp.Simple" : "Neck.BindingHelp.Toggle", _languageCode);
            if (press)
            {
                simpleLabel.SetBounds(12, 2, 175, 46);
                _simpleActivationBind.SetBounds(195, 4, 235, 38);
                clearSimple.SetBounds(440, 4, 90, 38);
                bindingHelp.SetBounds(12, 48, Math.Max(450, bindings.ClientSize.Width - 24), 44);
                speedLabel.SetBounds(12, 86, 245, 54);
                speedValue.SetBounds(Math.Max(510, bindings.ClientSize.Width - 76), 86, 64, 30);
                transitionSpeed.SetBounds(265, 86, Math.Max(230, bindings.ClientSize.Width - 355), 34);
                speedHelp.SetBounds(12, 144, Math.Max(450, bindings.ClientSize.Width - 24), 48);
            }
            else
            {
                int rightX = Math.Max(660, bindings.ClientSize.Width / 2);
                bindingHelp.SetBounds(12, 94, Math.Max(320, rightX - 30), 48);
                speedLabel.SetBounds(rightX, 4, 245, 28);
                speedValue.SetBounds(Math.Max(rightX + 250, bindings.ClientSize.Width - 76), 4, 64, 28);
                transitionSpeed.SetBounds(rightX, 36, Math.Max(180, bindings.ClientSize.Width - rightX - 20), 34);
                speedHelp.SetBounds(rightX, 76, Math.Max(180, bindings.ClientSize.Width - rightX - 20), 48);
            }
            LayoutPressOverview();
            if (press) QueuePressOverviewLayout();
            normalMode.BackColor = !press ? ThemePalette.FromArgb(25, 95, 55) : ThemePalette.FromArgb(35, 55, 60);
            pressMode.BackColor = press ? ThemePalette.FromArgb(25, 95, 55) : ThemePalette.FromArgb(35, 55, 60);
            _toggleActive = _settings.Enabled; _runtimeAssistanceActive = press ? false : _toggleActive;
            SaveSettings();
        }
        normalMode.CheckedChanged += (_, _) => { if (normalMode.Checked) ApplyMovementModeUi(); };
        pressMode.CheckedChanged += (_, _) => { if (pressMode.Checked) ApplyMovementModeUi(); };
        overview.SizeChanged += (_, _) => { if (pressOverview.Visible) LayoutPressOverview(); };
        VisibleChanged += (_, _) => { if (Visible && pressOverview.Visible) QueuePressOverviewLayout(); };
        Shown += (_, _) => { if (pressOverview.Visible) QueuePressOverviewLayout(); };
        ApplyMovementModeUi();
        bool syncingAxes = false;
        bool restoringDefaults = false;
        restoreAdvancedDefaults.Click += (_, _) =>
        {
            restoringDefaults = true;
            try
            {
                _settings.StartAngle = 21;
                _settings.ReturnAngle = 12;
                _settings.YawNaturalResumeAngle = 28;
                _settings.MaximumViewAngle = 180;
                _settings.YawCurvature = 98;
                _settings.TransitionWidth = 15;
                _settings.PitchStartAngle = 15;
                _settings.PitchReturnAngle = 9;
                _settings.PitchNaturalResumeAngle = 20;
                _settings.PitchMaximumViewAngle = 115;
                _settings.PitchCurvature = 98;
                _settings.PitchTransitionWidth = 10;
                _settings.LinkAxes = true;
                _settings.NaturalRearView = true;
                _settings.PitchEnabled = false;
                _settings.PositionCompensation = true;
                _settings.Curve = "Linear";
                _settings.YawPoints.Clear();
                _settings.YawBezier = new BezierNeckCurve();
                _settings.PitchBezier = new BezierNeckCurve();

                _startAngle.Value = 21;
                _returnAngle.Value = 12;
                yawResume.Value = 28;
                _maximumAngle.Value = 180;
                _smoothing.Value = 98;
                pitchStart.Value = 15;
                pitchReturn.Value = 9;
                pitchResume.Value = 20;
                pitchMaximum.Value = 115;
                pitchSmoothing.Value = 98;
                pitchEnabled.Checked = false;
                linkAxes.Checked = true;
                naturalRear.Checked = true;
                _positionCompensation.Checked = true;
                _curve.SelectedItem = "Linear";
                _curvePreview.Bezier = _settings.YawBezier;
                _pitchPreview.Bezier = _settings.PitchBezier;
            }
            finally { restoringDefaults = false; }
            SaveSettings();
            UpdateCurvePreview();
        };
        UiLanguage.BindToolTip(help, restoreAdvancedDefaults, "Neck.Tooltip.RestoreDefaults", _languageCode);
        void SyncAxes(bool fromPitch)
        {
            if (!_settings.LinkAxes || syncingAxes || restoringDefaults) return;
            syncingAxes = true;
            try
            {
                BezierNeckCurve copy = new();
                if (fromPitch)
                {
                    _settings.YawBezier = copy;
                    _startAngle.Value = Math.Clamp((int)Math.Round(_settings.PitchStartAngle * 110d / 80), _startAngle.Minimum, _startAngle.Maximum);
                    _returnAngle.Value = Math.Clamp((int)Math.Round(_settings.PitchReturnAngle * 110d / 80), _returnAngle.Minimum, _returnAngle.Maximum);
                    yawResume.Value = Math.Clamp((int)Math.Round(_settings.PitchNaturalResumeAngle * 110d / 80), yawResume.Minimum, yawResume.Maximum);
                    _maximumAngle.Value = Math.Clamp((int)Math.Round(_settings.PitchMaximumViewAngle * 220d / 140), _maximumAngle.Minimum, _maximumAngle.Maximum);
                    _smoothing.Value = _settings.PitchCurvature;
                }
                else
                {
                    _settings.PitchBezier = copy;
                    pitchStart.Value = Math.Clamp((int)Math.Round(_settings.StartAngle * 80d / 110), pitchStart.Minimum, pitchStart.Maximum);
                    pitchReturn.Value = Math.Clamp((int)Math.Round(_settings.ReturnAngle * 80d / 110), pitchReturn.Minimum, pitchReturn.Maximum);
                    pitchResume.Value = Math.Clamp((int)Math.Round(_settings.YawNaturalResumeAngle * 80d / 110), pitchResume.Minimum, pitchResume.Maximum);
                    pitchMaximum.Value = Math.Clamp((int)Math.Round(_settings.MaximumViewAngle * 140d / 220), pitchMaximum.Minimum, pitchMaximum.Maximum);
                    pitchSmoothing.Value = _settings.YawCurvature;
                }
                SaveSettings();
            }
            finally { syncingAxes = false; }
        }
        linkAxes.CheckedChanged += (_, _) => { _settings.LinkAxes = linkAxes.Checked; linkAxes.Text = linkAxes.Checked ? T("Assistant.LinkOn") : T("Assistant.LinkOff"); linkAxes.BackColor = linkAxes.Checked ? ThemePalette.FromArgb(25, 95, 55) : ThemePalette.FromArgb(35, 55, 60); SyncAxes(false); SaveSettings(); };
        UiLanguage.BindToolTip(help, linkAxes, "Neck.Tooltip.LinkAxes", _languageCode);
        _curvePreview.MappingChanged += (_, _) => SyncAxes(false);
        _pitchPreview.MappingChanged += (_, _) => SyncAxes(true);

        HookSlider(_startAngle, _startValue, "°", value => _settings.StartAngle = value);
        HookSlider(_maximumAngle, _maximumValue, "°", value => _settings.MaximumViewAngle = value);
        HookSlider(transition, _transitionValue, "°", value => _settings.TransitionWidth = value);
        HookSlider(_smoothing, _smoothingValue, "%", value => _settings.YawCurvature = value);
        HookSlider(_returnAngle, _returnValue, "°", value => _settings.ReturnAngle = value);
        HookSlider(yawResume, yawResumeValue, "°", value => _settings.YawNaturalResumeAngle = value);
        foreach (var bar in new[] { _startAngle, _returnAngle, yawResume, _maximumAngle, _smoothing }) bar.ValueChanged += (_, _) => SyncAxes(false);
        foreach (var bar in new[] { pitchStart, pitchReturn, pitchResume, pitchMaximum, pitchSmoothing }) bar.ValueChanged += (_, _) => SyncAxes(true);

        if (IllustratedTheme.Enabled)
        {
            // Simple/Advanced are global mode selectors, not graph content. Keep
            // them in the fixed header so both remain visible even when the
            // dashboard body is narrower or scrolled.
            overview.Controls.Remove(pressMode);
            overview.Controls.Remove(normalMode);
            header.Controls.Add(pressMode);
            header.Controls.Add(normalMode);
            pressMode.BringToFront();
            normalMode.BringToFront();

            // The illustrated shell has a sidebar. Reflow instead of squeezing the
            // old 1625px toolbar into its smaller content area.
            AutoScroll = true;
            bool arrangingTheme = false;
            void ArrangeTheme()
            {
                if (arrangingTheme) return;
                arrangingTheme = true;
                try
                {
                    bool simple = pressMode.Checked;
                    int width = Math.Max(700, ClientSize.Width - 24);
                    int toolbarExtra = width < 960 ? 46 : 0;
                    int overviewHeight = simple ? 200 : 506 + toolbarExtra;
                    int middleHeight = simple ? 220 : 304;
                    bool stackBindings = width < 1080;
                    int bindingHeight = simple ? 260 : 290;
                    const float uiScale = 1f;
                    dashboard.RowStyles[0].SizeType = SizeType.Absolute; dashboard.RowStyles[0].Height = overviewHeight;
                    dashboard.RowStyles[1].SizeType = SizeType.Absolute; dashboard.RowStyles[1].Height = middleHeight;
                    dashboard.RowStyles[2].SizeType = SizeType.Absolute; dashboard.RowStyles[2].Height = bindingHeight;
                    dashboard.SetBounds(0, dashboard.Parent == this ? header.Height : 0, width, overviewHeight + middleHeight + bindingHeight);
                    if (dashboard.Parent is ScrollableControl scrollHost) scrollHost.AutoScrollMinSize = new Size(width, dashboard.Height + 16);
                    width -= 40; // Dashboard padding and cell margins are not usable content width.
                    int enabledWidth = Math.Clamp(_enabled.GetPreferredSize(Size.Empty).Width + 8, 270, Math.Max(270, width - 180));
                    _enabled.SetBounds(12, 0, enabledWidth, 38);
                    enabledIndicator.SetBounds(_enabled.Right + 10, 0, Math.Max(150, width - _enabled.Right - 22), 38);
                    int headerWidth = Math.Max(520, header.ClientSize.Width);
                    pressMode.SetBounds(headerWidth - 310, 28, 130, 40);
                    normalMode.SetBounds(headerWidth - 170, 28, 150, 40);
                    title.Width = Math.Max(120, pressMode.Left - title.Left - 16);
                    // Do not let the runtime subtitle run underneath the fixed mode buttons.
                    runtimeSupport.Width = Math.Max(80, headerWidth - 440);
                    pitchEnabled.SetBounds(12,48,Math.Min(250, Math.Max(150, width / 3)),38);
                    int linkX = pitchEnabled.Right + 12;
                    int linkW = Math.Min(180, Math.Max(125, width / 4));
                    linkAxes.SetBounds(linkX,48,linkW,38);
                    int rearX = linkAxes.Right + 12;
                    int rearW = Math.Max(130, width - rearX - 12);
                    if (toolbarExtra > 0)
                    {
                        // Narrow view: the restore button gets its own row so it
                        // can never sit on top of the rear-view control.
                        naturalRear.SetBounds(rearX,48,rearW,38);
                        restoreAdvancedDefaults.SetBounds(Math.Max(12, width-200),94,185,38);
                    }
                    else
                    {
                        int restoreW = 185;
                        int restoreX = width - restoreW;
                        rearW = Math.Max(130, restoreX - rearX - 12);
                        naturalRear.SetBounds(rearX,48,rearW,38);
                        restoreAdvancedDefaults.SetBounds(restoreX,48,restoreW,38);
                    }
                    _backendStatus.SetBounds(12,simple ? 48 : 90+toolbarExtra,width-28,36);
                    _curvePreview.SetBounds(12,132+toolbarExtra,width-28,160);
                    _pitchPreview.SetBounds(12,300+toolbarExtra,width-28,160);
                    graphHelp.SetBounds(12,466+toolbarExtra,width-28,36);
                    pressOverview.SetBounds(20,100,width-40,82);
                    int half = (width-56)/2;
                    pressTitle.SetBounds(24,10,width-48,36); pressDescription.SetBounds(24,50,width-48,68);
                    pressAngleLabel.SetBounds(24,130,half-65,42); pressAngleValue.SetBounds(24+half-65,130,65,42);
                    pressRotation.SetBounds(24,180,half,44);
                    deadzoneLabel.SetBounds(40+half,130,half-65,42); deadzoneValue.SetBounds(width-82,130,65,42);
                    simpleDeadzone.SetBounds(40+half,180,half,44);
                    recenterLabel.SetBounds(12,4,175,36); _recenterBind.SetBounds(195,4,225,38); clearRecenter.SetBounds(430,4,90,38);
                    toggleLabel.SetBounds(12,50,175,36); _activationBind.SetBounds(195,50,225,38); clearToggle.SetBounds(430,50,90,38); advancedBehavior.SetBounds(530,50,125,38);
                    simpleLabel.SetBounds(12,2,270,48); _simpleActivationBind.SetBounds(292,4,225,38); clearSimple.SetBounds(527,4,90,38);
                    bindingHelp.SetBounds(12,simple ? 48 : 92,width-28,48);
                    int recenterY = simple ? 204 : 242;
                    int recenterWidth = Math.Min(300, (width - 136) / 2);
                    recenterNow.SetBounds(12, recenterY, recenterWidth, 38);
                    _recenterBind.SetBounds(recenterNow.Right + 10, recenterY, Math.Max(100, width - recenterNow.Right - 122), 38);
                    clearRecenter.SetBounds(width - 102, recenterY, 90, 38);
                    if (simple)
                    {
                        speedLabel.SetBounds(12,88,245,54);
                        speedValue.SetBounds(width-70,88,58,30);
                        transitionSpeed.SetBounds(265,88,Math.Max(220,width-355),34);
                        speedHelp.SetBounds(12,148,width-28,46);
                    }
                    else if (stackBindings)
                    {
                        speedLabel.SetBounds(12,144,245,30);
                        speedValue.SetBounds(width-70,144,58,30);
                        transitionSpeed.SetBounds(265,144,Math.Max(100,width-355),34);
                        speedHelp.SetBounds(12,184,width-28,48);
                    }
                    else
                    {
                        int rightX=Math.Max(660,width/2);
                        speedLabel.SetBounds(rightX,4,255,30);
                        speedValue.SetBounds(width-70,4,58,30);
                        transitionSpeed.SetBounds(rightX,38,Math.Max(190,width-rightX-12),34);
                        speedHelp.SetBounds(rightX,80,Math.Max(190,width-rightX-12),48);
                    }

                    ApplyThemeScaleFonts(this, uiScale, title, speedHelp, bindingHelp, graphHelp, pressDescription, runtimeSupport);
                    ResponsiveFonts.Set(_curvePreview, 17f, FontStyle.Regular, GraphicsUnit.Pixel);
                    ResponsiveFonts.Set(_pitchPreview, 17f, FontStyle.Regular, GraphicsUnit.Pixel);
                }
                finally { arrangingTheme = false; }
            }
            void ApplyThemeScaleFonts(Control parent, float scale, params Control[] keyControls)
            {
                foreach(Control child in parent.Controls)
                {
                    if(child is Label || child is ButtonBase || child is CheckBox || child is RadioButton)
                    {
                        float basePixels = child.Font.Bold ? 22f : 21f;
                        if (child == title) basePixels = 30f;
                        else if (child == speedHelp || child == bindingHelp || child == graphHelp || child == pressDescription) basePixels = 18f;
                        else if (child == runtimeSupport) basePixels = 18f;
                        ResponsiveFonts.Set(child, basePixels * scale, child.Font.Style, GraphicsUnit.Pixel);
                    }
                    ApplyThemeScaleFonts(child, scale, keyControls);
                }
            }
            ApplyThemeScaleFonts(this, 1.0f, title, speedHelp, bindingHelp, graphHelp, pressDescription, runtimeSupport);
            _curvePreview.Font = _pitchPreview.Font = new Font("Segoe UI",17,FontStyle.Regular,GraphicsUnit.Pixel);
            CaptureThemeFontSizes(this);
            ApplyThemeFontScale();
            string[] shortNames = [T("Neck.Card.Activation"), T("Neck.Card.Release"), T("Neck.Card.NaturalMotion"), T("Neck.Card.MaximumView"), T("Neck.Card.Softness")];
            for(int i=0;i<visibleSliders.Length;i++)
            {
                int index=i; TrackBar bar=visibleSliders[i]; Panel card=(Panel)bar.Parent!;
                Label caption=card.Controls.OfType<Label>().First(l=>l.TextAlign!=ContentAlignment.MiddleRight);
                Label value=card.Controls.OfType<Label>().First(l=>l.TextAlign==ContentAlignment.MiddleRight);
                card.Paint += (_,e) => { using Pen edge=new(IllustratedTheme.Muted,1); e.Graphics.DrawLine(edge,card.Width-1,12,card.Width-1,card.Height-12); };
                caption.Text=(i<5 ? T("Neck.Card.Horizontal") + "\n" : T("Neck.Card.Vertical") + "\n")+shortNames[i%5];
                void ArrangeCard() { caption.SetBounds(7,0,Math.Max(50,card.Width-14),64); value.SetBounds(7,64,Math.Max(50,card.Width-14),28); bar.SetBounds(0,96,card.Width,35); }
                card.SizeChanged+=(_,_)=>ArrangeCard(); ArrangeCard();
            }
            overview.SizeChanged+=(_,_)=>ArrangeTheme();
            header.SizeChanged+=(_,_)=>ArrangeTheme();
            ClientSizeChanged+=(_,_)=>{ ArrangeTheme(); ApplyThemeFontScale(); };
            pressMode.CheckedChanged+=(_,_)=>ArrangeTheme();
            normalMode.CheckedChanged+=(_,_)=>ArrangeTheme();
            _enabled.TextChanged+=(_,_)=>ArrangeTheme();
            enabledIndicator.TextChanged+=(_,_)=>ArrangeTheme();
            ArrangeTheme();
        }

        if (IllustratedTheme.Enabled)
        {
            // Keep the dashboard directly in the content viewport. Camera transition
            // controls remain inside the Bindings row, immediately below Simple Hold.
            AutoScroll = true;
            AutoScrollMinSize = new Size(0, dashboard.Bottom + 18);
            dashboard.Top = header.Height;
        }

        _statusTimer.Interval = 16;
        _statusTimer.Tick += (_, _) =>
        {
            PollBindings();
            _openXrBackend.UpdateMotion(_settings, _runtimeAssistanceActive);
            RefreshTelemetry();
            if (++_statusTicks >= 25) { _statusTicks = 0; RefreshStatus(); }
        };
        Shown += (_, _) => StartAssistanceRuntime();
        FormClosed += (_, _) =>
        {
            _statusTimer.Stop();
            SaveSettings(applyBackend: false);
            _openXrBackend.DisableLayer();
            _openXrBackend.Dispose();
        };
        ApplyLanguage(_languageCode);
    }

    protected override bool ShowWithoutActivation => false;

    private bool _runtimeStarted;
    public void StartAssistanceRuntime()
    {
        if (_runtimeStarted) return;
        _runtimeStarted = true;
        _openXrBackend.Apply(_settings);
        UpdateCurvePreview();
        RefreshStatus();
        _statusTimer.Start();
    }

    public void ApplyLanguage(string languageCode)
    {
        _languageCode = AppText.Normalize(languageCode);
        UiLanguage.Apply(this, _languageCode, LocalizedTextKeys);
        _recenterBind.Text = FormatBinding(_settings.RecenterBinding);
        _activationBind.Text = FormatBinding(_settings.ActivationBinding);
        _simpleActivationBind.Text = FormatBinding(_settings.SimpleActivationBinding);
        _curvePreview.LanguageCode = _languageCode;
        _curvePreview.AxisTitleKey = "Neck.HorizontalAxis";
        _curvePreview.Invalidate();
        if (_pitchPreview != null)
        {
            _pitchPreview.LanguageCode = _languageCode;
            _pitchPreview.AxisTitleKey = "Neck.VerticalAxis";
            _pitchPreview.Invalidate();
        }
        Text = T("Nav.Neck");
        RefreshStatus();
    }

    private string T(string key) => AppText.T(_languageCode, key);

    private (TrackBar Slider, Label Value) AddSlider(string text, int min, int max, int value, int y, string suffix)
    {
        Label label = MakeLabel(text, 10, FontStyle.Bold);
        label.SetBounds(20, y, 300, 34);
        Controls.Add(label);

        Label valueLabel = MakeLabel(value + suffix, 10, FontStyle.Bold);
        valueLabel.TextAlign = ContentAlignment.MiddleRight;
        valueLabel.SetBounds(325, y, 100, 34);
        Controls.Add(valueLabel);

        TrackBar slider = new AccentTrackBar()
        {
            Minimum = min,
            Maximum = max,
            Value = Math.Clamp(value, min, max),
            TickFrequency = Math.Max(1, (max - min) / 10),
            SmallChange = 1,
            LargeChange = 5
        };
        slider.SetBounds(15, y + 38, 415, 48);
        Controls.Add(slider);
        return (slider, valueLabel);
    }

    private void HookSlider(TrackBar slider, Label valueLabel, string suffix, Action<int> update)
    {
        slider.ValueChanged += (_, _) =>
        {
            valueLabel.Text = slider.Value + suffix;
            update(slider.Value);
            SaveSettings();
        };
    }

    private void AddDivider(int y)
    {
        Panel divider = new() { BackColor = ThemePalette.FromArgb(62, 82, 88) };
        divider.SetBounds(20, y, 410, 1);
        Controls.Add(divider);
    }

    private void CaptureThemeFontSizes(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            if (!_themeBaseFontSizes.ContainsKey(child)) _themeBaseFontSizes[child] = child.Font.Size;
            if (child.HasChildren) CaptureThemeFontSizes(child);
        }
    }

    private void ApplyThemeFontScale()
    {
        if (!IllustratedTheme.Enabled || _applyingThemeFontScale || ClientSize.Width <= 0 || ClientSize.Height <= 0) return;
        _applyingThemeFontScale = true;
        try
        {
            const float scale = 1f;
            foreach ((Control control, float baseSize) in _themeBaseFontSizes.ToArray())
            {
                if (control.IsDisposed) continue;
                float target = Math.Max(8f, baseSize * scale);
                if (Math.Abs(control.Font.Size - target) < .05f) continue;
                ResponsiveFonts.Set(control, target, control.Font.Style, control.Font.Unit);
            }
        }
        finally { _applyingThemeFontScale = false; }
    }

    private static Label MakeLabel(string text, float size, FontStyle style) => new()
    {
        Text = text,
        Font = new Font("Segoe UI", size, style),
        ForeColor = ThemePalette.FromArgb(235, 238, 240),
        BackColor = Color.Transparent,
        AutoSize = false
    };

    private static Button MakeButton(string text) => new ThemeButton()
    {
        Text = text,
        Font = new Font("Segoe UI", 9, FontStyle.Bold),
        BackColor = ThemePalette.FromArgb(16, 29, 33),
        ForeColor = Color.White,
        FlatStyle = FlatStyle.Flat
    };

    private void CaptureBinding(int kind)
    {
        _capturingBinding = true;
        string action = kind == 0 ? T("Assistant.RecenterCombo") : kind == 1 ? T("Assistant.AdvancedToggle") : T("Assistant.SimpleHold");
        using HotasBindingDialog dialog = new(string.Format(CultureInfo.CurrentCulture, T("Binding.Prompt"), action), _languageCode);
        DialogResult result;
        try { result = dialog.ShowDialog(this); }
        finally
        {
            _capturingBinding = false;
            _lastActivationPressed = true;
            _lastRecenterPressed = true;
        }
        if (result != DialogResult.OK || string.IsNullOrWhiteSpace(dialog.Binding)) return;

        if (kind == 0)
        {
            _settings.RecenterBinding = dialog.Binding;
            _recenterBind.Text = FormatBinding(dialog.Binding);
        }
        else if (kind == 1)
        {
            _settings.ActivationBinding = dialog.Binding;
            _activationBind.Text = FormatBinding(dialog.Binding);
        }
        else
        {
            _settings.SimpleActivationBinding = dialog.Binding;
            _simpleActivationBind.Text = FormatBinding(dialog.Binding);
        }
        SaveSettings();
    }

    private string FormatBinding(string binding)
    {
        if (string.IsNullOrWhiteSpace(binding) || binding == "Not assigned") return T("Assistant.BindInput");
        return HotasDirectInput.DisplayBinding(binding);
    }

    private NeckAssistSettings LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                return JsonSerializer.Deserialize<NeckAssistSettings>(File.ReadAllText(_settingsPath)) ?? new NeckAssistSettings();
            }
        }
        catch { }
        return new NeckAssistSettings();
    }

    private void SaveSettings(bool applyBackend = true)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
            string json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
            string temporary = _settingsPath + ".tmp";
            File.WriteAllText(temporary, json);
            File.Move(temporary, _settingsPath, true);
            if (applyBackend) _openXrBackend.Apply(_settings);
            UpdateCurvePreview();
        }
        catch { }
    }

    private void RefreshStatus()
    {
        string runtime = DetectRuntime();
        _runtimeStatus.Text = string.Format(CultureInfo.CurrentCulture, T("Neck.Runtime"), runtime);

        bool openXrBackend = _openXrBackend.FilesAvailable && _openXrBackend.IsRegistered;
        string backendFolder = Path.Combine(AppContext.BaseDirectory, "NeckAssist");
        bool steamVrBackend = File.Exists(Path.Combine(backendFolder, "WTVRNeckAssistSteamVR.exe"));
        bool legacyOpenVr = runtime.Contains("SteamVR (OpenVR)", StringComparison.OrdinalIgnoreCase);
        bool available = legacyOpenVr ? steamVrBackend : openXrBackend;
        bool connected = _openXrBackend.HasLiveTelemetry;
        if (!string.IsNullOrWhiteSpace(_openXrBackend.RegistrationError))
            _backendStatus.Text = string.Format(CultureInfo.CurrentCulture, T("Neck.OpenXrEnableFailed"), _openXrBackend.RegistrationError);
        else if (!_settings.Enabled)
            _backendStatus.Text = T("Neck.OffStatus");
        else if (connected)
            _backendStatus.Text = T("Neck.LiveStatus");
        else if (available)
            _backendStatus.Text = T("Neck.NoTelemetryStatus");
        else
            _backendStatus.Text = _openXrBackend.FilesAvailable ? T("Neck.NotRegisteredStatus") : T("Neck.MissingBackendStatus");
        _backendStatus.ForeColor = connected ? ThemePalette.FromArgb(110, 235, 125) : ThemePalette.FromArgb(255, 196, 80);
    }

    private void UpdateCurvePreview()
    {
        if (_curvePreview == null) return;
        _curvePreview.Points = _settings.YawPoints;
        _curvePreview.Bezier = _settings.YawBezier;
        if (_pitchPreview != null)
        {
            _pitchPreview.Bezier = _settings.PitchBezier;
            _pitchPreview.StartAngle = _settings.PitchStartAngle;
            _pitchPreview.ReturnAngle = _settings.PitchReturnAngle;
            _pitchPreview.NaturalResumeAngle = _settings.PitchNaturalResumeAngle;
            _pitchPreview.MaximumViewAngle = _settings.PitchMaximumViewAngle;
            _pitchPreview.Curvature = _settings.PitchCurvature;
            _pitchPreview.NaturalRearView = _settings.NaturalRearView;
            _pitchPreview.Invalidate();
        }
        _curvePreview.Curvature = _settings.YawCurvature;
        _curvePreview.StartAngle = _settings.StartAngle;
        _curvePreview.ReturnAngle = _settings.ReturnAngle;
        _curvePreview.NaturalResumeAngle = _settings.YawNaturalResumeAngle;
        _curvePreview.MaximumViewAngle = _settings.MaximumViewAngle;
        _curvePreview.NaturalRearView = _settings.NaturalRearView;
        _curvePreview.TransitionWidth = _settings.TransitionWidth;
        _curvePreview.UseSmoothstep = _settings.Curve == "Smoothstep";
        _curvePreview.Invalidate();
    }

    private void RefreshTelemetry()
    {
        try
        {
            if (_openXrBackend.FilesAvailable)
            {
                (float yaw, float pitch, float virtualYaw) = _openXrBackend.ReadTelemetry();
                if (_pitchPreview != null)
                {
                    _pitchPreview.PhysicalYaw = pitch;
                    _pitchPreview.VirtualYaw = _openXrBackend.ReadVirtualPitch();
                    _pitchPreview.HasTelemetry = _settings.PitchEnabled && _openXrBackend.HasLiveTelemetry;
                    _pitchPreview.Invalidate();
                }
                _curvePreview.PhysicalYaw = yaw;
                _curvePreview.AssistanceOn = _settings.Enabled;
                if (_pitchPreview != null) _pitchPreview.AssistanceOn = _settings.Enabled && _settings.PitchEnabled;
                _curvePreview.VirtualYaw = virtualYaw;
                _curvePreview.HasTelemetry = _openXrBackend.HasLiveTelemetry;
                _curvePreview.Invalidate();
                return;
            }

            string telemetryPath = Path.Combine(Path.GetDirectoryName(_settingsPath)!, "neck_assist_telemetry.json");
            if (!File.Exists(telemetryPath))
            {
                _curvePreview.HasTelemetry = false;
                _curvePreview.Invalidate();
                return;
            }

            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(telemetryPath));
            JsonElement root = document.RootElement;
            _curvePreview.PhysicalYaw = root.TryGetProperty("physicalYaw", out JsonElement physical) ? physical.GetSingle() : 0;
            _curvePreview.VirtualYaw = root.TryGetProperty("virtualYaw", out JsonElement virtualYawElement) ? virtualYawElement.GetSingle() : 0;
            _curvePreview.HasTelemetry = DateTime.UtcNow - File.GetLastWriteTimeUtc(telemetryPath) < TimeSpan.FromSeconds(3);
            _curvePreview.Invalidate();
        }
        catch
        {
            _curvePreview.HasTelemetry = false;
        }
    }

    private static bool IsVrProcessRunning() =>
        Process.GetProcessesByName("aces").Length > 0 ||
        Process.GetProcessesByName("aces_BE").Length > 0;

    private void RecenterNeck()
    {
        _toggleActive = false;
        _runtimeAssistanceActive = false;
        _openXrBackend.Recenter();
    }

    private void PollBindings()
    {
        if (_capturingBinding) return;
        bool togglePressed = HotasBindingDialog.IsBindingPressed(_settings.ActivationBinding);
        if (_settings.MovementMode == "Advanced" && _settings.AdvancedActivationBehavior == "Toggle" && togglePressed && !_lastActivationPressed) _toggleActive = !_toggleActive;
        _lastActivationPressed = togglePressed;
        if (!_settings.Enabled)
        {
            _runtimeAssistanceActive = false;
            _openXrBackend.SetHeld(true, _settings.PitchEnabled);
            return;
        }

        bool simplePressed = !HasAssignedBinding(_settings.SimpleActivationBinding) ||
                             HotasBindingDialog.IsBindingPressed(_settings.SimpleActivationBinding);
        _runtimeAssistanceActive = _settings.MovementMode == "Simple"
            ? simplePressed
            : _settings.AdvancedActivationBehavior == "Hold" ? togglePressed : _toggleActive;

        bool recenterPressed = HotasBindingDialog.IsBindingPressed(_settings.RecenterBinding);
        if (recenterPressed && !_lastRecenterPressed) RecenterNeck();
        _lastRecenterPressed = recenterPressed;

        _openXrBackend.SetHeld(false, _settings.PitchEnabled);
    }

    private static bool HasAssignedBinding(string? binding) =>
        !string.IsNullOrWhiteSpace(binding) &&
        !string.Equals(binding, "Not assigned", StringComparison.OrdinalIgnoreCase);

    private static string DetectRuntime()
    {
        try
        {
            if (Process.GetProcessesByName("vrserver").Length > 0 || Process.GetProcessesByName("vrmonitor").Length > 0)
            {
                return "SteamVR (running)";
            }

            using RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Khronos\OpenXR\1");
            string? manifest = key?.GetValue("ActiveRuntime") as string;
            if (!string.IsNullOrWhiteSpace(manifest))
            {
                string name = Path.GetFileName(manifest);
                if (manifest.Contains("VirtualDesktop", StringComparison.OrdinalIgnoreCase) || manifest.Contains("VDXR", StringComparison.OrdinalIgnoreCase)) return "VDXR / OpenXR";
                if (manifest.Contains("SteamVR", StringComparison.OrdinalIgnoreCase)) return "SteamVR OpenXR";
                if (manifest.Contains("Oculus", StringComparison.OrdinalIgnoreCase)) return "Meta OpenXR";
                return "OpenXR (" + name + ")";
            }
        }
        catch { }

        return "not detected";
    }
}

internal sealed class LegacyNeckCurvePreview : Control
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Curvature { get; set; } = 100;
    public event EventHandler? MappingChanged;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public List<NeckCurvePoint> Points { get; set; } = new();
    private int _dragPoint = -1;
    private PointF PointPosition(NeckCurvePoint p) => new(MapX(StartAngle + p.Input * (110 - StartAngle), Plot), MapY(StartAngle + p.Input * (110 - StartAngle) + Math.Max(0, MaximumViewAngle - 110) * p.Output, Plot));

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        if (e.Button != MouseButtons.Left || !Plot.Contains(e.Location)) return;
        float angle = Math.Abs((e.X - Plot.Left) * 240f / Plot.Width - 120);
        float input = Math.Clamp((angle - StartAngle) / Math.Max(1, 110 - StartAngle), 0.03f, 0.97f);
        if (Points.Any(p => Math.Abs(p.Input - input) < 0.025f)) return;
        Points.Add(new NeckCurvePoint { Input = input, Output = input });
        Points.Sort((a,b) => a.Input.CompareTo(b.Input));
        _dragPoint = Points.FindIndex(p => p.Input == input);
        MovePoint(e);
        _dragPoint = -1;
    }

    private void MovePoint(MouseEventArgs e)
    {
        if (_dragPoint < 0 || _dragPoint >= Points.Count) return;
        float angle = Math.Abs((e.X - Plot.Left) * 240f / Plot.Width - 120);
        float view = Math.Abs(220 - (e.Y - Plot.Top) * 440f / Plot.Height);
        NeckCurvePoint p = Points[_dragPoint];
        float lo = _dragPoint == 0 ? 0.01f : Points[_dragPoint - 1].Input + 0.01f;
        float hi = _dragPoint == Points.Count - 1 ? 0.99f : Points[_dragPoint + 1].Input - 0.01f;
        p.Input = Math.Clamp((angle - StartAngle) / Math.Max(1, 110 - StartAngle), lo, hi);
        p.Output = Math.Clamp((view - angle) / Math.Max(1, MaximumViewAngle - 110), _dragPoint == 0 ? 0 : Points[_dragPoint - 1].Output, _dragPoint == Points.Count - 1 ? 1 : Points[_dragPoint + 1].Output);
        MappingChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }
    private bool _draggingStart;
    private bool _draggingEnd;
    private Rectangle Plot => new(48, 24, Math.Max(10, Width - 70), Math.Max(10, Height - 70));

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        _dragPoint = Points.FindIndex(p => { PointF pos = PointPosition(p); return Math.Abs(pos.X - e.X) < 18 && Math.Abs(pos.Y - e.Y) < 18; });
        if (_dragPoint >= 0)
        {
            if (e.Button == MouseButtons.Right) { Points.RemoveAt(_dragPoint); _dragPoint = -1; MappingChanged?.Invoke(this, EventArgs.Empty); Invalidate(); }
            else if (e.Button == MouseButtons.Left) Capture = true;
            return;
        }
        if (e.Button != MouseButtons.Left) return;
        Rectangle plot = Plot;
        _draggingStart = Math.Abs(e.X - MapX(StartAngle, plot)) < 22 && Math.Abs(e.Y - MapY(StartAngle, plot)) < 22;
        _draggingEnd = !_draggingStart && Math.Abs(e.X - MapX(110, plot)) < 22 && Math.Abs(e.Y - MapY(MaximumViewAngle, plot)) < 22;
        if (false && !_draggingStart && !_draggingEnd && plot.Contains(e.Location))
        {
            // Clicking either half edits its mirrored mapping; near the centre
            // edits the activation threshold, farther out edits amplification.
            float physical = Math.Abs((e.X - plot.Left) * 240f / plot.Width - 120);
            _draggingStart = physical < 75;
            _draggingEnd = !_draggingStart;
        }
        Capture = _draggingStart || _draggingEnd;
        OnMouseMove(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragPoint >= 0) { MovePoint(e); return; }
        if (!_draggingStart && !_draggingEnd) return;
        Rectangle plot = Plot;
        if (_draggingStart) StartAngle = Math.Clamp((int)Math.Round(Math.Abs((e.X - plot.Left) * 240d / plot.Width - 120)), 10, 100);
        if (_draggingEnd) MaximumViewAngle = Math.Clamp((int)Math.Round(Math.Abs(220 - (e.Y - plot.Top) * 440d / plot.Height)), 110, 220);
        MappingChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _draggingStart = _draggingEnd = false;
        _dragPoint = -1;
        Capture = false;
    }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int StartAngle { get; set; } = 45;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int MaximumViewAngle { get; set; } = 180;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int TransitionWidth { get; set; } = 15;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool UseSmoothstep { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool HasTelemetry { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public float PhysicalYaw { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public float VirtualYaw { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string LanguageCode { get; set; } = "en";

    public LegacyNeckCurvePreview()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics graphics = e.Graphics;
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        Rectangle plot = Plot;
        using Pen grid = new(ThemePalette.FromArgb(55, 78, 84), 1);
        using Pen axis = new(ThemePalette.FromArgb(110, 135, 140), 1);
        using Pen curve = new(ThemePalette.FromArgb(75, 235, 125), 2.5f);
        using Pen threshold = new(ThemePalette.FromArgb(255, 190, 70), 1) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
        using Brush text = new SolidBrush(ThemePalette.FromArgb(195, 210, 214));
        string T(string key) => AppText.T(LanguageCode, key);

        graphics.DrawRectangle(grid, plot);
        int centerX = plot.Left + plot.Width / 2;
        int centerY = plot.Top + plot.Height / 2;
        graphics.DrawLine(axis, centerX, plot.Top, centerX, plot.Bottom);
        graphics.DrawLine(axis, plot.Left, centerY, plot.Right, centerY);

        using Font axisFont = new("Segoe UI", 7.5f, FontStyle.Bold);
        graphics.DrawString(T("Neck.HeadTurn"), axisFont, text, plot.Right - 88, centerY + 3);
        graphics.TranslateTransform(plot.Left + 3, plot.Top + 104);
        graphics.RotateTransform(-90);
        graphics.DrawString(T("Neck.VirtualView"), axisFont, text, 0, 0);
        graphics.ResetTransform();

        int leftThreshold = MapX(-StartAngle, plot);
        int rightThreshold = MapX(StartAngle, plot);
        graphics.DrawLine(threshold, leftThreshold, plot.Top, leftThreshold, plot.Bottom);
        graphics.DrawLine(threshold, rightThreshold, plot.Top, rightThreshold, plot.Bottom);
        graphics.DrawString(T("Neck.AssistStarts"), axisFont, text, rightThreshold + 3, plot.Top + 3);
        using Brush amber = new SolidBrush(ThemePalette.FromArgb(255, 190, 70));
        using Brush green = new SolidBrush(ThemePalette.FromArgb(75, 235, 125));
        graphics.FillEllipse(amber, MapX(StartAngle, plot) - 7, MapY(StartAngle, plot) - 7, 14, 14);
        graphics.FillEllipse(green, MapX(110, plot) - 7, MapY(MaximumViewAngle, plot) - 7, 14, 14);
        foreach (NeckCurvePoint p in Points)
        {
            PointF pos = PointPosition(p);
            graphics.FillEllipse(green, pos.X - 7, pos.Y - 7, 14, 14);
        }
        foreach (int degrees in new[] { -90, -45, 0, 45, 90 })
            graphics.DrawString(degrees + "°", axisFont, text, MapX(degrees, plot) - 10, plot.Bottom + 3);

        PointF? previous = null;
        for (int physical = -110; physical <= 110; physical += 2)
        {
            float output = MapPhysicalToVirtual(physical);
            PointF point = new(MapX(physical, plot), MapY(output, plot));
            if (previous.HasValue) graphics.DrawLine(curve, previous.Value, point);
            previous = point;
        }

        if (HasTelemetry)
        {
            float shownVirtual = MapPhysicalToVirtual(PhysicalYaw);
            using Pen physicalPen = new(ThemePalette.FromArgb(255, 80, 80), 2);
            using Pen modifiedPen = new(ThemePalette.FromArgb(75, 235, 125), 2);
            int physicalX = Math.Clamp(MapX(PhysicalYaw, plot), plot.Left, plot.Right);
            int rawY = Math.Clamp(MapY(PhysicalYaw, plot), plot.Top, plot.Bottom);
            int adjustedY = Math.Clamp(MapY(shownVirtual, plot), plot.Top, plot.Bottom);
            graphics.DrawLine(physicalPen, physicalX, plot.Top, physicalX, plot.Bottom);
            graphics.DrawLine(physicalPen, plot.Left, rawY, physicalX, rawY);
            graphics.DrawLine(modifiedPen, physicalX, adjustedY, plot.Right, adjustedY);
            PointF marker = new(MapX(PhysicalYaw, plot), MapY(shownVirtual, plot));
            using Brush markerBrush = new SolidBrush(Color.White);
            graphics.FillEllipse(markerBrush, marker.X - 5, marker.Y - 5, 10, 10);
            graphics.DrawString(string.Format(CultureInfo.CurrentCulture, T("Neck.TelemetryCaption"), PhysicalYaw, shownVirtual, VirtualYaw), Font, text, 6, Height - 23);
        }
        else
        {
            graphics.DrawString(T("Neck.WaitingTelemetry"), Font, text, 6, Height - 23);
        }
    }

    private float MapPhysicalToVirtual(float physical)
    {
        return NeckCurveMapping.Evaluate(physical, StartAngle, MaximumViewAngle, Points, Curvature);
    }

    private static int MapX(float degrees, Rectangle plot) => plot.Left + (int)((degrees + 120f) / 240f * plot.Width);
    private static int MapY(float degrees, Rectangle plot) => plot.Bottom - (int)((degrees + 220f) / 440f * plot.Height);
}

internal sealed class HotasBindingDialog : Form
{
    [StructLayout(LayoutKind.Sequential)]
    private struct JoyInfoEx
    {
        public int Size;
        public int Flags;
        public int X; public int Y; public int Z; public int R; public int U; public int V;
        public int Buttons;
        public int ButtonNumber;
        public int Pov;
        public int Reserved1;
        public int Reserved2;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct JoyCaps
    {
        public ushort Mid;
        public ushort Pid;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string ProductName;
        public uint XMin, XMax, YMin, YMax, ZMin, ZMax, NumButtons, PeriodMin, PeriodMax, RMin, RMax, UMin, UMax, VMin, VMax, Caps, MaxAxes, NumAxes, MaxButtons;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string RegKey;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string OemVxd;
    }

    [DllImport("winmm.dll")]
    private static extern int joyGetNumDevs();

    [DllImport("winmm.dll")]
    private static extern int joyGetPosEx(int joystickId, ref JoyInfoEx info);

    [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
    private static extern int joyGetDevCapsW(int joystickId, ref JoyCaps caps, int size);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    private const int JoyReturnButtons = 0x80;
    private readonly Label _status;
    private readonly Button _save;
    private string _pendingBinding = "";
    private readonly System.Windows.Forms.Timer _poll = new() { Interval = 40 };
    private string _candidate = "";
    private readonly string _languageCode;
    private bool _captureArmed;
    private HashSet<string>? _ignoredHeld;
    public string Binding { get; private set; } = "";

    public HotasBindingDialog(string instruction, string languageCode = "en")
    {
        _languageCode = AppText.Normalize(languageCode);
        Text = T("Binding.Title");
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(560, 290);
        BackColor = ThemePalette.FromArgb(5, 12, 14);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 11, FontStyle.Regular);

        Label heading = new()
        {
            Text = instruction,
            Font = new Font("Segoe UI", 13, FontStyle.Bold),
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter
        };
        heading.SetBounds(24, 16, 512, 64);
        Controls.Add(heading);

        _status = new Label
        {
            Text = T("Binding.ReadyStatus"),
            Font = new Font("Segoe UI", 11),
            ForeColor = ThemePalette.FromArgb(130, 230, 140),
            TextAlign = ContentAlignment.MiddleCenter
        };
        _status.SetBounds(30, 86, 500, 110);
        Controls.Add(_status);

        _save = new ThemeButton { Text = T("Binding.Save"), Enabled = false };
        _save.SetBounds(145, 224, 130, 42);
        _save.Click += (_, _) =>
        {
            string captured = _pendingBinding.Length > 0 ? _pendingBinding : _candidate;
            if (string.IsNullOrWhiteSpace(captured)) return;
            Binding = captured;
            DialogResult = DialogResult.OK;
            Close();
        };
        Controls.Add(_save);
        AcceptButton = _save;
        ThemeButton cancel = new() { Text = T("Binding.Cancel"), DialogResult = DialogResult.Cancel };
        cancel.SetBounds(285, 224, 130, 42);
        Controls.Add(cancel);
        CancelButton = cancel;

        Shown += (_, _) => _poll.Start();
        FormClosed += (_, _) => _poll.Stop();
        _poll.Tick += (_, _) => PollJoysticks();
    }

    private string T(string key) => AppText.T(_languageCode, key);

    private void PollJoysticks()
    {
        List<string> pressed = CurrentPressedBindings();
        // Dialog command clicks must not become part of a held HOTAS chord.
        if (Controls.OfType<Button>().Any(button => button.RectangleToScreen(button.ClientRectangle).Contains(Cursor.Position)))
            pressed.RemoveAll(p => p.StartsWith("MOUSE:", StringComparison.OrdinalIgnoreCase));

        if (!_captureArmed)
        {
            _ignoredHeld = pressed.ToHashSet(StringComparer.OrdinalIgnoreCase);
            _captureArmed = true;
            _status.Text = T("Binding.ReadyStatus");
            return;
        }

        _ignoredHeld ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _ignoredHeld.RemoveWhere(held => !pressed.Contains(held, StringComparer.OrdinalIgnoreCase));
        List<string> freshPressed = pressed
            .Where(input => !_ignoredHeld.Contains(input))
            .ToList();

        string current = string.Join(" + ", freshPressed);
        if (current.Length == 0)
        {
            if (_candidate.Length > 0)
            {
                _pendingBinding = _candidate;
                _status.Text = string.Format(CultureInfo.CurrentCulture, T("Binding.SaveStatus"), HotasDirectInput.DisplayBinding(_pendingBinding));
                _save.Enabled = true;
            }
            _candidate = "";
            return;
        }

        // A fresh attempt replaces the last capture; Save/Cancel clicks are not bindings.
        if (_pendingBinding.Length > 0)
        {
            if (freshPressed.All(p => p.StartsWith("MOUSE:", StringComparison.OrdinalIgnoreCase))) return;
            _pendingBinding = ""; _candidate = ""; _save.Enabled = false;
        }

        // Keep the fullest simultaneous press until every button is released.
        // This captures a short single tap without a hold-duration requirement,
        // and preserves a chord while its buttons are released one at a time.
        if (current.Split(" + ").Length >= _candidate.Split(" + ").Length || _candidate.Length == 0)
            _candidate = current;
        _save.Enabled = _candidate.Length > 0;
        _status.Text = string.Format(CultureInfo.CurrentCulture, T("Binding.ReleaseStatus"), HotasDirectInput.DisplayBinding(_candidate));
    }

    private static List<string> CurrentPressedBindings()
    {
        List<string> pressed = new();
        int[] mouseKeys = { 1, 2, 4, 5, 6 };
        for (int vk = 1; vk < 255; vk++)
        {
            // Hidden Keybinds can inject a keyboard shortcut from a HOTAS button.
            // Do not turn that synthetic shortcut into part of a Neck Assist binding.
            if ((GetAsyncKeyState(vk) & 0x8000) == 0 || SyntheticKeyGuard.IsSuppressed(vk)) continue;
            if (mouseKeys.Contains(vk))
            {
                string mouse = vk switch { 1 => "Left", 2 => "Right", 4 => "Middle", 5 => "X1", 6 => "X2", _ => vk.ToString() };
                pressed.Add("MOUSE:" + mouse);
            }
            else pressed.Add("KEY:" + ((Keys)vk & Keys.KeyCode));
        }

        pressed.AddRange(HotasDirectInput.ReadPressed());
        return pressed;
    }

    private static List<string> LegacyJoystickBindings()
    {
        List<string> pressed = new();
        int devices = Math.Min(16, joyGetNumDevs());
        for (int joystick = 0; joystick < devices; joystick++)
        {
            if (IsVirtualOutputJoystick(joystick)) continue;

            JoyInfoEx info = new() { Size = Marshal.SizeOf<JoyInfoEx>(), Flags = JoyReturnButtons };
            if (joyGetPosEx(joystick, ref info) != 0) continue;
            uint buttons = unchecked((uint)info.Buttons);
            for (int bit = 0; bit < 32; bit++)
            {
                if ((buttons & (1u << bit)) != 0) pressed.Add($"JOY{joystick + 1}:B{bit + 1}");
            }
        }

        return pressed;
    }

    public static bool IsBindingPressed(string binding)
    {
        if (string.IsNullOrWhiteSpace(binding) || binding == "Not assigned") return false;
        string[] tokens = binding.Split(" + ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0) return false;

        Dictionary<int, uint> states = new();
        HashSet<string>? direct = null;
        foreach (string token in tokens)
        {
            if (token.StartsWith("DI:", StringComparison.OrdinalIgnoreCase))
            {
                direct ??= HotasDirectInput.ReadPressed();
                if (!direct.Contains(token)) return false;
                continue;
            }
            if (token.StartsWith("KEY:", StringComparison.OrdinalIgnoreCase))
            {
                if (!Enum.TryParse(token[4..], true, out Keys key) || (GetAsyncKeyState((int)(key & Keys.KeyCode)) & 0x8000) == 0) return false;
                continue;
            }
            if (token.StartsWith("MOUSE:", StringComparison.OrdinalIgnoreCase))
            {
                int vk = token[6..].ToUpperInvariant() switch { "LEFT" => 1, "RIGHT" => 2, "MIDDLE" => 4, "X1" => 5, "X2" => 6, _ => 0 };
                if (vk == 0 || (GetAsyncKeyState(vk) & 0x8000) == 0) return false;
                continue;
            }
            Match match = System.Text.RegularExpressions.Regex.Match(token, @"^JOY(?<joy>\d+):B(?<button>\d+)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!match.Success || !int.TryParse(match.Groups["joy"].Value, out int joyNumber) ||
                !int.TryParse(match.Groups["button"].Value, out int buttonNumber) || buttonNumber is < 1 or > 32) return false;

            int joystickId = joyNumber - 1;
            if (!states.TryGetValue(joystickId, out uint buttons))
            {
                if (IsVirtualOutputJoystick(joystickId)) return false;

                JoyInfoEx info = new() { Size = Marshal.SizeOf<JoyInfoEx>(), Flags = JoyReturnButtons };
                if (joyGetPosEx(joystickId, ref info) != 0) return false;
                buttons = unchecked((uint)info.Buttons);
                states[joystickId] = buttons;
            }

            if ((buttons & (1u << (buttonNumber - 1))) == 0) return false;
        }
        return true;
    }

    private static bool IsVirtualOutputJoystick(int joystickId)
    {
        JoyCaps caps = new();
        if (joyGetDevCapsW(joystickId, ref caps, Marshal.SizeOf<JoyCaps>()) != 0)
            return true;

        return IsVirtualOutputJoystickName(caps.ProductName) ||
               IsVirtualOutputJoystickName(caps.RegKey);
    }

    internal static string DisplayLegacyJoystick(string token)
    {
        Match match = Regex.Match(token, @"^JOY(?<joy>\d+):B(?<button>\d+)$", RegexOptions.IgnoreCase);
        if (!match.Success || !int.TryParse(match.Groups["joy"].Value, out int number) || number < 1)
            return "Controller input";
        JoyCaps caps = new();
        if (joyGetDevCapsW(number - 1, ref caps, Marshal.SizeOf<JoyCaps>()) != 0)
            return $"Button {match.Groups["button"].Value} (disconnected)";
        if (IsVirtualOutputJoystick(number - 1)) return "Virtual output blocked - bind physical HOTAS";
        return $"Button {match.Groups["button"].Value}";
    }

    private static bool IsVirtualOutputJoystickName(string? name) =>
        !string.IsNullOrWhiteSpace(name) &&
        name.Contains("vJoy", StringComparison.OrdinalIgnoreCase);
}

internal static class SyntheticKeyGuard
{
    private static readonly Dictionary<int, long> SuppressedUntil = new();
    private static readonly object Sync = new();

    public static void Suppress(int virtualKey, int milliseconds = 400)
    {
        long until = Environment.TickCount64 + milliseconds;
        lock (Sync) SuppressedUntil[virtualKey] = until;
    }

    public static bool IsSuppressed(int virtualKey)
    {
        lock (Sync)
        {
            if (!SuppressedUntil.TryGetValue(virtualKey, out long until)) return false;
            if (Environment.TickCount64 <= until) return true;
            SuppressedUntil.Remove(virtualKey);
            return false;
        }
    }
}
