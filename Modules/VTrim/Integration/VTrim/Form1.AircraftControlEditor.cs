using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Text.Json;
using System.Windows.Forms;

namespace HOTASTrimUtility;

public partial class Form1
{
    private const string DampingUnknown = "Unknown";
    private const string DampingUnavailable = "NoDamping";
    private const string DampingAvailable = "HasDamping";
    // Flight Assistant is temporarily hard-locked in this release while the
    // project waits for official approval/clarification from Gaijin's legal team.
    // This is deliberately enforced below the UI so saved profiles or old HOTAS
    // bindings cannot re-enable the feature.
    // Runtime-read policy remains immutable, without compile-time unreachable branches.
    private static readonly bool FlightAssistantLockedPendingGaijinLegalReview = true;
    private const string FlightAssistantPendingReason =
        "Flight Assistant is temporarily disabled for all aircraft while awaiting official approval/clarification from Gaijin's legal team.";

    private bool _activeUseCustomControls;
    private bool _activeHasCustomControlsSnapshot;
    private string _activeDampingSupport = DampingUnknown;
    private SavedBindingsFile? _aircraftSettingsClipboard;
    private string? _aircraftSettingsClipboardClass;
    private string? _aircraftSettingsClipboardSource;
    private bool _flightAssistantDisclaimerAccepted;

    // Profiles-page editor state. Aircraft controls are edited inside the VTrim
    // Profiles page instead of opening a separate top-level window.
    private Control? _profilesPageView;
    private Control? _profilesBrowserView;
    private Control? _aircraftControlEditorView;
    private Control? _sharedTrimDashboard;
    private Control? _sharedTrimDashboardHome;
    private Control? _sharedTrimDashboardSurface;
    private readonly Dictionary<Control, Color> _aircraftDashboardOriginalColors = new();
    private readonly List<Action> _aircraftCurveRefreshers = new();
    private readonly Dictionary<TrimAction, Button> _aircraftBindingButtons = new();
    private readonly List<Button> _aircraftCurvePasteButtons = new();
    private AxisResponse? _aircraftCurveClipboard;
    private string? _aircraftCurveClipboardSource;

    private void RestoreSharedTrimDashboard()
    {
        if (_sharedTrimDashboard is null || _sharedTrimDashboard.IsDisposed ||
            _sharedTrimDashboardSurface is null || _sharedTrimDashboardSurface.IsDisposed) return;
        if (_sharedTrimDashboardSurface.Parent != _sharedTrimDashboard)
        {
            _sharedTrimDashboardSurface.Parent?.Controls.Remove(_sharedTrimDashboardSurface);
            RestoreAircraftDashboardColors();
            _sharedTrimDashboard.Controls.Add(_sharedTrimDashboardSurface);
            _sharedTrimDashboardSurface.Dock = DockStyle.Top;
            _sharedTrimDashboardSurface.Visible = true;
            SizeResponsiveScrollSurface(_sharedTrimDashboard, _sharedTrimDashboardSurface);
        }
    }

    private void ApplyAircraftDashboardColors()
    {
        if (_sharedTrimDashboardSurface is null) return;
        foreach (Control control in EnumerateControls(_sharedTrimDashboardSurface))
        {
            if (control is not TableLayoutPanel && !ReferenceEquals(control, _sharedTrimDashboardSurface)) continue;
            if (control.BackColor != Theme.Background) continue;
            if (!_aircraftDashboardOriginalColors.ContainsKey(control))
                _aircraftDashboardOriginalColors[control] = control.BackColor;
            control.BackColor = Color.Transparent;
        }
    }

    private void RestoreAircraftDashboardColors()
    {
        foreach ((Control control, Color color) in _aircraftDashboardOriginalColors.ToArray())
            if (!control.IsDisposed) control.BackColor = color;
        _aircraftDashboardOriginalColors.Clear();
    }

    private static IEnumerable<Control> EnumerateControls(Control root)
    {
        yield return root;
        foreach (Control child in root.Controls)
            foreach (Control nested in EnumerateControls(child))
                yield return nested;
    }

    private static string NormalizeDampingSupport(string? value) => value switch
    {
        DampingUnavailable => DampingUnavailable,
        DampingAvailable => DampingAvailable,
        _ => DampingUnknown
    };

    private static void NormalizeAircraftControlModeMetadata(SavedBindingsFile profile, bool isDefaultProfile)
    {
        if (profile.Version < 13)
        {
            // Preserve the behavior of existing user-created profiles. They were
            // already independent aircraft setups before the Default/Custom mode
            // switch existed, so migrate them as Custom Controls.
            profile.UseCustomControls = !isDefaultProfile;
            profile.HasCustomControlsSnapshot = !isDefaultProfile;
        }
        if (isDefaultProfile)
        {
            profile.UseCustomControls = false;
            profile.HasCustomControlsSnapshot = false;
            profile.InstructorModeEnabled = false;
        }
        if (FlightAssistantLockedPendingGaijinLegalReview)
            profile.InstructorModeEnabled = false;
        profile.DampingSupport = NormalizeDampingSupport(profile.DampingSupport);
        profile.Version = Math.Max(profile.Version, 13);
    }

    private SavedBindingsFile PrepareAircraftProfileForRuntime(SavedBindingsFile stored) =>
        PrepareAircraftProfileForRuntime(stored, DefaultSetupFilePath);

    private static SavedBindingsFile PrepareAircraftProfileForRuntime(SavedBindingsFile stored, string defaultPath)
    {
        NormalizeAircraftControlModeMetadata(stored, isDefaultProfile: false);
        if (stored.UseCustomControls) return stored;

        SavedBindingsFile runtime = ReadDefaultProfileTemplateFromPath(defaultPath);
        runtime.Id = stored.Id;
        runtime.Version = 13;
        runtime.ProfileName = stored.ProfileName;
        runtime.AircraftId = stored.AircraftId;
        runtime.DetectedAircraftKey = stored.DetectedAircraftKey;
        runtime.AircraftType = stored.AircraftType;
        runtime.UseCustomControls = false;
        runtime.HasCustomControlsSnapshot = stored.HasCustomControlsSnapshot;
        runtime.DampingSupport = NormalizeDampingSupport(stored.DampingSupport);
        // Flight Assistant is intentionally aircraft-specific and never runs
        // while an aircraft is following Default Controls.
        runtime.InstructorModeEnabled = false;
        return runtime;
    }

    private bool IsFlightAssistantEligibleForProfile(SavedBindingsFile profile, out string reason)
    {
        if (FlightAssistantLockedPendingGaijinLegalReview)
        {
            reason = FlightAssistantPendingReason;
            return false;
        }
        NormalizeAircraftControlModeMetadata(profile, string.IsNullOrWhiteSpace(profile.ProfileName));
        if (string.IsNullOrWhiteSpace(profile.ProfileName))
        {
            reason = "Flight Assistant is an aircraft-specific Custom Controls feature.";
            return false;
        }
        if (!profile.UseCustomControls)
        {
            reason = "Switch this aircraft to Custom Controls to configure Flight Assistant.";
            return false;
        }

        string type = NormalizeAircraftType(profile.AircraftType);
        if (type == "Prop Plane")
        {
            reason = "Available for this custom propeller-aircraft profile.";
            return true;
        }

        // Damping/SAS capability is intentionally not user-configurable.
        // Until an aircraft is explicitly supported by the application itself,
        // Flight Assistant stays hidden/disabled for jets and helicopters. This
        // prevents a profile toggle from enabling assistance on an aircraft that
        // already has its own in-game stability/damping system.
        reason = type == "Helicopter"
            ? "Flight Assistant is not used for helicopters."
            : "Flight Assistant is not used for this jet profile.";
        return false;
    }

    private bool IsFlightAssistantEligibleForActiveProfile(out string reason)
    {
        if (string.IsNullOrWhiteSpace(_activeProfileName))
        {
            reason = "Flight Assistant is an aircraft-specific Custom Controls feature.";
            return false;
        }
        var current = new SavedBindingsFile
        {
            Version = 13,
            ProfileName = _activeProfileName,
            AircraftType = NormalizeAircraftType(_profileTypeBox?.SelectedItem?.ToString()),
            UseCustomControls = _activeUseCustomControls,
            HasCustomControlsSnapshot = _activeHasCustomControlsSnapshot,
            DampingSupport = NormalizeDampingSupport(_activeDampingSupport)
        };
        return IsFlightAssistantEligibleForProfile(current, out reason);
    }

