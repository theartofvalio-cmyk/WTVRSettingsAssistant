using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace WTVRSettingsAssistant;

internal sealed class AviationHomePage : UserControl
{
    private readonly AlphaPictureBox _logo;
    public void AttachControlProfileSelectors(Control desktop, Control vr)
    {
        _monitor.Controls.Add(desktop);
        _vr.Controls.Add(vr);
        void ArrangeSelector(Control card, Control selector)
        {
            int size = Math.Clamp(card.Height / 5, 30, 56);
            selector.SetBounds(card.Width - size - 12, card.Height - size - 12, size, size);
        }
        _monitor.SizeChanged += (_, _) => ArrangeSelector(_monitor, desktop);
        _vr.SizeChanged += (_, _) => ArrangeSelector(_vr, vr);
        ArrangeSelector(_monitor, desktop);
        ArrangeSelector(_vr, vr);
    }
    private Control? _aircraftProfiles;
    private bool _showAircraftProfiles = true;
    public void SetAircraftProfilesVisible(bool visible)
    {
        _showAircraftProfiles = visible;
        if (_aircraftProfiles is not null) _aircraftProfiles.Visible = visible && _logo.Visible;
    }
    public void SetAircraftProfiles(Control browser)
    {
        _aircraftProfiles?.Dispose();
        _aircraftProfiles = browser;
        browser.Dock = DockStyle.None;
        Controls.Add(browser);
        Arrange();
    }
    private readonly Label _playMode;
    private readonly AviationActionCard _monitor;
    private readonly AviationActionCard _vr;
    private readonly AviationActionCard _neck;
    private readonly AviationActionCard _keys;
    private readonly AviationActionCard _trim;
    private readonly AviationServerSelector _server;
    private readonly AviationLaunchButton _launch;
    private readonly TableLayoutPanel _links;
    private bool _arranging;

    public AviationHomePage(
        Image logo,
        Image neckImage,
        Image keysImage,
        Action monitor,
        Action vr,
        Action openNeck,
        Action toggleNeck,
        Action openKeys,
        Action toggleKeys,
        Action<GameServerChannel> serverChanged,
        Action refreshServers,
        Action launch,
        Action logoClick,
        Action discord,
        Action youtube,
        Action support,
        Action? openTrim = null,
        Action? toggleTrim = null)
    {
        Dock = DockStyle.Fill;
        BackColor = IllustratedTheme.Background;
        AutoScroll = false;
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

        _playMode = MakeLabel("PLAY MODE", 20, FontStyle.Bold, ContentAlignment.MiddleCenter);
        _logo = new AlphaPictureBox(logo)
        {
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand
        };
        _logo.Click += (_, _) => logoClick();

        Image? monitorImage = LoadOptionalAsset("Home_Monitor_New.png");
        Image? vrImage = LoadOptionalAsset("Home_VR_New.png");
        Image? trimImage = LoadOptionalAsset("Home_VTrim_New.png");
        Image? cleanNeckImage = LoadOptionalAsset("IllustratedHead.png") ?? neckImage;
        Image? cleanKeysImage = LoadOptionalAsset("IllustratedKeys.png") ?? keysImage;
        Image? redLaunchFrame = LoadOptionalAsset("PlayButton.png") ?? LoadOptionalAsset("Launch_Red_Frame.png");
        Image? updateLaunchFrame = LoadOptionalAsset("UpdateButton.png") ?? LoadOptionalAsset("Launch_Update_Gold.png");
        Image? runningLaunchFrame = LoadOptionalAsset("GameStarted.png") ?? redLaunchFrame;

        _monitor = new AviationActionCard("MONITOR", "monitor", monitorImage, monitor);
        _vr = new AviationActionCard("VR", "visor", vrImage, vr);
        _neck = new AviationActionCard("Neck Assistant", "head", cleanNeckImage, openNeck) { ToggleClick = toggleNeck };
        _keys = new AviationActionCard("KeyBind Assistant", "keys", cleanKeysImage, openKeys) { ToggleClick = toggleKeys };
        _trim = new AviationActionCard("VTrim Assistant", "trim", trimImage, openTrim ?? (() => { })) { ToggleClick = toggleTrim };
        _server = new AviationServerSelector(serverChanged, refreshServers);
        _launch = new AviationLaunchButton(launch, redLaunchFrame, updateLaunchFrame, runningLaunchFrame);

        _links = new TableLayoutPanel
        {
            BackColor = Color.Transparent,
            ColumnCount = 3,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        _links.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
        _links.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
        _links.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.334f));
        _links.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        _links.Controls.Add(new AviationLinkButton("DISCORD", "discord", discord), 0, 0);
        _links.Controls.Add(new AviationLinkButton("YOUTUBE", "youtube", youtube), 1, 0);
        _links.Controls.Add(new AviationLinkButton("Buy me a Beer!", "beer", support), 2, 0);

