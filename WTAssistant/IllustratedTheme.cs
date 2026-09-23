using System.Drawing.Drawing2D;
using System.Globalization;

namespace WTVRSettingsAssistant;

// Theme data is deliberately independent of game profiles and input bindings.
internal static class IllustratedTheme
{
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
    public static void ApplyWindowChrome(Form form)
    {
        if (!Enabled || !form.IsHandleCreated) return;
        int dark=1, background=ColorTranslator.ToWin32(Background), ink=ColorTranslator.ToWin32(Ivory), border=ColorTranslator.ToWin32(Gold);
        // Unsupported attributes on older Windows simply return an HRESULT.
        DwmSetWindowAttribute(form.Handle,20,ref dark,4);
        DwmSetWindowAttribute(form.Handle,35,ref background,4);
        DwmSetWindowAttribute(form.Handle,36,ref ink,4);
        DwmSetWindowAttribute(form.Handle,34,ref border,4);
    }
    private sealed record AssetLabel(string Text, bool Active);
    public static bool DrawAsset(Graphics graphics, Image image, Rectangle bounds)
    {
        if (image.Tag is not AssetLabel label) return false;
        DrawFrame(graphics, Rectangle.Inflate(bounds,-3,-3),label.Active);
        bool mode = label.Text is "MONITOR" or "VR";
        bool launch = label.Text == "LAUNCH";
        if (mode && label.Active)
        {
            using Brush tint = new SolidBrush(Color.FromArgb(58, Gold));
            graphics.FillRectangle(tint, Rectangle.Inflate(bounds,-10,-10));
        }
        if (mode)
            DrawIcon(graphics,label.Text == "VR" ? "visor" : "monitor",new Rectangle(bounds.X+bounds.Width/2-40,bounds.Y+15,80,65),label.Active ? Gold : Ivory);
        if (launch)
            DrawIcon(graphics,"play",new Rectangle(bounds.X+30,bounds.Y+bounds.Height/2-30,60,60),Gold);
        float size = Math.Min(32,Math.Min(bounds.Height*0.35f,bounds.Width / Math.Max(1,label.Text.Length) * 1.25f));
        float canvasScale=Math.Max(.1f,Math.Abs(graphics.Transform.Elements[0]));
        using Font font=new("Segoe UI",Math.Max(18/canvasScale,size),FontStyle.Bold,GraphicsUnit.Pixel);
        using Brush brush=new SolidBrush(label.Active || launch ? Gold : Ivory);
        using StringFormat format=new(){Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center};
        Rectangle textBounds = bounds;
        if(mode) { textBounds.Y += 73; textBounds.Height -= 83; }
        if(launch) { textBounds.X += 55; textBounds.Width -= 55; }
        graphics.DrawString(label.Text,font,brush,textBounds,format);
        return true;
    }
    public static bool Enabled { get; } = ReadPreference();
    private static bool ReadPreference()
    {
        // Version 1.6 replaces the old interface. Keep the preference file itself
        // untouched for update safety, but do not allow it to revive the retired UI.
        return true;
    }
    private static bool IsMigTheme => AppThemeAssets.ActiveTheme.Equals("MiG29", StringComparison.OrdinalIgnoreCase);
    public static Color Background => IsMigTheme ? Color.FromArgb(24, 24, 24) : Color.FromArgb(29, 31, 33);
    public static Color Panel => IsMigTheme ? Color.FromArgb(31, 31, 31) : Color.FromArgb(36, 38, 40);
    public static Color Ivory => IsMigTheme ? Color.FromArgb(239, 226, 196) : Color.FromArgb(235, 238, 240);
    public static Color Gold => IsMigTheme ? Color.FromArgb(196, 54, 46) : Color.FromArgb(226, 180, 85);
    public static Color Muted => IsMigTheme ? Color.FromArgb(154, 148, 139) : Color.FromArgb(168, 168, 172);
    public static Color SelectedRow => IsMigTheme ? Color.FromArgb(88, 43, 39) : Color.FromArgb(91, 74, 42);
    public static void DrawSwitch(Graphics g, Rectangle bounds, bool selected)
    {
        using GraphicsPath shape = new();
        int d=bounds.Height;
        shape.AddArc(bounds.Left,bounds.Top,d,d,90,180);
        shape.AddArc(bounds.Right-d,bounds.Top,d,d,270,180);
        shape.CloseFigure();
        using Brush fill = new SolidBrush(selected ? Color.FromArgb(100,Gold) : Color.FromArgb(90,Muted));
        using Pen edge = new(selected ? Gold : Muted,1.5f);
        using Brush knob = new SolidBrush(selected ? Gold : Muted);
        g.FillPath(fill,shape); g.DrawPath(edge,shape);
        g.FillEllipse(knob,selected ? bounds.Right-d+2 : bounds.Left+2,bounds.Top+2,d-4,d-4);
    }
    private static readonly Dictionary<string, Image> Illustrations = new();
    private static readonly Dictionary<string, Image?> OptionalImages = new();
    private static Image Illustration(string name)
    {
        if (Illustrations.TryGetValue(name,out var image)) return image;
        using Image source = AssetManager.LoadImage($"Illustrated{name}.png");
        return Illustrations[name] = new Bitmap(source);
    }

