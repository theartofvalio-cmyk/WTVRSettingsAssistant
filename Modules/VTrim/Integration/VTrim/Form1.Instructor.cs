using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace HOTASTrimUtility;

public partial class Form1
{
    private readonly InstructorController _instructorController = new();
    private readonly LateralAssistController _rollAssist = new(), _rudderAssist = new();
    private LateralSample? _lateralSample;
    private LateralOutput _lastRollAssist, _lastRudderAssist;
    private InstructorTuning _instructorTuning = new();
    private readonly CancellationTokenSource _telemetryShutdown = new();
    private readonly HttpClient _warThunderTelemetryClient = new(new HttpClientHandler { UseProxy = false })
    {
        BaseAddress = new Uri("http://127.0.0.1:8111/"),
        Timeout = TimeSpan.FromMilliseconds(250)
    };
    private bool _telemetryRequestInProgress, _instructorSettingsOpen;
    private string _telemetryReason = "Waiting for War Thunder pitch telemetry.";
    private string _instructorState = "OFF";
    private string _pendingInstructorStatus = "";
    private double _pendingInstructorStatusSince;
    private double _lastInstructorUiUpdate;
    private bool _lastInstructorUiEnabled;
    private Label? _instructorStatusLabel;
    private Control? _instructorStrip;
    private Panel? _instructorLive;
    private Button? _instructorSettingsButton;
    private double _pitchManualUntil;
    private string _lastIndicatorsJson = string.Empty, _lastStateJson = string.Empty;
    private readonly Queue<string> _instructorLog = new();
    private double _lastInstructorLogTime;
    private PitchHoldOutput _lastPitchHold;
    private static double MonotonicSeconds => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
    // Retained name for the existing control loop. In v2.2 it means that the
    // active aircraft/profile is eligible for Flight Assistant, not merely that
    // it is a propeller aircraft.
    private bool IsPropProfile => IsFlightAssistantEligibleForActiveProfile(out _);

    private async void WarThunderTelemetryTimer_Tick(object? sender, EventArgs e)
    {
        if (_applicationClosing || _telemetryRequestInProgress) return;
        double requestedAt = MonotonicSeconds;
        bool instructorRequested = _instructorModeEnabled && IsPropProfile;
        // Auto-profile detection uses a lower polling rate when active flight
        // assistance does not need the faster telemetry cadence.
        if (!instructorRequested && requestedAt - _lastAutoProfilePollAt < 0.35) return;
        _lastAutoProfilePollAt = requestedAt;
        _telemetryRequestInProgress = true;
        try
        {
            Task<string> instruments = _warThunderTelemetryClient.GetStringAsync("indicators", _telemetryShutdown.Token);
            Task<string> flight = _warThunderTelemetryClient.GetStringAsync("state", _telemetryShutdown.Token);
            await Task.WhenAll(instruments, flight);
            if (_applicationClosing || IsDisposed) return;
            _lastIndicatorsJson = await instruments;
            _lastStateJson = await flight;
            ProcessAutomaticAircraftProfileTelemetry(_lastIndicatorsJson, _lastStateJson);

            if (!_instructorModeEnabled || !IsPropProfile) return;
            _lateralSample = WarThunderTelemetry.ParseLateral(_lastIndicatorsJson, _lastStateJson, requestedAt);
            if (WarThunderTelemetry.TryParse(_lastIndicatorsJson, _lastStateJson, requestedAt,
                    out AircraftSample sample, out string reason))
            {
                _instructorController.Accept(sample);
                _telemetryReason = reason;
            }
            else InvalidatePitchTelemetry(reason);
        }
        catch (OperationCanceledException)
        {
            _lateralSample = null;
            if (!_applicationClosing && _instructorModeEnabled) InvalidatePitchTelemetry("Local pitch telemetry timed out.");
        }
        catch (HttpRequestException ex)
        {
            _lateralSample = null;
            if (_instructorModeEnabled) InvalidatePitchTelemetry("Cannot read local pitch telemetry: " + ex.Message);
        }
        catch (ObjectDisposedException) when (_applicationClosing) { }
        finally
        {
            _telemetryRequestInProgress = false;
            if (!_applicationClosing && !IsDisposed) UpdateInstructorModeUi();
        }
    }