    private bool ConfirmFlightAssistantEnable()
    {
        if (FlightAssistantLockedPendingGaijinLegalReview)
        {
            _instructorModeEnabled = false;
            SetInstruction(FlightAssistantPendingReason, Theme.Warning);
            return false;
        }
        if (_flightAssistantDisclaimerAccepted) return true;
        const string message =
            "Flight Assistant is experimental controller assistance. It uses local War Thunder flight telemetry to apply bounded virtual-axis corrections while your physical controls are centered.\r\n\r\n" +
            "It is disabled for aircraft marked as having in-game damping/SAS. Use this feature only where permitted by War Thunder's rules.\r\n\r\nEnable it for this session?";
        if (MessageBox.Show(this, message, "Flight Assistant", MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            return false;
        _flightAssistantDisclaimerAccepted = true;
        return true;
    }

    private void ShowFlightAssistantEligibilityNotice(string reason)
    {
        _instructorModeEnabled = false;
        ResetAutomaticInstructorHold();
        UpdateInstructorModeUi();
        SetInstruction(reason, Theme.Warning);
        MessageBox.Show(this, reason, "Flight Assistant unavailable", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void SetActiveAircraftControlMode(bool useCustom)
    {
        if (string.IsNullOrWhiteSpace(_activeProfileName) || _activeUseCustomControls == useCustom) return;
        string profileName = _activeProfileName;

        if (useCustom)
        {
            // Save any Default Controls edits first; SaveBindings preserves an
            // existing custom snapshot while this aircraft is in Default mode.
            SaveBindings();
            SavedBindingsFile stored = ReadProfile(profileName);
            SavedBindingsFile custom;
            if (!stored.HasCustomControlsSnapshot)
            {
                custom = ReadDefaultProfileTemplate();
                custom.Id = stored.Id;
                custom.ProfileName = profileName;
                custom.AircraftId = stored.AircraftId;
                custom.DetectedAircraftKey = stored.DetectedAircraftKey;
                custom.AircraftType = stored.AircraftType;
                custom.DampingSupport = NormalizeDampingSupport(stored.DampingSupport);
                custom.InstructorModeEnabled = false;
            }
            else custom = stored;

            custom.Version = 13;
            custom.UseCustomControls = true;
            custom.HasCustomControlsSnapshot = true;
            if (!IsFlightAssistantEligibleForProfile(custom, out _)) custom.InstructorModeEnabled = false;
            WriteProfile(profileName, custom);
            ApplySavedProfile(custom);
            SetInstruction(VF("Profiles.CustomEnabled", profileName), Theme.Success);
        }
        else
        {
            // Flush the aircraft-specific setup, then switch runtime controls to
            // the live Default Profile without erasing the custom snapshot.
            SaveBindings();
            SavedBindingsFile stored = ReadProfile(profileName);
            stored.Version = 13;
            stored.UseCustomControls = false;
            stored.HasCustomControlsSnapshot = true;
            WriteProfile(profileName, stored);
            ApplySavedProfile(PrepareAircraftProfileForRuntime(stored));
            SetInstruction(VF("Profiles.FollowingDefault", profileName), Theme.Success);
        }

        WriteActiveProfileName();
        RefreshProfileList();
        UpdateInstructorModeUi();
    }

    private string? GetAircraftSettingsCompatibilityClass(SavedBindingsFile profile, out string description)
    {
        string type = NormalizeAircraftType(profile.AircraftType);
        if (type == "Prop Plane") { description = VT("Profiles.Compatibility.Prop"); return "PROP"; }
        if (type == "Helicopter") { description = VT("Profiles.Compatibility.Helicopter"); return "HELICOPTER"; }
        string damping = NormalizeDampingSupport(profile.DampingSupport);
        if (damping == DampingUnavailable) { description = VT("Profiles.Compatibility.JetNoDamping"); return "JET_NO_DAMPING"; }
        if (damping == DampingAvailable) { description = VT("Profiles.Compatibility.JetDamping"); return "JET_DAMPING"; }
        description = VT("Profiles.Compatibility.JetUnknown");
        return null;
    }

    private SavedBindingsFile CloneProfileData(SavedBindingsFile source) =>
        JsonSerializer.Deserialize<SavedBindingsFile>(JsonSerializer.Serialize(source, BindingsJsonOptions), BindingsJsonOptions)
        ?? new SavedBindingsFile();

    private void CopyActiveAircraftSettings()
    {
        if (string.IsNullOrWhiteSpace(_activeProfileName) || !_activeUseCustomControls)
        {
            MessageBox.Show(this, VT("Profiles.CopyCustomFirst"),
                VT("Profiles.CopySettingsTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        SavedBindingsFile source = CaptureCurrentProfile();
        string? compatibility = GetAircraftSettingsCompatibilityClass(source, out string description);
        if (compatibility is null)
        {
            MessageBox.Show(this, VT("Profiles.SetDampingFirstCopy"),
                VT("Profiles.CopySettingsTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        _aircraftSettingsClipboard = CloneProfileData(source);
        _aircraftSettingsClipboardClass = compatibility;
        _aircraftSettingsClipboardSource = _activeProfileName;
        SetInstruction(VF("Profiles.CopiedSettings", _activeProfileName, description), Theme.Success);
    }

    private void CopyControlSettings(SavedBindingsFile source, SavedBindingsFile destination)
    {
        SavedBindingsFile copy = CloneProfileData(source);
        destination.Bindings = copy.Bindings;
        destination.PitchStep = copy.PitchStep;
        destination.RollStep = copy.RollStep;
        destination.RudderStep = copy.RudderStep;
        destination.UniversalTriggerPassthrough = copy.UniversalTriggerPassthrough;
        destination.BlockUnassignedInputChords = copy.BlockUnassignedInputChords;
        destination.RepeatWhileHeld = copy.RepeatWhileHeld;
        destination.HeldTrimRate = copy.HeldTrimRate;
        destination.RollResponse = copy.RollResponse;
        destination.PitchResponse = copy.PitchResponse;
        destination.RudderResponse = copy.RudderResponse;
        destination.Instructor = copy.Instructor;
        destination.InstructorModeEnabled = copy.InstructorModeEnabled;
        destination.HorizontalRudderAssist = copy.HorizontalRudderAssist;
        destination.RudderRollCompensationPercent = copy.RudderRollCompensationPercent;
        destination.RudderRollCompensationDirection = copy.RudderRollCompensationDirection;
        destination.RudderPitchCompensationPercent = copy.RudderPitchCompensationPercent;
        destination.RudderPitchCompensationDirection = copy.RudderPitchCompensationDirection;
    }

    private bool PasteAircraftSettingsIntoActiveProfile()
    {
        if (_aircraftSettingsClipboard is null || string.IsNullOrWhiteSpace(_aircraftSettingsClipboardClass))
        {
            MessageBox.Show(this, VT("Profiles.PasteEmpty"), VT("Profiles.PasteSettingsTitle"),
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }
        if (string.IsNullOrWhiteSpace(_activeProfileName)) return false;

        SavedBindingsFile targetStored = ReadProfile(_activeProfileName);
        string? targetClass = GetAircraftSettingsCompatibilityClass(targetStored, out string targetDescription);
        if (targetClass is null)
        {
            MessageBox.Show(this, VT("Profiles.SetDampingFirstPaste"),
                VT("Profiles.PasteSettingsTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }
        if (!string.Equals(targetClass, _aircraftSettingsClipboardClass, StringComparison.Ordinal))
        {
            MessageBox.Show(this,
                VF("Profiles.PasteIncompatible", _aircraftSettingsClipboardSource ?? string.Empty, CompatibilityLabel(_aircraftSettingsClipboardClass), _activeProfileName, targetDescription),
                VT("Profiles.IncompatibleTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        if (!_activeUseCustomControls) SetActiveAircraftControlMode(true);
        SavedBindingsFile target = CaptureCurrentProfile();
        CopyControlSettings(_aircraftSettingsClipboard, target);
        target.Version = 13;
        target.UseCustomControls = true;
        target.HasCustomControlsSnapshot = true;
        target.DampingSupport = NormalizeDampingSupport(_activeDampingSupport);
        if (!IsFlightAssistantEligibleForProfile(target, out _)) target.InstructorModeEnabled = false;
        WriteProfile(_activeProfileName, target);
        ApplySavedProfile(target);
        RefreshProfileList();
        SetInstruction(VF("Profiles.PastedSettings", _aircraftSettingsClipboardSource ?? string.Empty, _activeProfileName), Theme.Success);
        return true;
    }

    private string CompatibilityLabel(string? value) => value switch
    {
        "PROP" => VT("Profiles.Compatibility.Prop"),
        "JET_NO_DAMPING" => VT("Profiles.Compatibility.JetNoDamping"),
        "JET_DAMPING" => VT("Profiles.Compatibility.JetDamping"),
        "HELICOPTER" => VT("Profiles.Compatibility.Helicopter"),
        _ => VT("Profiles.Compatibility.Unknown")
    };

    private void RegisterProfilesPage(Control host, Control browser)
    {
        _profilesPageView = host;
        _profilesBrowserView = browser;
        _aircraftControlEditorView = null;
        host.Disposed += (_, _) =>
        {
            _profilesPageView = null;
            _profilesBrowserView = null;
            _aircraftControlEditorView = null;
        };
    }

    private void CloseAircraftControlEditor(bool showProfilesBrowser)
    {
        // Detach the live controls before disposing the temporary aircraft page.
        RestoreSharedTrimDashboard();
        Control? editor = _aircraftControlEditorView;
        _aircraftControlEditorView = null;
        if (editor is not null && !editor.IsDisposed)
        {
            if (editor.Parent is not null) editor.Parent.Controls.Remove(editor);
            editor.Dispose();
        }

        if (showProfilesBrowser && _profilesBrowserView is not null && !_profilesBrowserView.IsDisposed)
        {
            _profilesBrowserView.Visible = true;
            _profilesBrowserView.BringToFront();
            AircraftProfilesChanged?.Invoke();
        }
    }

    private void OpenDefaultControlsFromAircraftEditor()
    {
        CloseAircraftControlEditor(showProfilesBrowser: true);
        BeginInvoke((Action)(() =>
        {
            SwitchToDefaultProfile();
            _openTrimDashboard?.Invoke();
        }));
    }

    private void OpenAircraftControlEditor(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName) || !ProfileExists(profileName)) return;
        if (_profilesPageView is null || _profilesPageView.IsDisposed ||
            _profilesBrowserView is null || _profilesBrowserView.IsDisposed) return;

        if (!string.Equals(profileName, _activeProfileName, StringComparison.OrdinalIgnoreCase))
            SwitchProfile(profileName);
        StartAircraftDatabase();

        SavedBindingsFile stored = ReadProfile(profileName);
        AircraftInfo? aircraft = _aircraftDatabase?.ResolveProfile(stored.AircraftId, stored.DetectedAircraftKey, profileName);

        CloseAircraftControlEditor(showProfilesBrowser: false);
        _aircraftCurveRefreshers.Clear();
        _aircraftCurvePasteButtons.Clear();
        _profilesBrowserView.Visible = false;

        // FIX9: the aircraft artwork now belongs to the scrollable content, not
        // to the fixed viewport. WinForms' native scroll optimization moves child
        // pixels while the thumb is dragged; a fixed transparent backdrop was
        // therefore shifted temporarily and produced the stretched/tearing effect
        // seen in FIX8. Keeping the cached backdrop in the scrolling content makes
        // the bitmap and controls move as one surface and avoids repaint storms.
        var editorHost = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Color.FromArgb(7, 18, 24),
            AccessibleName = $"{AircraftSearchService.CleanName(aircraft?.DisplayName ?? profileName)} aircraft controls"
        };
        _aircraftControlEditorView = editorHost;

        int S(int value) => Math.Max(1, (int)Math.Round(value * DeviceDpi / 96f));
        var scroll = new AircraftEditorScrollPanel
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = new Padding(S(8)),
            BackColor = Color.FromArgb(7, 18, 24)
        };
        var contentBackdrop = new AircraftEditorBackdropPanel
        {
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Color.FromArgb(7, 18, 24)
        };
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 6,
            Padding = Padding.Empty,
            Margin = Padding.Empty,
            BackColor = Color.Transparent
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, S(116)));
        // Profile actions belong at the top where users choose/edit the aircraft.
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, S(92)));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, S(68)));
        // Keep the custom-profile controls compact. The aircraft artwork is the
        // page background; controls should float over it instead of recreating
        // the tall opaque Trim Dashboard.
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, S(250)));
        // Curves stay immediately after trim/keybinds. Flight Assistant is now a
        // locked legal-review notice at the very bottom of every aircraft page.
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, S(615)));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, S(190)));
        root.Height = root.RowStyles.Cast<RowStyle>().Sum(r => (int)Math.Round(r.Height));

        var header = new AircraftEditorHeaderPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, S(8))
        };
        header.SetText(AircraftSearchService.CleanName(aircraft?.DisplayName ?? profileName),
            aircraft?.Nation ?? "", aircraft?.VehicleType ?? stored.AircraftType, FormatBattleRatings(aircraft));
        header.SetArtwork(AircraftIcons.CreateFallbackArtwork(aircraft?.FlightCategory ?? stored.AircraftType));
        root.Controls.Add(header, 0, 0);

