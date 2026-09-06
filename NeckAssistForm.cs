using Microsoft.Win32;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WTVRSettingsAssistant;

internal sealed class AccentTrackBar : TrackBar
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color Accent { get; set; } = Color.FromArgb(255, 190, 70);
    public AccentTrackBar()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        ValueChanged += (_, _) => Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor ?? BackColor);
        int left = 12, right = Math.Max(13, Width - 13), y = Height / 2;
        int x = left + (int)((Value - Minimum) / (float)Math.Max(1, Maximum - Minimum) * (right - left));
        using Pen rail = new(Color.FromArgb(70, 85, 88), 5);
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
}

internal sealed class NeckAssistForm : Form
{
    public event EventHandler? CloseRequested;
    private Panel? _navigation;
    public void ConfigureNavigation(Image home, Image info, Action showInfo)
    {
        if (_navigation == null) return;
        PictureBox homeButton = new() { Image = home, SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(76, 70), Cursor = Cursors.Hand, Anchor = AnchorStyles.Top | AnchorStyles.Right };
        PictureBox infoButton = new() { Image = info, SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(58, 58), Cursor = Cursors.Hand, Anchor = AnchorStyles.Top | AnchorStyles.Right };
        homeButton.Click += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);
        infoButton.Click += (_, _) => showInfo();
        _navigation.Controls.AddRange(new Control[] { homeButton, infoButton });
        void PlaceNavigation() { homeButton.Location = new Point(_navigation.Width - 92, 4); infoButton.Location = new Point(_navigation.Width - 164, 10); }
        _navigation.SizeChanged += (_, _) => PlaceNavigation();
        PlaceNavigation();
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
    private readonly ComboBox _activationMode;
    private readonly System.Windows.Forms.Timer _statusTimer = new() { Interval = 1500 };
    private bool _lastActivationPressed;
    private bool _lastRecenterPressed;
    private bool _toggleActive;
    private int _statusTicks;
    private bool _capturingBinding;

    public NeckAssistForm(string appFolder)
    {
        _settingsPath = Path.Combine(appFolder, "Settings", "neck_assist.json");
        _settings = LoadSettings();
        _settings.YawBezier ??= new();
        _settings.PitchBezier ??= new();
        _settings.YawBezier.Validate();
        _settings.PitchBezier.Validate();
        _openXrBackend = new OpenXrNeckBackend(appFolder);

        Text = "Neck Rotation Assistance";
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ClientSize = new Size(460, Math.Min(880, Screen.PrimaryScreen?.WorkingArea.Height - 90 ?? 800));
        AutoScroll = true;
        AutoScrollMinSize = new Size(0, 1510);
        BackColor = Color.FromArgb(5, 12, 14);
        ForeColor = Color.FromArgb(235, 238, 240);
        ShowInTaskbar = false;
        TopMost = false;

        Button closeButton = MakeButton("×");
        closeButton.Font = new Font("Segoe UI", 15, FontStyle.Bold);
        closeButton.SetBounds(414, 12, 34, 34);
        closeButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        closeButton.Click += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);
        Controls.Add(closeButton);

        Label title = MakeLabel("NECK ROTATION", 24, FontStyle.Bold);
        title.SetBounds(20, 14, 410, 58);
        Controls.Add(title);

        Label warning = MakeLabel("Gradually increases virtual yaw after the activation angle. Start conservatively—strong amplification can cause discomfort.", 10, FontStyle.Italic);
        warning.SetBounds(20, 74, 410, 68);
        Controls.Add(warning);

        _enabled = new CheckBox
        {
            Text = "Neck assistance ON / OFF",
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            Checked = _settings.Enabled,
            Location = new Point(20, 148)
        };
        _enabled.CheckedChanged += (_, _) =>
        {
            _settings.Enabled = _enabled.Checked;
            SaveSettings();
            _openXrBackend.SetHeld(!_settings.Enabled, _settings.PitchEnabled);
            RefreshStatus();
        };
        Controls.Add(_enabled);