        Controls.AddRange([_playMode, _monitor, _vr, _neck, _keys, _trim, _server, _launch, _links, _logo]);
        Resize += (_, _) => { Arrange(); Invalidate(); };
        HandleCreated += (_, _) => BeginInvoke((Action)Arrange);
    }

    public void SetState(bool monitorActive, bool vrActive, bool neckEnabled, bool keysEnabled, GameServerChannel server, bool canLaunch, bool gameRunning)
    {
        _monitor.Active = monitorActive;
        _vr.Active = vrActive;
        _neck.Active = neckEnabled;
        _keys.Active = keysEnabled;
        _server.Selected = server;
        _launch.GameRunning = gameRunning;
        _launch.CanLaunch = canLaunch && !gameRunning;
    }

    public void SetVTrimState(bool enabled) => _trim.Active = enabled;
    public void SetGameUpdateAvailable(bool updateAvailable) => _launch.UpdateAvailable = updateAvailable;
    public void SetServerVersions(Version? live, Version? test) => _server.SetVersions(live, test);
    public void SetServerStatuses(ServerSignal live, ServerSignal test) { /* Server availability indicators intentionally removed in v2.0. */ }

    public void SetLanguage(string languageCode)
    {
        _playMode.Text = AppText.T(languageCode, "Home.ModeHeading");
        _server.SetLanguage(languageCode);
        _launch.SetLabels(
            AppText.T(languageCode, "Home.LaunchAction"),
            AppText.T(languageCode, "Home.UpdateAction"),
            AppText.T(languageCode, "Home.RunningAction"));
        _monitor.Text = AppText.T(languageCode, "Home.Monitor");
        _monitor.AccessibleName = _monitor.Text;
        _vr.Text = AppText.T(languageCode, "Home.VR");
        _vr.AccessibleName = _vr.Text;
        SetCardText(_neck, AppText.T(languageCode, "Nav.Neck"));
        SetCardText(_keys, AppText.T(languageCode, "Nav.Keybind"));
        SetCardText(_trim, AppText.T(languageCode, "Nav.VTrim"));
        _neck.SetStatusText(AppText.T(languageCode, "Common.On"), AppText.T(languageCode, "Common.Off"));
        _keys.SetStatusText(AppText.T(languageCode, "Common.On"), AppText.T(languageCode, "Common.Off"));
        _trim.SetStatusText(AppText.T(languageCode, "Common.On"), AppText.T(languageCode, "Common.Off"));
        if (_links.Controls.Count >= 3)
        {
            _links.Controls[0].Text = AppText.T(languageCode, "Home.Discord");
            _links.Controls[1].Text = AppText.T(languageCode, "Home.YouTube");
            _links.Controls[2].Text = AppText.T(languageCode, "Home.Support");
        }
        Invalidate();
    }

    private static void SetCardText(AviationActionCard card, string text)
    {
        card.Text = text;
        card.AccessibleName = text;
        card.UpdateToolTip();
    }

    private static Image? LoadOptionalAsset(string fileName)
    {
        try { return AssetManager.LoadImage(fileName); }
        catch { return null; }
    }

    private void Arrange()
    {
        if (_arranging || ClientSize.Width <= 0 || ClientSize.Height <= 0) return;
        _arranging = true;
        SuspendLayout();
        try
        {
            // Design-space layout. Every Home-screen element uses ONE scale factor,
            // so maximizing the app enlarges cards, icons, text and the hero together
            // instead of stretching only the empty space between them.
            const int designWidth = 1240;
            const int designHeight = 820;
            const int logoAreaWidth = 520;
            const int designGap = 18;
            const int rightWidth = designWidth - logoAreaWidth - designGap;

            float scale = Math.Min(ClientSize.Width / (float)designWidth, ClientSize.Height / (float)designHeight);
            scale = Math.Clamp(scale, 0.35f, 2.25f);
            int S(float value) => Math.Max(1, (int)Math.Round(value * scale));

            int canvasWidth = S(designWidth);
            int canvasHeight = S(designHeight);
            int horizontalPad = S(10);
            int centeredX = Math.Max(0, (ClientSize.Width - canvasWidth) / 2);
            int rightAnchoredX = Math.Max(0, ClientSize.Width - canvasWidth - horizontalPad);
            int canvasX = Math.Max(centeredX, rightAnchoredX);
            int canvasY = Math.Max(0, (ClientSize.Height - canvasHeight) / 2);
            int rightX = canvasX + S(logoAreaWidth + designGap);
            int w = S(rightWidth);
            int gap = S(10);

            _logo.Visible = scale >= 0.58f;
            int profileStripHeight = Math.Max(64, S(68));
            if (_aircraftProfiles is not null)
            {
                _aircraftProfiles.Visible = _showAircraftProfiles && _logo.Visible;
                _aircraftProfiles.SetBounds(S(2), canvasY + canvasHeight - profileStripHeight, Math.Max(180, rightX - S(16)), profileStripHeight);
            }
            if (_logo.Visible)
            {
                // Keep the hero centered in the real open area to the left of the
                // Monitor/VR/server stack. In fullscreen the right stack is anchored
                // to the right, so the old fixed design-space logo cell drifted away
                // from the visual center of the left panel.
                int logoAreaLeft = 0;
                int logoAreaRight = Math.Max(logoAreaLeft + S(360), rightX - S(18));
                int logoAreaWidthActual = Math.Max(S(360), logoAreaRight - logoAreaLeft);
                int side = Math.Min(
                    Math.Min(S(560), canvasHeight - S(36)),
                    Math.Max(S(360), logoAreaWidthActual - S(42)));
                int logoX = logoAreaLeft + (logoAreaWidthActual - side) / 2;
                side = Math.Min(side, canvasHeight - profileStripHeight - S(20));
                int logoY = canvasY + (canvasHeight - profileStripHeight - side) / 2;
                _logo.SetBounds(logoX, logoY, side, side);
            }

            // Home composition follows the supplied reference: Live/Test at the top,
            // large Monitor/VR cards underneath, assistants below, then the wide
            // launch/update action and community links at the bottom.
            _playMode.Visible = false;
            int serverY = canvasY + S(8);
            int serverHeight = S(80);
            int modeY = canvasY + S(102);
            int modeHeight = S(220);
            int launchHeight = S(130);
            int linksHeight = S(44);
            int bottomPad = S(10);
            int launchGap = S(14);
            int sectionGap = S(18);
            int linksY = canvasY + canvasHeight - bottomPad - linksHeight;
            int launchY = linksY - launchGap - launchHeight;
            int featureY = modeY + modeHeight + sectionGap;
            int featureHeight = Math.Max(S(210), launchY - featureY - sectionGap);

            _server.SetBounds(rightX, serverY, w, serverHeight);

            int half = (w - gap) / 2;
            _monitor.SetBounds(rightX, modeY, half, modeHeight);
            _vr.SetBounds(rightX + half + gap, modeY, w - half - gap, modeHeight);

            int third = (w - gap * 2) / 3;
            _neck.SetBounds(rightX, featureY, third, featureHeight);
            _keys.SetBounds(rightX + third + gap, featureY, third, featureHeight);
            _trim.SetBounds(rightX + (third + gap) * 2, featureY, w - (third + gap) * 2, featureHeight);

            _launch.SetBounds(rightX, launchY, w, launchHeight);
            _links.SetBounds(rightX, linksY, w, linksHeight);
            foreach (Control control in _links.Controls)
            {
                control.Dock = DockStyle.Fill;
                control.Margin = new Padding(S(4), 0, S(4), 0);
            }

            AutoScroll = false;
            AutoScrollMinSize = Size.Empty;
        }
        finally
        {
            ResumeLayout(false);
            _arranging = false;
        }
    }

    private static void SetLabelPixelFont(Label label, int pixels, FontStyle style)
    {
        if (Math.Abs(label.Font.Size - pixels) < .1f && label.Font.Unit == GraphicsUnit.Pixel && label.Font.Style == style) return;
        ResponsiveFonts.Set(label, pixels, style, GraphicsUnit.Pixel);
    }

    private static Label MakeLabel(string text, int px, FontStyle style, ContentAlignment alignment) => new()
    {
        Text = text,
        Font = new Font("Segoe UI", px, style, GraphicsUnit.Pixel),
        ForeColor = IllustratedTheme.Ivory,
        BackColor = Color.Transparent,
        TextAlign = alignment,
        AutoSize = false
    };
}