    private void InvalidatePitchTelemetry(string reason)
    {
        _telemetryReason = reason;
        _instructorController.Invalidate();
    }

    private void ResetAutomaticInstructorHold()
    {
        _instructorController.ResetHold();
        _rollAssist.Reset(); _rudderAssist.Reset();
        _lastRollAssist = default; _lastRudderAssist = default;
        _lastPitchHold = default;
        if (!_instructorModeEnabled) _instructorState = "OFF";
        // Clear the last submitted correction immediately on disable/reset,
        // even if the next input poll has not yet run.
        if (_vJoyConnected && !_offlinePreview)
        {
            AxisVector manual = MonotonicSeconds - _lastAxisSnapshotTime <= .25 ? _currentManualCommand : default;
            try { _vJoy?.SendFlightAxes(manual.Roll, manual.Pitch, manual.Rudder); }
            catch (Exception ex) { HandleVJoyConnectionLost("Pitch hold reset: " + ex.Message); }
        }
    }

    private void MarkManualTrimOverride(TrimAction action)
    {
        if (action is TrimAction.NoseDown or TrimAction.NoseUp ||
            (action is TrimAction.RudderLeft or TrimAction.RudderRight && _horizontalRudderAssistBox.Checked))
        {
            _pitchManualUntil = MonotonicSeconds + .35;
            ResetAutomaticInstructorHold();
        }
    }

    private void ApplyAutomaticInstructorMode(double rawRoll, double rawPitch, double rawRudder,
        bool rollAvailable, bool pitchAvailable, bool rudderAvailable,
        ref double outputRoll, ref double outputPitch, ref double outputRudder)
    {
        ApplyAutomaticInstructorModeCore(MonotonicSeconds, GameWindow.GameFocused(), rawPitch,
            pitchAvailable, ref outputRoll, ref outputPitch, ref outputRudder);
        bool lateralEnabled = !FlightAssistantLockedPendingGaijinLegalReview && IsPropProfile && _instructorModeEnabled && _vJoyConnected && GameWindow.GameFocused() &&
            !_instructorSettingsOpen && !_horizontalRudderCalibrationArmed && !_captureAction.HasValue &&
            !_detectingAxis.HasValue && !_waitingForDeviceDetection;
        _lastRollAssist = _rollAssist.Step(MonotonicSeconds, rawRoll, outputRoll,
            lateralEnabled && !_storeTrimReturn.AnyWaiting, rollAvailable, _lateralSample, true, _instructorTuning.RollAssist);
        _lastRudderAssist = _rudderAssist.Step(MonotonicSeconds, rawRudder, outputRudder,
            lateralEnabled && !_storeTrimReturn.AnyWaiting, rudderAvailable, _lateralSample, false, _instructorTuning.RudderAssist);
        outputRoll = _lastRollAssist.Output; outputRudder = _lastRudderAssist.Output;
        if (_instructorLive is not null)
            _toolTip.SetToolTip(_instructorLive, $"Pitch: {_instructorState}\nRoll: {_lastRollAssist.Status}\nRudder: {_lastRudderAssist.Status}");
    }

