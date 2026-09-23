using System.Drawing.Imaging;

namespace WTVRSettingsAssistant;

public partial class MainForm
{
    private FooterProfileButton? _desktopFooterButton, _vrFooterButton;
    private ContextMenuStrip? _controlProfileMenu;
    private ToolTip? _controlProfileTips;

    private void EnsureControlProfileFooter()
    {
        if (_desktopFooterButton is not null) return;
        _desktopFooterButton = new FooterProfileButton { AccessibleName = "Desktop keybind profiles", Visible = false };
        _vrFooterButton = new FooterProfileButton { AccessibleName = "VR keybind profiles", Visible = false };
        _aviationHome?.AttachControlProfileSelectors(_desktopFooterButton, _vrFooterButton);
        if (_aviationHome is not null)
            _aviationHome.ControlProfileMenuRequested += isVr => ShowControlProfileMenuAtCursor(isVr ? AppliedMode.VR : AppliedMode.Monitor);
        _controlProfileTips = new ToolTip();
        _desktopFooterButton.Click += (_, _) => ShowControlProfileMenu(AppliedMode.Monitor, _desktopFooterButton);
        _vrFooterButton.Click += (_, _) => ShowControlProfileMenu(AppliedMode.VR, _vrFooterButton);
        Disposed += (_, _) => { _controlProfileMenu?.Dispose(); _controlProfileTips.Dispose(); };
    }

    private void LayoutControlProfileFooter()
    {
        if (_desktopFooterButton is null) return;
        bool visible = _showHomeKeybinds && _mainPanel.Visible &&
            _vtrimForm?.Visible != true && _neckAssistForm?.Visible != true && _hiddenKeybindsForm?.Visible != true;
        _desktopFooterButton.Visible = _vrFooterButton!.Visible = visible;
        if (!visible) _controlProfileMenu?.Close();
        RefreshControlProfileFooter();
    }

    private void RefreshControlProfileFooter()
    {
        if (_desktopFooterButton is null || _vrFooterButton is null) return;
        void Update(FooterProfileButton button, List<ControlProfileEntry> profiles, string selectedId, string mode)
        {
            button.Enabled = profiles.Any(p => !string.IsNullOrWhiteSpace(p.Path));
            string? selected = profiles.FirstOrDefault(p => p.Id == selectedId)?.Name;
            _controlProfileTips?.SetToolTip(button, selected is null ? mode + " keybind profiles" : mode + ": " + selected);
            button.Invalidate();
        }
        Update(_desktopFooterButton, _desktopControlProfiles, _selectedDesktopControlProfileId, "Desktop");
        Update(_vrFooterButton, _vrControlProfiles, _selectedVrControlProfileId, "VR");
        bool visible = _showHomeKeybinds && _mainPanel.Visible &&
            _vtrimForm?.Visible != true && _neckAssistForm?.Visible != true && _hiddenKeybindsForm?.Visible != true;
        _aviationHome?.SetControlProfileSelectorState(visible, _desktopFooterButton.Enabled, _vrFooterButton.Enabled);
    }