internal sealed class AlphaPictureBox : PictureBox
{
    public AlphaPictureBox(Image image)
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Color.Transparent;
        Image = image;
    }

    protected override void OnPaint(PaintEventArgs pe)
    {
        pe.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        pe.Graphics.SmoothingMode = SmoothingMode.HighQuality;
        pe.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        base.OnPaint(pe);
    }
}

internal class AviationActionCard : Control
{
    private readonly string _icon;
    private readonly Image? _image;
    private readonly Rectangle _sourceBounds;
    private readonly Action _click;
    private bool _active;
    private readonly ToolTip _toolTip = new() { InitialDelay = 350, ReshowDelay = 100, AutoPopDelay = 6000 };
    private string _onText = "On";
    private string _offText = "Off";

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Action? ToggleClick { get; init; }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Active { get => _active; set { if (_active == value) return; _active = value; UpdateToolTip(); Invalidate(); } }

    public void SetStatusText(string onText, string offText)
    {
        _onText = string.IsNullOrWhiteSpace(onText) ? "On" : onText;
        _offText = string.IsNullOrWhiteSpace(offText) ? "Off" : offText;
        UpdateToolTip();
    }

    public void UpdateToolTip() => _toolTip.SetToolTip(this, Text + (Active ? " · " + _onText : " · " + _offText));