    public static Image? Asset(string name)
    {
        if (!Enabled) return null;
        string key = Path.GetFileNameWithoutExtension(name).ToLowerInvariant();
        if (key == "mainscreenlogo")
        {
            return BrandingLogo.Create();
        }
        // Instructional screenshots and third-party logos remain unchanged.
        string? label = key switch
        {
            "monitor_red" or "monitor_orange" or "monitor_green" => "MONITOR",
            "vr_red" or "vr_orange" or "vr_green" => "VR",
            "play_on" or "play_off" => "LAUNCH",
            "browse_red" or "browse_green" => "BROWSE",
            "capturesettings" or "capturesettingsgray" => "CAPTURE SETTINGS",
            "remove" or "removegray" => "REMOVE",
            "button_on" => "ON", "button_off" => "OFF",
            "low_red" or "low_green" => "LOW", "medium_red" or "medium_green" => "MEDIUM",
            "high_red" or "high_green" => "HIGH", "recommendedsettings" => "RECOMMENDED SETTINGS",
            "nvidiaprofileinspector" => "PROFILE INSPECTOR", "youtube" => "YOUTUBE", "discord" => "DISCORD",
            "beer" => "SUPPORT", _ => null
        };
        string? icon = key switch
        {
            "home" => "home", "settings" => "gear", "info" or "help" or "moreinfo" => "info",
            "neckassist" or "neckassist_active" => "head",
            "hiddenkeybinds-v3" or "keybindassistant_inactive_v2" => "keys",
            "update_green" or "update_yellow" => "download",
            "back" or "backarrow" or "backgray" => "back", "next" or "nextgray" => "next", _ => null
        };
        if (label == null && icon == null) return null;
        bool active = key.EndsWith("_green") || key.EndsWith("_on") || key == "neckassist_active" || key == "hiddenkeybinds-v3";
        Bitmap bitmap = new(icon == null ? 640 : 160, icon == null ? 160 : 160);
        using Graphics g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        if (icon != null) DrawIcon(g, icon, new Rectangle(16, 16, 128, 128), active ? Gold : Ivory);
        else
        {
            bitmap.Tag = new AssetLabel(label!,active);
            DrawFrame(g, new Rectangle(5, 5, 629, 149), active);
            using Font font = new("Segoe UI", label!.Length > 19 ? 30 : 44, FontStyle.Bold, GraphicsUnit.Pixel);
            using Brush text = new SolidBrush(active ? Gold : Ivory);
            using StringFormat format = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(label, font, text, new RectangleF(18, 15, 604, 130), format);
        }
        return bitmap;
    }


    public static void DrawFrame(Graphics g, Rectangle r, bool active)
    {
        if (r.Width <= 0 || r.Height <= 0) return;
        using GraphicsPath shape = RoundedRectangle(r, Math.Min(10, r.Height / 4));
        using var fill = new LinearGradientBrush(r, active ? Color.FromArgb(51, 49, 43) : Color.FromArgb(42, 44, 46), Panel, 90f);
        using Pen outline = new(active ? Gold : Color.FromArgb(81, 83, 86), active ? 2 : 1);
        g.FillPath(fill, shape); g.DrawPath(outline, shape);
        if (active && r.Height > 60)
        {
            using Pen accent = new(Gold, 3);
            g.DrawLine(accent, r.Left + 18, r.Top + 1, r.Left + Math.Min(72, r.Width / 3), r.Top + 1);
        }
    }

