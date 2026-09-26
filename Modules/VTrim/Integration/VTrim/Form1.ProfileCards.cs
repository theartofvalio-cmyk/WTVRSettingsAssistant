namespace HOTASTrimUtility;

public partial class Form1
{
    private event Action? AircraftProfilesChanged;
    private event Action? AircraftProfileSelectionChanged;
    public event Action<string>? AircraftProfileEditorRequested;
    public Control CreateAircraftProfileBrowser(bool compact)
    {
        var root = new AircraftBrowserPanel { Dock = DockStyle.Fill, BackColor = Theme.Background };
        var viewport = new AircraftBrowserPanel { BackColor = Theme.Background, AutoScroll = !compact };
        var toolbar = new AircraftBrowserPanel { BackColor = Theme.Background };
        var filter = new TextBox { BackColor = Theme.Control, ForeColor = Theme.Text };
        string searchLabelKey = compact ? "Profiles.Filter" : "Profiles.Search";
        var label = I18n(new Label { ForeColor = Theme.Text, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 13) }, searchLabelKey);
        toolbar.Controls.AddRange([label, filter]);
        bool favoritesOnly = false;
        int first = 0, visibleCount = 5, generation = 0;
        var commands = new List<Button>();
        var previous = new AircraftNavigationButton(false);
        var next = new AircraftNavigationButton(true);
        var cards = new List<AircraftCard>();
        bool arranging = false;
        string? nation = null, category = null;
        var favoriteFilter = new AircraftFavoriteButton { BackColor = Theme.Background, AccessibleName = VT("Profiles.ShowFavorites") };
        toolbar.Controls.Add(favoriteFilter);
        var filters = new FlowLayoutPanel { WrapContents = false, AutoScroll = true, BackColor = Theme.Background };
        var tips = new ToolTip();
        tips.SetToolTip(favoriteFilter, VT("Profiles.ShowFavoritesOnly"));
        favoriteFilter.Click += (_, _) => { favoritesOnly = !favoritesOnly; favoriteFilter.Active = favoritesOnly;
            favoriteFilter.AccessibleName = favoritesOnly ? VT("Profiles.ShowAllAircraft") : VT("Profiles.ShowFavorites");
            tips.SetToolTip(favoriteFilter, favoritesOnly ? VT("Profiles.FavoritesOnlyTip") : VT("Profiles.ShowFavoritesOnly"));
            first = 0; RefreshCards(); };
        if (!compact)
        {
            toolbar.Controls.Add(filters);
            foreach (string id in new[] { "", "usa", "germany", "ussr", "britain", "japan", "china", "italy", "france", "sweden", "israel" })
            {
                var b = new Button { Width = 50, Height = 40, Text = id == "" ? VT("Profiles.All") : "", FlatStyle = FlatStyle.Flat, BackColor = Theme.Control, ForeColor = Theme.Text };
                if (id != "")
                {
                    string resource = typeof(Form1).Assembly.GetManifestResourceNames().First(n => n.EndsWith(".Nations." + id + ".png"));
                    using var stream = typeof(Form1).Assembly.GetManifestResourceStream(resource)!;
                    using var image = Image.FromStream(stream); b.Image = new Bitmap(image, 40, 25);
                    b.Disposed += (_, _) => b.Image?.Dispose();
                }
                tips.SetToolTip(b, id == "" ? VT("Profiles.AllNations") : id);
                b.Click += (_, _) => { nation = id == "" ? null : id; foreach (Button sibling in filters.Controls.OfType<Button>().Where(x => x.Tag is null)) sibling.BackColor = sibling == b ? Color.DimGray : Theme.Control; RefreshCards(); };
                filters.Controls.Add(b);
            }
            foreach (string type in new[] { "Prop Plane", "Jet Plane", "Helicopter" })
            {
                var b = new Button { Width = 50, Height = 40, Tag = type, FlatStyle = FlatStyle.Flat, BackColor = Theme.Control,
                    Margin = new Padding(type == "Prop Plane" ? 22 : 3, 3, 3, 3) };
                var icon = new Bitmap(44, 34); using (var g = Graphics.FromImage(icon)) AircraftIcons.Draw(g, new Rectangle(0, 0, 44, 34), type, Color.WhiteSmoke);
                b.Image = icon; b.Disposed += (_, _) => icon.Dispose(); tips.SetToolTip(b, type switch { "Prop Plane" => VT("Profiles.PropPlane"), "Jet Plane" => VT("Profiles.JetPlane"), _ => VT("Profiles.Helicopter") });
                b.Click += (_, _) => { category = category == type ? null : type; foreach (Button sibling in filters.Controls.OfType<Button>().Where(x => x.Tag is string)) sibling.BackColor = (string)sibling.Tag! == category ? Color.DimGray : Theme.Control; RefreshCards(); };
                filters.Controls.Add(b);
            }
        }
        if (!compact)
        {
            var favorite = I18n(CreateSecondaryButton(VT("Profiles.AddFavorite")), "Profiles.AddFavorite");
            var edit = I18n(CreateSecondaryButton(VT("Profiles.Edit")), "Profiles.Edit");
            var clone = I18n(CreateSecondaryButton(VT("Profiles.Clone")), "Profiles.Clone");
            clone.Click += (_, _) => CloneAircraftProfile();
            _deleteProfileButton = I18n(CreateResetButton(VT("Profiles.Delete")), "Profiles.Delete");
            favorite.Click += (_, _) => { if (string.IsNullOrEmpty(_activeProfileName)) return; string key = _profileAircraftId ?? _profileId; if (!_favoriteAircraft.Remove(key)) _favoriteAircraft.Add(key); SaveApplicationSettings(false); AircraftProfilesChanged?.Invoke(); };
            edit.Click += (_, _) => EditProfileAircraft();
            _deleteProfileButton.Click += (_, _) => DeleteProfile();
            commands.AddRange([favorite, clone, edit, _deleteProfileButton]);
            foreach (var command in commands) command.AutoEllipsis = true;
            toolbar.Controls.AddRange(commands.ToArray());
            root.Controls.Add(toolbar);
        }
        else root.Tag = toolbar; // Home places this filter in the main footer.
        root.Controls.Add(viewport);
        foreach (var arrow in new[] { previous, next })
        {
            arrow.FlatAppearance.BorderSize = 0; arrow.BackColor = Theme.Background;
            arrow.ForeColor = Color.Wheat; arrow.Visible = compact; root.Controls.Add(arrow);
            arrow.Font = new Font("Segoe UI", 26, FontStyle.Bold);
        }
        void Arrange()
        {
            if (arranging || root.IsDisposed || root.Width < 20) return;
            arranging = true;
            try
            {
                float dpi = root.DeviceDpi / 96f;
                int S(int n) => (int)Math.Round(n * dpi);
                int gap = S(6), header = compact ? 0 : S(104), buttonWidth = S(160);
                bool stacked = !compact && root.Width < S(1100);
                if (stacked)
                {
                    header += S(44);
                    buttonWidth = Math.Min(buttonWidth, Math.Max(1, root.Width / Math.Max(1, commands.Count) - gap));
                }
                if (!compact) toolbar.SetBounds(0, 0, root.Width, header);
                label.SetBounds(0, 0, S(75), compact ? toolbar.Height : S(44));
                int searchWidth = compact ? Math.Min(S(104), toolbar.Width - label.Width - gap - S(44)) : stacked ? Math.Min(S(460), toolbar.Width - S(135)) :
                    Math.Min(S(460), Math.Max(60, toolbar.Width - S(135) - commands.Count * (buttonWidth + gap)));
                filter.SetBounds(label.Right + gap, Math.Max(0, (label.Height - filter.PreferredHeight) / 2), Math.Max(40, searchWidth), filter.PreferredHeight);
                favoriteFilter.SetBounds(filter.Right + gap, 0, S(40), label.Height);
                filters.SetBounds(0, S(stacked ? 90 : 46), root.Width, S(56));
                if (!compact) { filters.Visible = true; filters.BringToFront(); filters.PerformLayout(); }
                for (int i = 0; i < commands.Count; i++)
                    commands[i].SetBounds(toolbar.Width - (commands.Count - i) * (buttonWidth + gap), S(stacked ? 48 : 4), buttonWidth, S(36));
                int compactWidth = Math.Min(S(210), Math.Max(S(80), (int)(root.Height / .42f)));
                int compactMinimum = Math.Min(compactWidth, S(170));
                bool overflow = compact && cards.Count > Math.Max(1, (root.Width + gap) / (compactMinimum + gap));
                previous.Visible = next.Visible = overflow;
                int arrowWidth = overflow ? Math.Max(S(24), Math.Min(S(48), root.Width / 25)) : 0;
                previous.SetBounds(0, 0, arrowWidth, root.Height);
                next.SetBounds(root.Width - arrowWidth, 0, arrowWidth, root.Height);
                viewport.SetBounds(arrowWidth, header, root.Width - arrowWidth * 2, Math.Max(0, root.Height - header));
                visibleCount = compact ? Math.Max(1, (viewport.Width + gap) / (compactMinimum + gap)) : Math.Max(1, viewport.Width / S(230));
                int width = Math.Max(80, (viewport.ClientSize.Width - gap * (visibleCount - 1) - (compact ? 0 : SystemInformation.VerticalScrollBarWidth)) / visibleCount);
                width = Math.Min(width, compact ? compactWidth : S(250));
                int height = compact ? Math.Max(40, Math.Min(viewport.Height, (int)(width * .43))) : (int)(width * .42);
                first = Math.Clamp(first, 0, Math.Max(0, cards.Count - visibleCount));
                for (int i = 0; i < cards.Count; i++)
                {
                    cards[i].Visible = !compact || (i >= first && i < first + visibleCount);
                    int index = compact ? i - first : i;
                    cards[i].SetBounds((index % visibleCount) * (width + gap),
                        compact ? (viewport.Height - height) / 2 : (index / visibleCount) * (height + gap) + viewport.AutoScrollPosition.Y, width, height);
                }
                previous.Enabled = first > 0; next.Enabled = first + visibleCount < cards.Count;
                if (!compact) viewport.AutoScrollMinSize = new Size(0, ((cards.Count + visibleCount - 1) / visibleCount) * (height + gap));
            }
            finally { arranging = false; }
        }
        previous.Click += (_, _) => { first = Math.Max(0, first - visibleCount); Arrange(); };
        next.Click += (_, _) => { first = Math.Min(Math.Max(0, cards.Count - visibleCount), first + visibleCount); Arrange(); };
        if (compact)
        {
            viewport.MouseWheel += (_, e) =>
            {
                int maxFirst = Math.Max(0, cards.Count - visibleCount);
                int direction = e.Delta < 0 ? 1 : -1;
                first = Math.Clamp(first + direction, 0, maxFirst);
                Arrange();
            };
        }
        root.SizeChanged += (_, _) => Arrange();
        toolbar.SizeChanged += (_, _) => Arrange();
        async void RefreshCards()
        {
            if (root.IsDisposed) return;
            int version = ++generation;
            var pending = new List<(AircraftCard Card, AircraftInfo Aircraft)>();
            viewport.SuspendLayout();
            try
            {
                foreach (var old in cards) old.Dispose();
                cards.Clear();
                if (!compact)
                {
                    bool aircraftProfileActive = !string.IsNullOrWhiteSpace(_activeProfileName);
                    commands[0].Text = aircraftProfileActive && _favoriteAircraft.Contains(_profileAircraftId ?? _profileId)
                        ? VT("Profiles.RemoveFavorite")
                        : VT("Profiles.AddFavorite");
                    foreach (Button command in commands) command.Enabled = aircraftProfileActive;
                }

                // The shared Default Profile is a template, not an aircraft. It is
                // intentionally hidden from the compact Home strip, but the full
                // Profiles page always pins its dedicated template card at the top-left.
                if (!compact)
                {
                    var defaultCard = new AircraftCard
                    {
                        Tag = string.Empty,
                        Text = VT("Profiles.DefaultProfile"),
                        Active = string.IsNullOrWhiteSpace(_activeProfileName),
                        AccessibleName = VT("Profiles.DefaultProfileTip")
                    };
                    defaultCard.SetArtwork(AircraftIcons.CreateDefaultProfileArtwork());
                    defaultCard.Click += (_, _) =>
                    {
                        PreserveManualProfileSelection();
                        if (!string.IsNullOrWhiteSpace(_activeProfileName)) SwitchToDefaultProfile();
                        _openTrimDashboard?.Invoke();
                    };
                    cards.Add(defaultCard);
                    viewport.Controls.Add(defaultCard);
                }

                foreach (string name in GetProfileNames())
                {
                    // Default Profile is a shared template, never an aircraft card.
                    // It remains selectable where the template itself is edited, but
                    // it must not occupy space in either the Home aircraft strip or
                    // the full aircraft-card browser.
                    if (string.Equals(name, DefaultProfileDisplayName, StringComparison.OrdinalIgnoreCase)) continue;

                    SavedBindingsFile profile;
                    try { profile = ReadProfile(name); } catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException) { continue; }
                    // The Custom Base Profile is an editing/template entry. Keep it in
                    // the full Profiles browser (double-click opens its trim editor),
                    // but do not advertise it as an aircraft on the compact main screen.
                    if (compact && IsCustomBaseProfileName(name)) continue;
                    var aircraft = _aircraftDatabase?.ResolveProfile(profile.AircraftId, profile.DetectedAircraftKey, name);
                    if (favoritesOnly && !_favoriteAircraft.Contains(profile.AircraftId ?? profile.Id)) continue;
                    if (nation is not null && aircraft?.Nation != nation) continue;
                    if (category is not null && (aircraft?.FlightCategory ?? profile.AircraftType) != category) continue;
                    if (!AircraftSearchService.Normalize(name + " " + aircraft?.DisplayName).Contains(AircraftSearchService.Normalize(filter.Text))) continue;
                    var card = new AircraftCard { Tag = name, Text = AircraftSearchService.CleanName(aircraft?.DisplayName ?? name), Premium = aircraft?.IsPremium == true,
                        Active = string.Equals(name, _activeProfileName, StringComparison.OrdinalIgnoreCase),
                        Favorite = !compact && _favoriteAircraft.Contains(profile.AircraftId ?? profile.Id),
                        // Mode badges belong to the full Profiles page only. The Home
                        // strip should show aircraft names/artwork without DEFAULT/CUSTOM labels.
                        ModeBadge = compact ? string.Empty : VT(profile.UseCustomControls ? "Profiles.CustomMode" : "Profiles.DefaultMode"),
                        CustomMode = profile.UseCustomControls,
                        AccessibleName = VF("Profiles.CardAccessible", AircraftSearchService.CleanName(aircraft?.DisplayName ?? name), profile.UseCustomControls ? VT("Profiles.CustomControlsLabel") : VT("Profiles.DefaultControlsLabel")) };
                    card.Click += (_, _) =>
                    {
                        PreserveManualProfileSelection();
                        if (!string.Equals(name, _activeProfileName, StringComparison.OrdinalIgnoreCase))
                            SwitchProfile(name, refreshProfileUi: false);
                        foreach (AircraftCard sibling in cards)
                            sibling.Active = ReferenceEquals(sibling, card);
                    };
                    card.DoubleClick += (_, _) =>
                    {
                        if (compact) AircraftProfileEditorRequested?.Invoke(name);
                        else OpenAircraftControlEditor(name);
                    };
                    cards.Add(card); viewport.Controls.Add(card);
                    if (aircraft?.Id.StartsWith("universal-", StringComparison.Ordinal) == true)
                    {
                        card.SetArtwork(AircraftIcons.CreateFallbackArtwork(aircraft.FlightCategory ?? profile.AircraftType));
                    }
                    else
                    {
                        card.SetArtwork(AircraftIcons.CreateFallbackArtwork(aircraft?.FlightCategory ?? profile.AircraftType));
                        if (aircraft is not null) pending.Add((card, aircraft));
                    }
                }
                if (!compact)
                {
                    var add = new AircraftCard { Text = "+" };
                    add.Click += (_, _) => CreateProfile(); cards.Add(add); viewport.Controls.Add(add);
                }
                Arrange();
            }
            finally { viewport.ResumeLayout(true); }
            // Start visible-card artwork requests together. The asset cache limits
            // network concurrency, while this avoids one slow/missing Wiki image
            // blocking every card after it in the profile row.
            await Task.WhenAll(pending.Select(async item =>
            {
                if (version != generation || root.IsDisposed || item.Card.IsDisposed) return;
                try
                {
                    string? path = _aircraftAssets is null ? null :
                        await _aircraftAssets.GetIconAsync(item.Aircraft, _telemetryShutdown.Token);
                    if (version != generation || root.IsDisposed || item.Card.IsDisposed) return;
                    if (path is null)
                    {
                        item.Card.SetArtwork(AircraftIcons.CreateFallbackArtwork(item.Aircraft.FlightCategory ?? item.Aircraft.VehicleType));
                        return;
                    }
                    using var image = Image.FromFile(path);
                    item.Card.SetArtwork(new Bitmap(image));
                }
                catch (Exception ex) when (ex is OperationCanceledException or IOException or ArgumentException or
                    System.Runtime.InteropServices.ExternalException) { }
            }));
        }
        void RefreshSelection()
        {
            if (root.IsDisposed) return;
            foreach (var card in cards)
                if (card.Tag is string name)
                    card.Active = string.IsNullOrEmpty(name)
                        ? string.IsNullOrWhiteSpace(_activeProfileName)
                        : string.Equals(name, _activeProfileName, StringComparison.OrdinalIgnoreCase);
            if (!compact)
            {
                bool active = !string.IsNullOrWhiteSpace(_activeProfileName);
                commands[0].Text = active && _favoriteAircraft.Contains(_profileAircraftId ?? _profileId)
                    ? VT("Profiles.RemoveFavorite")
                    : VT("Profiles.AddFavorite");
                foreach (var command in commands) command.Enabled = active;
            }
        }
        AircraftProfileSelectionChanged += RefreshSelection;
        filter.TextChanged += (_, _) => { first = 0; RefreshCards(); };
        AircraftProfilesChanged += RefreshCards;
        root.Disposed += (_, _) => { generation++; tips.Dispose(); AircraftProfilesChanged -= RefreshCards; AircraftProfileSelectionChanged -= RefreshSelection; if (compact) toolbar.Dispose(); };
        root.HandleCreated += (_, _) => { RefreshCards(); if (!compact) { filters.CreateControl(); foreach (Control child in filters.Controls) child.CreateControl(); } };
        return root;
    }

    private static bool IsCustomBaseProfileName(string? name)
    {
        string normalized = AircraftSearchService.Normalize(name ?? string.Empty);
        return normalized is "CUSTOMBASEPROFILE" or "CUSTOMBASE" or "BASECUSTOMPROFILE" or "CUSTOMDEFAULTBASE";
    }
}