        var actionBar = new AircraftEditorGlassPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, S(6)),
            Padding = new Padding(S(5))
        };
        var actionLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        // FIX14: keep every actionable control inside a fixed-height button row
        // and reserve the whole right half for the hero aircraft. The status text
        // sits on its own line below instead of competing with the buttons.
        actionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        actionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        actionLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(44)));
        actionLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var buttonStrip = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = new Padding(S(2), S(2), 0, 0)
        };
        Button copy = I18n(CreateSecondaryButton(VT("Profiles.Copy")), "Profiles.Copy");
        Button paste = I18n(CreateSecondaryButton(VT("Profiles.Paste")), "Profiles.Paste");
        Button back = I18n(CreatePrimaryButton(VT("Profiles.BackToProfiles")), "Profiles.BackToProfiles");
        copy.Size = new Size(S(82), S(34));
        paste.Size = new Size(S(82), S(34));
        back.Size = new Size(S(134), S(34));
        foreach (Button button in new[] { copy, paste, back })
        {
            button.Dock = DockStyle.None;
            button.Margin = new Padding(0, 0, S(6), 0);
            button.Font = new Font("Segoe UI Semibold", 8.8F);
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.AutoEllipsis = false;
            button.UseMnemonic = false;
        }
        _toolTip.SetToolTip(copy, VT("Profiles.CopySettingsTip"));
        _toolTip.SetToolTip(paste, VT("Profiles.PasteSettingsTip"));
        _toolTip.SetToolTip(back, VT("Profiles.BackToProfilesTip"));
        buttonStrip.Controls.Add(copy);
        buttonStrip.Controls.Add(paste);
        buttonStrip.Controls.Add(back);

        var clipboardStatus = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = Color.Gainsboro,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
            Padding = new Padding(S(4), 0, S(4), 0),
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 8.2F)
        };

        actionLayout.Controls.Add(buttonStrip, 0, 0);
        actionLayout.Controls.Add(clipboardStatus, 0, 1);
        var aircraftClearZone = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        actionLayout.Controls.Add(aircraftClearZone, 1, 0);
        actionLayout.SetRowSpan(aircraftClearZone, 2);
        actionBar.Controls.Add(actionLayout);
        root.Controls.Add(actionBar, 0, 1);

        var modeBar = new AircraftEditorGlassPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, S(8)),
            Padding = new Padding(S(8))
        };
        var modeLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        modeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22));
        modeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22));
        modeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56));

        Button defaultMode = I18n(CreateSecondaryButton(VT("Profiles.DefaultMode")), "Profiles.DefaultMode");
        Button customMode = I18n(CreateSecondaryButton(VT("Profiles.CustomMode")), "Profiles.CustomMode");
        var modeText = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = Color.Gainsboro,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(S(12), 0, S(8), 0),
            AutoEllipsis = false,
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 9.4F)
        };
        defaultMode.Dock = customMode.Dock = DockStyle.Fill;
        defaultMode.Margin = new Padding(0, 0, S(5), 0);
        customMode.Margin = new Padding(S(5), 0, S(5), 0);
        modeLayout.Controls.Add(defaultMode, 0, 0);
        modeLayout.Controls.Add(customMode, 1, 0);
        modeLayout.Controls.Add(modeText, 2, 0);
        modeBar.Controls.Add(modeLayout);
        root.Controls.Add(modeBar, 0, 2);

        // Do not embed/re-parent the full Trim Dashboard here. Besides being far
        // too tall for an aircraft profile, that also created opaque panels over
        // the plane background and made scrolling while dragging sliders repaint
        // badly. This compact editor is linked to the same live settings/profile.
        _aircraftBindingButtons.Clear();
        Control trimEditor = CreateAircraftTrimEditor(_aircraftBindingButtons);
        Control trimSection = CreateAircraftEditorSection("Profiles.Section.TrimKeybinds", trimEditor);
        trimSection.Margin = new Padding(0, 0, 0, S(10));
        root.Controls.Add(trimSection, 0, 3);

        Control curves = CreateAircraftCurveEditor();
        Control curveSection = CreateAircraftEditorSection("Profiles.Section.AxisCurves", curves);
        curveSection.Margin = new Padding(0, 0, 0, S(10));
        root.Controls.Add(curveSection, 0, 4);

        var flightBody = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Margin = Padding.Empty };
        AircraftFlightEditorUi flightUi = BuildAircraftFlightAssistantEditor(flightBody);
        Control flightSection = CreateAircraftEditorSection("Profiles.Section.FlightLocked", flightBody);
        flightSection.Margin = new Padding(0, 0, 0, S(10));
        root.Controls.Add(flightSection, 0, 5);

        // The backdrop is now the scrollable content parent. Transparent child
        // controls inherit/paint against this moving cached surface, so dragging
        // the vertical scrollbar cannot smear a viewport-anchored image anymore.
        contentBackdrop.Controls.Add(root);
        scroll.AttachContent(contentBackdrop);
        editorHost.Controls.Add(scroll);
        _profilesPageView.Controls.Add(editorHost);
        editorHost.BringToFront();

        void SizeScrollContent()
        {
            if (scroll.IsDisposed || root.IsDisposed || contentBackdrop.IsDisposed) return;
            int contentHeight = root.Height + S(96);
            int width = scroll.CalculateContentWidth(S(660), contentHeight);
            root.Width = width;
            contentBackdrop.Size = new Size(width, contentHeight);
            // FIX10: the custom profile uses a manual single-parent scroll path.
            // Native Panel.AutoScroll bit-blits child windows while the thumb is
            // tracked; that was the remaining source of stretched/glitching UI
            // controls even after FIX9 made the backdrop itself stable.
            scroll.SetContentExtent(new Size(width, contentHeight));
        }
        scroll.SizeChanged += (_, _) => SizeScrollContent();
        scroll.HandleCreated += (_, _) => BeginInvoke((Action)SizeScrollContent);
        SizeScrollContent();

        void RefreshModeUi()
        {
            if (editorHost.IsDisposed) return;
            bool custom = _activeUseCustomControls;
            defaultMode.BackColor = !custom ? Color.FromArgb(92, 70, 48) : Theme.Control;
            customMode.BackColor = custom ? Color.FromArgb(92, 70, 48) : Theme.Control;
            defaultMode.FlatAppearance.BorderColor = !custom ? Theme.Accent : Theme.Border;
            customMode.FlatAppearance.BorderColor = custom ? Theme.Accent : Theme.Border;
            modeText.Tag = custom ? "i18n:Profiles.Mode.CustomText" : "i18n:Profiles.Mode.DefaultText";
            modeText.Text = custom
                ? VT("Profiles.Mode.CustomText")
                : VT("Profiles.Mode.DefaultText");
            copy.Enabled = custom;
            paste.Enabled = custom && _aircraftSettingsClipboard is not null;
            clipboardStatus.Text = _aircraftSettingsClipboard is null
                ? VT("Profiles.Clipboard.Empty")
                : string.Format(CultureInfo.CurrentCulture, VT("Profiles.Clipboard.Value"), _aircraftSettingsClipboardSource, CompatibilityLabel(_aircraftSettingsClipboardClass));
            RefreshAircraftFlightAssistantEditor(flightUi);
            foreach (Action refreshCurve in _aircraftCurveRefreshers.ToArray()) refreshCurve();
        }

        defaultMode.Click += (_, _) =>
        {
            if (_activeUseCustomControls) SetActiveAircraftControlMode(false);
            RefreshModeUi();
        };
        customMode.Click += (_, _) =>
        {
            if (!_activeUseCustomControls) SetActiveAircraftControlMode(true);
            RefreshModeUi();
        };
        copy.Click += (_, _) => { CopyActiveAircraftSettings(); RefreshModeUi(); };
        paste.Click += (_, _) =>
        {
            if (!PasteAircraftSettingsIntoActiveProfile()) return;
            RefreshModeUi();
        };
        back.Click += (_, _) => CloseAircraftControlEditor(showProfilesBrowser: true);

        editorHost.Disposed += (_, _) =>
        {
            CancelBindingCapture();
            _aircraftCurveRefreshers.Clear();
            _aircraftCurvePasteButtons.Clear();
            _aircraftBindingButtons.Clear();
            if (!_applicationClosing) SaveBindings();
        };
        RefreshModeUi();

        async Task LoadArtworkAsync()
        {
            if (aircraft is null || _aircraftAssets is null) return;
            try
            {
                Task<string?> backgroundTask = _aircraftAssets.GetBackgroundAsync(aircraft, _telemetryShutdown.Token);
                Task<string?> heroTask = _aircraftAssets.GetFrameAsync(aircraft, _telemetryShutdown.Token);
                Task<string?> iconTask = _aircraftAssets.GetIconAsync(aircraft, _telemetryShutdown.Token);
                await Task.WhenAll(backgroundTask, heroTask, iconTask);
                if (editorHost.IsDisposed || !ReferenceEquals(_aircraftControlEditorView, editorHost)) return;
                string? backgroundPath = await backgroundTask;
                string? heroPath = await heroTask;
                string? iconPath = await iconTask;
                Image? background = LoadDetachedImage(backgroundPath);
                Image? artwork = LoadDetachedImage(heroPath) ?? LoadDetachedImage(iconPath);
                Image? headerArtwork = LoadDetachedImage(iconPath) ?? LoadDetachedImage(heroPath);
                contentBackdrop.SetArtwork(background, artwork);
                if (headerArtwork is not null) header.SetArtwork(headerArtwork);
            }
            catch (OperationCanceledException) { }
        }
        _ = LoadArtworkAsync();
    }

    private static string FormatBattleRatings(AircraftInfo? aircraft)
    {
        if (aircraft?.BattleRatings is not { Count: > 0 }) return "";

        // Aircraft profile headers only need the three air-battle ratings.
        // TRB/TSB are Ground Realistic / Ground Simulator ratings for using an
        // aircraft in combined-arms lineups; they are not Test Server ratings.
        string[] displayOrder = ["AB", "RB", "SB"];
        var ratings = new List<string>(displayOrder.Length);
        foreach (string mode in displayOrder)
        {
            KeyValuePair<string, string> match = aircraft.BattleRatings
                .FirstOrDefault(pair => string.Equals(pair.Key, mode, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(match.Value))
                ratings.Add($"{mode} {match.Value}");
        }
        return string.Join("   ", ratings);
    }

    private static Image? LoadDetachedImage(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
        try { using var image = Image.FromFile(path); return new Bitmap(image); }
        catch (Exception ex) when (ex is IOException or ArgumentException or OutOfMemoryException) { return null; }
    }

    private string BindingEditorText(TrimAction action) => _captureAction == action
        ? $"{GetActionName(action)}  ·  LISTENING..."
        : _bindings.TryGetValue(action, out ActionBinding? binding)
            ? $"{GetActionName(action)}  ·  {binding.CompactDisplay}"
            : $"{GetActionName(action)}  ·  Not assigned";

    private Control CreateAircraftEditorSection(string titleKey, Control content)
    {
        var section = new AircraftEditorSectionPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, Math.Max(8, DeviceDpi / 10)),
            Padding = new Padding(Math.Max(8, DeviceDpi / 10))
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, Math.Max(30, DeviceDpi * 30 / 96)));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Label
        {
            Text = VT(titleKey),
            Tag = "i18n:" + titleKey,
            Dock = DockStyle.Fill,
            ForeColor = Theme.Text,
            Font = new Font("Segoe UI Semibold", 11F),
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.Transparent,
            Padding = new Padding(4, 0, 0, 0)
        }, 0, 0);
        content.Dock = DockStyle.Fill;
        content.Margin = Padding.Empty;
        layout.Controls.Add(content, 0, 1);
        section.Controls.Add(layout);
        return section;
    }

    private Control CreateAircraftTrimEditor(Dictionary<TrimAction, Button> buttons)
    {
        int S(int value) => Math.Max(1, (int)Math.Round(value * DeviceDpi / 96f));
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 4,
            MinimumSize = new Size(0, S(185)),
            Padding = new Padding(S(4)),
            Margin = Padding.Empty,
            BackColor = Color.Transparent
        };
        for (int c = 0; c < 4; c++) layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(44)));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(44)));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(72)));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(24)));

        // Eight aircraft-specific trim commands fit in two compact rows. The
        // tooltip carries the long action name so buttons do not become giant
        // just to accommodate one line of text.
        TrimAction[] actions =
        [
            TrimAction.NoseUp, TrimAction.NoseDown, TrimAction.RollLeft, TrimAction.RollRight,
            TrimAction.RudderLeft, TrimAction.RudderRight, TrimAction.StoreCurrentTrim, TrimAction.ResetAll
        ];

        string CompactText(TrimAction action) => GetActionName(action);

        for (int i = 0; i < actions.Length; i++)
        {
            TrimAction action = actions[i];
            Button button = CreateSecondaryButton(CompactText(action));
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(S(3), S(2), S(3), S(2));
            button.Font = new Font("Segoe UI Semibold", 8.4F);
            button.Padding = new Padding(S(2));
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.AutoEllipsis = true;
            button.UseMnemonic = false;
            button.Click += (_, _) => BeginBinding(action);
            button.MouseUp += (_, e) =>
            {
                if (e.Button != MouseButtons.Right) return;
                _bindings.Remove(action);
                _trimHoldStartedTimes.Remove(action);
                ResetForeignChordSuppression();
                UpdateBindingButton(action);
                SaveBindings();
            };
            _toolTip.SetToolTip(button, VT("Profiles.BindButtonTip"));
            buttons[action] = button;
            layout.Controls.Add(button, i % 4, i / 4);
        }

        var settings = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            Padding = new Padding(S(2), S(4), S(2), S(2)),
            Margin = Padding.Empty,
            BackColor = Color.Transparent
        };
        for (int c = 0; c < 4; c++) settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));

        NumericUpDown Step(int column, string label, decimal value, Action<decimal> changed)
        {
            var host = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Margin = new Padding(S(3), 0, S(3), 0),
                BackColor = Color.Transparent
            };
            host.RowStyles.Add(new RowStyle(SizeType.Absolute, S(22)));
            host.RowStyles.Add(new RowStyle(SizeType.Absolute, S(32)));
            host.Controls.Add(new Label
            {
                Text = label,
                Dock = DockStyle.Fill,
                ForeColor = Theme.Muted,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = false,
                Font = new Font("Segoe UI", 8.1F)
            }, 0, 0);
            var n = new NumericUpDown
            {
                Minimum = .01m,
                Maximum = 20m,
                DecimalPlaces = 2,
                Increment = .05m,
                Value = Math.Clamp(value, .01m, 20m),
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                BackColor = Theme.Control,
                ForeColor = Theme.Text,
                Font = new Font("Segoe UI Semibold", 9F)
            };
            n.ValueChanged += (_, _) => changed(n.Value);
            host.Controls.Add(n, 0, 1);
            settings.Controls.Add(host, column, 0);
            return n;
        }
        Step(0, VT("Profiles.PitchStep"), _pitchStepBox.Value, v => { _pitchStepBox.Value = v; SaveBindings(); });
        Step(1, VT("Profiles.RollStep"), _rollStepBox.Value, v => { _rollStepBox.Value = v; SaveBindings(); });
        Step(2, VT("Profiles.RudderStep"), _rudderStepBox.Value, v => { _rudderStepBox.Value = v; SaveBindings(); });

        var repeatHost = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(S(3), 0, S(3), 0),
            BackColor = Color.Transparent
        };
        repeatHost.RowStyles.Add(new RowStyle(SizeType.Absolute, S(22)));
        repeatHost.RowStyles.Add(new RowStyle(SizeType.Absolute, S(32)));
        var repeat = new CheckBox
        {
            Text = VT("Profiles.RepeatHeld"),
            Tag = "i18n:Profiles.RepeatHeld",
            Checked = _repeatWhileHeldBox.Checked,
            Dock = DockStyle.Fill,
            ForeColor = Theme.Text,
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 8.1F),
            AutoEllipsis = false
        };
        var rate = new NumericUpDown
        {
            Minimum = _repeatSpeedSlider.Minimum,
            Maximum = _repeatSpeedSlider.Maximum,
            Value = Math.Clamp(_repeatSpeedSlider.Value, _repeatSpeedSlider.Minimum, _repeatSpeedSlider.Maximum),
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            BackColor = Theme.Control,
            ForeColor = Theme.Text,
            Font = new Font("Segoe UI Semibold", 8.9F)
        };
        repeat.CheckedChanged += (_, _) => { _repeatWhileHeldBox.Checked = repeat.Checked; SaveBindings(); };
        rate.ValueChanged += (_, _) => { _repeatSpeedSlider.Value = (int)rate.Value; SaveBindings(); };
        repeatHost.Controls.Add(repeat, 0, 0);
        var rateHost = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            BackColor = Color.Transparent
        };
        rateHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        rateHost.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, S(56)));
        rateHost.Controls.Add(new Label
        {
            Text = VT("Profiles.Rate"),
            Tag = "i18n:Profiles.Rate",
            ForeColor = Theme.Muted,
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = false,
            Font = new Font("Segoe UI", 8F)
        }, 0, 0);
        rateHost.Controls.Add(rate, 1, 0);
        repeatHost.Controls.Add(rateHost, 0, 1);
        settings.Controls.Add(repeatHost, 3, 0);
        layout.Controls.Add(settings, 0, 2);
        layout.SetColumnSpan(settings, 4);

        var hint = new Label
        {
            Text = VT("Profiles.BindHint"),
            Tag = "i18n:Profiles.BindHint",
            ForeColor = Theme.Muted,
            BackColor = Color.Transparent,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(S(4), 0, 0, 0),
            AutoEllipsis = false,
            Font = new Font("Segoe UI", 8.1F)
        };
        layout.Controls.Add(hint, 0, 3);
        layout.SetColumnSpan(hint, 4);
        return layout;
    }

    private Control CreateAircraftCurveEditor()
    {
        int S(int value) => Math.Max(1, (int)Math.Round(value * DeviceDpi / 96f));
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            MinimumSize = new Size(0, S(430)),
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(S(2)),
            Margin = Padding.Empty,
            BackColor = Color.Transparent
        };
        for (int c = 0; c < 3; c++) layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        AxisTarget[] axes = [AxisTarget.Roll, AxisTarget.Pitch, AxisTarget.Rudder];
        for (int i = 0; i < axes.Length; i++)
        {
            Control card = CreateAircraftCurveEditorCard(axes[i]);
            card.Dock = DockStyle.Fill;
            card.Margin = new Padding(S(4));
            layout.Controls.Add(card, i, 0);
        }
        return layout;
    }

    private Control CreateAircraftCurveEditorCard(AxisTarget axis)
    {
        int S(int value) => Math.Max(1, (int)Math.Round(value * DeviceDpi / 96f));
        string titleKey = axis switch { AxisTarget.Roll => "Curves.Roll", AxisTarget.Pitch => "Curves.Pitch", _ => "Curves.RudderYaw" };
        string title = VT(titleKey);
        var card = new AircraftEditorSubCard
        {
            Dock = DockStyle.Fill,
            MinimumSize = new Size(S(205), S(430)),
            Padding = new Padding(S(8)),
            Margin = Padding.Empty
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Color.Transparent,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(28)));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(32)));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(220)));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Label
        {
            Text = VT(titleKey),
            Tag = "i18n:" + titleKey,
            Dock = DockStyle.Fill,
            ForeColor = Theme.Text,
            Font = new Font("Segoe UI Semibold", 10.3F),
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        layout.Controls.Add(new Label
        {
            Text = VT("Profiles.CurveEditHint"),
            Tag = "i18n:Profiles.CurveEditHint",
            Dock = DockStyle.Fill,
            ForeColor = Theme.Muted,
            Font = new Font("Segoe UI", 8.1F),
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = false,
            UseMnemonic = false,
            Padding = new Padding(S(2), 0, S(2), 0)
        }, 0, 1);

        AxisResponse response = ResponseFor(axis);
        response.Normalize();
        var preview = new AxisCurvePreview
        {
            Dock = DockStyle.Fill,
            MinimumSize = new Size(S(145), S(175)),
            InputLabel = VT("Curves.Input"),
            OutputLabel = VT("Curves.Output"),
            Editable = true,
            ShowEditorHelp = false
        };
        preview.SetResponse(response);
        layout.Controls.Add(preview, 0, 2);

        var fields = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 5,
            Margin = new Padding(0, S(4), 0, 0),
            BackColor = Color.Transparent
        };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, S(104)));
        for (int r = 0; r < 4; r++) fields.RowStyles.Add(new RowStyle(SizeType.Absolute, S(34)));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, S(40)));
        NumericUpDown Field(int row, string label, decimal min, decimal max, decimal value, bool decimals = false)
        {
            fields.Controls.Add(new Label
            {
                Text = label,
                Dock = DockStyle.Fill,
                ForeColor = Theme.Muted,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = false,
                Font = new Font("Segoe UI", 8.7F)
            }, 0, row);
            var n = new NumericUpDown
            {
                Minimum = min,
                Maximum = max,
                Value = value,
                DecimalPlaces = decimals ? 1 : 0,
                Increment = decimals ? .1m : 1m,
                Dock = DockStyle.Fill,
                Margin = new Padding(S(2), S(2), 0, S(2)),
                BackColor = Theme.Control,
                ForeColor = Theme.Text,
                Font = new Font("Segoe UI", 9F)
            };
            fields.Controls.Add(n, 1, row);
            return n;
        }
        var curve = Field(0, VT("Curves.CurveShort"), -100, 100, response.Curve);
        var deadzone = Field(1, VT("Curves.CenterDeadzoneShort"), 0, 30, (decimal)response.Deadzone, true);
        var input = Field(2, VT("Curves.InputRangeShort"), 40, 100, (decimal)response.InputRange);
        var output = Field(3, VT("Curves.OutputRangeShort"), 5, 100, (decimal)response.OutputRange);

        var curveActions = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0, S(3), 0, 0),
            Padding = Padding.Empty,
            BackColor = Color.Transparent
        };
        for (int c = 0; c < 3; c++) curveActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3));
        Button copyCurve = I18n(CreateSecondaryButton(VT("Profiles.Copy")), "Profiles.Copy");
        Button pasteCurve = I18n(CreateSecondaryButton(VT("Profiles.Paste")), "Profiles.Paste");
        Button resetCurve = I18n(CreateSecondaryButton(VT("Profiles.Reset")), "Profiles.Reset");
        foreach (Button button in new[] { copyCurve, pasteCurve, resetCurve })
        {
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(S(2), 0, S(2), 0);
            button.Padding = Padding.Empty;
            button.Font = new Font("Segoe UI Semibold", 7.8F);
            button.AutoEllipsis = false;
        }
        pasteCurve.Enabled = _aircraftCurveClipboard is not null;
        _aircraftCurvePasteButtons.Add(pasteCurve);
        card.Disposed += (_, _) => _aircraftCurvePasteButtons.Remove(pasteCurve);
        _toolTip.SetToolTip(copyCurve, string.Format(CultureInfo.CurrentCulture, VT("Profiles.CopyCurveTip"), title));
        _toolTip.SetToolTip(pasteCurve, _aircraftCurveClipboardSource is null
            ? VT("Profiles.CopyCurveFirst")
            : string.Format(CultureInfo.CurrentCulture, VT("Profiles.PasteCurveTip"), _aircraftCurveClipboardSource));
        _toolTip.SetToolTip(resetCurve, string.Format(CultureInfo.CurrentCulture, VT("Profiles.ResetCurveTip"), title));
        curveActions.Controls.Add(copyCurve, 0, 0);
        curveActions.Controls.Add(pasteCurve, 1, 0);
        curveActions.Controls.Add(resetCurve, 2, 0);
        fields.Controls.Add(curveActions, 0, 4);
        fields.SetColumnSpan(curveActions, 2);

        bool syncing = false;

        void SyncFieldsFromResponse(bool includeCurve)
        {
            AxisResponse r = ResponseFor(axis);
            r.Normalize();
            syncing = true;
            try
            {
                if (includeCurve)
                    curve.Value = Math.Clamp(r.Curve, (int)curve.Minimum, (int)curve.Maximum);
                deadzone.Value = Math.Clamp((decimal)r.Deadzone, deadzone.Minimum, deadzone.Maximum);
                input.Value = Math.Clamp((decimal)r.InputRange, input.Minimum, input.Maximum);
                output.Value = Math.Clamp((decimal)r.OutputRange, output.Minimum, output.Maximum);
            }
            finally { syncing = false; }
        }

        copyCurve.Click += (_, _) =>
        {
            AxisResponse source = ResponseFor(axis);
            source.Normalize();
            _aircraftCurveClipboard = CloneAxisResponse(source);
            _aircraftCurveClipboardSource = $"{AircraftSearchService.CleanName(_activeProfileName)} · {title}";
            RefreshAircraftCurvePasteButtons();
        };
        pasteCurve.Click += (_, _) =>
        {
            if (_aircraftCurveClipboard is null) return;
            SetResponseFor(axis, CloneAxisResponse(_aircraftCurveClipboard));
            foreach (Action refreshCurve in _aircraftCurveRefreshers.ToArray()) refreshCurve();
            RefreshCurveEditors();
            ResetAutomaticInstructorHold();
            SaveBindings();
        };
        resetCurve.Click += (_, _) =>
        {
            SetResponseFor(axis, new AxisResponse());
            foreach (Action refreshCurve in _aircraftCurveRefreshers.ToArray()) refreshCurve();
            RefreshCurveEditors();
            ResetAutomaticInstructorHold();
            SaveBindings();
        };

        void ScalarChanged(bool curveChanged)
        {
            if (syncing) return;
            AxisResponse r = ResponseFor(axis);
            if (curveChanged)
            {
                r.Curve = (int)curve.Value;
                r.ClearCustomCurve();
            }
            r.Deadzone = (double)deadzone.Value;
            r.InputRange = (double)input.Value;
            r.OutputRange = (double)output.Value;
            r.Normalize();
            preview.SetResponse(r);
            RefreshCurveEditors();
            ResetAutomaticInstructorHold();
            SaveBindings();
        }
        curve.ValueChanged += (_, _) => ScalarChanged(true);
        deadzone.ValueChanged += (_, _) => ScalarChanged(false);
        input.ValueChanged += (_, _) => ScalarChanged(false);
        output.ValueChanged += (_, _) => ScalarChanged(false);

        // During a drag only update the inexpensive scalar readouts. Saving the
        // whole profile and refreshing every curve on every mouse-move made the
        // point feel sticky and difficult to position.
        preview.ResponseEdited += (_, _) =>
        {
            if (syncing) return;
            AxisResponse r = ResponseFor(axis);
            if (r.HasCustomCurve) r.Curve = r.EstimateEquivalentCurve();
            r.Normalize();
            // Keep all visible scalar values synchronized while dragging, but do
            // not refresh every editor or touch disk until the mouse is released.
            SyncFieldsFromResponse(includeCurve: true);
            ResetAutomaticInstructorHold();
        };
        preview.ResponseEditCompleted += (_, _) =>
        {
            AxisResponse r = ResponseFor(axis);
            if (r.HasCustomCurve) r.Curve = r.EstimateEquivalentCurve();
            r.Normalize();
            SyncFieldsFromResponse(includeCurve: true);
            RefreshCurveEditors();
            ResetAutomaticInstructorHold();
            SaveBindings();
        };

        void RefreshLocalCurveEditor()
        {
            if (card.IsDisposed) return;
            AxisResponse r = ResponseFor(axis);
            r.Normalize();
            SyncFieldsFromResponse(includeCurve: true);
            preview.SetResponse(r);
        }
        _aircraftCurveRefreshers.Add(RefreshLocalCurveEditor);
        card.Disposed += (_, _) => _aircraftCurveRefreshers.Remove(RefreshLocalCurveEditor);
        _toolTip.SetToolTip(preview, VT("Curves.EditorTooltip"));
        layout.Controls.Add(fields, 0, 3);
        card.Controls.Add(layout);
        return card;
    }

    private static AxisResponse CloneAxisResponse(AxisResponse source)
    {
        source.Normalize();
        return new AxisResponse
        {
            Curve = source.Curve,
            Deadzone = source.Deadzone,
            InputRange = source.InputRange,
            OutputRange = source.OutputRange,
            CustomPoints = source.CustomPoints.Select(point => new AxisCurvePoint(point.X, point.Y)).ToList()
        };
    }

    private void SetResponseFor(AxisTarget axis, AxisResponse response)
    {
        response.Normalize();
        if (axis == AxisTarget.Roll) _rollResponse = response;
        else if (axis == AxisTarget.Pitch) _pitchResponse = response;
        else _rudderResponse = response;
    }

    private void RefreshAircraftCurvePasteButtons()
    {
        foreach (Button button in _aircraftCurvePasteButtons.ToArray())
        {
            if (button.IsDisposed) continue;
            button.Enabled = _aircraftCurveClipboard is not null;
            _toolTip.SetToolTip(button, _aircraftCurveClipboardSource is null
                ? VT("Profiles.CopyCurveFirstStatus")
                : VF("Profiles.PasteCurveStatus", _aircraftCurveClipboardSource));
        }
    }

    private sealed record AircraftFlightEditorUi(Label Status, Label Disclaimer);

    private AircraftFlightEditorUi BuildAircraftFlightAssistantEditor(Control parent)
    {
        int S(int value) => Math.Max(1, (int)Math.Round(value * DeviceDpi / 96f));
        _instructorModeEnabled = false;
        ResetAutomaticInstructorHold();

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(S(10), S(8), S(10), S(8)),
            Margin = Padding.Empty,
            BackColor = Color.Transparent
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, S(72)));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(42)));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var cross = new Label
        {
            Text = "✕",
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(235, 88, 78),
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI Symbol", 28F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            UseMnemonic = false
        };
        layout.Controls.Add(cross, 0, 0);
        layout.SetRowSpan(cross, 2);

        var status = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = Theme.Warning,
            BackColor = Color.Transparent,
            AutoEllipsis = false,
            Padding = new Padding(S(4), 0, 0, 0),
            Font = new Font("Segoe UI Semibold", 10.2F),
            TextAlign = ContentAlignment.MiddleLeft,
            UseMnemonic = false,
            Text = VT("Profiles.FlightLockedTitle"),
            Tag = "i18n:Profiles.FlightLockedTitle"
        };
        layout.Controls.Add(status, 1, 0);

        var disclaimer = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = Color.Gainsboro,
            BackColor = Color.Transparent,
            AutoEllipsis = false,
            Padding = new Padding(S(4), S(2), S(8), 0),
            Font = new Font("Segoe UI", 9F),
            TextAlign = ContentAlignment.TopLeft,
            UseMnemonic = false,
            Text = VT("Profiles.FlightLockedText"),
            Tag = "i18n:Profiles.FlightLockedText"
        };
        layout.Controls.Add(disclaimer, 1, 1);
        parent.Controls.Add(layout);

        return new AircraftFlightEditorUi(status, disclaimer);
    }

    private void RefreshAircraftFlightAssistantEditor(AircraftFlightEditorUi ui)
    {
        // Hard lock: this release never exposes an enable control. Keep runtime
        // state off even if an older profile or binding previously stored ON.
        _instructorModeEnabled = false;
        ResetAutomaticInstructorHold();
        ui.Status.Text = VT("Profiles.FlightLockedTitle");
        ui.Status.Tag = "i18n:Profiles.FlightLockedTitle";
        ui.Status.ForeColor = Theme.Warning;
        ui.Disclaimer.Text = VT("Profiles.FlightLockedText");
        ui.Disclaimer.Tag = "i18n:Profiles.FlightLockedText";
    }


}