    private static void DrawDashboardIcon(Graphics g, Pen p, string icon, Color color)
    {
        using Brush glass = new SolidBrush(Color.FromArgb(28, color));
        using Brush ink = new SolidBrush(color);
        if (icon == "monitor")
        {
            using GraphicsPath screen = RoundedRectangle(new RectangleF(5, 16, 90, 58), 4);
            g.FillPath(glass, screen); g.DrawPath(p, screen);
            using Brush display = new SolidBrush(Color.FromArgb(29,31,33));
            g.FillRectangle(display, 10, 21, 80, 44);
            g.DrawLine(p, 44, 75, 42, 85); g.DrawLine(p, 56, 75, 58, 85);
            g.DrawLine(p, 30, 86, 70, 86);
            using Pen glint = new(Color.FromArgb(100, color), 1.2f);
            g.DrawLine(glint, 15, 26, 34, 26);
            g.FillEllipse(ink, 49, 68, 2, 2);
        }
        else if (icon == "visor")
        {
            using GraphicsPath shell = new();
            shell.AddBezier(10, 42, 10, 27, 90, 27, 90, 42);
            shell.AddLine(90, 42, 87, 68);
            shell.AddBezier(87, 68, 85, 78, 64, 77, 57, 68);
            shell.AddBezier(57, 68, 52, 62, 48, 62, 43, 68);
            shell.AddBezier(43, 68, 36, 77, 15, 78, 13, 68);
            shell.CloseFigure();
            g.FillPath(glass, shell); g.DrawPath(p, shell);
            g.DrawArc(p, 25, 11, 50, 43, 192, 156);
            g.DrawLine(p, 4, 43, 4, 61); g.DrawLine(p, 96, 43, 96, 61);
            g.FillEllipse(ink, 24, 48, 4, 4); g.FillEllipse(ink, 72, 48, 4, 4);
            using Pen seam = new(Color.FromArgb(100, color), 1.2f);
            g.DrawBezier(seam, 20, 40, 35, 35, 65, 35, 80, 40);
        }
        else
        {
            for (int y = 15; y <= 65; y += 25)
            {
                using GraphicsPath rack = RoundedRectangle(new RectangleF(10, y, 66, 20), 4);
                g.FillPath(glass, rack); g.DrawPath(p, rack);
                g.FillEllipse(ink, 18, y+7, 6, 6); g.DrawLine(p, 34, y+10, 62, y+10);
            }
            using Brush bg = new SolidBrush(Background);
            g.FillEllipse(bg, 55, 54, 39, 39); g.DrawEllipse(p, 55, 54, 39, 39);
            if (icon == "live") g.DrawLines(p, [new(64,74), new(72,81), new(85,66)]);
            else g.DrawLines(p, [new(69,63), new(80,63), new(80,71), new(86,82), new(64,82), new(69,71), new(69,63)]);
        }
    }