    public AviationActionCard(string text, string icon, Image? image, Action click)
    {
        Text = text;
        _icon = icon;
        _image = image;
        _sourceBounds = image == null ? Rectangle.Empty : FindVisibleSourceBounds(image);
        _click = click;
        Cursor = Cursors.Hand;
        DoubleBuffered = true;
        BackColor = IllustratedTheme.Background;
        AccessibleName = text;
        _toolTip.SetToolTip(this, text);
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
        TabStop = true;
        MouseUp += (_, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            if (ToggleClick != null) ToggleClick();
            else _click();
        };
        KeyDown += (_, e) =>
        {
            if (e.KeyCode is Keys.Enter or Keys.Space)
            {
                (ToggleClick ?? _click)();
                e.Handled = true;
            }
        };
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
        e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        using (Brush panelFill = new SolidBrush(IllustratedTheme.Panel))
            e.Graphics.FillRectangle(panelFill, 8, 8, Math.Max(1, Width - 16), Math.Max(1, Height - 16));
        IllustratedTheme.DrawFrame(e.Graphics, new Rectangle(2, 2, Width - 5, Height - 5), Active);
        if (Active)
        {
            using Brush tint = new SolidBrush(Color.FromArgb(26, IllustratedTheme.Gold));
            e.Graphics.FillRectangle(tint, 9, 9, Width - 18, Height - 18);
        }

        // Icon-only Home controls; names remain available on hover and to screen readers.
        bool playModeCard = _icon is "monitor" or "visor";
        if (_image != null)
        {
            Rectangle imageBox = playModeCard
                ? new Rectangle(Width / 10, Height / 12, Width - Width / 5, Height - Height / 6)
                : _icon == "trim"
                    ? new Rectangle(Width / 14, Height / 18, Math.Max(1, Width - Width / 7), Math.Max(1, Height - Height / 9))
                : new Rectangle(6, 6, Math.Max(1, Width - 12), Math.Max(1, Height - 12));
            Rectangle drawBounds = FitImageBounds(_sourceBounds, imageBox);
            e.Graphics.DrawImage(_image, drawBounds, _sourceBounds, GraphicsUnit.Pixel);
        }
        else
        {
            int iconTop = playModeCard ? Height / 14 : Math.Max(6, Height / 22);
            int iconBottom = playModeCard ? Height - Height / 12 : Height - Math.Max(6, Height / 24);
            int maxIcon = playModeCard ? (int)(Height * .80) : iconBottom - iconTop;
            int iconSize = Math.Min(maxIcon, Math.Min(Width - 12, Math.Max(32, iconBottom - iconTop)));
            Rectangle iconBounds = new(Width / 2 - iconSize / 2, iconTop, iconSize, iconSize);
            IllustratedTheme.DrawIcon(e.Graphics, _icon, iconBounds, Active ? IllustratedTheme.Gold : IllustratedTheme.Ivory);
        }

        if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(5, 5, Width - 11, Height - 11));
    }

    protected override void Dispose(bool disposing) { if (disposing) _toolTip.Dispose(); base.Dispose(disposing); }

    private static int FitFontSize(Graphics graphics, string text, int width, int preferred, int minimum)
    {
        for (int size = preferred; size > minimum; size--)
        {
            using Font candidate = new("Segoe UI", size, FontStyle.Bold, GraphicsUnit.Pixel);
            if (TextRenderer.MeasureText(graphics, text, candidate, Size.Empty, TextFormatFlags.NoPadding).Width <= width)
                return size;
        }
        return minimum;
    }

    private static Rectangle FitImageBounds(Rectangle source, Rectangle target)
    {
        if (source.Width <= 0 || source.Height <= 0 || target.Width <= 0 || target.Height <= 0)
            return target;

        float scale = Math.Min(target.Width / (float)source.Width, target.Height / (float)source.Height);
        int width = Math.Max(1, (int)Math.Round(source.Width * scale));
        int height = Math.Max(1, (int)Math.Round(source.Height * scale));
        return new Rectangle(
            target.X + (target.Width - width) / 2,
            target.Y + (target.Height - height) / 2,
            width,
            height);
    }

    private static Rectangle FindVisibleSourceBounds(Image image)
    {
        Rectangle full = new(0, 0, image.Width, image.Height);
        if (image.Width <= 0 || image.Height <= 0) return full;

        Bitmap? createdBitmap = image as Bitmap is null ? new Bitmap(image) : null;
        Bitmap bitmap = image as Bitmap ?? createdBitmap!;
        int left = bitmap.Width;
        int top = bitmap.Height;
        int right = -1;
        int bottom = -1;

        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).A <= 8) continue;
                if (x < left) left = x;
                if (x > right) right = x;
                if (y < top) top = y;
                if (y > bottom) bottom = y;
            }
        }

        try
        {
            if (right < left || bottom < top) return full;

            int padX = Math.Max(1, (right - left + 1) / 40);
            int padY = Math.Max(1, (bottom - top + 1) / 40);
            left = Math.Max(0, left - padX);
            top = Math.Max(0, top - padY);
            right = Math.Min(bitmap.Width - 1, right + padX);
            bottom = Math.Min(bitmap.Height - 1, bottom + padY);
            return new Rectangle(left, top, right - left + 1, bottom - top + 1);
        }
        finally
        {
            createdBitmap?.Dispose();
        }
    }
}

