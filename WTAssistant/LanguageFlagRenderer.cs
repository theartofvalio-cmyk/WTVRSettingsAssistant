using System.Drawing.Drawing2D;

namespace WTVRSettingsAssistant;

/// <summary>
/// Draws real flag swatches beside language names so the selector does not
/// depend on Windows' inconsistent regional-indicator emoji rendering.
/// </summary>
internal static class LanguageFlagRenderer
{
    public static void Configure(ComboBox combo)
    {
        combo.DrawMode = DrawMode.OwnerDrawFixed;
        combo.ItemHeight = Math.Max(combo.ItemHeight, 34);
        combo.DrawItem += DrawItem;
    }

    private static void DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not ComboBox combo || e.Index < 0) return;
        if (combo.Items[e.Index] is not LanguageOption option) return;

        bool selected = (e.State & DrawItemState.Selected) != 0;
        Color background = selected ? IllustratedTheme.SelectedRow : combo.BackColor;
        Color foreground = selected ? Color.White : combo.ForeColor;
        using (Brush back = new SolidBrush(background)) e.Graphics.FillRectangle(back, e.Bounds);

        int flagHeight = Math.Max(14, Math.Min(20, e.Bounds.Height - 8));
        int flagWidth = 30;
        Rectangle flag = new(e.Bounds.Left + 10, e.Bounds.Top + (e.Bounds.Height - flagHeight) / 2, flagWidth, flagHeight);
        DrawFlag(e.Graphics, flag, option.Code);

        Rectangle text = new(flag.Right + 10, e.Bounds.Top, Math.Max(1, e.Bounds.Right - flag.Right - 14), e.Bounds.Height);
        // Keep every language item visually consistent inside the selector.
        // Hebrew remains written in Hebrew, but the combo list itself stays
        // left-aligned with the flag on the left like the other languages.
        TextFormatFlags textFlags = TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis |
            TextFormatFlags.NoPrefix |
            TextFormatFlags.Left;
        TextRenderer.DrawText(e.Graphics, option.Name, combo.Font, text, foreground, textFlags);
        e.DrawFocusRectangle();
    }

    private static void DrawFlag(Graphics g, Rectangle r, string code)
    {
        GraphicsState state = g.Save();
        try
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (Brush white = new SolidBrush(Color.White)) g.FillRectangle(white, r);
            string language = AppText.Normalize(code);
            switch (language)
            {
                case "en": DrawUnitedKingdom(g, r); break;
                case "bg": Horizontal(g, r, Color.White, Color.FromArgb(0, 150, 110), Color.FromArgb(214, 38, 56)); break;
                case "es": Spanish(g, r); break;
                case "de": Horizontal(g, r, Color.FromArgb(20, 20, 20), Color.FromArgb(221, 0, 0), Color.FromArgb(255, 206, 0)); break;
                case "fr": Vertical(g, r, Color.FromArgb(0, 85, 164), Color.White, Color.FromArgb(239, 65, 53)); break;
                case "pt": Portuguese(g, r); break;
                case "pl": Horizontal2(g, r, Color.White, Color.FromArgb(220, 20, 60)); break;
                case "ru": Horizontal(g, r, Color.White, Color.FromArgb(0, 57, 166), Color.FromArgb(213, 43, 30)); break;
                case "uk": Horizontal2(g, r, Color.FromArgb(0, 87, 184), Color.FromArgb(255, 215, 0)); break;
                case "tr": Turkish(g, r); break;
                case "el": Greek(g, r); break;
                case "ro": Vertical(g, r, Color.FromArgb(0, 43, 127), Color.FromArgb(252, 209, 22), Color.FromArgb(206, 17, 38)); break;
                case "he": Israeli(g, r); break;
                case "zh-Hans": Chinese(g, r); break;
                default: Horizontal2(g, r, IllustratedTheme.Muted, IllustratedTheme.Panel); break;
            }
            using Pen border = new(Color.FromArgb(110, IllustratedTheme.Ivory), 1f);
            g.DrawRectangle(border, r.X, r.Y, Math.Max(1, r.Width - 1), Math.Max(1, r.Height - 1));
        }
        finally { g.Restore(state); }
    }

    private static void Horizontal(Graphics g, Rectangle r, Color a, Color b, Color c)
    {
        int h1 = r.Height / 3, h2 = r.Height / 3;
        Fill(g, new Rectangle(r.X, r.Y, r.Width, h1), a);
        Fill(g, new Rectangle(r.X, r.Y + h1, r.Width, h2), b);
        Fill(g, new Rectangle(r.X, r.Y + h1 + h2, r.Width, r.Height - h1 - h2), c);
    }

    private static void Horizontal2(Graphics g, Rectangle r, Color a, Color b)
    {
        int h = r.Height / 2;
        Fill(g, new Rectangle(r.X, r.Y, r.Width, h), a);
        Fill(g, new Rectangle(r.X, r.Y + h, r.Width, r.Height - h), b);
    }

    private static void Vertical(Graphics g, Rectangle r, Color a, Color b, Color c)
    {
        int w1 = r.Width / 3, w2 = r.Width / 3;
        Fill(g, new Rectangle(r.X, r.Y, w1, r.Height), a);
        Fill(g, new Rectangle(r.X + w1, r.Y, w2, r.Height), b);
        Fill(g, new Rectangle(r.X + w1 + w2, r.Y, r.Width - w1 - w2, r.Height), c);
    }

    private static void Spanish(Graphics g, Rectangle r)
    {
        int band = Math.Max(2, r.Height / 4);
        Color red = Color.FromArgb(170, 21, 27), yellow = Color.FromArgb(241, 191, 0);
        Fill(g, new Rectangle(r.X, r.Y, r.Width, band), red);
        Fill(g, new Rectangle(r.X, r.Y + band, r.Width, r.Height - 2 * band), yellow);
        Fill(g, new Rectangle(r.X, r.Bottom - band, r.Width, band), red);
    }

    private static void Portuguese(Graphics g, Rectangle r)
    {
        int green = (int)Math.Round(r.Width * .4);
        Fill(g, new Rectangle(r.X, r.Y, green, r.Height), Color.FromArgb(4, 106, 56));
        Fill(g, new Rectangle(r.X + green, r.Y, r.Width - green, r.Height), Color.FromArgb(218, 41, 28));
        using Brush gold = new SolidBrush(Color.FromArgb(255, 204, 0));
        g.FillEllipse(gold, r.X + green - 4, r.Y + r.Height / 2 - 4, 8, 8);
    }

    private static void Turkish(Graphics g, Rectangle r)
    {
        Fill(g, r, Color.FromArgb(227, 10, 23));
        int d = Math.Max(8, r.Height - 6);
        int x = r.X + 7, y = r.Y + (r.Height - d) / 2;
        using Brush white = new SolidBrush(Color.White);
        using Brush red = new SolidBrush(Color.FromArgb(227, 10, 23));
        g.FillEllipse(white, x, y, d, d);
        g.FillEllipse(red, x + d / 4, y + 2, d - 2, d - 4);
        PointF[] star = Star(new PointF(x + d + 3, r.Y + r.Height / 2f), Math.Max(3, d * .22f), Math.Max(1.5f, d * .09f));
        g.FillPolygon(white, star);
    }

    private static void Greek(Graphics g, Rectangle r)
    {
        Color blue = Color.FromArgb(13, 94, 175);
        int stripe = Math.Max(1, r.Height / 9);
        for (int i = 0; i < 9; i++) Fill(g, new Rectangle(r.X, r.Y + i * stripe, r.Width, i == 8 ? r.Height - i * stripe : stripe), i % 2 == 0 ? blue : Color.White);
        int canton = Math.Min(r.Width / 2, stripe * 5);
        Fill(g, new Rectangle(r.X, r.Y, canton, stripe * 5), blue);
        Fill(g, new Rectangle(r.X + canton / 2 - stripe / 2, r.Y, stripe, stripe * 5), Color.White);
        Fill(g, new Rectangle(r.X, r.Y + stripe * 2, canton, stripe), Color.White);
    }

    private static void Israeli(Graphics g, Rectangle r)
    {
        Color blue = Color.FromArgb(0, 56, 184);
        Fill(g, r, Color.White);
        int stripe = Math.Max(2, r.Height / 7);
        Fill(g, new Rectangle(r.X, r.Y + stripe, r.Width, stripe), blue);
        Fill(g, new Rectangle(r.X, r.Bottom - stripe * 2, r.Width, stripe), blue);
        PointF center = new(r.X + r.Width / 2f, r.Y + r.Height / 2f);
        float radius = Math.Max(4f, r.Height * .23f);
        PointF[] up = new PointF[3];
        PointF[] down = new PointF[3];
        for (int i = 0; i < 3; i++)
        {
            double a = -Math.PI / 2 + i * 2 * Math.PI / 3;
            up[i] = new PointF(center.X + (float)Math.Cos(a) * radius, center.Y + (float)Math.Sin(a) * radius);
            double b = Math.PI / 2 + i * 2 * Math.PI / 3;
            down[i] = new PointF(center.X + (float)Math.Cos(b) * radius, center.Y + (float)Math.Sin(b) * radius);
        }
        using Pen pen = new(blue, Math.Max(1f, r.Height * .07f));
        g.DrawPolygon(pen, up);
        g.DrawPolygon(pen, down);
    }

    private static void Chinese(Graphics g, Rectangle r)
    {
        Fill(g, r, Color.FromArgb(222, 41, 16));
        using Brush yellow = new SolidBrush(Color.FromArgb(255, 222, 0));
        PointF[] star = Star(new PointF(r.X + r.Width * .23f, r.Y + r.Height * .34f), r.Height * .22f, r.Height * .09f);
        g.FillPolygon(yellow, star);
    }

    private static void DrawUnitedKingdom(Graphics g, Rectangle r)
    {
        Color blue = Color.FromArgb(1, 33, 105), red = Color.FromArgb(200, 16, 46);
        Fill(g, r, blue);
        using Pen whiteDiag = new(Color.White, Math.Max(4, r.Height * .28f));
        using Pen redDiag = new(red, Math.Max(2, r.Height * .12f));
        g.DrawLine(whiteDiag, r.Left, r.Top, r.Right, r.Bottom);
        g.DrawLine(whiteDiag, r.Right, r.Top, r.Left, r.Bottom);
        g.DrawLine(redDiag, r.Left, r.Top, r.Right, r.Bottom);
        g.DrawLine(redDiag, r.Right, r.Top, r.Left, r.Bottom);
        int whiteV = Math.Max(5, (int)Math.Round(r.Height * .42));
        int whiteH = Math.Max(5, (int)Math.Round(r.Height * .42));
        Fill(g, new Rectangle(r.X + r.Width / 2 - whiteV / 2, r.Y, whiteV, r.Height), Color.White);
        Fill(g, new Rectangle(r.X, r.Y + r.Height / 2 - whiteH / 2, r.Width, whiteH), Color.White);
        int redV = Math.Max(2, whiteV / 2), redH = Math.Max(2, whiteH / 2);
        Fill(g, new Rectangle(r.X + r.Width / 2 - redV / 2, r.Y, redV, r.Height), red);
        Fill(g, new Rectangle(r.X, r.Y + r.Height / 2 - redH / 2, r.Width, redH), red);
    }

    private static PointF[] Star(PointF center, float outer, float inner)
    {
        PointF[] points = new PointF[10];
        for (int i = 0; i < 10; i++)
        {
            double angle = -Math.PI / 2 + i * Math.PI / 5;
            float radius = i % 2 == 0 ? outer : inner;
            points[i] = new PointF(center.X + (float)Math.Cos(angle) * radius, center.Y + (float)Math.Sin(angle) * radius);
        }
        return points;
    }

    private static void Fill(Graphics g, Rectangle r, Color color)
    {
        using Brush brush = new SolidBrush(color);
        g.FillRectangle(brush, r);
    }
}