    public static void DrawIcon(Graphics g, string icon, Rectangle bounds, Color color)
    {
        if (icon is "head" or "keys")
        {
            g.DrawImage(Illustration(icon == "head" ? "Head" : "Keys"),bounds);
            return;
        }
        if (icon == "trim" && OptionalImage("Home_VTrim_New.png") is Image trimImage)
        {
            Rectangle drawBounds = FitImage(trimImage.Size, bounds);
            g.DrawImage(trimImage, drawBounds);
            return;
        }
        var state = g.Save(); g.TranslateTransform(bounds.X,bounds.Y); g.ScaleTransform(bounds.Width/100f,bounds.Height/100f);
        using Pen p = new(color, 3) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
        if (icon is "monitor" or "visor" or "live" or "test")
        {
            DrawDashboardIcon(g, p, icon, color);
            g.Restore(state);
            return;
        }
        switch (icon)
        {
            case "trim":
                g.DrawLines(p, [new(50,8),new(57,42),new(88,62),new(88,69),new(56,58),new(55,78),new(67,86),new(67,91),new(50,87),new(33,91),new(33,86),new(45,78),new(44,58),new(12,69),new(12,62),new(43,42),new(50,8)]);
                g.DrawLine(p, 50, 18, 50, 78);
                g.DrawLine(p, 15, 16, 35, 16); g.DrawLine(p, 25, 11, 25, 32);
                g.DrawLines(p, [new(20,27),new(25,32),new(30,27)]);
                g.DrawLine(p, 65, 16, 85, 16); g.DrawLine(p, 75, 11, 75, 32);
                g.DrawLines(p, [new(70,27),new(75,32),new(80,27)]); break;
            case "monitor":
                // Cleaner modern display icon with a distinct panel, bezel and stand.
                using (GraphicsPath screen = RoundedRectangle(new RectangleF(8, 10, 84, 58), 7)) g.DrawPath(p, screen);
                using (GraphicsPath inner = RoundedRectangle(new RectangleF(15, 17, 70, 43), 3)) g.DrawPath(p, inner);
                g.DrawLine(p, 50, 68, 50, 82);
                g.DrawLine(p, 34, 82, 66, 82);
                g.DrawLine(p, 25, 88, 75, 88);
                using (var b = new SolidBrush(color)) g.FillEllipse(b, 47, 62, 6, 6);
                break;
            case "visor":
                // VR headset silhouette with two lenses and head strap.
                using (GraphicsPath visor = RoundedRectangle(new RectangleF(10, 28, 80, 43), 13)) g.DrawPath(p, visor);
                using (GraphicsPath leftLens = RoundedRectangle(new RectangleF(20, 38, 25, 21), 6)) g.DrawPath(p, leftLens);
                using (GraphicsPath rightLens = RoundedRectangle(new RectangleF(55, 38, 25, 21), 6)) g.DrawPath(p, rightLens);
                g.DrawArc(p, 20, 11, 60, 40, 205, 130);
                g.DrawLine(p, 10, 43, 3, 49);
                g.DrawLine(p, 90, 43, 97, 49);
                g.DrawLines(p, [new(43, 71), new(47, 78), new(53, 78), new(57, 71)]);
                break;
            case "play": using(var b=new SolidBrush(color)) g.FillPolygon(b,[new(25,10),new(85,50),new(25,90)]); break;
            case "refresh":
                g.DrawArc(p,18,18,64,64,205,250);
                g.DrawLines(p,[new(76,13),new(83,31),new(63,30)]);
                break;
            case "discord":
                g.DrawArc(p,17,29,66,45,195,150); g.DrawArc(p,17,29,66,45,15,150);
                g.DrawLine(p,24,31,31,20); g.DrawLine(p,76,31,69,20);
                g.DrawEllipse(p,35,45,5,5); g.DrawEllipse(p,60,45,5,5);
                g.DrawArc(p,39,48,22,13,5,170);
                break;
            case "youtube":
                using (GraphicsPath yt = RoundedRectangle(new RectangleF(10,24,80,52),12)) g.DrawPath(p,yt);
                using (var b = new SolidBrush(color)) g.FillPolygon(b,[new(43,36),new(43,64),new(67,50)]);
                break;
            case "beer":
                g.DrawLines(p,[new(25,27),new(66,27),new(62,78),new(30,78),new(25,27)]);
                g.DrawArc(p,60,37,24,29,270,180); g.DrawLine(p,84,51,84,60);
                g.DrawLine(p,20,27,70,27); g.DrawArc(p,28,17,14,18,180,175); g.DrawArc(p,42,15,16,20,180,175);
                break;
            case "live":
                // Globe + live broadcast waves.
                g.DrawEllipse(p, 9, 18, 60, 60);
                g.DrawArc(p, 22, 18, 34, 60, 90, 180);
                g.DrawArc(p, 22, 18, 34, 60, 270, 180);
                g.DrawLine(p, 10, 48, 68, 48);
                g.DrawArc(p, 64, 20, 22, 22, 205, 110);
                g.DrawArc(p, 61, 12, 38, 38, 205, 110);
                using (var b = new SolidBrush(color)) g.FillEllipse(b, 73, 36, 7, 7);
                break;
            case "test":
                // Server rack + laboratory/test badge.
                using (GraphicsPath rack = RoundedRectangle(new RectangleF(8, 19, 57, 62), 5)) g.DrawPath(p, rack);
                g.DrawLine(p, 8, 39, 65, 39);
                g.DrawLine(p, 8, 60, 65, 60);
                using (var b = new SolidBrush(color))
                {
                    g.FillEllipse(b, 16, 28, 5, 5);
                    g.FillEllipse(b, 16, 49, 5, 5);
                    g.FillEllipse(b, 16, 69, 5, 5);
                }
                g.DrawLine(p, 75, 15, 75, 43);
                g.DrawLine(p, 68, 15, 82, 15);
                g.DrawLines(p, [new(75, 43), new(64, 66), new(64, 77), new(92, 77), new(92, 66), new(81, 43)]);
                g.DrawLine(p, 67, 63, 89, 63);
                break;
            case "folder": g.DrawLines(p,[new(9,30),new(9,18),new(40,18),new(50,30),new(89,30),new(89,81),new(9,81),new(9,30)]); g.DrawLine(p,15,39,82,39); break;
            case "home": g.DrawLines(p,[new(12,46),new(50,13),new(88,46)]); g.DrawLines(p,[new(24,39),new(24,85),new(43,85),new(43,61),new(59,61),new(59,85),new(77,85),new(77,39)]); break;
            case "keys":
                g.DrawRectangle(p,10,20,80,60);
                for(int y=0;y<2;y++) for(int x=0;x<5;x++) g.DrawRectangle(p,19+x*13,30+y*15,7,7);
                g.DrawRectangle(p,30,63,40,7); break;
            case "head":
                g.DrawArc(p,25,8,51,55,180,190); g.DrawRectangle(p,27,30,58,23);
                g.DrawLines(p,[new(72,53),new(69,66),new(54,69),new(54,83)]);
                g.DrawLines(p,[new(28,48),new(33,65),new(33,80)]);
                g.DrawArc(p,8,63,83,28,10,285); g.DrawLines(p,[new(85,68),new(92,79),new(80,82)]); break;
            case "gear":
                for(int i=0;i<12;i++) { double a=i*Math.PI/6; g.DrawLine(p,50+(float)Math.Cos(a)*31,50+(float)Math.Sin(a)*31,50+(float)Math.Cos(a)*42,50+(float)Math.Sin(a)*42); }
                g.DrawEllipse(p,19,19,62,62); g.DrawEllipse(p,37,37,26,26); break;
            case "download": g.DrawLine(p,50,12,50,66); g.DrawLines(p,[new(30,48),new(50,68),new(70,48)]); g.DrawLines(p,[new(17,69),new(17,86),new(83,86),new(83,69)]); break;
            case "back": g.DrawLines(p,[new(65,18),new(30,50),new(65,82)]); break;
            case "next": g.DrawLines(p,[new(35,18),new(70,50),new(35,82)]); break;
            default: g.DrawEllipse(p,12,12,76,76); g.DrawLine(p,50,44,50,71); g.DrawEllipse(p,48,27,4,4); break;
        }
        g.Restore(state);
    }