    private void ApplyAutomaticInstructorModeCore(double now, bool gameFocused, double rawPitch,
        bool pitchAvailable, ref double outputRoll, ref double outputPitch, ref double outputRudder)
    {
        string? bypass = FlightAssistantLockedPendingGaijinLegalReview ? "PENDING GAIJIN REVIEW" : !IsPropProfile ? "FLIGHT ASSISTANT UNAVAILABLE" : !_instructorModeEnabled ? "OFF" :
            !_vJoyConnected ? "NO OUTPUT" : !gameFocused ? "GAME NOT FOCUSED" :
            _instructorSettingsOpen || _horizontalRudderCalibrationArmed || _storeTrimReturn.PitchWaiting ||
            _captureAction.HasValue || _detectingAxis.HasValue || _waitingForDeviceDetection ? "CONFIGURING" :
            now < _pitchManualUntil ? "MANUAL TRIM" : null;
        _lastPitchHold = _instructorController.Step(now, rawPitch, outputPitch,
            bypass is null && _instructorTuning.PitchEnabled, pitchAvailable, _instructorTuning);
        outputPitch = _lastPitchHold.Elevator;
        _instructorState = bypass ?? _lastPitchHold.Status;
        // Lateral assistance is applied independently by the caller.
        if (_instructorTuning.DiagnosticsEnabled && now - _lastInstructorLogTime >= .05)
        {
            _lastInstructorLogTime = now;
            var sample = _instructorController.Sample;
            _instructorLog.Enqueue(FormattableString.Invariant(
                $"{now:F6},{rawPitch:F5},{sample?.Pitch:F5},{_lastPitchHold.TargetPitch:F5},{_lastPitchHold.PitchRate:F5},{_lastPitchHold.Correction:F5},{outputPitch:F5},{_instructorTuning.PitchDirection},{_instructorState}"));
            while (_instructorLog.Count > 2400) _instructorLog.Dequeue();
        }
        UpdateInstructorModeUi();
    }

    private void UpdateInstructorModeUi()
    {
        double uiNow = MonotonicSeconds;
        if (_lastInstructorUiEnabled == _instructorModeEnabled && uiNow - _lastInstructorUiUpdate < .1) return;
        _lastInstructorUiUpdate = uiNow;
        _lastInstructorUiEnabled = _instructorModeEnabled;
        bool available = IsPropProfile;
        if (!available && _instructorModeEnabled)
        { _instructorModeEnabled = false; ResetAutomaticInstructorHold(); }

        // Flight Assistant is intentionally absent from the main Trim Dashboard.
        // Runtime eligibility stays active, but every legacy dashboard surface is
        // forced hidden. The only visible controls live inside an aircraft's Custom
        // Profile editor.
        if (_instructorModeButton is not null) _instructorModeButton.Visible = false;
        if (_instructorStrip is not null) _instructorStrip.Visible = false;
        if (_instructorLive is not null) _instructorLive.Visible = false;

        // This reference is also used by the aircraft Custom Profile editor, so it
        // follows eligibility instead of being globally hidden.
        if (!_instructorModeEnabled) _instructorState = "OFF";
        if (_instructorStatusLabel is not null)
        {
            string status = _instructorState;
            if (_pendingInstructorStatus != _instructorState)
            {
                _pendingInstructorStatus = _instructorState;
                _pendingInstructorStatusSince = MonotonicSeconds;
            }
            if (!_instructorModeEnabled || MonotonicSeconds - _pendingInstructorStatusSince >= .25)
            {
                if (_instructorStatusLabel.Text != status) _instructorStatusLabel.Text = status;
                _instructorStatusLabel.ForeColor = _instructorState == "PITCH HOLD" ? Theme.Success : Theme.Muted;
            }
            _toolTip.SetToolTip(_instructorStatusLabel, _telemetryReason);
        }
    }

