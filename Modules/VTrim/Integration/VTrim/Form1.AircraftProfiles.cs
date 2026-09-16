using System.Net.Http;

namespace HOTASTrimUtility;

public partial class Form1
{
    private AircraftDatabaseService? _aircraftDatabase;
    private AircraftAssetCache? _aircraftAssets;
    private readonly HttpClient _aircraftHttp = new() { Timeout = TimeSpan.FromSeconds(30) };
    private string? _profileAircraftId;
    private string _profileId = Guid.NewGuid().ToString("D");
    private void StartAircraftDatabase()
    {
        if (_offlinePreview || _aircraftDatabase is not null) return;
        _aircraftDatabase = new(SettingsDirectory, new WarThunderWikiAircraftProvider(_aircraftHttp));
        _aircraftAssets = new(SettingsDirectory, _aircraftHttp);
        _aircraftDatabase.Changed += () =>
        {
            try { if (IsHandleCreated && !IsDisposed) BeginInvoke((Action)(() => { UpdateProfileSummary(); AircraftProfilesChanged?.Invoke(); })); }
            catch (InvalidOperationException) { }
        };
        _ = Task.Run(async () =>
        {
            try
            {
                await _aircraftDatabase.InitializeAsync(_telemetryShutdown.Token);
                foreach (var aircraft in _aircraftDatabase.Items)
                {
                    _telemetryShutdown.Token.ThrowIfCancellationRequested();
                    await _aircraftAssets.GetIconAsync(aircraft, _telemetryShutdown.Token);
                }
            }
            catch (OperationCanceledException) { }
        });
        Disposed += (_, _) => _aircraftHttp.Dispose();
    }

    private bool ChooseAircraftProfile(string initialName, string? initialAircraft, out string name, out string? aircraftId)
    {
        StartAircraftDatabase();
        name = initialName; aircraftId = initialAircraft;
        using var dialog = new Form { Text = initialName.Length == 0 ? "New Profile" : "Edit Profile",
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
        var save = CreatePrimaryButton(initialName.Length == 0 ? "Create" : "Save");
        save.Name = "SaveAircraftProfile";
        var cancel = CreateSecondaryButton("Cancel");
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
                BackColor = Theme.Control, ForeColor = Theme.Text, Text = id.Length == 0 ? "All" : "", AccessibleName = title };
            button.FlatAppearance.BorderColor = id.Length == 0 ? Color.WhiteSmoke : Theme.Control;
            if (id.Length > 0)
            {
                string resource = typeof(Form1).Assembly.GetManifestResourceNames().First(n => n.EndsWith(".Nations." + id + ".png", StringComparison.Ordinal));
                using var stream = typeof(Form1).Assembly.GetManifestResourceStream(resource)!;
                using var original = Image.FromStream(stream);
                var flag = new Bitmap(original, new Size(40, 25)); flags.Add(flag); button.Image = flag;
            }
            tips.SetToolTip(button, title);
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
        layout.Controls.Add(new Label { Text = "Profile name", Dock = DockStyle.Fill }, 0, 0);
        layout.Controls.Add(input, 0, 1);
        input.Visible = false;
        layout.Controls.Add(nations, 0, 2);
        layout.Controls.Add(new Label { Text = "Aircraft", Dock = DockStyle.Fill }, 0, 3);
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
            if (dialog.ShowDialog(this) != DialogResult.OK) return false;
            name = AircraftSearchService.CleanName(_aircraftDatabase?.GetById(selected)?.DisplayName ?? input.Text.Trim()); aircraftId = selected; return true;
        }
        finally
        {
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
            $"You're about to overwrite {target}'s settings with the current aircraft's settings. Do you want to proceed?",
            "Clone aircraft settings", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
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
        var copy = CaptureCurrentProfile();
        copy.Id = destination?.Id ?? Guid.NewGuid().ToString("D");
        copy.AircraftId = aircraftId;
        copy.AircraftType = _aircraftDatabase?.GetById(aircraftId)?.FlightCategory ?? copy.AircraftType;
        WriteProfile(existingName ?? name, copy);
        RefreshProfileList();
        AircraftProfilesChanged?.Invoke();
        SetInstruction($"Cloned settings to {existingName ?? name}.", Theme.Success);
        return true;
    }

    private void EditProfileAircraft()
    {
        if (string.IsNullOrEmpty(_activeProfileName)) return;
        if (!ChooseAircraftProfile(_activeProfileName, _profileAircraftId, out string name, out string? id)) return;
        name = NormalizeProfileName(name);
        bool renamed = !string.Equals(name, _activeProfileName, StringComparison.OrdinalIgnoreCase);
        if (renamed && ProfileExists(name)) { ShowProfileExistsMessage(name); return; }
        var profile = CaptureCurrentProfile();
        profile.ProfileName = name;
        profile.AircraftId = id;
        string oldPath = GetProfileFilePath(_activeProfileName);
        WriteProfile(name, profile);
        _activeProfileName = name;
        _profileAircraftId = id;
        WriteActiveProfileName();
        if (renamed && File.Exists(oldPath)) File.Delete(oldPath);
        RefreshProfileList();
        UpdateProfileSummary();
    }
}