internal sealed class AviationServerSelector : Control
{
    private readonly Action<GameServerChannel> _changed;
    private GameServerChannel _selected;
    private string _liveVersion = "version ...";
    private string _testVersion = "version ...";
    private string _liveTitle = "LIVE SERVER";
    private string _testTitle = "TEST SERVER";
    private string _versionUnknown = "version unknown";

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public GameServerChannel Selected { get => _selected; set { _selected = value; Invalidate(); } }

    public AviationServerSelector(Action<GameServerChannel> changed, Action refresh)
    {
        _changed = changed;
        Cursor = Cursors.Hand;
        DoubleBuffered = true;
        BackColor = IllustratedTheme.Background;
        AccessibleName = "War Thunder server selector";
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
        TabStop = true;
        MouseUp += (_, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            if (LeftCardBounds().Contains(e.Location)) _changed(GameServerChannel.Live);
            else if (RightCardBounds().Contains(e.Location)) _changed(GameServerChannel.Test);
        };
        KeyDown += (_, e) =>
        {
            if (e.KeyCode is Keys.Left or Keys.Right)
            {
                _changed(e.KeyCode == Keys.Left ? GameServerChannel.Live : GameServerChannel.Test);
                e.Handled = true;
            }
        };
    }

    public void SetSignals(ServerSignal live, ServerSignal test) { }