    private static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
    {
        float d = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        GraphicsPath path = new();
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static Image IconAsset(string icon, int size = 160)
    {
        Bitmap bitmap = new(size, size);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        DrawIcon(graphics, icon, new Rectangle(8, 8, size - 16, size - 16), Gold);
        return bitmap;
    }

    private static Image LoadThemeImageDirect(string fileName)
    {
        var assembly = typeof(IllustratedTheme).Assembly;
        string? resourceName = AppThemeAssets.ResolveResourceName(assembly, fileName);
        if (resourceName is null)
            throw new FileNotFoundException($"Theme image resource not found: {fileName}");

        using Stream? stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
            throw new FileNotFoundException($"Theme image stream not found: {fileName}");

        using Image source = Image.FromStream(stream);
        return new Bitmap(source);
    }

    private static Image? OptionalImage(string fileName)
    {
        if (OptionalImages.TryGetValue(fileName, out Image? cached)) return cached;
        try
        {
            using Image source = LoadThemeImageDirect(fileName);
            return OptionalImages[fileName] = new Bitmap(source);
        }
        catch (FileNotFoundException)
        {
            return OptionalImages[fileName] = null;
        }
    }

    private static Rectangle FitImage(Size imageSize, Rectangle bounds)
    {
        if (imageSize.Width <= 0 || imageSize.Height <= 0 || bounds.Width <= 0 || bounds.Height <= 0) return bounds;
        float scale = Math.Min(bounds.Width / (float)imageSize.Width, bounds.Height / (float)imageSize.Height);
        int width = Math.Max(1, (int)Math.Round(imageSize.Width * scale));
        int height = Math.Max(1, (int)Math.Round(imageSize.Height * scale));
        return new Rectangle(bounds.X + (bounds.Width - width) / 2, bounds.Y + (bounds.Height - height) / 2, width, height);
    }
}

internal static class ThemePalette
{
    public static Color FromArgb(int argb) => Map(Color.FromArgb(argb));
    public static Color FromArgb(int r,int g,int b) => Map(Color.FromArgb(r,g,b));
    public static Color FromArgb(int a,int r,int g,int b) => Color.FromArgb(a,Map(Color.FromArgb(r,g,b)));
    public static Color FromArgb(int a,Color color) => Color.FromArgb(a,Map(color));
    public static Color Map(Color c)
    {
        if (!IllustratedTheme.Enabled) return c;
        if (c.R < 40 && c.G < 45 && c.B < 50) return IllustratedTheme.Background;
        if (c.R < 65 && c.G < 100 && c.B < 100) return IllustratedTheme.Panel;
        if (c.R > 220 && c.G > 220 && c.B > 220) return IllustratedTheme.Ivory;
        if (c.G > 170 && c.B > 170 && c.R < 200) return IllustratedTheme.Ivory;
        if (c.G > 170 && c.G > c.R*1.2 && c.B < 175) return IllustratedTheme.Gold;
        if (c.R > 220 && c.G > 150 && c.B < 140) return IllustratedTheme.Gold;
        return c; // Keep warning/error and graph marker distinctions accessible.
    }
}

internal sealed class IllustratedNavButton : Button
{
    private long _lastMouseDown;
    public event EventHandler? QuickDoubleClick;
    protected override void OnMouseDown(MouseEventArgs e)
    {
        long now = Environment.TickCount64;
        if (e.Button == MouseButtons.Left && now - _lastMouseDown <= SystemInformation.DoubleClickTime)
        {
            _lastMouseDown = 0;
            QuickDoubleClick?.Invoke(this, EventArgs.Empty);
        }
        else _lastMouseDown = now;
        base.OnMouseDown(e);
    }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool Selected { get; set; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool FeatureActive { get; set; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public string PageKey { get; set; } = "";
    private readonly string _icon;
    private bool _hover;
    public IllustratedNavButton(string text, string icon)
    {
        Text = text; _icon = icon; FlatStyle = FlatStyle.Flat;
        ForeColor = IllustratedTheme.Ivory; BackColor = IllustratedTheme.Panel;
        AccessibleName = text; Font = new Font("Segoe UI", 25, FontStyle.Bold, GraphicsUnit.Pixel);
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }
    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        if (_hover || Focused || Selected)
        {
            using Brush tint = new SolidBrush(Color.FromArgb(Selected ? 60 : 25,IllustratedTheme.Gold));
            e.Graphics.FillRectangle(tint,2,2,Width-5,Height-5);
            using Pen edge = new(IllustratedTheme.Gold,1);
            e.Graphics.DrawRectangle(edge,2,2,Width-5,Height-5);
            using Brush gold = new SolidBrush(IllustratedTheme.Gold);
            e.Graphics.FillRectangle(gold,2,2,4,Height-5);
        }
        float contentScale = Math.Max(.82f, Font.Size / 23f);
        int inset = Math.Max(8, (int)Math.Round(9 * contentScale));
        int iconSize = Math.Min((int)Math.Round(56 * contentScale), Height - inset * 2);
        int textLeft = inset + iconSize + Math.Max(10, (int)Math.Round(12 * contentScale));
        IllustratedTheme.DrawIcon(e.Graphics, _icon, new Rectangle(inset,(Height-iconSize)/2,iconSize,iconSize), FeatureActive || Selected ? IllustratedTheme.Gold : ForeColor);
        Rectangle textBounds = new(textLeft, 2, Math.Max(1, Width - textLeft - inset), Math.Max(1, Height - 4));
        float drawSize = Font.Size;
        bool singleLine = false;
        while (drawSize > 10F)
        {
            using Font test = new(Font.FontFamily, drawSize, Font.Style, Font.Unit);
            Size oneLine = TextRenderer.MeasureText(e.Graphics, Text, test, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            if (oneLine.Width <= textBounds.Width && oneLine.Height <= textBounds.Height)
            {
                singleLine = true;
                break;
            }
            Size wrapped = TextRenderer.MeasureText(e.Graphics, Text, test, textBounds.Size, TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            if (wrapped.Width <= textBounds.Width && wrapped.Height <= textBounds.Height) break;
            drawSize -= 0.5F;
        }
        using Font drawFont = new(Font.FontFamily, Math.Max(10F, drawSize), Font.Style, Font.Unit);
        TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix;
        flags |= singleLine ? TextFormatFlags.SingleLine : TextFormatFlags.WordBreak;
        TextRenderer.DrawText(e.Graphics, Text, drawFont, textBounds, ForeColor, flags);
        if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(4,4,Width-9,Height-9));
    }
}

internal class ThemeButton : Button
{
    protected override void OnPaint(PaintEventArgs e)
    {
        if (ClientSize.Width <= 11 || ClientSize.Height <= 11) return;
        if (!IllustratedTheme.Enabled) { base.OnPaint(e); return; }
        e.Graphics.Clear(IllustratedTheme.Panel);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle frame = new(2, 2, Width - 5, Height - 5);
        using (Brush fill = new LinearGradientBrush(frame, Color.FromArgb(42, 44, 46), Color.FromArgb(31, 33, 35), 90f))
            e.Graphics.FillRectangle(fill, frame);
        IllustratedTheme.DrawFrame(e.Graphics, frame, Focused || ClientRectangle.Contains(PointToClient(MousePosition)));
        Rectangle textBounds = new(9,4,Math.Max(1,Width-18),Math.Max(1,Height-8));
        using Font fitted = FitCaption(e.Graphics, Text, Font, textBounds.Size);
        TextRenderer.DrawText(e.Graphics, Text, fitted, textBounds, Enabled ? IllustratedTheme.Ivory : IllustratedTheme.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(5,5,Width-11,Height-11));
    }
    private static Font FitCaption(Graphics g, string text, Font source, Size bounds)
    {
        FontFamily family = FontFamily.GenericSansSerif;
        FontStyle style = FontStyle.Regular;
        float pixels = 16f;
        try
        {
            family = source.FontFamily;
            style = source.Style;
            pixels = source.Unit == GraphicsUnit.Pixel ? source.Size : source.SizeInPoints * g.DpiY / 72f;
        }
        catch (ArgumentException)
        {
        }
        while (pixels > 6)
        {
            using Font test = new(family, pixels, style, GraphicsUnit.Pixel);
            Size measured = TextRenderer.MeasureText(g, text, test, Size.Empty, TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            if (measured.Width <= bounds.Width && measured.Height <= bounds.Height) break;
            pixels -= .5f;
        }
        return new Font(family, Math.Max(6, pixels), style, GraphicsUnit.Pixel);
    }
}

internal sealed class AssistantInfoPage : UserControl
{
    private readonly Label _title, _purpose, _legal, _heading, _body, _profileHeading, _profileBody;
    private readonly Panel _card, _profileCard;
    private readonly string _version;
    private bool _arranging;

    public AssistantInfoPage(string version)
    {
        _version = version;
        Dock = DockStyle.Fill;
        BackColor = IllustratedTheme.Background;
        AutoScroll = true;

        Label LabelFor(string text, int size, bool bold = false) => new()
        {
            Text = text,
            ForeColor = IllustratedTheme.Ivory,
            BackColor = BackColor,
            Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel),
            AutoSize = false,
            UseMnemonic = false
        };

        _title = LabelFor("WAR THUNDER VR ASSISTANT", 38, true);
        _purpose = LabelFor("Your control center for War Thunder VR.", 23);
        _legal = LabelFor("Free and open source community software.", 20);
        _heading = LabelFor($"Version {version} Patch Notes", 30, true);
        _body = LabelFor(ReleaseNotes.Features, 22);
        _profileHeading = LabelFor("Aircraft profiles and vJoy setup", 30, true);
        _profileBody = LabelFor(string.Empty, 19);

        _card = new Panel { BackColor = IllustratedTheme.Panel };
        _heading.BackColor = _body.BackColor = _card.BackColor;
        _card.Controls.AddRange([_heading, _body]);
        _card.Paint += (_, e) => IllustratedTheme.DrawFrame(e.Graphics, new Rectangle(2, 2, _card.Width - 5, _card.Height - 5), false);

        _profileCard = new Panel { BackColor = IllustratedTheme.Panel };
        _profileHeading.BackColor = _profileBody.BackColor = _profileCard.BackColor;
        _profileCard.Controls.AddRange([_profileHeading, _profileBody]);
        _profileCard.Paint += (_, e) => IllustratedTheme.DrawFrame(e.Graphics, new Rectangle(2, 2, _profileCard.Width - 5, _profileCard.Height - 5), false);

        Controls.AddRange([_title, _purpose, _card, _profileCard, _legal]);
        _title.Visible = _purpose.Visible = false;
        SizeChanged += (_, _) => Arrange();
        Arrange();
    }

    public void SetLanguage(string languageCode)
    {
        _title.Text = $"{AppText.T(languageCode, "Info.Title")}  ·  v{_version}";
        _purpose.Text = AppText.T(languageCode, "Info.Purpose");
        _heading.Text = string.Format(CultureInfo.CurrentCulture, AppText.T(languageCode, "Info.ChangesTitle"), _version);
        _body.Text = AppText.T(languageCode, "Info.ChangesText");
        _profileHeading.Text = AppText.T(languageCode, "Info.ProfileGuideTitle");
        _profileBody.Text = AppText.T(languageCode, "Info.ProfileGuideText");
        _legal.Text = AppText.T(languageCode, "Info.OpenSource");
        Arrange();
        Invalidate();
    }

    private void Arrange()
    {
        if (_arranging) return;
        _arranging = true;
        try
        {
            int width = Math.Max(160, ClientSize.Width - 48);
            Point scroll = AutoScrollPosition;
            int Fit(Label label, int x, int y, int w)
            {
                int h = TextRenderer.MeasureText(label.Text, label.Font, new Size(w, 10000), TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl).Height + 10;
                label.SetBounds(x, y, w, h);
                return h;
            }

            int top = 20;

            int headingHeight = Fit(_heading, 22, 18, width - 44);
            int bodyHeight = Fit(_body, 22, 30 + headingHeight, width - 44);
            int cardHeight = headingHeight + bodyHeight + 58;
            _card.SetBounds(20 + scroll.X, top + scroll.Y, width, cardHeight);

            int bottom = top + cardHeight + 24;
            int profileHeadingHeight = Fit(_profileHeading, 22, 18, width - 44);
            int profileBodyHeight = Fit(_profileBody, 22, 30 + profileHeadingHeight, width - 44);
            int profileCardHeight = profileHeadingHeight + profileBodyHeight + 58;
            _profileCard.SetBounds(20 + scroll.X, bottom + scroll.Y, width, profileCardHeight);
            bottom += profileCardHeight + 24;
            bottom += Fit(_legal, 20 + scroll.X, bottom + scroll.Y, width) + 42;
            AutoScrollMinSize = new Size(0, bottom);
        }
        finally { _arranging = false; }
    }
}

internal class ThemeCheckBox : CheckBox
{
    public override Size GetPreferredSize(Size proposedSize) => IllustratedTheme.Enabled
        ? ThemeTogglePainter.PreferredSize(this, proposedSize, Appearance == Appearance.Button)
        : base.GetPreferredSize(proposedSize);

    public ThemeCheckBox()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = IllustratedTheme.Panel;
        UpdateSwitchRegion();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateSwitchRegion();
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        UpdateSwitchRegion();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        UpdateSwitchRegion();
    }

    private void UpdateSwitchRegion()
    {
        if (!IllustratedTheme.Enabled || Appearance == Appearance.Button || !string.IsNullOrWhiteSpace(Text))
        {
            Region = null;
            return;
        }

        float scale = Math.Clamp(Font.Size / 20F, 1.18F, 1.80F);
        int switchWidth = Math.Max(54, (int)Math.Round(46 * scale));
        int switchHeight = Math.Max(28, (int)Math.Round(24 * scale));
        int y = Math.Max(0, (Height - switchHeight) / 2);
        int x = Math.Max(0, (Width - switchWidth) / 2);
        Rectangle bounds = new(x, Math.Max(0, y - 2), Math.Min(Width - x, switchWidth), Math.Min(Height - Math.Max(0, y - 2), switchHeight + 4));
        if (bounds.Width < 4 || bounds.Height < 4)
        {
            Region = null;
            return;
        }

        using GraphicsPath path = RoundedRect(bounds, bounds.Height / 2);
        Region = new Region(path);
    }

    private static GraphicsPath RoundedRect(Rectangle rect, int radius)
    {
        int diameter = Math.Max(2, radius * 2);
        GraphicsPath path = new();
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (!IllustratedTheme.Enabled) { base.OnPaint(e); return; }
        ThemeTogglePainter.Paint(this,e.Graphics,Checked,Appearance == Appearance.Button);
    }
}
internal class ThemeRadioButton : RadioButton
{
    public override Size GetPreferredSize(Size proposedSize) => IllustratedTheme.Enabled
        ? ThemeTogglePainter.PreferredSize(this, proposedSize, Appearance == Appearance.Button)
        : base.GetPreferredSize(proposedSize);

