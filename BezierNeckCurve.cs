using System.ComponentModel;

namespace WTVRSettingsAssistant;

internal sealed class BezierNeckCurve
{
    public List<NeckCurvePoint> Knots { get; set; } = new();
    // Coordinates are local to each segment, so slider changes preserve shape.
    public List<NeckCurvePoint> Handles { get; set; } = new()
    {
        new() { Input = 0.33f, Output = 0.33f }, new() { Input = 0.67f, Output = 0.67f },
        new() { Input = 0.33f, Output = 0.10f }, new() { Input = 0.67f, Output = 0.90f }
    };

    public void Validate()
    {
        Knots ??= new();
        Knots = Knots.Where(p => float.IsFinite(p.Input) && float.IsFinite(p.Output) && p.Input > 0 && p.Input < 1).OrderBy(p => p.Input).ToList();
        float previous = 0;
        foreach (var p in Knots) { p.Output = Math.Clamp(p.Output, previous, 1); previous = p.Output; }
        if (Handles == null || Handles.Count != 4) Handles = new BezierNeckCurve().Handles;
        for (int i = 0; i < 4; i++)
        {
            Handles[i].Input = float.IsFinite(Handles[i].Input) ? Math.Clamp(Handles[i].Input, 0.01f, 0.99f) : (i % 2 == 0 ? 0.33f : 0.67f);
            Handles[i].Output = float.IsFinite(Handles[i].Output) ? Math.Clamp(Handles[i].Output, 0, 1) : Handles[i].Input;
        }
        for (int i = 0; i < 4; i += 2)
        {
            Handles[i + 1].Input = Math.Max(Handles[i].Input, Handles[i + 1].Input);
            Handles[i + 1].Output = Math.Max(Handles[i].Output, Handles[i + 1].Output);
        }
    }

    public float Evaluate(float physical, int pivot, int maximum, int limit, int strength, bool naturalRearView = false, int naturalResumeAngle = 0)
    {
        float angle = Math.Abs(physical);
        if (angle <= pivot) return physical;
        float span = naturalRearView ? Math.Max(3, naturalResumeAngle - pivot) : Math.Max(1, limit - pivot);
        float t = Math.Clamp((angle - pivot) / span, 0, 1);
        float smooth = t * t * (3 - 2 * t);
        float blend = t + (smooth - t) * Math.Clamp(strength / 100f, 0, 1);
        return Math.Sign(physical) * (angle + Math.Max(0, maximum - limit) * blend);
    }

    private float EvaluateLegacyBezier(float physical, int pivot, int maximum, int limit, int strength)
    {
        if (!float.IsFinite(physical)) return 0;
        float angle = Math.Abs(physical);
        if (angle >= limit) return Math.Sign(physical) * maximum;
        pivot = Math.Clamp(pivot, 1, limit - 1);
        int segment = angle <= pivot ? 0 : 2;
        float originX = segment == 0 ? 0 : pivot;
        float spanX = segment == 0 ? pivot : limit - pivot;
        float originY = segment == 0 ? 0 : pivot;
        float spanY = segment == 0 ? pivot : maximum - pivot;
        float wantedX = (angle - originX) / spanX;
        float lo = 0, hi = 1;
        for (int i = 0; i < 22; i++)
        {
            float mid = (lo + hi) * 0.5f;
            if (Cubic(mid, Handles[segment].Input, Handles[segment + 1].Input) < wantedX) lo = mid; else hi = mid;
        }
        float t = (lo + hi) * 0.5f;
        float weight = Math.Clamp(strength / 100f, 0, 1);
        float a = Handles[segment].Input + (Handles[segment].Output - Handles[segment].Input) * weight;
        float b = Handles[segment + 1].Input + (Handles[segment + 1].Output - Handles[segment + 1].Input) * weight;
        return Math.Sign(physical) * (originY + spanY * Cubic(t, a, b));
    }

    private static float Cubic(float t, float a, float b) => 3 * (1 - t) * (1 - t) * t * a + 3 * (1 - t) * t * t * b + t * t * t;
}