internal sealed class AircraftEditorScrollPanel : Panel
{
    private readonly AircraftEditorScrollViewport _viewport;
    private readonly VScrollBar _verticalScroll;
    private Control? _content;
    private int _maxScrollOffset;
    private int _scrollOffset;
    private bool _updatingMetrics;
    private bool _syncingScrollBar;

    internal AircraftEditorScrollPanel()
    {
        // FIX11: keep one explicit scroll model. FIX10 removed AutoScroll, but the
        // native VScrollBar and wheel path could still disagree because one path
        // treated ScrollBar.Value as a pixel offset while the thumb itself uses an
        // effective range of Maximum-LargeChange+1. _scrollOffset is now the only
        // source of truth and both thumb tracking and mouse-wheel input map through it.
        AutoScroll = false;
        DoubleBuffered = true;
        ResizeRedraw = false;
        BackColor = Color.FromArgb(7, 18, 24);
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint, true);

        _viewport = new AircraftEditorScrollViewport
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            BackColor = Color.FromArgb(7, 18, 24),
            TabStop = false
        };
        _verticalScroll = new VScrollBar
        {
            Dock = DockStyle.Right,
            Visible = false,
            Minimum = 0,
            SmallChange = 48,
            LargeChange = 240,
            TabStop = false
        };
        Controls.Add(_viewport);
        Controls.Add(_verticalScroll);
        _verticalScroll.BringToFront();

        _verticalScroll.ValueChanged += (_, _) => ScrollBarValueChanged();
        _viewport.MouseWheel += (_, e) => ScrollByWheel(e);
        _viewport.SizeChanged += (_, _) =>
        {
            UpdateScrollMetrics();
            ApplyScrollPosition();
        };
    }

    internal void AttachContent(Control content)
    {
        if (_content is not null && !ReferenceEquals(_content, content))
            _viewport.Controls.Remove(_content);

        _content = content;
        if (!ReferenceEquals(content.Parent, _viewport))
            _viewport.Controls.Add(content);
        content.BringToFront();
        content.SizeChanged += ContentExtentChanged;
        content.Layout += (_, _) => ContentExtentChanged(content, EventArgs.Empty);
        UpdateScrollMetrics();
        ApplyScrollPosition();
    }

    internal int CalculateContentWidth(int minimumWidth, int contentHeight)
    {
        bool needsVerticalScroll = contentHeight + Padding.Vertical > Math.Max(1, ClientSize.Height);
        int scrollWidth = needsVerticalScroll ? SystemInformation.VerticalScrollBarWidth : 0;
        return Math.Max(minimumWidth,
            ClientSize.Width - Padding.Horizontal - scrollWidth - 2);
    }

    internal void SetContentExtent(Size contentSize)
    {
        if (_content is null || _content.IsDisposed) return;
        if (_content.Size != contentSize) _content.Size = contentSize;
        UpdateScrollMetrics();
        ApplyScrollPosition();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateScrollMetrics();
        ApplyScrollPosition();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if (HasCapturedDescendant(this)) return;
        if (ScrollByWheel(e)) return;
        base.OnMouseWheel(e);
    }

    private void ContentExtentChanged(object? sender, EventArgs e)
    {
        if (_updatingMetrics) return;
        UpdateScrollMetrics();
        ApplyScrollPosition();
    }

    private int MeasureContentExtent()
    {
        if (_content is null || _content.IsDisposed) return 0;
        int extent = Math.Max(0, _content.Height);
        foreach (Control child in _content.Controls)
        {
            if (!child.Visible) continue;
            extent = Math.Max(extent, child.Bottom);
        }
        return extent;
    }

    private void UpdateScrollMetrics()
    {
        if (_updatingMetrics || _content is null || _content.IsDisposed) return;
        _updatingMetrics = true;
        try
        {
            int availableHeight = Math.Max(1, ClientSize.Height - Padding.Vertical);
            int contentExtent = MeasureContentExtent();
            bool needsVertical = contentExtent > availableHeight;

            if (_verticalScroll.Visible != needsVertical)
            {
                _verticalScroll.Visible = needsVertical;
                PerformLayout();
            }

            int page = Math.Max(1, _viewport.ClientSize.Height);
            contentExtent = MeasureContentExtent();
            _maxScrollOffset = Math.Max(0, contentExtent - page);
            _scrollOffset = Math.Clamp(_scrollOffset, 0, _maxScrollOffset);

            // Use direct pixel offsets for the scrollbar model. The native
            // WinForms scrollbar can only reach Maximum-LargeChange+1, so set
            // Maximum so that this effective end exactly equals _maxScrollOffset.
            _verticalScroll.Minimum = 0;
            _verticalScroll.SmallChange = Math.Max(24, page / 10);
            _verticalScroll.LargeChange = Math.Max(1, Math.Min(page, Math.Max(1, contentExtent)));
            _verticalScroll.Maximum = Math.Max(_verticalScroll.Minimum,
                _maxScrollOffset + _verticalScroll.LargeChange - 1);
            SyncScrollBarFromOffset();
        }
        finally
        {
            _updatingMetrics = false;
        }
    }

    private int EffectiveScrollBarMaximum()
    {
        if (!_verticalScroll.Visible) return 0;
        return Math.Max(0, _verticalScroll.Maximum - _verticalScroll.LargeChange + 1);
    }

    private int ScrollBarValueToOffset(int value)
    {
        int barMax = EffectiveScrollBarMaximum();
        if (barMax <= 0 || _maxScrollOffset <= 0) return 0;
        return Math.Clamp(value, 0, _maxScrollOffset);
    }

    private int OffsetToScrollBarValue(int offset)
    {
        int barMax = EffectiveScrollBarMaximum();
        if (barMax <= 0 || _maxScrollOffset <= 0) return 0;
        return Math.Clamp(offset, 0, Math.Min(barMax, _maxScrollOffset));
    }

    private void ScrollBarValueChanged()
    {
        if (_syncingScrollBar || _updatingMetrics || !_verticalScroll.Visible) return;
        int next = ScrollBarValueToOffset(_verticalScroll.Value);
        if (next == _scrollOffset) return;
        _scrollOffset = next;
        ApplyScrollPosition();
    }

    private void SyncScrollBarFromOffset()
    {
        int target = OffsetToScrollBarValue(_scrollOffset);
        if (_verticalScroll.Value == target) return;
        _syncingScrollBar = true;
        try
        {
            _verticalScroll.Value = Math.Clamp(target, _verticalScroll.Minimum,
                Math.Max(_verticalScroll.Minimum, _verticalScroll.Maximum));
        }
        finally { _syncingScrollBar = false; }
    }

    private void ApplyScrollPosition()
    {
        if (_content is null || _content.IsDisposed) return;
        _scrollOffset = Math.Clamp(_scrollOffset, 0, _maxScrollOffset);
        int targetX = 0;
        int targetY = -_scrollOffset;
        if (_content.Left == targetX && _content.Top == targetY) return;

        _content.SetBounds(targetX, targetY, _content.Width, _content.Height,
            BoundsSpecified.Location);
        // Keep thumb tracking asynchronous: never call Update() here.
        _viewport.Invalidate(true);
    }

    private bool ScrollByWheel(MouseEventArgs e)
    {
        if (!_verticalScroll.Visible || HasCapturedDescendant(this)) return false;
        const int wheelDelta = 120;
        int notches = e.Delta / wheelDelta;
        if (notches == 0) notches = Math.Sign(e.Delta);
        int lines = SystemInformation.MouseWheelScrollLines;
        int unit = lines > 0
            ? Math.Max(16, _verticalScroll.SmallChange * Math.Min(lines, 6) / 3)
            : _verticalScroll.LargeChange;
        int next = Math.Clamp(_scrollOffset - (notches * unit), 0, _maxScrollOffset);
        if (next == _scrollOffset) return true;
        _scrollOffset = next;
        SyncScrollBarFromOffset();
        ApplyScrollPosition();
        return true;
    }

    private static bool HasCapturedDescendant(Control root)
    {
        foreach (Control child in root.Controls)
        {
            if (child.Capture || HasCapturedDescendant(child)) return true;
        }
        return false;
    }
}

