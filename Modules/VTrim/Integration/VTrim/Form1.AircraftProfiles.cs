using System.Net.Http;
using System.Text.Json;

namespace HOTASTrimUtility;

public partial class Form1
{
    private const string DefaultProfileDisplayName = "Default Profile";
    private const int AutoProfileConfirmationReads = 3;
    private AircraftDatabaseService? _aircraftDatabase;
    private AircraftAssetCache? _aircraftAssets;
    private readonly HttpClient _aircraftHttp = new() { Timeout = TimeSpan.FromSeconds(30) };
    private string? _profileAircraftId;
    private string? _profileDetectedAircraftKey;
    private string? _autoProfileCandidateKey;
    private string? _manualProfileAircraftKey;
    private bool _choosingAircraftProfile;

    private void PreserveManualProfileSelection()
    {
        if (!_autoProfileSwitchInProgress) _manualProfileAircraftKey = _autoProfileCandidateKey;
    }
    private int _autoProfileCandidateReads;
    private bool _autoProfileSwitchInProgress;
    private double _lastAutoProfilePollAt;
    private string _profileId = Guid.NewGuid().ToString("D");
    private void StartAircraftDatabase()
    {
        if (_offlinePreview || _aircraftDatabase is not null) return;
        _aircraftDatabase = new(SettingsDirectory, new WarThunderWikiAircraftProvider(_aircraftHttp));
        _aircraftAssets = new(SettingsDirectory, _aircraftHttp);
        _aircraftDatabase.Changed += () =>
        {
            try { if (IsHandleCreated && !IsDisposed) BeginInvoke((Action)(() => { RepairExistingAircraftProfiles(); ReconcileActiveProfileAircraftMetadata(); UpdateProfileSummary(); AircraftProfilesChanged?.Invoke(); })); }
            catch (InvalidOperationException) { }
        };
        _ = Task.Run(async () =>
        {
            try
            {
                await _aircraftDatabase.InitializeAsync(_telemetryShutdown.Token);
                // Restore the original persistent local-icon workflow, but make it
                // incremental: cached icons are reused immediately and only new,
                // missing, corrupt or source-changed aircraft assets are downloaded.
                await _aircraftAssets.SynchronizeIconsAsync(_aircraftDatabase.Items, _aircraftDatabase.ChangedIconIds, _telemetryShutdown.Token);
            }
            catch (OperationCanceledException) { }
        });
        Disposed += (_, _) => _aircraftHttp.Dispose();
    }