internal sealed class AircraftBrowserPanel : Panel
{
    public AircraftBrowserPanel()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw, true);
    }
}

internal sealed class AircraftFavoriteButton : Button
{
    private bool _active;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool Active { get => _active; set { if (_active == value) return; _active = value; Invalidate(); } }
    public AircraftFavoriteButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Cursor = Cursors.Hand;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        float radius = Math.Min(Width, Height) * .38f;
        var center = new PointF(Width / 2f, Height / 2f);
        var points = Enumerable.Range(0, 10).Select(i =>
        {
            double angle = -Math.PI / 2 + i * Math.PI / 5;
            float r = radius * (i % 2 == 0 ? 1 : .44f);
            return new PointF(center.X + r * (float)Math.Cos(angle), center.Y + r * (float)Math.Sin(angle));
        }).ToArray();
        using var fill = new System.Drawing.Drawing2D.LinearGradientBrush(ClientRectangle,
            Active ? Color.FromArgb(255, 237, 169) : Color.FromArgb(167, 148, 91),
            Active ? Color.FromArgb(214, 149, 30) : Color.FromArgb(102, 88, 53), 90f);
        e.Graphics.FillPolygon(fill, points);
        using var edge = new Pen(Active ? Color.Wheat : Color.FromArgb(211, 177, 93), 1.2f);
        e.Graphics.DrawPolygon(edge, points);
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, ClientRectangle);
    }
}

