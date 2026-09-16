using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace HOTASTrimUtility;

public partial class Form1
{
    private readonly bool _embeddedMode;
    private readonly string? _embeddedSettingsDirectory;

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool OutputConnected => _vJoyConnected;

    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool OutputEnabled => _autoConnectToVJoyBox.Checked;

    public bool SetOutputEnabled(bool enabled)
    {
        if (InvokeRequired) throw new InvalidOperationException("Use the UI thread to change VTrim output.");
        if (_applicationClosing || IsDisposed) return false;
        _vJoyAutoConnectTimer.Stop();
        _manualVJoyDisconnect = !enabled;
        bool loading = _loadingApplicationSettings;
        _loadingApplicationSettings = true;
        _autoConnectToVJoyBox.Checked = enabled;
        _loadingApplicationSettings = loading;
        if (!_offlinePreview) SaveApplicationSettings(showConfirmation: false);
        if (!enabled) { DisconnectVJoy(); return true; }
        if (_offlinePreview) return false;
        StartAutomaticVJoyConnection();
        return _vJoyConnected;
    }

    private Control CreateEmbeddedHeader()
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 2,
            Margin = Padding.Empty,
            Padding = new Padding(18, 9, 18, 9),
            BackColor = Theme.Header
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86F));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 69F));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 31F));
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 54F));
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 46F));

        var logo = new VTrimEmbeddedLogo
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 14, 0),
            BackColor = Color.Transparent
        };
        var title = new Label
        {
            Text = VT("Header.Title"),
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Font = new Font("Segoe UI Semibold", 20F),
            ForeColor = Theme.Text,
            TextAlign = ContentAlignment.BottomLeft,
            AutoEllipsis = false,
            UseMnemonic = false,
            Tag = "i18n:Header.Title"
        };
        var subtitle = new Label
        {
            Text = VT("Header.Subtitle"),
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Font = new Font("Segoe UI Semibold", 10F),
            ForeColor = Theme.AccentLight,
            TextAlign = ContentAlignment.TopLeft,
            AutoEllipsis = false,
            UseMnemonic = false,
            Tag = "i18n:Header.Subtitle"
        };
        var description = new Label
        {
            Text = VT("Header.Description"),
            Dock = DockStyle.Fill,
            Margin = new Padding(12, 0, 0, 0),
            Font = new Font("Segoe UI", 9.5F),
            ForeColor = Theme.Muted,
            TextAlign = ContentAlignment.MiddleRight,
            AutoEllipsis = false,
            UseMnemonic = false,
            Tag = "i18n:Header.Description"
        };

        header.Controls.Add(logo, 0, 0);
        header.SetRowSpan(logo, 2);
        header.Controls.Add(title, 1, 0);
        header.Controls.Add(subtitle, 1, 1);
        header.Controls.Add(description, 2, 0);
        header.SetRowSpan(description, 2);
        return header;
    }
    private void ApplyEmbeddedAviationTheme(Control root)
    {
        void Style(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                if (child is Label label)
                {
                    float size = label.Font.Size;
                    float target = size < 9f ? 10f : size < 11f ? size + 1.25f : size;
                    if (target > size + .05f)
                        label.Font = new Font("Segoe UI", target, label.Font.Style, GraphicsUnit.Point);
                    // Never replace VTrim text with ellipsis. Responsive sizing/wrapping in Form1
                    // is responsible for fitting captions and dynamic status text to the actual cell.
                    label.AutoEllipsis = false;
                }
                else if (child is ButtonBase button)
                {
                    float size = button.Font.Size;
                    if (size < 10.5f)
                        button.Font = new Font("Segoe UI", 10.5f, FontStyle.Bold, GraphicsUnit.Point);
                }

                if (child is Panel panel && panel.BackColor == Theme.Panel && panel.Width > 160 && panel.Height > 70)
                {
                    panel.Paint += (_, e) => DrawEmbeddedFrame(e.Graphics, new Rectangle(2, 2, panel.ClientSize.Width - 5, panel.ClientSize.Height - 5));
                }
                Style(child);
            }
        }
        Style(root);
    }

    private static void DrawEmbeddedFrame(Graphics g, Rectangle r)
    {
        if (r.Width < 12 || r.Height < 12) return;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using Pen main = new(Theme.Text, 1.35f);
        using Pen accent = new(Theme.Accent, 1.35f);
        int c = Math.Clamp(Math.Min(r.Width, r.Height) / 7, 10, 22);
        Point[] outline =
        [
            new(r.Left + c, r.Top), new(r.Right - c, r.Top), new(r.Right, r.Top + c),
            new(r.Right, r.Bottom - c), new(r.Right - c, r.Bottom), new(r.Left + c, r.Bottom),
            new(r.Left, r.Bottom - c), new(r.Left, r.Top + c), new(r.Left + c, r.Top)
        ];
        g.DrawLines(main, outline);
        g.DrawLine(accent, r.Left + c + 8, r.Top, Math.Min(r.Right - c - 8, r.Left + c + 92), r.Top);
        g.DrawLine(accent, Math.Max(r.Left + c + 8, r.Right - c - 92), r.Bottom, r.Right - c - 8, r.Bottom);
    }

}