    private void RepairExistingAircraftProfiles()
    {
        if (_aircraftDatabase is null || _aircraftDatabase.Items.Count == 0) return;
        foreach (string name in GetProfileNames())
        {
            if (string.Equals(name, DefaultProfileDisplayName, StringComparison.OrdinalIgnoreCase) ||
                IsCustomBaseProfileName(name)) continue;
            try
            {
                // Leave damaged files to the existing backup recovery path. A
                // metadata repair must never replace a user's controls with defaults.
                string path = GetProfileFilePath(name);
                if (!File.Exists(path)) continue;
                SavedBindingsFile? profile = JsonSerializer.Deserialize<SavedBindingsFile>(
                    File.ReadAllText(path), BindingsJsonOptions);
                if (profile is null) continue;
                AircraftInfo? aircraft = _aircraftDatabase.ResolveProfile(profile.AircraftId, profile.DetectedAircraftKey, name);
                if (!BackfillAircraftId(profile, aircraft)) continue;
                // Repair only identity metadata. Keep the user's controls, mode,
                // profile name and saved detection key exactly as they were.
                WriteProfile(name, profile);
                if (string.Equals(name, _activeProfileName, StringComparison.OrdinalIgnoreCase))
                    _profileAircraftId = profile.AircraftId;
            }
            catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or UnauthorizedAccessException) { }
        }
    }

    private static bool BackfillAircraftId(SavedBindingsFile profile, AircraftInfo? aircraft)
    {
        if (aircraft is null || string.Equals(profile.AircraftId, aircraft.Id, StringComparison.Ordinal)) return false;
        profile.AircraftId = aircraft.Id;
        return true;
    }

    private bool ChooseAircraftProfile(string initialName, string? initialAircraft, out string name, out string? aircraftId)
    {
        StartAircraftDatabase();
        name = initialName; aircraftId = initialAircraft;
        using var dialog = new Form { Text = initialName.Length == 0 ? VT("Profiles.NewProfile") : VT("Profiles.EditProfile"),
            ClientSize = new Size(490, 380), MinimumSize = new Size(490, 410), StartPosition = FormStartPosition.CenterParent,
            BackColor = Theme.Background, ForeColor = Theme.Text, AutoScaleMode = AutoScaleMode.Dpi,
            AutoScaleDimensions = new SizeF(96, 96), Font = new Font("Segoe UI", 11) };
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_telemetryShutdown.Token);
        using var tips = new ToolTip();
        var images = new Dictionary<string, Image>();
        var requested = new HashSet<string>();
        var flags = new List<Image>();
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 7 };
        foreach (int height in new[] { 0, 0, 92, 26, 34, 90, 44 }) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var input = new TextBox { Name = "ProfileName", Dock = DockStyle.Fill, Text = AircraftSearchService.CleanName(initialName), BackColor = Theme.Control, ForeColor = Theme.Text, MaxLength = 48 };
        var query = new TextBox { Name = "AircraftSearch", Dock = DockStyle.Fill, BackColor = Theme.Control, ForeColor = Theme.Text };
        var nations = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true };
        var results = new ComboBox { Name = "AircraftResults", Width = 330, Anchor = AnchorStyles.Top | AnchorStyles.Left, DrawMode = DrawMode.OwnerDrawFixed,
            DropDownStyle = ComboBoxStyle.DropDownList, ItemHeight = 76, DropDownHeight = 320, IntegralHeight = false,
            BackColor = Theme.Control, ForeColor = Theme.Text, DisplayMember = nameof(AircraftInfo.DisplayName) };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var save = CreatePrimaryButton(initialName.Length == 0 ? VT("Profiles.Create") : VT("Profiles.Save"));
        save.Name = "SaveAircraftProfile";
        var cancel = CreateSecondaryButton(VT("Common.Cancel"));
        save.AutoSize = cancel.AutoSize = true;
        cancel.DialogResult = DialogResult.Cancel;
        buttons.Controls.AddRange([save, cancel]);
        string? selected = initialAircraft, nation = null;
        string? autoName = null;
        bool synchronizing = false;
        void UpdateSave() => save.Enabled = selected is not null && !string.IsNullOrWhiteSpace(input.Text);
        void RefreshResults()
        {
            if (dialog.IsDisposed || cancellation.IsCancellationRequested) return;
            var source = (_aircraftDatabase?.Items ?? []).Concat(AircraftDatabaseService.UniversalAircraft);
            var found = AircraftSearchService.Search(source.Where(a => nation is null || a.Nation == nation), query.Text).ToArray();
            results.BeginUpdate();
            results.DataSource = found;
            results.SelectedItem = found.FirstOrDefault(a => a.Id == selected);
            results.EndUpdate();
            UpdateSave();
        }
        async void LoadImage(AircraftInfo aircraft)
        {
            if (_aircraftAssets is null || !requested.Add(aircraft.Id) || cancellation.IsCancellationRequested) return;
            try
            {
                string? path = await _aircraftAssets.GetIconAsync(aircraft, cancellation.Token);
                if (dialog.IsDisposed || cancellation.IsCancellationRequested || path is null) return;
                using var bitmap = Image.FromFile(path);
                images[aircraft.Id] = AircraftCard.PrepareArtwork(bitmap);
                results.Invalidate();
            }
            catch (Exception ex) when (ex is OperationCanceledException or IOException or ArgumentException) { }
        }
        results.DrawItem += (_, e) =>
        {
            if (e.Index < 0 || results.Items[e.Index] is not AircraftInfo aircraft) return;
            images.TryGetValue(aircraft.Id, out var image);
            if (image is null) LoadImage(aircraft);
            AircraftCard.PaintAircraft(e.Graphics, e.Bounds, aircraft.DisplayName, image, aircraft.IsPremium,
                (e.State & DrawItemState.Selected) != 0, results.Font);
        };
        results.SelectionChangeCommitted += (_, _) =>
        {
            if (results.SelectedItem is not AircraftInfo aircraft) return;
            selected = aircraft.Id;
            synchronizing = true;
            query.Text = AircraftSearchService.CleanName(aircraft.DisplayName);
            input.Text = autoName = query.Text;
            synchronizing = false;
            UpdateSave();
        };
        input.TextChanged += (_, _) =>
        {
            if (synchronizing) return;
            if (string.IsNullOrWhiteSpace(input.Text))
            {
                synchronizing = true; query.Clear(); synchronizing = false;
                selected = null; autoName = null; RefreshResults();
            }
            UpdateSave();
        };
        query.TextChanged += (_, _) =>
        {
            if (synchronizing) return;
            selected = null;
            if (input.Text == autoName)
            {
                synchronizing = true; input.Clear(); synchronizing = false; autoName = null;
            }
            RefreshResults();
        };
        var nationButtons = new List<Button>();
        foreach (var (id, title) in new[] { ("", "All"), ("usa", "USA"), ("germany", "Germany"), ("ussr", "USSR"),
            ("britain", "Britain"), ("japan", "Japan"), ("china", "China"), ("italy", "Italy"), ("france", "France"), ("sweden", "Sweden"), ("israel", "Israel") })
        {
            var button = new Button { Width = 50, Height = 40, Margin = new Padding(2), FlatStyle = FlatStyle.Flat,
                BackColor = Theme.Control, ForeColor = Theme.Text, Text = id.Length == 0 ? VT("Profiles.All") : "", AccessibleName = title };
            button.FlatAppearance.BorderColor = id.Length == 0 ? Color.WhiteSmoke : Theme.Control;
            if (id.Length > 0)
            {
                string resource = typeof(Form1).Assembly.GetManifestResourceNames().First(n => n.EndsWith(".Nations." + id + ".png", StringComparison.Ordinal));
                using var stream = typeof(Form1).Assembly.GetManifestResourceStream(resource)!;
                using var original = Image.FromStream(stream);
                var flag = new Bitmap(original, new Size(40, 25)); flags.Add(flag); button.Image = flag;
            }
            tips.SetToolTip(button, id.Length == 0 ? VT("Profiles.AllNations") : title);
            button.Click += (_, _) =>
            {
                nation = id.Length == 0 ? null : id;
                selected = null;
                foreach (var other in nationButtons) other.FlatAppearance.BorderColor = other == button ? Color.WhiteSmoke : Theme.Control;
                RefreshResults();
            };
            nationButtons.Add(button); nations.Controls.Add(button);
        }
        save.Click += (_, _) => { if (selected is not null && !string.IsNullOrWhiteSpace(input.Text)) dialog.DialogResult = DialogResult.OK; };
        layout.Controls.Add(new Label { Text = VT("Profiles.ProfileName"), Dock = DockStyle.Fill }, 0, 0);
        layout.Controls.Add(input, 0, 1);
        input.Visible = false;
        layout.Controls.Add(nations, 0, 2);
        layout.Controls.Add(new Label { Text = VT("Profiles.AircraftLabel"), Dock = DockStyle.Fill }, 0, 3);
        layout.Controls.Add(query, 0, 4);
        layout.Controls.Add(results, 0, 5);
        layout.Controls.Add(buttons, 0, 6);
        dialog.Controls.Add(layout);
        void Changed()
        {
            try { if (dialog.IsHandleCreated && !dialog.IsDisposed) dialog.BeginInvoke((Action)RefreshResults); }
            catch (InvalidOperationException) { }
        }
        if (_aircraftDatabase is not null) _aircraftDatabase.Changed += Changed;
        try
        {
            synchronizing = true;
            query.Text = AircraftSearchService.CleanName(_aircraftDatabase?.GetById(initialAircraft)?.DisplayName);
            synchronizing = false;
            RefreshResults();
            _choosingAircraftProfile = true;
            if (dialog.ShowDialog(this) != DialogResult.OK) return false;
            name = AircraftSearchService.CleanName(_aircraftDatabase?.GetById(selected)?.DisplayName ?? input.Text.Trim()); aircraftId = selected; return true;
        }
        finally
        {
            _choosingAircraftProfile = false;
            cancellation.Cancel();
            if (_aircraftDatabase is not null) _aircraftDatabase.Changed -= Changed;
            foreach (var image in images.Values) image.Dispose();
            foreach (var flag in flags) flag.Dispose();
        }
    }

    private void CloneAircraftProfile()
    {
        if (string.IsNullOrEmpty(_activeProfileName)) return;
        if (!ChooseAircraftProfile("", null, out string name, out string? aircraftId)) return;
        CloneToAircraft(name, aircraftId, target => MessageBox.Show(this,
            VF("Profiles.CloneOverwrite", target),
            VT("Profiles.CloneTitle"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2) == DialogResult.Yes);
    }

    private bool CloneToAircraft(string name, string? aircraftId, Func<string, bool> confirmOverwrite)
    {
        if (string.IsNullOrEmpty(aircraftId)) return false;
        name = NormalizeProfileName(name);
        var existingName = GetProfileNames().FirstOrDefault(n => ReadProfile(n)?.AircraftId == aircraftId);
        // Different aircraft can share a display name; never overwrite one by name alone.
        if (existingName is null && ProfileExists(name)) name = GetUniqueProfileName(name);
        if (existingName is not null && !confirmOverwrite(existingName)) return false;
        var destination = existingName is null ? null : ReadProfile(existingName);
        var targetAircraft = _aircraftDatabase?.GetById(aircraftId);
        var source = CaptureCurrentProfile();
        string targetType = NormalizeAircraftType(targetAircraft?.FlightCategory ?? destination?.AircraftType);
        var compatibilityTarget = destination ?? new SavedBindingsFile
        {
            ProfileName = name, AircraftType = targetType, UseCustomControls = true, HasCustomControlsSnapshot = true,
            DampingSupport = targetType == "Jet Plane" ? DampingUnknown : DampingUnavailable, Version = 13
        };
        string? sourceClass = GetAircraftSettingsCompatibilityClass(source, out string sourceDescription);
        string? targetClass = GetAircraftSettingsCompatibilityClass(compatibilityTarget, out string targetDescription);
        if (sourceClass is null || targetClass is null || !string.Equals(sourceClass, targetClass, StringComparison.Ordinal))
        {
            MessageBox.Show(this, VF("Profiles.IncompatibleCopy", sourceDescription, targetDescription),
                VT("Profiles.IncompatibleTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }
        var copy = destination is null ? new SavedBindingsFile() : CloneProfileData(destination);
        CopyControlSettings(source, copy);
        copy.Id = destination?.Id ?? Guid.NewGuid().ToString("D");
        copy.AircraftId = aircraftId;
        copy.DetectedAircraftKey = null;
        copy.AircraftType = targetType;
        copy.UseCustomControls = true;
        copy.HasCustomControlsSnapshot = true;
        copy.DampingSupport = compatibilityTarget.DampingSupport;
        copy.Version = 13;
        WriteProfile(existingName ?? name, copy);
        RefreshProfileList();
        AircraftProfilesChanged?.Invoke();
        SetInstruction(VF("Profiles.Cloned", existingName ?? name), Theme.Success);
        return true;
    }

    private void EditProfileAircraft()
    {
        if (string.IsNullOrEmpty(_activeProfileName)) return;
        if (!ChooseAircraftProfile(_activeProfileName, _profileAircraftId, out string name, out string? id)) return;
        name = NormalizeProfileName(name);
        bool renamed = !string.Equals(name, _activeProfileName, StringComparison.OrdinalIgnoreCase);
        if (renamed && ProfileExists(name)) { ShowProfileExistsMessage(name); return; }
        SaveBindings();
        PreserveManualProfileSelection();
        // Editing identity must preserve a dormant Custom Controls snapshot.
        var profile = ReadProfile(_activeProfileName);
        bool aircraftChanged = !string.Equals(profile.AircraftId, id, StringComparison.Ordinal);
        profile.ProfileName = name;
        profile.AircraftId = id;
        if (aircraftChanged)
        {
            profile.DetectedAircraftKey = null;
            _profileDetectedAircraftKey = null;
            profile.AircraftType = _aircraftDatabase?.GetById(id)?.FlightCategory ?? profile.AircraftType;
            if (!IsFlightAssistantEligibleForProfile(profile, out _)) profile.InstructorModeEnabled = false;
        }
        string oldPath = GetProfileFilePath(_activeProfileName);
        WriteProfile(name, profile);
        _activeProfileName = name;
        ApplySavedProfile(PrepareAircraftProfileForRuntime(profile));
        WriteActiveProfileName();
        if (renamed)
            foreach (string path in new[] { oldPath, oldPath + ".bak", oldPath + ".pre-1.3.1.bak" })
                if (File.Exists(path)) File.Delete(path);
        RefreshProfileList();
        UpdateProfileSummary();
    }
    private static string NormalizeDetectedAircraftKey(string? value) =>
        AircraftSearchService.Normalize(AircraftSearchService.CleanName(value));

    private AircraftInfo? ResolveDetectedAircraft(string detectedType)
    {
        string key = NormalizeDetectedAircraftKey(detectedType);
        if (key.Length == 0 || _aircraftDatabase is null) return null;
        var exact = _aircraftDatabase.Items.Where(aircraft =>
            string.Equals(AircraftSearchService.Normalize(aircraft.Id), key, StringComparison.Ordinal) ||
            string.Equals(AircraftSearchService.Normalize(aircraft.DisplayName), key, StringComparison.Ordinal) ||
            (aircraft.SearchAliases?.Any(alias =>
                string.Equals(AircraftSearchService.Normalize(alias), key, StringComparison.Ordinal)) ?? false)).ToArray();
        return exact.Length == 1 ? exact[0] : null;
    }

    private bool CurrentProfileMatchesDetectedAircraft(string key, AircraftInfo? aircraft)
    {
        if (string.IsNullOrWhiteSpace(_activeProfileName)) return false;
        if (string.Equals(NormalizeDetectedAircraftKey(_profileDetectedAircraftKey), key, StringComparison.Ordinal)) return true;
        return aircraft is not null && string.Equals(NormalizeDetectedAircraftKey(_profileAircraftId),
            NormalizeDetectedAircraftKey(aircraft.Id), StringComparison.Ordinal);
    }

    private string? FindProfileForDetectedAircraft(string key, AircraftInfo? aircraft)
    {
        string? idMatch = null, nameMatch = null;
        foreach (string name in GetProfileNames())
        {
            SavedBindingsFile profile;
            try { profile = ReadProfile(name); }
            catch (Exception ex) when (ex is IOException or JsonException) { continue; }
            if (string.Equals(NormalizeDetectedAircraftKey(profile.DetectedAircraftKey), key, StringComparison.Ordinal))
                return name;
            if (aircraft is not null && string.Equals(NormalizeDetectedAircraftKey(profile.AircraftId),
                NormalizeDetectedAircraftKey(aircraft.Id), StringComparison.Ordinal))
                idMatch ??= name;
            if (_aircraftDatabase?.GetById(profile.AircraftId) is null &&
                string.Equals(AircraftSearchService.Normalize(name), key, StringComparison.Ordinal))
                nameMatch ??= name;
        }
        return idMatch ?? nameMatch;
    }

    private SavedBindingsFile ReadDefaultProfileTemplate() => ReadDefaultProfileTemplateFromPath(DefaultSetupFilePath);

    private static SavedBindingsFile ReadDefaultProfileTemplateFromPath(string path)
    {
        SavedBindingsFile profile = ReadBindingsFileWithBackup(path) ?? new SavedBindingsFile();
        profile.ProfileName = string.Empty;
        profile.AircraftId = null;
        profile.DetectedAircraftKey = null;
        profile.InstructorModeEnabled = false;
        NormalizeAircraftControlModeMetadata(profile, isDefaultProfile: true);
        profile.Version = Math.Max(profile.Version, 13);
        return profile;
    }

    // Called by --self-test. Exercises the production persistence/runtime paths
    // using temporary files only; does not construct a form or contact hardware.
    internal static IReadOnlyList<string> RunProfileStorageSelfTests()
    {
        var failures = new List<string>();
        string folder = Path.Combine(Path.GetTempPath(), "WTA-profile-test-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(folder, "defaults.json");
        void Check(bool condition, string name) { if (!condition) failures.Add(name); }
        Directory.CreateDirectory(folder);
        try
        {
            var defaults = new SavedBindingsFile { ProfileName = "old metadata", AircraftId = "old-aircraft",
                DetectedAircraftKey = "old-key", PitchStep = 1.25M, InstructorModeEnabled = true };
            WriteTextWithBackupAtomic(path, JsonSerializer.Serialize(defaults, BindingsJsonOptions));
            defaults.PitchStep = 2.50M;
            WriteTextWithBackupAtomic(path, JsonSerializer.Serialize(defaults, BindingsJsonOptions));
            File.WriteAllText(path, "{broken");
            var recovered = ReadDefaultProfileTemplateFromPath(path);
            Check(recovered.PitchStep == 1.25M, "Profile: corrupt default recovers backup");
            Check(recovered.ProfileName.Length == 0 && recovered.AircraftId is null &&
                recovered.DetectedAircraftKey is null && !recovered.InstructorModeEnabled &&
                !recovered.UseCustomControls, "Profile: default template strips aircraft metadata");
            File.Delete(path);
            Check(ReadDefaultProfileTemplateFromPath(path).PitchStep == 1.25M,
                "Profile: missing default recovers backup");

            var stored = new SavedBindingsFile { ProfileName = "Test Aircraft", AircraftId = "test-id",
                PitchStep = 4M, UseCustomControls = false, HasCustomControlsSnapshot = true };
            var runtime = PrepareAircraftProfileForRuntime(stored, path);
            Check(runtime.PitchStep == 1.25M && runtime.AircraftId == "test-id" &&
                runtime.Id == stored.Id && runtime.HasCustomControlsSnapshot,
                "Profile: default mode inherits controls and preserves aircraft identity");
            runtime.PitchStep = 8M;
            Check(stored.PitchStep == 4M, "Profile: inherited edits preserve saved custom snapshot");
            stored.UseCustomControls = true;
            Check(PrepareAircraftProfileForRuntime(stored, path).PitchStep == 4M,
                "Profile: custom mode restores saved custom settings");
            stored.Version = 12;
            stored.UseCustomControls = false;
            stored.HasCustomControlsSnapshot = false;
            Check(PrepareAircraftProfileForRuntime(stored, path).UseCustomControls &&
                stored.HasCustomControlsSnapshot && stored.PitchStep == 4M,
                "Profile: legacy aircraft migrates without losing custom controls");
            var legacy = new SavedBindingsFile { ProfileName = "ah_64e", DetectedAircraftKey = "AH_64E",
                PitchStep = 3.25M, UseCustomControls = true, HasCustomControlsSnapshot = true };
            var knownAircraft = new AircraftInfo("ah_64e", "AH-64E", "usa", "Helicopter", new(), null);
            Check(BackfillAircraftId(legacy, knownAircraft) && legacy.AircraftId == "ah_64e" &&
                legacy.ProfileName == "ah_64e" && legacy.DetectedAircraftKey == "AH_64E" &&
                legacy.PitchStep == 3.25M && legacy.UseCustomControls && legacy.HasCustomControlsSnapshot &&
                !BackfillAircraftId(legacy, knownAircraft),
                "Profile: aircraft metadata repair preserves saved controls and is idempotent");
        }
        catch (Exception ex) { failures.Add("Profile storage self-test: " + ex.Message); }
        finally { try { Directory.Delete(folder, recursive: true); } catch { } }
        return failures;
    }

    private void SwitchToDefaultProfile()
    {
        if (_offlinePreview || _loadingSavedSettings || _switchingProfile) return;
        PreserveManualProfileSelection();
        _autoProfileSwitchInProgress = true;
        try
        {
            SaveBindings();
            _activeProfileName = string.Empty;
            _profileAircraftId = null;
            _profileDetectedAircraftKey = null;
            ApplySavedProfile(ReadDefaultProfileTemplate());
            DeleteActiveProfilePointer();
            RefreshProfileList();
            SetInstruction(VT("Profiles.DefaultControlsLoaded"), Theme.Success);
        }
        finally { _autoProfileSwitchInProgress = false; }
    }

    private void ProcessAutomaticAircraftProfileTelemetry(string indicators, string state)
    {
        if (_offlinePreview || _loadingSavedSettings || _switchingProfile || _autoProfileSwitchInProgress ||
            _choosingAircraftProfile || _aircraftControlEditorView is { IsDisposed: false }) return;
        if (!WarThunderTelemetry.TryGetAircraftIdentity(indicators, state, out string detectedType))
        {
            _autoProfileCandidateKey = null;
            _autoProfileCandidateReads = 0;
            return;
        }

        string key = NormalizeDetectedAircraftKey(detectedType);
        if (key.Length == 0) return;
        if (!string.Equals(_autoProfileCandidateKey, key, StringComparison.Ordinal))
        {
            _autoProfileCandidateKey = key;
            _autoProfileCandidateReads = 1;
            return;
        }
        _autoProfileCandidateReads = Math.Min(AutoProfileConfirmationReads, _autoProfileCandidateReads + 1);
        if (_autoProfileCandidateReads < AutoProfileConfirmationReads) return;
        // A deliberate selection lasts until telemetry confirms a different aircraft.
        // Brief invalid/loading telemetry must not undo the user's selection.
        if (string.Equals(_manualProfileAircraftKey, key, StringComparison.Ordinal)) return;
        _manualProfileAircraftKey = null;

        StartAircraftDatabase();
        AircraftInfo? aircraft = ResolveDetectedAircraft(detectedType);
        if (CurrentProfileMatchesDetectedAircraft(key, aircraft))
        {
            ReconcileActiveProfileAircraftMetadata(detectedType, aircraft);
            return;
        }

        _autoProfileSwitchInProgress = true;
        try
        {
            string? profileName = FindProfileForDetectedAircraft(key, aircraft);
            if (profileName is null)
                profileName = CreateProfileFromDefaultForDetectedAircraft(detectedType, aircraft);
            else
                BackfillDetectedProfileMetadata(profileName, detectedType, aircraft);

            if (!string.Equals(profileName, _activeProfileName, StringComparison.OrdinalIgnoreCase))
            {
                SwitchProfile(profileName);
                SetInstruction(VF("Profiles.DetectedSelected", AircraftSearchService.CleanName(aircraft?.DisplayName ?? detectedType), profileName), Theme.Success);
            }
        }
        finally { _autoProfileSwitchInProgress = false; }
    }

    private string CreateProfileFromDefaultForDetectedAircraft(string detectedType, AircraftInfo? aircraft)
    {
        // If the user is currently editing the Default Profile, flush the latest
        // values before cloning the template for a newly detected aircraft.
        if (string.IsNullOrWhiteSpace(_activeProfileName)) SaveBindings();
        SavedBindingsFile profile = ReadDefaultProfileTemplate();
        string baseName = AircraftSearchService.CleanName(aircraft?.DisplayName ?? detectedType);
        if (string.IsNullOrWhiteSpace(baseName)) baseName = "Detected Aircraft";
        string profileName = NormalizeProfileName(baseName);
        if (ProfileExists(profileName)) profileName = GetUniqueProfileName(profileName);
        profile.Id = Guid.NewGuid().ToString("D");
        profile.Version = 13;
        profile.ProfileName = profileName;
        profile.AircraftId = aircraft?.Id;
        profile.DetectedAircraftKey = detectedType.Trim();
        profile.UseCustomControls = false;
        profile.HasCustomControlsSnapshot = false;
        profile.DampingSupport = DampingUnknown;
        profile.InstructorModeEnabled = false;
        if (!string.IsNullOrWhiteSpace(aircraft?.FlightCategory)) profile.AircraftType = aircraft.FlightCategory!;
        WriteProfile(profileName, profile);
        AircraftProfilesChanged?.Invoke();
        SetInstruction(VF("Profiles.DetectedCreated", baseName, profileName, VT("Profiles.DefaultProfile")), Theme.Success);
        return profileName;
    }

    private void BackfillDetectedProfileMetadata(string profileName, string detectedType, AircraftInfo? aircraft)
    {
        SavedBindingsFile profile = ReadProfile(profileName);
        bool changed = false;
        if (!string.Equals(NormalizeDetectedAircraftKey(profile.DetectedAircraftKey), NormalizeDetectedAircraftKey(detectedType), StringComparison.Ordinal))
        { profile.DetectedAircraftKey = detectedType.Trim(); changed = true; }
        if (aircraft is not null && !string.Equals(profile.AircraftId, aircraft.Id, StringComparison.Ordinal))
        { profile.AircraftId = aircraft.Id; profile.AircraftType = aircraft.FlightCategory ?? profile.AircraftType; changed = true; }
        NormalizeAircraftControlModeMetadata(profile, isDefaultProfile: false);
        if (changed) WriteProfile(profileName, profile);
    }

    private void ReconcileActiveProfileAircraftMetadata(string? detectedType = null, AircraftInfo? aircraft = null)
    {
        if (string.IsNullOrWhiteSpace(_activeProfileName)) return;
        detectedType ??= _profileDetectedAircraftKey;
        if (string.IsNullOrWhiteSpace(detectedType)) return;
        aircraft ??= ResolveDetectedAircraft(detectedType);
        string detectedKey = NormalizeDetectedAircraftKey(detectedType);
        bool idAlreadyMatches = aircraft is not null && string.Equals(_profileAircraftId, aircraft.Id, StringComparison.Ordinal);
        bool keyAlreadyMatches = string.Equals(NormalizeDetectedAircraftKey(_profileDetectedAircraftKey), detectedKey, StringComparison.Ordinal);
        if (idAlreadyMatches && keyAlreadyMatches) return;
        try
        {
            SavedBindingsFile profile = ReadProfile(_activeProfileName);
            bool changed = false;
            if (!keyAlreadyMatches)
            {
                profile.DetectedAircraftKey = detectedType.Trim();
                _profileDetectedAircraftKey = profile.DetectedAircraftKey;
                changed = true;
            }
            if (aircraft is not null && !idAlreadyMatches)
            {
                profile.AircraftId = aircraft.Id;
                profile.AircraftType = aircraft.FlightCategory ?? profile.AircraftType;
                _profileAircraftId = aircraft.Id;
                changed = true;
            }
            NormalizeAircraftControlModeMetadata(profile, isDefaultProfile: false);
            if (changed)
            {
                WriteProfile(_activeProfileName, profile);
                AircraftProfilesChanged?.Invoke();
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { }
    }

}