internal sealed class NeckCurvePreview : Control
{
    public event EventHandler? MappingChanged;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public BezierNeckCurve Bezier { get; set; } = new();
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public string AxisTitle { get; set; } = "HORIZONTAL · LEFT / RIGHT";
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public int PhysicalLimit { get; set; } = 110;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public int ViewLimit { get; set; } = 220;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public int StartAngle { get; set; } = 45;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public int ReturnAngle { get; set; } = 35;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public int MaximumViewAngle { get; set; } = 180;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public int Curvature { get; set; } = 100;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public bool NaturalRearView { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public int NaturalResumeAngle { get; set; } = 75;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public float PhysicalYaw { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public float VirtualYaw { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public bool HasTelemetry { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public bool AssistanceOn { get; set; }
    // Compatibility with saved settings from the earlier point editor.
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public List<NeckCurvePoint> Points { get; set; } = new();
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public int TransitionWidth { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)] public bool UseSmoothstep { get; set; }
    private int _drag;
    private PointF KnotPosition(NeckCurvePoint p)
    {
        float x = StartAngle + p.Input * (PhysicalLimit - StartAngle);
        return ScreenPoint(x, x + Math.Max(0, MaximumViewAngle - PhysicalLimit) * p.Output);
    }
    private RectangleF Plot => new(42, 46, Math.Max(40, Width - 65), Math.Max(40, Height - 100));
    public NeckCurvePreview() { DoubleBuffered = true; Cursor = Cursors.Hand; SetStyle(ControlStyles.ResizeRedraw, true); }
    private PointF ScreenPoint(float x, float y) => new(Plot.Left + (x / PhysicalLimit + 1) * Plot.Width / 2, Plot.Bottom - (y / ViewLimit + 1) * Plot.Height / 2);
    private PointF HandlePoint(int i)
    {
        NeckCurvePoint p = Bezier.Handles[i];
        float start = i < 2 ? 0 : StartAngle;
        return ScreenPoint(start + p.Input * (i < 2 ? StartAngle : PhysicalLimit - StartAngle), start + p.Output * (i < 2 ? StartAngle : MaximumViewAngle - StartAngle));
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using Pen grid = new(Color.FromArgb(55, 78, 84));
        using Pen yellow = new(Color.FromArgb(255, 190, 70), 1.5f);
        using Pen green = new(Enabled ? Color.FromArgb(75, 235, 125) : Color.Gray, 2);
        using Pen red = new(Color.FromArgb(255, 80, 80), 1.5f);
        using Brush text = new SolidBrush(Enabled ? ForeColor : Color.Gray);
        using Brush amber = new SolidBrush(Color.FromArgb(255, 190, 70));
        using Brush greenBrush = new SolidBrush(green.Color);
        g.DrawString(AxisTitle, Font, text, 8, 5);
        string state = !Enabled || !AssistanceOn ? "OFF" : !HasTelemetry ? "WAITING FOR HEADSET" : Math.Abs(PhysicalYaw) >= StartAngle ? "ASSISTANCE ACTIVE" : "BELOW ACTIVATION ANGLE";
        g.DrawString(state, Font, text, Math.Max(240, Width - 240), 5);
        g.DrawString("Virtual view ↑", Font, text, 8, 25);
        g.DrawRectangle(grid, Plot.X, Plot.Y, Plot.Width, Plot.Height);
        PointF centre = ScreenPoint(0, 0);
        g.DrawLine(grid, centre.X, Plot.Top, centre.X, Plot.Bottom);
        g.DrawLine(grid, Plot.Left, centre.Y, Plot.Right, centre.Y);
        PointF previous = ScreenPoint(-PhysicalLimit, -MaximumViewAngle);
        for (int step = 1; step <= 400; step++)
        {
            float x = -PhysicalLimit + 2f * PhysicalLimit * step / 400;
            PointF p = ScreenPoint(x, Bezier.Evaluate(x, StartAngle, MaximumViewAngle, PhysicalLimit, Curvature, NaturalRearView, NaturalResumeAngle));
            g.DrawLine(green, previous, p); previous = p;
        }
        PointF pivot = ScreenPoint(StartAngle, StartAngle), end = ScreenPoint(PhysicalLimit, MaximumViewAngle);
        yellow.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
        g.DrawLine(yellow, pivot.X, Plot.Top, pivot.X, Plot.Bottom);
        PointF opposite = ScreenPoint(-StartAngle, -StartAngle);
        g.DrawLine(yellow, opposite.X, Plot.Top, opposite.X, Plot.Bottom);
        using Pen returnPen = new(Color.FromArgb(255, 145, 45), 1.5f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot };
        PointF release = ScreenPoint(ReturnAngle, ReturnAngle), releaseOpposite = ScreenPoint(-ReturnAngle, -ReturnAngle);
        g.DrawLine(returnPen, release.X, Plot.Top, release.X, Plot.Bottom);
        g.DrawLine(returnPen, releaseOpposite.X, Plot.Top, releaseOpposite.X, Plot.Bottom);
        g.FillEllipse(amber, pivot.X - 6, pivot.Y - 6, 12, 12);
        g.FillEllipse(Brushes.DarkOrange, release.X - 5, release.Y - 5, 10, 10);
        g.FillEllipse(greenBrush, end.X - 6, end.Y - 6, 12, 12);
        if (NaturalRearView)
        {
            using Pen cyan = new(Color.FromArgb(70, 220, 240), 1.7f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
            PointF resume = ScreenPoint(NaturalResumeAngle, Bezier.Evaluate(NaturalResumeAngle, StartAngle, MaximumViewAngle, PhysicalLimit, Curvature, true, NaturalResumeAngle));
            PointF resumeOpposite = ScreenPoint(-NaturalResumeAngle, -Math.Abs(resume.Y));
            g.DrawLine(cyan, resume.X, Plot.Top, resume.X, Plot.Bottom);
            g.DrawLine(cyan, ScreenPoint(-NaturalResumeAngle, 0).X, Plot.Top, ScreenPoint(-NaturalResumeAngle, 0).X, Plot.Bottom);
            using Brush cyanBrush = new SolidBrush(cyan.Color);
            g.FillEllipse(cyanBrush, resume.X - 6, resume.Y - 6, 12, 12);
        }
        if (HasTelemetry && Enabled)
        {
            PointF raw = ScreenPoint(Math.Clamp(PhysicalYaw, -PhysicalLimit, PhysicalLimit), Math.Clamp(PhysicalYaw, -ViewLimit, ViewLimit));
            PointF adjusted = ScreenPoint(Math.Clamp(PhysicalYaw, -PhysicalLimit, PhysicalLimit), Bezier.Evaluate(PhysicalYaw, StartAngle, MaximumViewAngle, PhysicalLimit, Curvature, NaturalRearView, NaturalResumeAngle));
            g.DrawLine(red, raw.X, Plot.Top, raw.X, Plot.Bottom);
            g.DrawLine(red, Plot.Left, raw.Y, raw.X, raw.Y);
            g.DrawLine(green, adjusted.X, adjusted.Y, Plot.Right, adjusted.Y);
            using Brush headBrush = new SolidBrush(red.Color);
            g.FillEllipse(headBrush, raw.X - 5, raw.Y - 5, 10, 10);
            g.FillEllipse(greenBrush, adjusted.X - 5, adjusted.Y - 5, 10, 10);
        }
        g.DrawString($"−{PhysicalLimit}°", Font, text, Plot.Left, Plot.Bottom + 3);
        g.DrawString("0°", Font, text, centre.X, Plot.Bottom + 3);
        g.DrawString($"{PhysicalLimit}° head", Font, text, Plot.Right - 70, Plot.Bottom + 3);
        string caption = !Enabled ? "Enable up/down assistance to edit this curve" : HasTelemetry ? $"Red head: {PhysicalYaw:0}°   Applied view: {VirtualYaw:0}°" : "Drag yellow = activate · orange = release · green = maximum view";
        g.DrawString(caption, Font, text, 8, Height - 24);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (!Enabled || e.Button != MouseButtons.Left) return;
        float DistanceX(PointF p) => Math.Abs(e.X - p.X);
        PointF start = ScreenPoint(StartAngle, StartAngle), release = ScreenPoint(ReturnAngle, ReturnAngle), end = ScreenPoint(PhysicalLimit, MaximumViewAngle), resume = ScreenPoint(NaturalResumeAngle, 0);
        float[] distances = { DistanceX(start), DistanceX(release), Math.Abs(e.Y - end.Y), NaturalRearView ? DistanceX(resume) : float.MaxValue };
        int nearest = Array.IndexOf(distances, distances.Min());
        if (distances[nearest] <= 22) { _drag = nearest + 1; Capture = true; }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_drag == 0) return;
        float x = Math.Clamp((e.X - Plot.Left) / Plot.Width * 2 - 1, 0, 1) * PhysicalLimit;
        float y = Math.Clamp((Plot.Bottom - e.Y) / Plot.Height * 2 - 1, 0, 1) * ViewLimit;
        if (_drag == 1) StartAngle = Math.Clamp((int)Math.Round(x), Math.Max(5, ReturnAngle + 2), PhysicalLimit - 5);
        else if (_drag == 2) ReturnAngle = Math.Clamp((int)Math.Round(x), 0, Math.Max(0, StartAngle - 2));
        else if (_drag == 3) MaximumViewAngle = Math.Clamp((int)Math.Round(y), PhysicalLimit, ViewLimit);
        else NaturalResumeAngle = Math.Clamp((int)Math.Round(x), StartAngle + 3, PhysicalLimit);
        MappingChanged?.Invoke(this, EventArgs.Empty); Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e) { _drag = 0; Capture = false; }
}