internal sealed class AircraftEditorScrollViewport : Panel
{
    private const int WsExComposited = 0x02000000;

    internal AircraftEditorScrollViewport()
    {
        AutoScroll = false;
        DoubleBuffered = true;
        ResizeRedraw = false;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint, true);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            // Composite descendant painting into one frame. This prevents the
            // transparent/card controls from exposing partially moved child
            // windows while the custom scrollbar is being dragged quickly.
            cp.ExStyle |= WsExComposited;
            return cp;
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
    }
}


internal sealed class AircraftEditorBackdropPanel : Panel
{
    private static readonly Color WikiBlue = Color.FromArgb(12, 70, 101);
    private Image? _background;
    private Image? _aircraft;
    private Bitmap? _renderCache;
    private Size _renderCacheSize;
    private string _title = "Aircraft", _nation = "", _role = "", _br = "";

    public AircraftEditorBackdropPanel()
    {
        DoubleBuffered = true;
        ResizeRedraw = false;
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        BackColor = Color.FromArgb(7, 18, 24);
    }

    public void SetText(string title, string nation, string role, string br)
    {
        _title = title;
        _nation = nation;
        _role = role;
        _br = br;
        Invalidate();
    }

    public void SetArtwork(Image? background, Image? aircraft)
    {
        _background?.Dispose();
        _aircraft?.Dispose();
        _background = background;
        _aircraft = aircraft;
        ResetRenderCache();
        Invalidate(true);
    }

