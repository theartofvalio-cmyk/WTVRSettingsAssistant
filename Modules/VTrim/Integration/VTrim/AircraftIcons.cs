using System.Drawing;
using System.Drawing.Drawing2D;

namespace HOTASTrimUtility;

internal static class AircraftIcons
{
    public static void Draw(Graphics g, Rectangle bounds, string type, Color ink)
    {
        GraphicsState state = g.Save();
        float scale = Math.Min(bounds.Width, bounds.Height) / 100f;
        g.TranslateTransform(bounds.X + (bounds.Width - 100 * scale) / 2,
            bounds.Y + (bounds.Height - 100 * scale) / 2);
        g.ScaleTransform(scale, scale);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using Pen outline = new(ink, 2.6f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
        using Pen gold = new(Theme.Accent, 3f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        using Brush metal = new SolidBrush(Color.FromArgb(65, ink));
        using Brush glass = new SolidBrush(Color.FromArgb(130, 168, 207));
        void Shape(PointF[] points)
        { g.FillPolygon(metal, points); g.DrawPolygon(outline, points); }
        if (type == "Helicopter")
        {
            // Side silhouette: glazed nose, turbine pod, long boom and skids.
            using var cabin = new GraphicsPath();
            cabin.AddBezier(17, 66, 5, 65, 10, 46, 23, 42);
            cabin.AddBezier(23, 42, 33, 38, 49, 43, 52, 52);
            cabin.AddLine(52, 52, 82, 42); cabin.AddLine(82, 42, 88, 45);
            cabin.AddLine(88, 45, 51, 62);
            cabin.AddBezier(51, 62, 44, 70, 28, 70, 17, 66); cabin.CloseFigure();
            g.FillPath(metal, cabin); g.DrawPath(outline, cabin);
            g.FillPolygon(glass, new PointF[] { new(17, 52), new(23, 45), new(29, 45), new(29, 57), new(14, 57) });
            g.DrawLine(outline, 32, 45, 32, 66); g.DrawLine(outline, 33, 39, 33, 29);
            g.DrawEllipse(gold, 5, 25, 66, 6); g.DrawLine(gold, 7, 28, 70, 28);
            g.DrawLine(outline, 22, 70, 20, 77); g.DrawLine(outline, 43, 68, 46, 77);
            g.DrawLines(outline, new PointF[] { new(12, 73), new(14, 78), new(55, 78) });
            g.DrawEllipse(outline, 82, 32, 13, 13);
            g.DrawLine(gold, 85, 35, 92, 42); g.DrawLine(gold, 92, 35, 85, 42);
        }
        else if (type == "Jet Plane")
        {
            Shape(new PointF[] { new(50, 6), new(56, 20), new(59, 39), new(91, 68), new(91, 74),
                new(60, 62), new(59, 76), new(73, 86), new(73, 92), new(54, 87), new(46, 87),
                new(27, 92), new(27, 86), new(41, 76), new(40, 62), new(9, 74), new(9, 68), new(41, 39), new(44, 20) });
            g.FillEllipse(glass, 46, 24, 8, 21); g.DrawEllipse(outline, 46, 24, 8, 21);
            g.DrawLine(outline, 45, 52, 45, 80); g.DrawLine(outline, 55, 52, 55, 80);
            g.DrawLine(gold, 18, 67, 36, 61); g.DrawLine(gold, 64, 61, 82, 67);
            g.DrawLine(gold, 45, 89, 45, 94); g.DrawLine(gold, 55, 89, 55, 94);
        }
        else
        {
            Shape(new PointF[] { new(45, 34), new(15, 43), new(9, 50), new(9, 59), new(44, 54),
                new(46, 75), new(30, 80), new(30, 88), new(50, 84), new(70, 88), new(70, 80),
                new(54, 75), new(56, 54), new(91, 59), new(91, 50), new(85, 43), new(55, 34), new(54, 20), new(50, 13), new(46, 20) });
            g.FillEllipse(glass, 46, 38, 8, 16); g.DrawEllipse(outline, 46, 38, 8, 16);
            g.DrawLine(outline, 50, 59, 50, 79);
            g.DrawLine(gold, 29, 17, 71, 17); g.DrawEllipse(outline, 47, 14, 6, 6);
            g.DrawLine(gold, 18, 51, 34, 48); g.DrawLine(gold, 66, 48, 82, 51);
        }
        g.Restore(state);
    }
}