    private Control CreateInstructorStrip()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1,
            Margin = new Padding(0, 5, 0, 0), BackColor = Theme.Panel };
        _instructorStrip = panel;
        // Match the trim / stick / rudder columns directly above this strip.
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _instructorStatusLabel = new Label { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 12, 0),
            UseMnemonic = false, AutoEllipsis = true, ForeColor = Theme.Muted, TextAlign = ContentAlignment.TopLeft, Padding = new Padding(0, 12, 0, 0) };
        Button settings = CreateSecondaryButton("Flight Assistant Settings");
        _instructorSettingsButton = settings;
        settings.Dock = DockStyle.Fill; settings.Margin = new Padding(0, 6, 0, 0);
        settings.Click += (_, _) => ShowInstructorSettings();
        panel.Controls.Add(_instructorStatusLabel, 0, 0);
        _instructorLive = new InstructorLivePanel { Dock = DockStyle.Fill, BackColor = Theme.Panel, Margin = new Padding(5, 0, 5, 0) };
        _instructorLive.Paint += PaintInstructorLive;
        return panel;
    }

    private sealed class InstructorLivePanel : Panel
    {
        public InstructorLivePanel() { DoubleBuffered = true; ResizeRedraw = true; }
    }

    private void PaintInstructorLive(object? sender, PaintEventArgs e)
    {
        if (_instructorLive is null) return;
        int width = _instructorLive.ClientSize.Width;
        bool inputFresh = MonotonicSeconds - _lastAxisSnapshotTime <= .25;
        double physical = inputFresh ? _lastPhysicalPitch : 0;
        double correction = inputFresh && _vJoyConnected ? _lastPitchHold.Correction : 0;
        double output = inputFresh && _vJoyConnected ? _lastPitchHold.Elevator : 0;
        var rows = new[] { ("Physical", physical, Color.DeepSkyBlue),
            ("Correction", correction, Color.Gold), ("Output", output, Color.LightGreen),
            ("Roll assist", inputFresh && _vJoyConnected ? _lastRollAssist.Correction : 0, Color.Gold),
            ("Rudder assist", inputFresh && _vJoyConnected ? _lastRudderAssist.Correction : 0, Color.Gold) };
        for (int i = 0; i < rows.Length; i++)
        {
            var (name, value, color) = rows[i];
            int rowHeight = Math.Max(1, Math.Min(Math.Max(28, Font.Height + 12), (_instructorLive.ClientSize.Height - 4) / rows.Length));
            int y = i * rowHeight + (rowHeight - 25) / 2;
            int labelWidth = Math.Min(width / 2, TextRenderer.MeasureText("Rudder assist", Font).Width + 6);
            TextRenderer.DrawText(e.Graphics, name, Font, new Rectangle(0, y, labelWidth, 25), color,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            int valueWidth = TextRenderer.MeasureText("-100.0%", Font).Width + 6;
            int left = labelWidth + 4, barWidth = Math.Max(1, width - left - valueWidth - 4);
            TextRenderer.DrawText(e.Graphics, (value * 100).ToString("0.0", CultureInfo.InvariantCulture) + "%", Font,
                new Rectangle(width - valueWidth, y, valueWidth, 25), color,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            e.Graphics.FillRectangle(SystemBrushes.ControlDarkDark, left, y + 9, barWidth, 8);
            int center = left + barWidth / 2;
            int marker = left + (int)((Math.Clamp(value, -1, 1) + 1) * .5 * barWidth);
            using var brush = new SolidBrush(color);
            e.Graphics.FillRectangle(brush, Math.Min(center, marker), y + 9, Math.Max(2, Math.Abs(marker - center)), 8);
            e.Graphics.DrawLine(Pens.White, center, y + 6, center, y + 20);
        }
    }

    private void ShowInstructorSettings()
    {
        if (!IsFlightAssistantEligibleForActiveProfile(out string eligibilityReason))
        {
            ShowFlightAssistantEligibilityNotice(eligibilityReason);
            return;
        }
        _instructorSettingsOpen = true;
        ResetAutomaticInstructorHold();
        try
        {
            using var dialog = new Form { Text = "Flight Assistant · Axis assistance", StartPosition = FormStartPosition.CenterParent,
                ClientSize = new Size(860, 780), MinimumSize = new Size(800, 446), BackColor = Theme.Background,
                ForeColor = Theme.Text, Font = new Font("Segoe UI", 10), AutoScaleMode = AutoScaleMode.Dpi,
                MinimizeBox = false, MaximizeBox = false };
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 16,
                Padding = new Padding(24), AutoScroll = true };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 12));
            for (int i = 0; i < 15; i++) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            void LabelAt(int row, string text) => layout.Controls.Add(new Label { Text = text, Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft }, 0, row);
            var enabled = new CheckBox { Text = "Pitch attitude hold", Checked = _instructorTuning.PitchEnabled, Dock = DockStyle.Fill };
            layout.Controls.Add(enabled, 0, 0); layout.SetColumnSpan(enabled, 3);
            TrackBar Slider(int row, string label, int value, int maximum)
            {
                LabelAt(row, label);
                var slider = new TrackBar { Minimum = 0, Maximum = maximum, Value = Math.Clamp(value, 0, maximum),
                    TickStyle = TickStyle.None, Dock = DockStyle.Fill };
                var number = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
                void RefreshValue() => number.Text = slider.Value + "%";
                slider.ValueChanged += (_, _) => RefreshValue(); RefreshValue();
                layout.Controls.Add(slider, 1, row); layout.Controls.Add(number, 2, row);
                return slider;
            }
            var hold = Slider(1, "Attitude Hold Strength", _instructorTuning.AttitudeHoldStrength, 400);
            var damping = Slider(2, "Pitch Dampening Strength", _instructorTuning.PitchDampingStrength, 100);
            var maximum = Slider(3, "Maximum Automatic Elevator Correction", _instructorTuning.MaximumCorrection, 100);
            var deadzone = Slider(4, "Physical Pitch Centre Deadzone", _instructorTuning.PitchCentreDeadzone, 20);
            LabelAt(5, "Smoothing / response (ms)");
            var smoothing = new NumericUpDown { Minimum = 0, Maximum = 500, Increment = 10,
                Value = _instructorTuning.SmoothingMilliseconds, Dock = DockStyle.Fill };
            layout.Controls.Add(smoothing, 1, 5);
            var rollEnabled = new CheckBox { Text = "Roll Assist - wings level", Checked = _instructorTuning.RollAssist.Enabled, Dock = DockStyle.Fill };
            var rudderEnabled = new CheckBox { Text = "Rudder Assist - reduce sideslip", Checked = _instructorTuning.RudderAssist.Enabled, Dock = DockStyle.Fill };
            layout.Controls.Add(rollEnabled, 0, 6); layout.SetColumnSpan(rollEnabled, 3);
            var rollStrength = Slider(7, "Roll strength", _instructorTuning.RollAssist.Strength, 100);
            var rollLimit = Slider(8, "Maximum aileron correction", _instructorTuning.RollAssist.MaximumCorrection, 50);
            var rollDeadzone = Slider(9, "Roll centre deadzone", _instructorTuning.RollAssist.CentreDeadzone, 20);
            layout.Controls.Add(rudderEnabled, 0, 10); layout.SetColumnSpan(rudderEnabled, 3);
            var rudderStrength = Slider(11, "Rudder strength", _instructorTuning.RudderAssist.Strength, 100);
            var rudderLimit = Slider(12, "Maximum rudder correction", _instructorTuning.RudderAssist.MaximumCorrection, 10);
            var rudderDeadzone = Slider(13, "Pedal centre deadzone", _instructorTuning.RudderAssist.CentreDeadzone, 20);
            var aggression = Slider(14, "Correction aggressiveness", _instructorTuning.AggressivenessPercent, 400);
            using var help = new ToolTip { AutoPopDelay = 15000, InitialDelay = 400, ReshowDelay = 100 };
            string[] explanations =
            [
                "Holds the pitch attitude captured when you release the stick. Moving the stick overrides assistance immediately. Requires valid flight telemetry.",
                "How strongly the elevator corrects a difference from the captured pitch angle. Higher values respond harder and can cause oscillation. This does not hold altitude.",
                "Opposes pitch rotation to reduce overshoot. Higher values resist rotation more strongly while your pitch stick is centered.",
                "Maximum added elevator input. Automatic authority is capped at 20% at 100% aggressiveness, up to 80% at 400%, and reduced at higher airspeed. Lower values further limit assistance.",
                "Physical stick range treated as centered. At 0%, even tiny sensor noise can interrupt hold. Increase this if hold keeps dropping out.",
                "Time used to smooth corrections. Lower values react faster; higher values soften changes but add response lag."
            ];
            foreach (Control control in layout.Controls)
            {
                int row = layout.GetRow(control);
                if (row >= 0 && row < explanations.Length) help.SetToolTip(control, explanations[row]);
            }
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            Button save = CreatePrimaryButton("Save setup"); save.Size = new Size(140, 38);
            Button cancel = CreateSecondaryButton(VT("Common.Cancel")); cancel.Size = new Size(100, 38); cancel.DialogResult = DialogResult.Cancel;
            save.Click += (_, _) =>
            {
                _instructorTuning = new InstructorTuning
                {
                    PitchEnabled = enabled.Checked,
                    AggressivenessPercent = aggression.Value,
                    RollAssist = new AxisAssistTuning { Enabled = rollEnabled.Checked, Strength = rollStrength.Value, MaximumCorrection = rollLimit.Value, CentreDeadzone = rollDeadzone.Value },
                    RudderAssist = new AxisAssistTuning { Enabled = rudderEnabled.Checked, Strength = rudderStrength.Value, MaximumCorrection = rudderLimit.Value, CentreDeadzone = rudderDeadzone.Value },
                    AttitudeHoldStrength = hold.Value, PitchDampingStrength = damping.Value,
                    MaximumCorrection = maximum.Value, PitchCentreDeadzone = deadzone.Value,
                    SmoothingMilliseconds = (int)smoothing.Value,
                    DiagnosticsEnabled = false
                };
                bool wantsEnabled = enabled.Checked || rollEnabled.Checked || rudderEnabled.Checked;
                _instructorModeEnabled = wantsEnabled && IsFlightAssistantEligibleForActiveProfile(out _) &&
                    (!wantsEnabled || ConfirmFlightAssistantEnable());
                ResetAutomaticInstructorHold(); UpdateInstructorModeUi(); SaveBindings();
                dialog.DialogResult = DialogResult.OK;
            };
            buttons.Controls.Add(save); buttons.Controls.Add(cancel);
            layout.Controls.Add(buttons, 0, 15); layout.SetColumnSpan(buttons, 3);
            dialog.Controls.Add(layout); dialog.ShowDialog(this);
        }
        finally { _instructorSettingsOpen = false; ResetAutomaticInstructorHold(); UpdateInstructorModeUi(); }
    }

    private void ExportPitchDiagnostics(IWin32Window owner)
    {
        using var save = new SaveFileDialog { Filter = "CSV diagnostics|*.csv", FileName = "vtrim-pitch-diagnostics.csv" };
        if (save.ShowDialog(owner) != DialogResult.OK) return;
        try
        {
            File.WriteAllLines(save.FileName,
                new[] { "time_s,physical_pitch,pitch_nose_up_deg,target_deg,derived_pitch_rate_deg_s,automatic_vjoy_y,final_vjoy_y,nose_up_direction,status" }
                    .Concat(_instructorLog), Encoding.UTF8);
            File.WriteAllText(Path.ChangeExtension(save.FileName, ".telemetry.txt"),
                "/indicators\r\n" + _lastIndicatorsJson + "\r\n/state\r\n" + _lastStateJson, Encoding.UTF8);
        }
        catch (IOException ex) { MessageBox.Show(owner, ex.Message, "Diagnostics export"); }
        catch (UnauthorizedAccessException ex) { MessageBox.Show(owner, ex.Message, "Diagnostics export"); }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_applicationClosing)
        {
            if (!_offlinePreview) Form1_FormClosed(this, new FormClosedEventArgs(CloseReason.ApplicationExitCall));
            else
            {
                _applicationClosing = true; _telemetryShutdown.Cancel(); _warThunderTelemetryClient.Dispose();
            }
        }
        if (disposing)
        {
            _pollTimer.Dispose(); _vJoyAutoConnectTimer.Dispose(); _warThunderTelemetryTimer.Dispose(); _toolTip.Dispose();
        }
        base.Dispose(disposing);
    }
}