        _runtimeStatus = MakeLabel("Runtime: detecting…", 10, FontStyle.Bold);
        _runtimeStatus.SetBounds(20, 190, 410, 32);
        Controls.Add(_runtimeStatus);

        _backendStatus = MakeLabel("Backend: not installed", 10, FontStyle.Regular);
        _backendStatus.ForeColor = Color.FromArgb(255, 196, 80);
        _backendStatus.SetBounds(20, 222, 410, 52);
        Controls.Add(_backendStatus);

        _curvePreview = new NeckCurvePreview
        {
            BackColor = Color.FromArgb(8, 18, 21),
            ForeColor = Color.White
        };
        _curvePreview.SetBounds(20, 280, 410, 150);
        Controls.Add(_curvePreview);

        AddDivider(444);
        int y = 460;
        Label yawHeading = MakeLabel("LEFT / RIGHT (YAW)", 12, FontStyle.Bold);
        yawHeading.ForeColor = Color.FromArgb(125, 225, 240);
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
            BackColor = Color.FromArgb(16, 29, 33),
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
        pitchHeading.ForeColor = Color.FromArgb(125, 225, 240);
        pitchHeading.SetBounds(20, y + 8, 280, 36);
        Controls.Add(pitchHeading);
        CheckBox pitchEnabled = new()
        {
            Text = "Enable",
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            Checked = _settings.PitchEnabled,
            Location = new Point(345, y + 12)
        };
        pitchEnabled.CheckedChanged += (_, _) => { _settings.PitchEnabled = pitchEnabled.Checked; SaveSettings(); };
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

        _positionCompensation = new CheckBox
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
        _recenterBind.Click += (_, _) => CaptureBinding(true);
        Controls.Add(_recenterBind);

        Label recenterHint = MakeLabel("Set this exact same HOTAS combo as War Thunder's VR recenter command.", 8.5f, FontStyle.Italic);
        recenterHint.ForeColor = Color.FromArgb(255, 205, 110);
        recenterHint.SetBounds(20, bindingY + 44, 410, 38);
        Controls.Add(recenterHint);