    protected override void OnPaintBackground(PaintEventArgs e) => DrawBackdrop(e.Graphics);

    internal void DrawBackdrop(Graphics graphics)
    {
        if (ClientSize.Width <= 0 || ClientSize.Height <= 0)
        {
            graphics.Clear(WikiBlue);
            return;
        }
        if (_renderCache is null || _renderCacheSize != ClientSize) BuildRenderCache();
        if (_renderCache is not null) graphics.DrawImageUnscaled(_renderCache, 0, 0);
        else graphics.Clear(WikiBlue);
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        ResetRenderCache();
        base.OnSizeChanged(e);
    }

    private void ResetRenderCache()
    {
        _renderCache?.Dispose();
        _renderCache = null;
        _renderCacheSize = Size.Empty;
    }

    private void BuildRenderCache()
    {
        ResetRenderCache();
        if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return;
        var bitmap = new Bitmap(ClientSize.Width, ClientSize.Height);
        using Graphics g = Graphics.FromImage(bitmap);
        g.Clear(WikiBlue);
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

        if (_background is not null)
        {
            // Keep the flag anchored in the upper-left corner at its native
            // aspect ratio, with the Wiki-blue surface across the rest of the page.
            float flagWidth = Math.Min(1280f, Width * .76f);
            float flagHeight = flagWidth * _background.Height / _background.Width;
            if (flagHeight > 660f)
            {
                flagHeight = 660f;
                flagWidth = flagHeight * _background.Width / _background.Height;
            }
            RectangleF flagArea = new(10f, 10f, flagWidth, flagHeight);
            g.DrawImage(_background, flagArea);

            float fadeWidth = flagArea.Width * .38f;
            using var rightFade = new System.Drawing.Drawing2D.LinearGradientBrush(
                new RectangleF(flagArea.Right - fadeWidth, flagArea.Top, fadeWidth, flagArea.Height),
                Color.FromArgb(0, WikiBlue), WikiBlue, 0f);
            g.FillRectangle(rightFade, flagArea.Right - fadeWidth, flagArea.Top, fadeWidth, flagArea.Height);

            float fadeHeight = flagArea.Height * .34f;
            using var bottomFade = new System.Drawing.Drawing2D.LinearGradientBrush(
                new RectangleF(flagArea.Left, flagArea.Bottom - fadeHeight, flagArea.Width, fadeHeight),
                Color.FromArgb(0, WikiBlue), WikiBlue, 90f);
            g.FillRectangle(bottomFade, flagArea.Left, flagArea.Bottom - fadeHeight, flagArea.Width, fadeHeight);
        }
        if (_aircraft is not null && Width > 80 && Height > 80)
        {
            // Keep the aircraft near the top, like the Wiki detail card, so
            // the trim and curve controls below sit on the calm blue surface.
            float heroHeight = Math.Min(440f, Math.Max(220f, Width * .34f));
            RectangleF aircraftArea = new(Width * .40f, 12f, Width * .57f, heroHeight);
            DrawContain(g, _aircraft, aircraftArea);
        }
        using var shade = new SolidBrush(Color.FromArgb(28, 5, 11, 15));
        g.FillRectangle(shade, 0, 0, ClientSize.Width, ClientSize.Height);
        using var topShade = new System.Drawing.Drawing2D.LinearGradientBrush(
            new Rectangle(0, 0, Math.Max(1, Width), Math.Max(1, Math.Min(260, Height))),
            Color.FromArgb(32, 0, 0, 0), Color.FromArgb(0, 0, 0, 0), 90f);
        g.FillRectangle(topShade, 0, 0, Width, Math.Min(260, Height));
        _renderCache = bitmap;
        _renderCacheSize = ClientSize;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var border = new Pen(Color.FromArgb(125, 205, 220, 226));
        if (Width > 1 && Height > 1) e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
    }