    public ThemeRadioButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = IllustratedTheme.Panel;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (!IllustratedTheme.Enabled) { base.OnPaint(e); return; }
        ThemeTogglePainter.Paint(this,e.Graphics,Checked,Appearance == Appearance.Button);
    }
}
internal static class ThemeTogglePainter
{
    public static Size PreferredSize(ButtonBase control, Size proposed, bool button)
    {
        bool iconOnly = string.IsNullOrWhiteSpace(control.Text);
        float scale = Math.Clamp(control.Font.Size / 20F, iconOnly ? 1.18F : .72F, 1.8F);
        int switchWidth = Math.Max(iconOnly ? 54 : 34, (int)Math.Round(46 * scale));
        int switchHeight = Math.Max(iconOnly ? 28 : 18, (int)Math.Round(24 * scale));
        int inset = button ? 14 : switchWidth + Math.Max(7, (int)Math.Round(9 * scale)) + 2;
        int available = proposed.Width > inset ? proposed.Width - inset : int.MaxValue;
        Size text = TextRenderer.MeasureText(control.Text, control.Font, new Size(available, int.MaxValue), TextFormatFlags.WordBreak);
        return new Size(text.Width + inset, Math.Max(text.Height + 6, switchHeight + 4));
    }

    public static void Paint(ButtonBase control, Graphics g, bool selected, bool button)
    {
        if (control.ClientSize.Width <= 11 || control.ClientSize.Height <= 11) return;
        Color clear = button ? IllustratedTheme.Panel : (control.BackColor == Color.Transparent ? IllustratedTheme.Panel : control.BackColor);
        g.Clear(clear);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Color ink = control.Enabled ? selected ? IllustratedTheme.Gold : IllustratedTheme.Ivory : IllustratedTheme.Muted;
        bool iconOnlySwitch = !button && string.IsNullOrWhiteSpace(control.Text);
        float minScale = iconOnlySwitch ? 1.18F : 0.72F;
        float scale = Math.Clamp(control.Font.Size / 20F, minScale, 1.80F);
        int switchWidth = Math.Max(iconOnlySwitch ? 54 : 34, (int)Math.Round(46 * scale));
        int switchHeight = Math.Max(iconOnlySwitch ? 28 : 18, (int)Math.Round(24 * scale));
        int gap = Math.Max(7, (int)Math.Round(9 * scale));
        int textLeft = switchWidth + gap + 2;
        Rectangle text = new(textLeft,0,Math.Max(1,control.Width-textLeft),control.Height);
        if (button)
        {
            Rectangle frame = new(2, 2, control.Width - 5, control.Height - 5);
            using (Brush fill = new LinearGradientBrush(frame, selected ? Color.FromArgb(54, 49, 37) : Color.FromArgb(42, 44, 46), selected ? Color.FromArgb(44, 39, 28) : Color.FromArgb(31, 33, 35), 90f))
                g.FillRectangle(fill, frame);
            IllustratedTheme.DrawFrame(g,frame,selected);
            if(selected)
            {
                using Brush fill=new SolidBrush(IllustratedTheme.Gold);
                g.FillRectangle(fill,5,5,Math.Max(1,control.Width-11),Math.Max(1,control.Height-11));
                ink=IllustratedTheme.Background;
            }
            text = new Rectangle(7,3,control.Width-14,control.Height-6);
        }
        else
        {
            int x = iconOnlySwitch ? Math.Max(0, (control.Width - switchWidth) / 2) : 2;
            int y = Math.Max(0, (control.Height - switchHeight) / 2);
            IllustratedTheme.DrawSwitch(g,new Rectangle(x, y, switchWidth, switchHeight),selected);
        }
        TextRenderer.DrawText(g,control.Text,control.Font,text,ink,TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | (button ? TextFormatFlags.HorizontalCenter : TextFormatFlags.Left));
        if(control.Focused && !string.IsNullOrWhiteSpace(control.Text)) ControlPaint.DrawFocusRectangle(g,new Rectangle(1,1,control.Width-3,control.Height-3));
    }
}