        // Activation is controlled by the main ON/OFF switch. Keep these objects only
        // for settings-file compatibility with earlier test builds.
        _activationBind = MakeButton(FormatBinding(_settings.ActivationBinding));
        _activationMode = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 10),
            BackColor = Color.FromArgb(16, 29, 33),
            ForeColor = Color.White
        };
        _activationMode.Items.Add("Main ON / OFF switch");
        _activationMode.SelectedIndex = 0;

        Label activationHint = MakeLabel("Use the ON / OFF switch above to activate or completely pause assistance. Only the recenter HOTAS combo needs to be bound.", 9, FontStyle.Italic);
        activationHint.ForeColor = Color.FromArgb(125, 225, 240);
        activationHint.SetBounds(20, bindingY + 91, 410, 58);
        Controls.Add(activationHint);

        Label hint = MakeLabel("Example: 45° activation and 180° maximum maps the remaining physical turn progressively toward a rear view.", 9, FontStyle.Italic);
        hint.ForeColor = Color.FromArgb(180, 195, 200);
        hint.SetBounds(20, bindingY + 150, 410, 45);
        Controls.Add(hint);

        Button clearRecenter = MakeButton("CLEAR");
        clearRecenter.SetBounds(340, bindingY + 7, 90, 34);
        _recenterBind.SetBounds(190, bindingY + 7, 140, 34);
        clearRecenter.Click += (_, _) =>
        {
            _settings.RecenterBinding = "Not assigned";
            _recenterBind.Text = "BIND…";
            _lastRecenterPressed = false;
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
        title.Text = "NECK ROTATION ASSISTANCE";
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
        title.Left = 20;

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
        activationHint.Text = "Use the ON / OFF switch or your toggle combo. Press the combo once to enable assistance, again to disable it.";
        Label toggleLabel = MakeLabel("NECK ASSIST TOGGLE", 11, FontStyle.Bold);
        toggleLabel.SetBounds(20, 215, 220, 36);
        _activationBind.SetBounds(250, 215, 260, 38);
        _activationBind.Click += (_, _) => CaptureBinding(false);
        Button clearToggle = MakeButton("CLEAR");
        clearToggle.SetBounds(522, 215, 100, 38);
        clearToggle.Click += (_, _) => { _settings.ActivationBinding = "Not assigned"; _activationBind.Text = "BIND…"; _lastActivationPressed = false; SaveSettings(); };
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
        dashboard.RowStyles.Add(new RowStyle(SizeType.Absolute, 228));
        dashboard.RowStyles.Add(new RowStyle(SizeType.Absolute, 170));
        overview.Dock = DockStyle.Fill;
        overview.Margin = Padding.Empty;
        warning.Visible = false;
        _enabled.SetBounds(12, 0, 300, 30);
        _runtimeStatus.Visible = false;
        _backendStatus.SetBounds(12, 34, 1000, 30);
        graphHelp.Text = "Drag yellow to activate, orange to release, and green to set maximum view. Sliders stay synchronized.";
        overview.SizeChanged += (_, _) =>
        {
            _backendStatus.Width = Math.Max(200, overview.Width - 24);
            _curvePreview.SetBounds(12, 70, Math.Max(200, overview.Width - 24), Math.Max(80, overview.Height - 124));
            graphHelp.SetBounds(12, overview.Height - 50, overview.Width - 24, 50);
        };
        TableLayoutPanel compactSliders = new() { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 2, Margin = Padding.Empty };
        for (int i = 0; i < 5; i++) compactSliders.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        for (int i = 0; i < 2; i++) compactSliders.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        TrackBar[] visibleSliders = { _startAngle, _returnAngle, yawResume, _maximumAngle, _smoothing, pitchStart, pitchReturn, pitchResume, pitchMaximum, pitchSmoothing };
        string[] names = { "Left/right: boost starts", "Left/right: release", "Natural motion resumes", "Left/right: maximum view", "Boost softness", "Up/down: boost starts", "Up/down: release", "Natural pitch resumes", "Up/down: maximum view", "Pitch boost softness" };
        ToolTip help = new() { AutoPopDelay = 20000, InitialDelay = 300 };
        Disposed += (_, _) => help.Dispose();
        string[] descriptions = { "Yellow: normal 1:1 motion ends and the rear-view boost begins.", "Orange: assistance releases here when returning to center.", "Cyan: the boost is complete and natural 1:1 movement continues with the added offset.", "Green: final view angle produced by the boost.", "Controls how gently the boost accelerates and settles.", "Yellow pitch boost start.", "Orange pitch release.", "Cyan: natural 1:1 pitch movement resumes.", "Green: maximum pitch view.", "Controls pitch boost softness." };
        for (int i = 0; i < visibleSliders.Length; i++)
        {
            TrackBar bar = visibleSliders[i];
            Panel card = (Panel)bar.Parent!;
            card.Dock = DockStyle.Fill;
            card.Margin = new Padding(4);
            Label caption = card.Controls.OfType<Label>().First(l => l.TextAlign != ContentAlignment.MiddleRight);
            Label value = card.Controls.OfType<Label>().First(l => l.TextAlign == ContentAlignment.MiddleRight);
            caption.Text = names[i];
            Color accent = i % 5 == 3 ? Color.FromArgb(75, 235, 125) : i % 5 == 2 ? Color.FromArgb(70, 220, 240) : i % 5 == 1 ? Color.DarkOrange : Color.FromArgb(255, 190, 70);
            caption.ForeColor = value.ForeColor = accent;
            if (bar is AccentTrackBar colored) colored.Accent = accent;
            caption.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            void FitCard() { caption.SetBounds(6, 0, Math.Max(120, card.Width - 75), 30); value.SetBounds(card.Width - 65, 0, 60, 30); bar.SetBounds(0, 30, card.Width, 48); }
            card.SizeChanged += (_, _) => FitCard();
            help.SetToolTip(bar, descriptions[i]); help.SetToolTip(caption, descriptions[i]);
            compactSliders.Controls.Add(card, i % 5, i / 5);
        }
        bindings.Dock = DockStyle.Fill;
        foreach (Control c in bindings.Controls) c.Visible = false;
        recenterLabel.Visible = _recenterBind.Visible = clearRecenter.Visible = toggleLabel.Visible = _activationBind.Visible = clearToggle.Visible = true;
        recenterLabel.SetBounds(12, 4, 235, 36); _recenterBind.SetBounds(250, 4, 250, 38); clearRecenter.SetBounds(515, 4, 100, 38);
        toggleLabel.SetBounds(12, 54, 235, 36); _activationBind.SetBounds(250, 54, 250, 38); clearToggle.SetBounds(515, 54, 100, 38);
        pitchEnabled.Text = "Enable up/down assistance";
        bindings.Controls.Add(pitchEnabled);
        pitchEnabled.AutoSize = false;
        pitchEnabled.SetBounds(640, 8, 320, 38);
        Label bindingHelp = MakeLabel("Recenter: use the same combo in War Thunder. Toggle: press once ON, again OFF. Clear removes the binding.", 9, FontStyle.Regular);
        bindingHelp.SetBounds(12, 108, 1100, 44); bindings.Controls.Add(bindingHelp);
        help.SetToolTip(_recenterBind, "Capture fresh button presses to replace the recenter combo. Buttons already held are ignored until released.");
        help.SetToolTip(_activationBind, "Bind a combo to switch Neck Assist ON or OFF without using the mouse.");
        help.SetToolTip(clearRecenter, "Remove the recenter binding."); help.SetToolTip(clearToggle, "Remove the ON/OFF binding.");
        dashboard.Controls.Add(overview, 0, 0); dashboard.Controls.Add(compactSliders, 0, 1); dashboard.Controls.Add(bindings, 0, 2);
        Controls.Add(dashboard); dashboard.BringToFront(); header.BringToFront();
        dashboard.Dock = DockStyle.None;
        void FitDashboard() => dashboard.SetBounds(0, header.Height, ClientSize.Width, Math.Max(480, ClientSize.Height - header.Height));
        ClientSizeChanged += (_, _) => FitDashboard();
        FitDashboard();

        _pitchPreview = new NeckCurvePreview
        {
            AxisTitle = "VERTICAL · UP / DOWN", PhysicalLimit = 80, ViewLimit = 140,
            Bezier = _settings.PitchBezier, StartAngle = _settings.PitchStartAngle, ReturnAngle = _settings.PitchReturnAngle, NaturalResumeAngle = _settings.PitchNaturalResumeAngle,
            MaximumViewAngle = _settings.PitchMaximumViewAngle, Curvature = _settings.PitchCurvature,
            BackColor = _curvePreview.BackColor, ForeColor = _curvePreview.ForeColor,
            Enabled = _settings.PitchEnabled
        };
        overview.Controls.Add(_pitchPreview);
        overview.Controls.Add(pitchEnabled);
        void LayoutGraphs()
        {
            pitchEnabled.SetBounds(420, 0, 300, 32);
            int width = Math.Max(240, overview.Width - 24);
            int graphHeight = Math.Max(125, (overview.Height - 122) / 2);
            _curvePreview.SetBounds(12, 70, width, graphHeight);
            _pitchPreview.SetBounds(12, 76 + graphHeight, width, graphHeight);
        }
        overview.SizeChanged += (_, _) => LayoutGraphs();
        LayoutGraphs();
        graphHelp.Text = "Yellow: activate • Orange: release • Green: maximum view • Red: headset • transition controls the easing";
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
        CheckBox linkAxes = new() { Text = _settings.LinkAxes ? "Link axes: ON" : "Link axes: OFF", Appearance = Appearance.Button, AutoSize = false, Checked = _settings.LinkAxes, ForeColor = Color.White, BackColor = Color.FromArgb(35, 55, 60), TextAlign = ContentAlignment.MiddleCenter };
        linkAxes.SetBounds(750, 0, 255, 32);
        overview.Controls.Add(linkAxes);
        CheckBox naturalRear = new() { Text = _settings.NaturalRearView ? "Rear-view boost: ON" : "Rear-view boost: OFF", Appearance = Appearance.Button, AutoSize = false, Checked = _settings.NaturalRearView, ForeColor = Color.White, BackColor = _settings.NaturalRearView ? Color.FromArgb(25, 95, 55) : Color.FromArgb(35, 55, 60), TextAlign = ContentAlignment.MiddleCenter };
        naturalRear.SetBounds(1020, 0, 255, 32); overview.Controls.Add(naturalRear);
        void ExplainMovementMode() => graphHelp.Text = naturalRear.Checked
            ? "REAR-VIEW BOOST: extra rotation is added early; after the boost, your head and view continue together at 1:1."
            : "STANDARD: extra rotation increases throughout the turn. Drag yellow/orange/green markers or use the matching sliders.";
        void SetRearControls() { yawResume.Enabled = naturalRear.Checked; pitchResume.Enabled = naturalRear.Checked && pitchEnabled.Checked; }
        naturalRear.CheckedChanged += (_, _) => { _settings.NaturalRearView = naturalRear.Checked; naturalRear.Text = naturalRear.Checked ? "Rear-view boost: ON" : "Rear-view boost: OFF"; naturalRear.BackColor = naturalRear.Checked ? Color.FromArgb(25, 95, 55) : Color.FromArgb(35, 55, 60); SetRearControls(); ExplainMovementMode(); SaveSettings(); };
        help.SetToolTip(naturalRear, "OFF: amplification grows across your full head turn. ON: the extra rear angle is added sooner, then normal 1:1 movement is preserved.");
        ExplainMovementMode();
        SetRearControls();
        bool syncingAxes = false;
        void SyncAxes(bool fromPitch)
        {
            if (!_settings.LinkAxes || syncingAxes) return;
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
        linkAxes.CheckedChanged += (_, _) => { _settings.LinkAxes = linkAxes.Checked; linkAxes.Text = linkAxes.Checked ? "Link axes: ON" : "Link axes: OFF"; linkAxes.BackColor = linkAxes.Checked ? Color.FromArgb(25, 95, 55) : Color.FromArgb(35, 55, 60); SyncAxes(false); SaveSettings(); };
        help.SetToolTip(linkAxes, "When linked, edits on either graph update both curves. Angles scale proportionally to each axis range. Vertical assistance still needs its own enable switch.");
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

        _statusTimer.Interval = 16;
        _statusTimer.Tick += (_, _) =>
        {
            PollBindings();
            _openXrBackend.UpdateMotion(_settings);
            RefreshTelemetry();
            if (++_statusTicks >= 25) { _statusTicks = 0; RefreshStatus(); }
        };
        Shown += (_, _) =>
        {
            _openXrBackend.Apply(_settings);
            UpdateCurvePreview();
            RefreshStatus();
            _statusTimer.Start();
        };
        FormClosed += (_, _) => { _statusTimer.Stop(); SaveSettings(); _openXrBackend.Dispose(); };
    }

    protected override bool ShowWithoutActivation => false;

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
        Panel divider = new() { BackColor = Color.FromArgb(62, 82, 88) };
        divider.SetBounds(20, y, 410, 1);
        Controls.Add(divider);
    }

    private static Label MakeLabel(string text, float size, FontStyle style) => new()
    {
        Text = text,
        Font = new Font("Segoe UI", size, style),
        ForeColor = Color.FromArgb(235, 238, 240),
        BackColor = Color.Transparent,
        AutoSize = false
    };

    private static Button MakeButton(string text) => new()
    {
        Text = text,
        Font = new Font("Segoe UI", 9, FontStyle.Bold),
        BackColor = Color.FromArgb(16, 29, 33),
        ForeColor = Color.White,
        FlatStyle = FlatStyle.Flat
    };

    private void CaptureBinding(bool recenter)
    {
        _capturingBinding = true;
        using HotasBindingDialog dialog = new(recenter ? "Press a RECENTER button or combination" : "Press a TOGGLE button or combination");
        DialogResult result;
        try { result = dialog.ShowDialog(this); }
        finally
        {
            _capturingBinding = false;
            _lastActivationPressed = true;
            _lastRecenterPressed = true;
        }
        if (result != DialogResult.OK || string.IsNullOrWhiteSpace(dialog.Binding)) return;

        if (recenter)
        {
            _settings.RecenterBinding = dialog.Binding;
            _recenterBind.Text = FormatBinding(dialog.Binding);
        }
        else
        {
            _settings.ActivationBinding = dialog.Binding;
            _activationBind.Text = FormatBinding(dialog.Binding);
        }
        SaveSettings();
    }

    private static string FormatBinding(string binding)
    {
        if (string.IsNullOrWhiteSpace(binding) || binding == "Not assigned") return "BIND…";
        return binding.Length <= 24 ? binding : binding[..21] + "…";
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

    private void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
            string json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
            string temporary = _settingsPath + ".tmp";
            File.WriteAllText(temporary, json);
            File.Move(temporary, _settingsPath, true);
            _openXrBackend.Apply(_settings);
            UpdateCurvePreview();
        }
        catch { }
    }

    private void RefreshStatus()
    {
        string runtime = DetectRuntime();
        _runtimeStatus.Text = "Runtime: " + runtime;

        bool openXrBackend = _openXrBackend.FilesAvailable && _openXrBackend.IsRegistered;
        string backendFolder = Path.Combine(AppContext.BaseDirectory, "NeckAssist");
        bool steamVrBackend = File.Exists(Path.Combine(backendFolder, "WTVRNeckAssistSteamVR.exe"));
        bool legacyOpenVr = runtime.Contains("SteamVR (OpenVR)", StringComparison.OrdinalIgnoreCase);
        bool available = legacyOpenVr ? steamVrBackend : openXrBackend;
        bool connected = _openXrBackend.HasLiveTelemetry;
        if (!string.IsNullOrWhiteSpace(_openXrBackend.RegistrationError))
            _backendStatus.Text = "OpenXR layer could not be enabled: " + _openXrBackend.RegistrationError;
        else if (!_settings.Enabled)
            _backendStatus.Text = "Neck Assist is off.";
        else if (connected)
            _backendStatus.Text = "Headset connected — adjustments apply live; no game restart is needed for slider changes.";
        else if (available)
            _backendStatus.Text = "Layer enabled, but no headset movement is arriving. Start the VR game after enabling Neck Assist once.";
        else
            _backendStatus.Text = _openXrBackend.FilesAvailable ? "Layer is installed but not registered. Toggle Neck Assist off and on once." : "Backend native component is missing from this build.";
        _backendStatus.ForeColor = connected ? Color.FromArgb(110, 235, 125) : Color.FromArgb(255, 196, 80);
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

    private void PollBindings()
    {
        if (_capturingBinding) return;
        bool togglePressed = HotasBindingDialog.IsBindingPressed(_settings.ActivationBinding);
        if (togglePressed && !_lastActivationPressed) _enabled.Checked = !_enabled.Checked;
        _lastActivationPressed = togglePressed;
        if (!_settings.Enabled)
        {
            _openXrBackend.SetHeld(true, _settings.PitchEnabled);
            return;
        }

        bool recenterPressed = HotasBindingDialog.IsBindingPressed(_settings.RecenterBinding);
        if (recenterPressed && !_lastRecenterPressed) _openXrBackend.Recenter();
        _lastRecenterPressed = recenterPressed;

        _openXrBackend.SetHeld(false, _settings.PitchEnabled);
    }

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
        using Pen grid = new(Color.FromArgb(55, 78, 84), 1);
        using Pen axis = new(Color.FromArgb(110, 135, 140), 1);
        using Pen curve = new(Color.FromArgb(75, 235, 125), 2.5f);
        using Pen threshold = new(Color.FromArgb(255, 190, 70), 1) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
        using Brush text = new SolidBrush(Color.FromArgb(195, 210, 214));

        graphics.DrawRectangle(grid, plot);
        int centerX = plot.Left + plot.Width / 2;
        int centerY = plot.Top + plot.Height / 2;
        graphics.DrawLine(axis, centerX, plot.Top, centerX, plot.Bottom);
        graphics.DrawLine(axis, plot.Left, centerY, plot.Right, centerY);

        using Font axisFont = new("Segoe UI", 7.5f, FontStyle.Bold);
        graphics.DrawString("HEAD TURN →", axisFont, text, plot.Right - 88, centerY + 3);
        graphics.TranslateTransform(plot.Left + 3, plot.Top + 104);
        graphics.RotateTransform(-90);
        graphics.DrawString("VIRTUAL VIEW →", axisFont, text, 0, 0);
        graphics.ResetTransform();

        int leftThreshold = MapX(-StartAngle, plot);
        int rightThreshold = MapX(StartAngle, plot);
        graphics.DrawLine(threshold, leftThreshold, plot.Top, leftThreshold, plot.Bottom);
        graphics.DrawLine(threshold, rightThreshold, plot.Top, rightThreshold, plot.Bottom);
        graphics.DrawString("ASSIST STARTS", axisFont, text, rightThreshold + 3, plot.Top + 3);
        using Brush amber = new SolidBrush(Color.FromArgb(255, 190, 70));
        using Brush green = new SolidBrush(Color.FromArgb(75, 235, 125));
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
            using Pen physicalPen = new(Color.FromArgb(255, 80, 80), 2);
            using Pen modifiedPen = new(Color.FromArgb(75, 235, 125), 2);
            int physicalX = Math.Clamp(MapX(PhysicalYaw, plot), plot.Left, plot.Right);
            int rawY = Math.Clamp(MapY(PhysicalYaw, plot), plot.Top, plot.Bottom);
            int adjustedY = Math.Clamp(MapY(shownVirtual, plot), plot.Top, plot.Bottom);
            graphics.DrawLine(physicalPen, physicalX, plot.Top, physicalX, plot.Bottom);
            graphics.DrawLine(physicalPen, plot.Left, rawY, physicalX, rawY);
            graphics.DrawLine(modifiedPen, physicalX, adjustedY, plot.Right, adjustedY);
            PointF marker = new(MapX(PhysicalYaw, plot), MapY(shownVirtual, plot));
            using Brush markerBrush = new SolidBrush(Color.White);
            graphics.FillEllipse(markerBrush, marker.X - 5, marker.Y - 5, 10, 10);
            graphics.DrawString($"RED: headset {PhysicalYaw:0}°   GREEN: configured view {shownVirtual:0}°   Reported view: {VirtualYaw:0}°", Font, text, 6, Height - 23);
        }
        else
        {
            graphics.DrawString("Waiting for headset telemetry", Font, text, 6, Height - 23);
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

    [DllImport("winmm.dll")]
    private static extern int joyGetNumDevs();

    [DllImport("winmm.dll")]
    private static extern int joyGetPosEx(int joystickId, ref JoyInfoEx info);

    private const int JoyReturnButtons = 0x80;
    private readonly Label _status;
    private readonly Button _assign;
    private string _pendingBinding = "";
    private readonly System.Windows.Forms.Timer _poll = new() { Interval = 40 };
    private string _candidate = "";
    private int _stableTicks;
    private readonly Dictionary<int, uint> _ignoredHeld = new();
    private bool _baselineCaptured;
    public string Binding { get; private set; } = "";

    public HotasBindingDialog(string instruction)
    {
        Text = "Bind HOTAS buttons";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(540, 245);
        BackColor = Color.FromArgb(5, 12, 14);
        ForeColor = Color.White;

        Label heading = new()
        {
            Text = instruction,
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter
        };
        heading.SetBounds(20, 18, 500, 38);
        Controls.Add(heading);

        _status = new Label
        {
            Text = "Tap one button, or press a combination together.\nRelease, then click Assign.",
            Font = new Font("Segoe UI", 10),
            ForeColor = Color.FromArgb(130, 230, 140),
            TextAlign = ContentAlignment.MiddleCenter
        };
        _status.SetBounds(25, 66, 490, 82);
        Controls.Add(_status);

        _assign = new Button { Text = "Assign", Enabled = false };
        _assign.SetBounds(75, 178, 120, 40);
        _assign.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_pendingBinding)) return;
            Binding = _pendingBinding;
            DialogResult = DialogResult.OK;
            Close();
        };
        Controls.Add(_assign);
        AcceptButton = _assign;
        Button retry = new() { Text = "Try again" };
        retry.SetBounds(210, 178, 120, 40);
        retry.Click += (_, _) =>
        {
            _pendingBinding = _candidate = "";
            _stableTicks = 0;
            _baselineCaptured = false;
            _ignoredHeld.Clear();
            _assign.Enabled = false;
            _status.Text = "Press your combo, then click Assign to save it.";
        };
        Controls.Add(retry);
        Button cancel = new() { Text = "Cancel", DialogResult = DialogResult.Cancel };
        cancel.SetBounds(345, 178, 120, 40);
        Controls.Add(cancel);
        CancelButton = cancel;

        Shown += (_, _) => _poll.Start();
        FormClosed += (_, _) => _poll.Stop();
        _poll.Tick += (_, _) => PollJoysticks();
    }

    private void PollJoysticks()
    {
        if (_pendingBinding.Length > 0) return;
        List<string> pressed = new();
        int devices = Math.Min(16, joyGetNumDevs());
        for (int joystick = 0; joystick < devices; joystick++)
        {
            JoyInfoEx info = new() { Size = Marshal.SizeOf<JoyInfoEx>(), Flags = JoyReturnButtons };
            if (joyGetPosEx(joystick, ref info) != 0) continue;
            uint buttons = unchecked((uint)info.Buttons);
            if (!_baselineCaptured) _ignoredHeld[joystick] = buttons;
            _ignoredHeld.TryGetValue(joystick, out uint ignored);
            _ignoredHeld[joystick] = ignored & buttons;
            buttons &= ~_ignoredHeld[joystick];
            for (int bit = 0; bit < 32; bit++)
            {
                if ((buttons & (1u << bit)) != 0) pressed.Add($"JOY{joystick + 1}:B{bit + 1}");
            }
        }

        _baselineCaptured = true;
        string current = string.Join(" + ", pressed);
        if (current.Length == 0)
        {
            if (_candidate.Length > 0)
            {
                _pendingBinding = _candidate;
                _status.Text = _pendingBinding + "\nClick Assign to save, or Try again.";
                _assign.Enabled = true;
            }
            _candidate = "";
            _stableTicks = 0;
            return;
        }

        // Keep the fullest simultaneous press until every button is released.
        // This captures a short single tap without a hold-duration requirement,
        // and preserves a chord while its buttons are released one at a time.
        if (current.Split(" + ").Length >= _candidate.Split(" + ").Length || _candidate.Length == 0)
            _candidate = current;
        _status.Text = _candidate + "\nRelease the button(s), then click Assign.";
    }

    public static bool IsBindingPressed(string binding)
    {
        if (string.IsNullOrWhiteSpace(binding) || binding == "Not assigned") return false;
        string[] tokens = binding.Split(" + ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0) return false;

        Dictionary<int, uint> states = new();
        foreach (string token in tokens)
        {
            Match match = System.Text.RegularExpressions.Regex.Match(token, @"^JOY(?<joy>\d+):B(?<button>\d+)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!match.Success || !int.TryParse(match.Groups["joy"].Value, out int joyNumber) ||
                !int.TryParse(match.Groups["button"].Value, out int buttonNumber) || buttonNumber is < 1 or > 32) return false;

            int joystickId = joyNumber - 1;
            if (!states.TryGetValue(joystickId, out uint buttons))
            {
                JoyInfoEx info = new() { Size = Marshal.SizeOf<JoyInfoEx>(), Flags = JoyReturnButtons };
                if (joyGetPosEx(joystickId, ref info) != 0) return false;
                buttons = unchecked((uint)info.Buttons);
                states[joystickId] = buttons;
            }

            if ((buttons & (1u << (buttonNumber - 1))) == 0) return false;
        }
        return true;
    }
}