    private ContextMenuStrip CreateControlProfileMenu(AppliedMode mode)
    {
        var menu = new ContextMenuStrip { BackColor = IllustratedTheme.Panel, ForeColor = IllustratedTheme.Ivory,
            Font = new Font("Segoe UI", 12), ShowImageMargin = false, ShowCheckMargin = true, Renderer = new ProfileMenuRenderer() };
        var profiles = mode == AppliedMode.Monitor ? _desktopControlProfiles : _vrControlProfiles;
        string selectedId = mode == AppliedMode.Monitor ? _selectedDesktopControlProfileId : _selectedVrControlProfileId;
        var none = new ToolStripMenuItem("No profile") { Checked = string.IsNullOrEmpty(selectedId), AutoSize = false,
            Size = new Size(460, menu.Font.Height + 16) };
        none.Click += (_, _) => { SelectControlProfile(mode, null); RefreshControlProfileCombos(); };
        menu.Items.Add(none);
        foreach (var profile in profiles.Where(p => !string.IsNullOrWhiteSpace(p.Path)))
        {
            var item = new ToolStripMenuItem(profile.Name) { Checked = profile.Id == selectedId, Tag = profile.Id, AutoSize = false };
            int textWidth = Math.Min(420, Math.Max(160, Screen.FromControl(this).WorkingArea.Width / 3));
            item.Size = new Size(textWidth + 40, TextRenderer.MeasureText(profile.Name, menu.Font, new Size(textWidth, int.MaxValue), TextFormatFlags.WordBreak).Height + 16);
            item.Click += (_, _) =>
            {
                SelectControlProfile(mode, profile);
                RefreshControlProfileCombos();
            };
            menu.Items.Add(item);
        }
        menu.AutoSize = false;
        menu.Size = new Size(menu.Items.Cast<ToolStripItem>().Select(i => i.Width).DefaultIfEmpty(200).Max() + 4,
            Math.Min(Screen.FromControl(this).WorkingArea.Height - 40, menu.Items.Cast<ToolStripItem>().Sum(i => i.Height) + 4));
        return menu;
    }

    private void ShowControlProfileMenuAtCursor(AppliedMode mode)
    {
        if (_aviationHome is null) return;
        _controlProfileMenu?.Dispose();
        _controlProfileMenu = CreateControlProfileMenu(mode);
        if (_controlProfileMenu.Items.Count == 0) return;
        Point location = _aviationHome.PointToClient(Cursor.Position);
        location.X = Math.Clamp(location.X, 0, Math.Max(0, _aviationHome.ClientSize.Width - 8));
        location.Y = Math.Clamp(location.Y + 4, 0, Math.Max(0, _aviationHome.ClientSize.Height - 8));
        _controlProfileMenu.Show(_aviationHome, location, ToolStripDropDownDirection.BelowLeft);
    }

    private void ShowControlProfileMenu(AppliedMode mode, FooterProfileButton button)
    {
        if (_controlProfileMenu?.Visible == true && ReferenceEquals(_controlProfileMenu.SourceControl, button))
        { _controlProfileMenu.Close(); return; }
        _controlProfileMenu?.Dispose();
        _controlProfileMenu = CreateControlProfileMenu(mode);
        if (_controlProfileMenu.Items.Count == 0) return;
        button.MenuOpen = true;
        _controlProfileMenu.Closed += (_, _) => button.MenuOpen = false;
        _controlProfileMenu.Show(button, new Point(button.Width, button.Height), ToolStripDropDownDirection.BelowLeft);
    }
}

internal sealed class FooterProfileButton : Control
{
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool MenuOpen { get; set; }
    public FooterProfileButton()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        TabStop = true;
        AccessibleRole = AccessibleRole.PushButton;
        SetStyle(ControlStyles.Selectable, true);
    }
    public void PerformClick() { if (Enabled && Visible) OnClick(EventArgs.Empty); }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Enter or Keys.Space) { PerformClick(); e.Handled = true; e.SuppressKeyPress = true; }
        base.OnKeyDown(e);
    }
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        if (Parent is null) { base.OnPaintBackground(e); return; }
        var state = e.Graphics.Save();
        try
        {
            e.Graphics.TranslateTransform(-Left, -Top);
            using var parentPaint = new PaintEventArgs(e.Graphics, Bounds);
            InvokePaintBackground(Parent, parentPaint);
            InvokePaint(Parent, parentPaint);
        }
        finally { e.Graphics.Restore(state); }
    }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(Color.FromArgb(Enabled ? 255 : 64, Color.Wheat));
        e.Graphics.FillPolygon(brush, new[] { new PointF(Width * .2f, Height * .3f),
            new PointF(Width * .8f, Height * .3f), new PointF(Width * .5f, Height * .8f) });
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, ClientRectangle);
    }
}
internal sealed class ProfileMenuRenderer : ToolStripProfessionalRenderer
{
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        TextRenderer.DrawText(e.Graphics, e.Text, e.TextFont,
            new Rectangle(32, 8, e.Item.Width - 40, e.Item.Height - 16),
            IllustratedTheme.Ivory, TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
    }
}