    public void SetVersions(Version? live, Version? test)
    {
        _liveVersion = live is null ? _versionUnknown : $"v {live}";
        _testVersion = test is null ? _versionUnknown : $"v {test}";
        Invalidate();
    }

    public void SetLanguage(string languageCode)
    {
        _liveTitle = AppText.T(languageCode, "Home.LiveServer");
        _testTitle = AppText.T(languageCode, "Home.TestServer");
        _versionUnknown = AppText.T(languageCode, "Home.VersionChecking");
        if (!_liveVersion.StartsWith("v ", StringComparison.OrdinalIgnoreCase)) _liveVersion = _versionUnknown;
        if (!_testVersion.StartsWith("v ", StringComparison.OrdinalIgnoreCase)) _testVersion = _versionUnknown;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        if (Width < 20 || Height < 20) return;
        DrawCard(e.Graphics, LeftCardBounds(), _liveTitle, _liveVersion, "live", Selected == GameServerChannel.Live);
        DrawCard(e.Graphics, RightCardBounds(), _testTitle, _testVersion, "test", Selected == GameServerChannel.Test);
        if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(2, 2, Width - 5, Height - 5));
    }

    private Rectangle LeftCardBounds()
    {
        int gap = Math.Clamp(Height / 9, 8, 16);
        int w = Math.Max(1, (Width - gap) / 2);
        return new Rectangle(1, 1, w - 1, Math.Max(1, Height - 2));
    }

    private Rectangle RightCardBounds()
    {
        int gap = Math.Clamp(Height / 9, 8, 16);
        int leftWidth = Math.Max(1, (Width - gap) / 2);
        return new Rectangle(leftWidth + gap, 1, Math.Max(1, Width - leftWidth - gap - 1), Math.Max(1, Height - 2));
    }

    private static void DrawCard(Graphics g, Rectangle r, string titleText, string versionText, string icon, bool active)
    {
        using (Brush panelFill = new SolidBrush(IllustratedTheme.Panel))
            g.FillRectangle(panelFill, Rectangle.Inflate(r, -8, -8));
        IllustratedTheme.DrawFrame(g, Rectangle.Inflate(r, -2, -2), active);
        if (active)
        {
            using Brush b = new SolidBrush(Color.FromArgb(35, IllustratedTheme.Gold));
            g.FillRectangle(b, Rectangle.Inflate(r, -9, -9));
        }

        int iconSize = Math.Max(20, (int)Math.Round(r.Height * 0.64));
        int iconInset = Math.Max(6, (int)Math.Round(r.Height * 0.18));
        Rectangle iconBounds = new(r.X + iconInset, r.Y + (r.Height - iconSize) / 2, iconSize, iconSize);
        IllustratedTheme.DrawIcon(g, icon, iconBounds, active ? IllustratedTheme.Gold : IllustratedTheme.Ivory);

        int textX = iconBounds.Right + 10;
        int textW = Math.Max(40, r.Right - textX - 12);
        int titleSize = Math.Max(9, (int)Math.Round(r.Height * 0.22));
        int versionSize = Math.Max(12, (int)Math.Round(r.Height * 0.18));
        while (titleSize > 14)
        {
            using Font test = new("Segoe UI", titleSize, FontStyle.Bold, GraphicsUnit.Pixel);
            if (TextRenderer.MeasureText(g, titleText, test, Size.Empty, TextFormatFlags.NoPadding).Width <= textW) break;
            titleSize--;
        }
        while (versionSize > 12)
        {
            using Font test = new("Segoe UI", versionSize, FontStyle.Bold, GraphicsUnit.Pixel);
            if (TextRenderer.MeasureText(g, versionText, test, Size.Empty, TextFormatFlags.NoPadding).Width <= textW) break;
            versionSize--;
        }
        using Font title = new("Segoe UI", titleSize, FontStyle.Bold, GraphicsUnit.Pixel);
        using Font version = new("Segoe UI", versionSize, FontStyle.Bold, GraphicsUnit.Pixel);
        Rectangle titleRect = new(textX, r.Y + 15, textW, Math.Max(26, r.Height / 2));
        Rectangle versionRect = new(textX, r.Bottom - Math.Max(38, r.Height / 3), textW, Math.Max(30, r.Height / 4));
        TextRenderer.DrawText(g, titleText, title, titleRect, active ? IllustratedTheme.Gold : IllustratedTheme.Ivory,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        TextRenderer.DrawText(g, versionText, version, versionRect, active ? IllustratedTheme.Gold : IllustratedTheme.Muted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
    }
}

internal sealed class AviationLaunchButton : Control
{
    private readonly Action _click;
    private readonly Image? _launchFrame;
    private readonly Image? _updateFrame;
    private readonly Image? _runningFrame;
    private bool _canLaunch;
    private bool _updateAvailable;
    private bool _gameRunning;
    private string _launchLabel = "LAUNCH";
    private string _updateLabel = "UPDATE";
    private string _runningLabel = "RUNNING";

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool CanLaunch
    {
        get => _canLaunch;
        set
        {
            if (_canLaunch == value) return;
            _canLaunch = value;
            Cursor = value ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }
    }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool UpdateAvailable
    {
        get => _updateAvailable;
        set
        {
            if (_updateAvailable == value) return;
            _updateAvailable = value;
            Invalidate();
        }
    }

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool GameRunning
    {
        get => _gameRunning;
        set
        {
            if (_gameRunning == value) return;
            _gameRunning = value;
            Cursor = value ? Cursors.Default : Cursor;
            Invalidate();
        }
    }

    public void SetLabels(string launch, string update, string running)
    {
        _launchLabel = string.IsNullOrWhiteSpace(launch) ? "LAUNCH" : launch;
        _updateLabel = string.IsNullOrWhiteSpace(update) ? "UPDATE" : update;
        _runningLabel = string.IsNullOrWhiteSpace(running) ? "RUNNING" : running;
        Text = _launchLabel;
        Invalidate();
    }

    public AviationLaunchButton(Action click, Image? launchFrame, Image? updateFrame, Image? runningFrame)
    {
        _click = click;
        _launchFrame = launchFrame;
        _updateFrame = updateFrame;
        _runningFrame = runningFrame;
        DoubleBuffered = true;
        BackColor = IllustratedTheme.Background;
        AccessibleName = "Launch War Thunder";
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
        TabStop = true;
        MouseUp += (_, e) => { if (e.Button == MouseButtons.Left && CanLaunch && !GameRunning) _click(); };
        KeyDown += (_, e) =>
        {
            if (CanLaunch && !GameRunning && e.KeyCode is (Keys.Enter or Keys.Space))
            {
                _click();
                e.Handled = true;
            }
        };
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
        e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        bool running = GameRunning;
        bool update = !running && CanLaunch && UpdateAvailable;
        Color color = CanLaunch || running ? Color.FromArgb(255, 239, 225) : IllustratedTheme.Muted;
        Rectangle launchBounds = new(2, 2, Width - 5, Height - 5);
        if (launchBounds.Width <= 0 || launchBounds.Height <= 0) return;

        Image? frame = running ? _runningFrame : update ? _updateFrame : _launchFrame;
        if (frame != null)
        {
            e.Graphics.DrawImage(frame, launchBounds);
        }
        else
        {
            using (var shape = new GraphicsPath())
            {
                int c = 10;
                shape.AddPolygon(new Point[] { new(c+2,2), new(Width-c-3,2), new(Width-3,c+2),
                    new(Width-3,Height-c-3), new(Width-c-3,Height-3), new(c+2,Height-3), new(2,Height-c-3), new(2,c+2) });
                using var fill = new LinearGradientBrush(launchBounds, update ? Color.FromArgb(204, 170, 0) : CanLaunch ? Color.FromArgb(172, 33, 39) : IllustratedTheme.Panel,
                    update ? Color.FromArgb(126, 98, 0) : CanLaunch ? Color.FromArgb(91, 15, 24) : IllustratedTheme.Background, 90f);
                using var edge = new Pen(update ? IllustratedTheme.Gold : CanLaunch ? Color.FromArgb(225, 76, 65) : IllustratedTheme.Muted, 2);
                e.Graphics.FillPath(fill, shape); e.Graphics.DrawPath(edge, shape);
                using var glint = new Pen(Color.FromArgb(100, 255, 219, 188), 1);
                e.Graphics.DrawLine(glint, 18, 6, Width - 18, 6);
            }
        }
        int preferred = Math.Max(12, (int)Math.Round(Height * 0.38));
        int labelGuard = Math.Max(18, (int)Math.Round(Width * 0.12));
        int available = Math.Max(80, Width - labelGuard * 2);
        string label = running ? _runningLabel : update ? _updateLabel : _launchLabel;
        while (preferred > 10)
        {
            using Font candidate = new("Segoe UI", preferred, FontStyle.Bold, GraphicsUnit.Pixel);
            if (TextRenderer.MeasureText(e.Graphics, label, candidate, Size.Empty, TextFormatFlags.NoPadding).Width <= available) break;
            preferred--;
        }
        using Font f = new("Segoe UI", preferred, FontStyle.Bold, GraphicsUnit.Pixel);
        TextRenderer.DrawText(e.Graphics, label, f, new Rectangle(labelGuard, 5, Math.Max(1, Width - labelGuard * 2), Height - 10), color,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(5, 5, Width - 11, Height - 11));
    }
}

internal sealed class AviationLinkButton : Control
{
    private readonly string _icon;
    private readonly Action _click;
    private bool _hover;

    public AviationLinkButton(string text, string icon, Action click)
    {
        Text = text;
        _icon = icon;
        _click = click;
        Cursor = Cursors.Hand;
        DoubleBuffered = true;
        BackColor = IllustratedTheme.Background;
        AccessibleName = text;
        TabStop = true;
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
        MouseEnter += (_, _) => { _hover = true; Invalidate(); };
        MouseLeave += (_, _) => { _hover = false; Invalidate(); };
        MouseUp += (_, e) => { if (e.Button == MouseButtons.Left) _click(); };
        KeyDown += (_, e) =>
        {
            if (e.KeyCode is Keys.Enter or Keys.Space)
            {
                _click();
                e.Handled = true;
            }
        };
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        bool beer = _icon == "beer";
        if (ClientSize.Width <= 0 || ClientSize.Height <= 0) return;
        Color fillColor = beer ? Color.FromArgb(154, 113, 6) : IllustratedTheme.Panel;
        using (Brush panelFill = new LinearGradientBrush(ClientRectangle, beer ? Color.FromArgb(228, 177, 34) : fillColor, fillColor, 90f))
            e.Graphics.FillRectangle(panelFill, 6, 6, Math.Max(1, Width - 12), Math.Max(1, Height - 12));
        IllustratedTheme.DrawFrame(e.Graphics, new Rectangle(1, 1, Width - 3, Height - 3), beer || _hover || Focused);
        Color color = beer ? Color.FromArgb(255, 250, 232) : _hover || Focused ? IllustratedTheme.Gold : IllustratedTheme.Ivory;
        int iconSize = Math.Clamp(Height - 18, 22, 34);
        int textSize = Math.Clamp(Height / 3, 11, 18);
        int gap = beer ? 7 : 9;
        int textBudget = Math.Max(24, Width - iconSize - gap - 24);
        while (textSize > 9)
        {
            using Font test = new("Segoe UI", textSize, FontStyle.Bold, GraphicsUnit.Pixel);
            if (TextRenderer.MeasureText(Text, test, Size.Empty, TextFormatFlags.NoPadding).Width <= textBudget) break;
            textSize--;
        }
        using Font font = new("Segoe UI", textSize, FontStyle.Bold, GraphicsUnit.Pixel);
        int measuredTextWidth = TextRenderer.MeasureText(Text, font, Size.Empty, TextFormatFlags.NoPadding).Width + 4;
        int textWidth = Math.Min(textBudget, measuredTextWidth);
        int total = iconSize + gap + textWidth;
        int startX = Math.Max(8, (Width - total) / 2);
        IllustratedTheme.DrawIcon(e.Graphics, _icon, new Rectangle(startX, (Height - iconSize) / 2, iconSize, iconSize), color);
        TextRenderer.DrawText(e.Graphics, Text, font, new Rectangle(startX + iconSize + gap, 0, Math.Max(1, Width - startX - iconSize - gap - 8), Height),
            color, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(4, 4, Width - 9, Height - 9));
    }
}