    private static void DrawContain(Graphics g, Image image, RectangleF destination)
    {
        if (image.Width <= 0 || image.Height <= 0 || destination.Width <= 0 || destination.Height <= 0) return;
        float scale = Math.Min(destination.Width / image.Width, destination.Height / image.Height);
        float width = image.Width * scale;
        float height = image.Height * scale;
        g.DrawImage(image, destination.X + (destination.Width - width) / 2f,
            destination.Y + (destination.Height - height) / 2f, width, height);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _background?.Dispose();
            _aircraft?.Dispose();
            _renderCache?.Dispose();
        }
        base.Dispose(disposing);
    }
}


internal sealed class AircraftEditorGlassPanel : Panel
{
    public AircraftEditorGlassPanel()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        BackColor = Color.Transparent;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        base.OnPaintBackground(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        // Border only: do not paint a gray panel behind these controls. The
        // aircraft/theme artwork is intentionally visible through the frame.
        using var border = new Pen(Color.FromArgb(95, 205, 220, 226));
        if (Width > 1 && Height > 1) e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
        base.OnPaint(e);
    }
}

internal sealed class AircraftEditorHeaderPanel : Panel
{
    private string _title = "Aircraft", _nation = "", _role = "", _br = "";
    private Image? _artwork;

    public AircraftEditorHeaderPanel()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        BackColor = Color.Transparent;
    }

    public void SetText(string title, string nation, string role, string br)
    { _title = title; _nation = nation; _role = role; _br = br; Invalidate(); }

    public void SetArtwork(Image artwork)
    {
        _artwork?.Dispose();
        _artwork = AircraftCard.PrepareArtwork(artwork);
        artwork.Dispose();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        Rectangle frame = new(10, 8, Math.Max(1, Width - 20), Math.Max(80, Height - 16));
        using var panel = new SolidBrush(Color.FromArgb(48, 12, 23, 29));
        g.FillRectangle(panel, frame);
        using var border = new Pen(Color.FromArgb(80, 220, 230, 235));
        g.DrawRectangle(border, frame);

        int artworkWidth = _artwork is null ? 0 : Math.Clamp((int)(frame.Width * .30f), 150, 330);
        int textRight = _artwork is null ? frame.Right - 16 : frame.Right - artworkWidth - 28;
        int textWidth = Math.Max(160, textRight - (frame.X + 16));
        using var titleFont = new Font("Segoe UI Semibold", Math.Clamp(Height * .25f, 22f, 34f), FontStyle.Regular, GraphicsUnit.Pixel);
        using var subFont = new Font("Segoe UI", Math.Clamp(Height * .125f, 12f, 16f), FontStyle.Regular, GraphicsUnit.Pixel);
        TextRenderer.DrawText(g, _title, titleFont,
            new Rectangle(frame.X + 16, frame.Y + 10, textWidth, 42), Color.WhiteSmoke,
            TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(g, $"{_nation.ToUpperInvariant()}   {_role}", subFont,
            new Rectangle(frame.X + 16, frame.Y + 54, textWidth, 25), Color.Gainsboro,
            TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        if (!string.IsNullOrWhiteSpace(_br))
            TextRenderer.DrawText(g, _br, subFont,
                new Rectangle(frame.X + 16, frame.Y + 80, textWidth, Math.Max(20, frame.Height - 86)), Color.Wheat,
                TextFormatFlags.Left | TextFormatFlags.WordBreak);

        if (_artwork is not null)
        {
            RectangleF artArea = new(frame.Right - artworkWidth - 12, frame.Y + 8,
                artworkWidth, Math.Max(20, frame.Height - 16));
            float scale = Math.Min(artArea.Width / _artwork.Width, artArea.Height / _artwork.Height);
            float w = _artwork.Width * scale;
            float h = _artwork.Height * scale;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(_artwork, artArea.X + (artArea.Width - w) / 2f,
                artArea.Y + (artArea.Height - h) / 2f, w, h);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _artwork?.Dispose();
        base.Dispose(disposing);
    }
}

internal class AircraftEditorSectionPanel : Panel
{
    protected virtual int VeilAlpha => 0;

    public AircraftEditorSectionPanel()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Color.Transparent;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        base.OnPaintBackground(e);
        if (VeilAlpha > 0)
        {
            using var veil = new SolidBrush(Color.FromArgb(VeilAlpha, 8, 13, 17));
            e.Graphics.FillRectangle(veil, ClientRectangle);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var border = new Pen(Color.FromArgb(105, 145, 158, 166));
        if (Width > 1 && Height > 1) e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
    }
}

internal sealed class AircraftEditorSubCard : AircraftEditorSectionPanel
{
    protected override int VeilAlpha => 0;
}