internal sealed class VTrimEmbeddedLogo : Control
{
    private static Image? _hostedLogo;
    private static bool _hostedLogoLoaded;

    public VTrimEmbeddedLogo()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.AllPaintingInWmPaint | ControlStyles.SupportsTransparentBackColor, true);
    }

    private static Image? TryGetHostedLogo()
    {
        if (_hostedLogoLoaded) return _hostedLogo;
        _hostedLogoLoaded = true;
        try
        {
            Type? assetManager = Type.GetType("WTVRSettingsAssistant.AssetManager, WTVRSettingsAssistant", throwOnError: false);
            object? loaded = assetManager?.GetMethod("LoadImage", [typeof(string)])?.Invoke(null, ["Home_VTrim_New.png"]);
            _hostedLogo = loaded as Image;
        }
        catch
        {
            _hostedLogo = null;
        }
        return _hostedLogo;
    }

    private static Rectangle FitImage(Size imageSize, Rectangle bounds)
    {
        if (imageSize.Width <= 0 || imageSize.Height <= 0 || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return bounds;
        }

        float scale = Math.Min(bounds.Width / (float)imageSize.Width, bounds.Height / (float)imageSize.Height);
        int width = Math.Max(1, (int)Math.Round(imageSize.Width * scale));
        int height = Math.Max(1, (int)Math.Round(imageSize.Height * scale));
        return new Rectangle(bounds.X + (bounds.Width - width) / 2, bounds.Y + (bounds.Height - height) / 2, width, height);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        if (TryGetHostedLogo() is Image hostedLogo)
        {
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            e.Graphics.CompositingQuality = CompositingQuality.HighQuality;
            Rectangle drawBounds = FitImage(hostedLogo.Size, ClientRectangle);
            e.Graphics.DrawImage(hostedLogo, drawBounds);
            return;
        }

        float scale = Math.Max(0.1f, Math.Min(Width, Height) / 100f);
        float ox = (Width - 100f * scale) / 2f;
        float oy = (Height - 100f * scale) / 2f;
        PointF P(float x, float y) => new(ox + x * scale, oy + y * scale);
        using var pen = new Pen(Theme.Text, Math.Max(1.4f, 2.4f * scale))
        {
            LineJoin = LineJoin.Round,
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        using var accent = new Pen(Theme.Accent, Math.Max(1.4f, 2.4f * scale))
        {
            LineJoin = LineJoin.Round,
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        e.Graphics.DrawLines(pen, new[]
        {
            P(50,8),P(57,42),P(88,62),P(88,69),P(56,58),P(55,78),P(67,86),P(67,91),
            P(50,87),P(33,91),P(33,86),P(45,78),P(44,58),P(12,69),P(12,62),P(43,42),P(50,8)
        });
        e.Graphics.DrawLine(pen, P(50,18), P(50,78));
        e.Graphics.DrawLine(accent, P(15,16), P(35,16));
        e.Graphics.DrawLine(accent, P(25,11), P(25,32));
        e.Graphics.DrawLines(accent, new[] { P(20,27), P(25,32), P(30,27) });
        e.Graphics.DrawLine(accent, P(65,16), P(85,16));
        e.Graphics.DrawLine(accent, P(75,11), P(75,32));
        e.Graphics.DrawLines(accent, new[] { P(70,27), P(75,32), P(80,27) });
    }

}