internal sealed class AircraftNavigationButton : Button
{
    private readonly bool _right;
    public AircraftNavigationButton(bool right)
    {
        _right = right;
        AccessibleName = right ? "Next aircraft" : "Previous aircraft";
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        FlatStyle = FlatStyle.Flat;
    }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        float width = Math.Min(Width * .72f, Height * .42f), height = width * 1.15f;
        float left = (Width - width) / 2, top = (Height - height) / 2;
        using var brush = new SolidBrush(Color.FromArgb(Enabled ? 255 : 64, Color.Wheat));
        e.Graphics.FillPolygon(brush, _right
            ? new[] { new PointF(left, top), new PointF(left + width, Height / 2f), new PointF(left, top + height) }
            : new[] { new PointF(left + width, top), new PointF(left, Height / 2f), new PointF(left + width, top + height) });
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, ClientRectangle);
    }
}

internal sealed class AircraftCard : Button
{
    private Image? _artwork;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool Active
    {
        get => _active;
        set { if (_active == value) return; _active = value; Invalidate(); }
    }
    private bool _active;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool Premium { get; set; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool Favorite { get; set; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string ModeBadge { get; set; } = string.Empty;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool CustomMode { get; set; }
    public AircraftCard()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
            ControlStyles.StandardClick | ControlStyles.StandardDoubleClick, true);
        Cursor = Cursors.Hand; ForeColor = Color.WhiteSmoke; FlatStyle = FlatStyle.Flat;
        AccessibleRole = AccessibleRole.PushButton;
    }
    public void SetArtwork(Image artwork)
    {
        _artwork?.Dispose();
        _artwork = PrepareArtwork(artwork);
        artwork.Dispose(); Invalidate();
    }
    internal static Bitmap PrepareArtwork(Image artwork)
    {
        // Normalize transparent margins so the visible aircraft, not its canvas, sets the scale.
        using var bitmap = new Bitmap(artwork);
        int left = bitmap.Width, top = bitmap.Height, right = -1, bottom = -1;
        for (int y = 0; y < bitmap.Height; y++)
        for (int x = 0; x < bitmap.Width; x++)
            if (bitmap.GetPixel(x, y).A > 0) { left = Math.Min(left, x); top = Math.Min(top, y); right = Math.Max(right, x); bottom = Math.Max(bottom, y); }
        return right >= left ? bitmap.Clone(Rectangle.FromLTRB(left, top, right + 1, bottom + 1), System.Drawing.Imaging.PixelFormat.Format32bppArgb) : new Bitmap(bitmap);
    }
    internal static void PaintAircraft(Graphics graphics, Rectangle bounds, string name, Image? artwork, bool premium, bool selected, Font font)
    {
        using var background = new SolidBrush(premium ? Color.FromArgb(73, 63, 31) : Color.FromArgb(43, 63, 73));
        graphics.FillRectangle(background, bounds);
        using var border = new Pen(selected ? Color.WhiteSmoke : premium ? Color.FromArgb(145, 119, 43) : Color.FromArgb(66, 87, 98), selected ? 2 : 1);
        graphics.DrawRectangle(border, bounds.X + 1, bounds.Y + 1, bounds.Width - 3, bounds.Height - 3);
        int padding = Math.Max(6, (int)Math.Round(graphics.DpiX / 96f * 6));
        var pictureBounds = new RectangleF(bounds.X + padding, bounds.Y + padding,
            Math.Max(1, bounds.Width * .60f - padding * 2), Math.Max(1, bounds.Height - padding * 2));
        if (artwork is not null)
        {
            float scale = Math.Min(pictureBounds.Width / artwork.Width, pictureBounds.Height / artwork.Height);
            float width = artwork.Width * scale, height = artwork.Height * scale;
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            graphics.DrawImage(artwork, pictureBounds.X,
                pictureBounds.Y + (pictureBounds.Height - height) / 2, width, height);
        }
        TextRenderer.DrawText(graphics, AircraftSearchService.CleanName(name), font,
            new Rectangle(bounds.X + 8, bounds.Y + 6, bounds.Width - 16, Math.Min(bounds.Height - 12, font.Height + 6)), Color.WhiteSmoke,
            TextFormatFlags.Right | TextFormatFlags.Top | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width < 20 || Height < 20) return;
        using var font = new Font("Segoe UI", Text == "+" ? Math.Min(32, Height * .4f) : Math.Clamp(Height * .21f, 11, 24), FontStyle.Regular, GraphicsUnit.Pixel);
        if (Text != "+")
        {
            PaintAircraft(e.Graphics, ClientRectangle, Text, _artwork, Premium, Active, font);
            // Profile mode belongs at the lower-right of full Profiles cards.
            // If the aircraft is a favorite, the star owns the far-right slot and
            // the DEFAULT/CUSTOM badge sits immediately to its left.
            float favoriteRadius = Favorite ? Math.Clamp(Height * .13f, 8, 18) : 0f;
            int favoriteSlot = Favorite ? (int)Math.Ceiling(favoriteRadius * 2f) + 12 : 0;
            if (!string.IsNullOrWhiteSpace(ModeBadge))
            {
                using var badgeFont = new Font("Segoe UI Semibold", Math.Clamp(Height * .11f, 8, 12), FontStyle.Regular, GraphicsUnit.Pixel);
                Size badgeText = TextRenderer.MeasureText(ModeBadge, badgeFont);
                int badgeWidth = Math.Min(Math.Max(36, Width - 16 - favoriteSlot), badgeText.Width + 18);
                int badgeHeight = badgeText.Height + 7;
                int badgeX = Math.Max(8, Width - 8 - favoriteSlot - badgeWidth);
                int badgeY = Math.Max(8, Height - 8 - badgeHeight);
                var badgeRect = new Rectangle(badgeX, badgeY, badgeWidth, badgeHeight);
                using var badgeFill = new SolidBrush(Color.FromArgb(205, 15, 26, 32));
                using var badgeEdge = new Pen(CustomMode ? Theme.Accent : Color.FromArgb(110, 145, 155));
                e.Graphics.FillRectangle(badgeFill, badgeRect); e.Graphics.DrawRectangle(badgeEdge, badgeRect);
                TextRenderer.DrawText(e.Graphics, ModeBadge, badgeFont, badgeRect, CustomMode ? Color.Wheat : Color.Gainsboro,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            }
            if (Favorite)
            {
                float radius = favoriteRadius;
                float cx = Width - radius - 8, cy = Height - radius - 8;
                var points = Enumerable.Range(0, 10).Select(i =>
                {
                    double angle = -Math.PI / 2 + i * Math.PI / 5;
                    float r = i % 2 == 0 ? radius : radius * .45f;
                    return new PointF(cx + (float)Math.Cos(angle) * r, cy + (float)Math.Sin(angle) * r);
                }).ToArray();
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using var fill = new SolidBrush(Color.FromArgb(231, 185, 76));
                e.Graphics.FillPolygon(fill, points);
            }
            return;
        }
        e.Graphics.Clear(Theme.Background);
        using var border = new Pen(Color.WhiteSmoke, 1);
        e.Graphics.DrawRectangle(border, 1, 1, Width - 3, Height - 3);
        TextRenderer.DrawText(e.Graphics, "+", font, ClientRectangle, ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
    protected override void Dispose(bool disposing) { if (disposing) _artwork?.Dispose(); base.Dispose(disposing); }
}
