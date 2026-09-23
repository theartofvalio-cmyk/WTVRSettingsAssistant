using System.Drawing;
using System.Drawing.Drawing2D;
using System.ComponentModel;
using System.Windows.Forms;

namespace HOTASTrimUtility;

public partial class Form1
{
    private AxisResponse _rollResponse = new();
    private AxisResponse _pitchResponse = new();
    private AxisResponse _rudderResponse = new();
    private bool _refreshingCurveEditors;
    private readonly Dictionary<AxisTarget, CurveEditor> _curveEditors = new();

    private sealed record CurveEditor(NumericUpDown Curve, NumericUpDown Deadzone,
        NumericUpDown InputRange, NumericUpDown OutputRange, AxisCurvePreview Preview);

    private AxisResponse ResponseFor(AxisTarget axis) => axis switch
    {
        AxisTarget.Roll => _rollResponse,
        AxisTarget.Pitch => _pitchResponse,
        _ => _rudderResponse
    };

    private void BuildCurvesTab(Control parent)
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top, ColumnCount = 1, RowCount = 4,
            BackColor = Theme.Background, Margin = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        for (int i = 0; i < 3; i++) layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / 3));
        var heading = new Label
        {
            Dock = DockStyle.Fill, Text = VT("Curves.Heading"), Tag = "i18n:Curves.Heading",
            ForeColor = Theme.Text, Font = new Font("Segoe UI", 10), UseMnemonic = false,
            TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(8, 0, 8, 8)
        };
        layout.Controls.Add(heading, 0, 0);
        int row = 1;
        foreach (AxisTarget axis in Enum.GetValues<AxisTarget>())
            layout.Controls.Add(CreateCurveCard(axis), 0, row++);
        RegisterResponsiveScrollSurface(parent, layout, 1080);
        RefreshCurveEditors();
    }

    private Control CreateCurveCard(AxisTarget axis)
    {
        Panel card = CreateSectionPanel(Point.Empty, Size.Empty);
        card.Dock = DockStyle.Fill; card.Margin = new Padding(0, 0, 0, 8);
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3,
            BackColor = Theme.Panel, Padding = new Padding(12), Margin = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        // Keep the editing instructions OUTSIDE the graph. Earlier builds drew
        // the sentence on top of the preview and the graph border clipped it.
        // A dedicated, wrapping row also gives longer translations room.
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        string axisKey = axis switch
        {
            AxisTarget.Roll => "Curves.Roll",
            AxisTarget.Pitch => "Curves.Pitch",
            _ => "Curves.RudderYaw"
        };
        layout.Controls.Add(I18n(new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = Theme.Text, Font = new Font("Segoe UI Semibold", 11), UseMnemonic = false
        }, axisKey), 0, 0);
        var helpLabel = I18n(new Label
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(14, 0, 16, 4),
            ForeColor = Theme.Muted,
            BackColor = Theme.Inner,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = false,
            UseMnemonic = false,
            Font = new Font("Segoe UI", 9F),
            Padding = new Padding(8, 0, 8, 0)
        }, "Curves.EditorHelp");
        layout.Controls.Add(helpLabel, 0, 1);

        var preview = new AxisCurvePreview
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 16, 6),
            MinimumSize = new Size(220, 150),
            InputLabel = VT("Curves.Input"),
            OutputLabel = VT("Curves.Output"),
            EditorHelpText = VT("Curves.EditorHelp"),
            Editable = true,
            ShowEditorHelp = false
        };
        layout.Controls.Add(preview, 0, 2);
        var controls = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 5, BackColor = Theme.Panel,
            Margin = Padding.Empty
        };
        controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64));
        controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
        for (int i = 0; i < 5; i++) controls.RowStyles.Add(new RowStyle(SizeType.Percent, 20));
        NumericUpDown Add(int fieldRow, string key, decimal min, decimal max, decimal value, bool decimals = false)
        {
            controls.Controls.Add(I18n(new Label
            {
                Dock = DockStyle.Fill, ForeColor = Theme.Muted,
                TextAlign = ContentAlignment.MiddleLeft, UseMnemonic = false
            }, key), 0, fieldRow);
            var number = new NumericUpDown
            {
                Minimum = min, Maximum = max, Value = value,
                DecimalPlaces = decimals ? 1 : 0, Increment = decimals ? 0.1m : 1m,
                Dock = DockStyle.Fill, Margin = new Padding(2, 3, 2, 3),
                BackColor = Theme.Control, ForeColor = Theme.Text, TextAlign = HorizontalAlignment.Right
            };
            controls.Controls.Add(number, 1, fieldRow);
            return number;
        }
        var editor = new CurveEditor(Add(0, "Curves.Curve", -100, 100, 0),
            Add(1, "Curves.CenterDeadzone", 0, 30, 1.8m, true),
            Add(2, "Curves.InputRange", 40, 100, 100),
            Add(3, "Curves.OutputRange", 5, 100, 100), preview);
        _curveEditors[axis] = editor;
        Button reset = I18n(CreateSecondaryButton(VT("Curves.ResetAxis")), "Curves.ResetAxis");
        reset.Dock = DockStyle.Fill; reset.Margin = new Padding(0, 4, 0, 0);
        reset.Click += (_, _) =>
        {
            if (axis == AxisTarget.Roll) _rollResponse = new();
            else if (axis == AxisTarget.Pitch) _pitchResponse = new();
            else _rudderResponse = new();
            _storeTrimReturn.Cancel(); ResetAutomaticInstructorHold();
            RefreshCurveEditors(); SaveBindings();
        };
        controls.Controls.Add(reset, 0, 4); controls.SetColumnSpan(reset, 2);

        void ScalarChanged(bool curveChanged)
        {
            if (_refreshingCurveEditors) return;
            AxisResponse response = ResponseFor(axis);
            if (curveChanged)
            {
                response.Curve = (int)editor.Curve.Value;
                response.ClearCustomCurve();
            }
            response.Deadzone = (double)editor.Deadzone.Value;
            response.InputRange = (double)editor.InputRange.Value;
            response.OutputRange = (double)editor.OutputRange.Value;
            response.Normalize();
            preview.SetResponse(response);
            ResetAutomaticInstructorHold();
            SaveBindings();
        }
        editor.Curve.ValueChanged += (_, _) => ScalarChanged(true);
        editor.Deadzone.ValueChanged += (_, _) => ScalarChanged(false);
        editor.InputRange.ValueChanged += (_, _) => ScalarChanged(false);
        editor.OutputRange.ValueChanged += (_, _) => ScalarChanged(false);
        preview.ResponseEdited += (_, _) =>
        {
            if (_refreshingCurveEditors) return;
            AxisResponse response = ResponseFor(axis);
            response.Normalize();
            if (response.HasCustomCurve) response.Curve = response.EstimateEquivalentCurve();
            _refreshingCurveEditors = true;
            try
            {
                // Numeric fields follow the mouse live, including the equivalent
                // scalar Curve value. Disk persistence is still deferred to MouseUp.
                editor.Curve.Value = response.Curve;
                editor.Deadzone.Value = (decimal)response.Deadzone;
                editor.InputRange.Value = (decimal)response.InputRange;
                editor.OutputRange.Value = (decimal)response.OutputRange;
            }
            finally { _refreshingCurveEditors = false; }
            ResetAutomaticInstructorHold();
        };
        preview.ResponseEditCompleted += (_, _) =>
        {
            if (_refreshingCurveEditors) return;
            AxisResponse response = ResponseFor(axis);
            if (response.HasCustomCurve) response.Curve = response.EstimateEquivalentCurve();
            response.Normalize();
            _refreshingCurveEditors = true;
            try
            {
                editor.Curve.Value = response.Curve;
                editor.Deadzone.Value = (decimal)response.Deadzone;
                editor.InputRange.Value = (decimal)response.InputRange;
                editor.OutputRange.Value = (decimal)response.OutputRange;
            }
            finally { _refreshingCurveEditors = false; }
            ResetAutomaticInstructorHold();
            SaveBindings();
        };
        _toolTip.SetToolTip(editor.Curve, VT("Curves.CurveTooltip"));
        _toolTip.SetToolTip(editor.OutputRange, VT("Curves.OutputTooltip"));
        _toolTip.SetToolTip(preview, VT("Curves.PreviewTooltip"));
        layout.Controls.Add(controls, 1, 0); layout.SetRowSpan(controls, 3);
        card.Controls.Add(layout);
        return card;
    }

    private void RefreshCurveEditors()
    {
        _refreshingCurveEditors = true;
        try
        {
            foreach ((AxisTarget axis, CurveEditor editor) in _curveEditors)
            {
                AxisResponse response = ResponseFor(axis); response.Normalize();
                editor.Curve.Value = response.Curve;
                editor.Deadzone.Value = (decimal)response.Deadzone;
                editor.InputRange.Value = (decimal)response.InputRange;
                editor.OutputRange.Value = (decimal)response.OutputRange;
                editor.Preview.InputLabel = VT("Curves.Input");
                editor.Preview.OutputLabel = VT("Curves.Output");
                editor.Preview.EditorHelpText = VT("Curves.EditorHelp");
                editor.Preview.SetResponse(response);
            }
        }
        finally { _refreshingCurveEditors = false; }
    }

    private void UpdateCurvePreview(AxisTarget axis, double physical)
    {
        if (_curveEditors.TryGetValue(axis, out CurveEditor? editor))
            editor.Preview.SetInput(physical);
    }
}

internal sealed class AxisCurvePreview : Control
{
    private enum DragMode { None, Point, Deadzone, Range }

    private AxisResponse _response = new();
    private double _input;
    private DragMode _dragMode;
    private AxisCurvePoint? _dragPoint;
    private int _dragSide = 1;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string InputLabel { get; set; } = "Input";
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string OutputLabel { get; set; } = "Output";
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string EditorHelpText { get; set; } = "Drag points  •  click line to add  •  right-click point to remove";
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Editable { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowEditorHelp { get; set; } = true;
    public event EventHandler? ResponseEdited;
    public event EventHandler? ResponseEditCompleted;

    public AxisCurvePreview()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        BackColor = Theme.Inner;
        Cursor = Cursors.Cross;
        TabStop = true;
    }

    internal void SetResponse(AxisResponse response)
    {
        _response = response;
        _response.Normalize();
        Invalidate();
    }

    internal void SetInput(double input)
    {
        input = double.IsFinite(input) ? Math.Clamp(input, -1, 1) : 0;
        if (Math.Abs(input - _input) < 0.00001) return;
        _input = input; Invalidate();
    }

    private bool TryGraph(out float left, out float top, out float width, out float height)
    {
        const int readoutHeight = 38;
        left = 18;
        top = Editable && ShowEditorHelp ? 42 : 10;
        width = Width - 36;
        height = Math.Max(40, Height - top - readoutHeight - 14);
        return width > 10 && height > 10;
    }

    private PointF Map(double x, double y)
    {
        TryGraph(out float left, out float top, out float width, out float height);
        return new(left + (float)((x + 1) * 0.5 * width), top + (float)((1 - y) * 0.5 * height));
    }

    private (double X, double Y) Unmap(Point point)
    {
        TryGraph(out float left, out float top, out float width, out float height);
        double x = ((point.X - left) / Math.Max(1, width)) * 2 - 1;
        double y = 1 - ((point.Y - top) / Math.Max(1, height)) * 2;
        return (Math.Clamp(x, -1, 1), Math.Clamp(y, -1, 1));
    }

    private AxisCurvePoint? HitPoint(Point location, out int side)
    {
        side = 1;
        double deadzone = _response.Deadzone / 100.0;
        double range = _response.InputRange / 100.0;
        double output = _response.OutputRange / 100.0;
        foreach (AxisCurvePoint point in _response.CustomPoints)
        {
            double rawX = deadzone + point.X * Math.Max(0.001, range - deadzone);
            double rawY = point.Y * output;
            foreach (int sign in new[] { 1, -1 })
            {
                PointF mapped = Map(sign * rawX, sign * rawY);
                if (Distance(mapped, location) <= 34)
                {
                    side = sign;
                    return point;
                }
            }
        }
        return null;
    }

    private DragMode HitSpecialHandle(Point location, out int side)
    {
        side = 1;
        double dz = _response.Deadzone / 100.0;
        double range = _response.InputRange / 100.0;
        double output = _response.OutputRange / 100.0;
        foreach (int sign in new[] { 1, -1 })
        {
            if (Distance(Map(sign * dz, 0), location) <= 26) { side = sign; return DragMode.Deadzone; }
            if (Distance(Map(sign * range, sign * output), location) <= 28) { side = sign; return DragMode.Range; }
        }
        return DragMode.None;
    }

    private static double Distance(PointF a, Point b)
    {
        double dx = a.X - b.X, dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private void NotifyEdited(bool estimateCurve)
    {
        _response.Normalize();
        if (estimateCurve && _response.HasCustomCurve)
            _response.Curve = _response.EstimateEquivalentCurve();
        ResponseEdited?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (!Editable || e.Button is not (MouseButtons.Left or MouseButtons.Right)) return;
        Focus();
        _response.Normalize();

        AxisCurvePoint? point = HitPoint(e.Location, out int pointSide);
        if (e.Button == MouseButtons.Right)
        {
            if (point is not null)
            {
                _response.CustomPoints.Remove(point);
                NotifyEdited(true);
                ResponseEditCompleted?.Invoke(this, EventArgs.Empty);
            }
            return;
        }

        DragMode special = HitSpecialHandle(e.Location, out int specialSide);
        if (special != DragMode.None)
        {
            _dragMode = special;
            _dragSide = specialSide;
            Capture = true;
            return;
        }
        if (point is not null)
        {
            _dragMode = DragMode.Point;
            _dragPoint = point;
            _dragSide = pointSide;
            Capture = true;
            return;
        }

        (double rawX, double rawY) = Unmap(e.Location);
        double dz = _response.Deadzone / 100.0;
        double range = _response.InputRange / 100.0;
        double output = Math.Max(0.05, _response.OutputRange / 100.0);
        double absX = Math.Abs(rawX);
        if (absX <= dz + 0.005 || absX >= range - 0.005) return;
        double x = Math.Clamp((absX - dz) / Math.Max(0.001, range - dz), 0.015, 0.985);
        double y = Math.Clamp(Math.Abs(rawY) / output, 0, 1);
        var added = new AxisCurvePoint(x, y);
        _response.CustomPoints.Add(added);
        _response.Normalize();
        _dragPoint = _response.CustomPoints.OrderBy(p => Math.Abs(p.X - x) + Math.Abs(p.Y - y)).FirstOrDefault();
        _dragMode = DragMode.Point;
        _dragSide = rawX < 0 ? -1 : 1;
        Capture = true;
        NotifyEdited(false);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!Editable) return;
        // Once a handle has captured the mouse, keep dragging until MouseUp even
        // if WinForms reports MouseButtons.None during fast movement/scrolling.
        if (_dragMode == DragMode.None)
        {
            AxisCurvePoint? hover = HitPoint(e.Location, out _);
            DragMode specialHover = HitSpecialHandle(e.Location, out _);
            Cursor = hover is not null ? Cursors.SizeAll : specialHover != DragMode.None ? Cursors.Hand : Cursors.Cross;
            return;
        }
        (double rawX, double rawY) = Unmap(e.Location);
        double absX = Math.Abs(rawX);
        double absY = Math.Abs(rawY);

        if (_dragMode == DragMode.Deadzone)
        {
            _response.Deadzone = Math.Clamp(absX * 100.0, 0, Math.Min(30, _response.InputRange - 5));
            NotifyEdited(false);
            return;
        }
        if (_dragMode == DragMode.Range)
        {
            _response.InputRange = Math.Clamp(absX * 100.0, Math.Max(40, _response.Deadzone + 5), 100);
            _response.OutputRange = Math.Clamp(absY * 100.0, 5, 100);
            NotifyEdited(false);
            return;
        }
        if (_dragPoint is null) return;

        double dz = _response.Deadzone / 100.0;
        double range = _response.InputRange / 100.0;
        double output = Math.Max(0.05, _response.OutputRange / 100.0);
        double targetX = Math.Clamp((absX - dz) / Math.Max(0.001, range - dz), 0.015, 0.985);
        double targetY = Math.Clamp(absY / output, 0, 1);
        AxisCurvePoint dragged = _dragPoint;
        dragged.X = targetX;
        dragged.Y = targetY;
        _response.Normalize();
        // Keep the exact object under the mouse whenever normalization did not
        // intentionally merge it with a neighboring point. Re-selecting the
        // nearest point on every move could make the handle jump between two
        // close points and was another source of "tricky" dragging.
        _dragPoint = _response.CustomPoints.Contains(dragged)
            ? dragged
            : _response.CustomPoints.OrderBy(p => Math.Abs(p.X - targetX) + Math.Abs(p.Y - targetY)).FirstOrDefault();
        NotifyEdited(false);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        bool completedEdit = _dragMode != DragMode.None;
        bool completedPointEdit = _dragMode == DragMode.Point;
        _dragMode = DragMode.None;
        _dragPoint = null;
        Capture = false;
        Cursor = Cursors.Cross;
        if (completedPointEdit) NotifyEdited(true);
        if (completedEdit) ResponseEditCompleted?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        if (Capture || _dragMode == DragMode.None) return;
        bool completedPointEdit = _dragMode == DragMode.Point;
        _dragMode = DragMode.None;
        _dragPoint = null;
        if (completedPointEdit) NotifyEdited(true);
        ResponseEditCompleted?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (Width < 60 || Height < 60 || !TryGraph(out float left, out float top, out float w, out float h)) return;
        Graphics g = e.Graphics;
        g.Clear(BackColor); g.SmoothingMode = SmoothingMode.AntiAlias;
        using var grid = new Pen(Theme.Border, 1);
        using var reference = new Pen(Theme.Muted, 1) { DashStyle = DashStyle.Dash };
        using var curve = new Pen(Theme.AccentLight, 2.2f);
        using var dot = new SolidBrush(Theme.Text);
        g.DrawRectangle(grid, left, top, w, h);
        PointF a = Map(-1, 0), b = Map(1, 0), c = Map(0, -1), d = Map(0, 1);
        g.DrawLine(grid, a, b); g.DrawLine(grid, c, d);
        g.DrawLine(reference, Map(-1, -1), Map(1, 1));
        var points = new PointF[201];
        for (int i = 0; i < points.Length; i++)
        { double x = i / 100.0 - 1; points[i] = Map(x, _response.Apply(x)); }
        g.DrawLines(curve, points);

        if (Editable)
        {
            if (ShowEditorHelp)
            {
                string help = string.IsNullOrWhiteSpace(EditorHelpText)
                    ? "Drag points  •  click line to add  •  right-click point to remove"
                    : EditorHelpText;
                using Font helpFont = new("Segoe UI", Math.Clamp(Font.Size, 8.2F, 9.6F), FontStyle.Regular, GraphicsUnit.Point);
                Rectangle helpRect = new(18, 6, Math.Max(1, Width - 36), 28);
                TextRenderer.DrawText(g, help, helpFont, helpRect, Theme.Muted,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
            double dz = _response.Deadzone / 100.0;
            double range = _response.InputRange / 100.0;
            double output = _response.OutputRange / 100.0;
            using var pointFill = new SolidBrush(Theme.AccentLight);
            using var handleFill = new SolidBrush(Theme.Warning);
            using var edge = new Pen(Color.Black, 1);
            foreach (AxisCurvePoint point in _response.CustomPoints)
            {
                double rawX = dz + point.X * Math.Max(0.001, range - dz);
                double rawY = point.Y * output;
                foreach (int sign in new[] { 1, -1 })
                {
                    PointF hp = Map(sign * rawX, sign * rawY);
                    g.FillEllipse(pointFill, hp.X - 11, hp.Y - 11, 22, 22);
                    g.DrawEllipse(edge, hp.X - 11, hp.Y - 11, 22, 22);
                }
            }
            foreach (int sign in new[] { 1, -1 })
            {
                PointF dead = Map(sign * dz, 0);
                g.FillEllipse(handleFill, dead.X - 9, dead.Y - 9, 18, 18);
                PointF end = Map(sign * range, sign * output);
                g.FillRectangle(handleFill, end.X - 10, end.Y - 10, 20, 20);
                g.DrawRectangle(edge, end.X - 10, end.Y - 10, 20, 20);
            }
        }

        PointF p = Map(_input, _response.Apply(_input));
        g.FillEllipse(dot, p.X - 4, p.Y - 4, 8, 8);
        float readoutSize = Math.Clamp(Font.Size, 8.8F, 10.2F);
        string readout = $"{InputLabel} {_input * 100:+0.0;-0.0;0.0}%    {OutputLabel} {_response.Apply(_input) * 100:+0.0;-0.0;0.0}%";
        Font readoutFont = new("Segoe UI Semibold", readoutSize, FontStyle.Regular, GraphicsUnit.Point);
        while (readoutSize > 7.8F && TextRenderer.MeasureText(readout, readoutFont, Size.Empty, TextFormatFlags.NoPadding).Width > Math.Max(40, Width - 20))
        {
            readoutFont.Dispose();
            readoutSize -= .4F;
            readoutFont = new Font("Segoe UI Semibold", readoutSize, FontStyle.Regular, GraphicsUnit.Point);
        }
        using (readoutFont)
        {
        const int readoutHeight = 32;
        int readoutTop = Math.Min(Math.Max((int)Math.Round(top + h + 5), 0), Math.Max(0, Height - readoutHeight));
        Rectangle readoutRect = new(0, readoutTop, Width, readoutHeight);
        using (Brush readoutFill = new SolidBrush(Color.FromArgb(160, BackColor)))
        {
            Size textSize = TextRenderer.MeasureText(readout, readoutFont);
            Rectangle fillRect = new(
                Math.Max(0, Width / 2 - textSize.Width / 2 - 12),
                readoutRect.Top + Math.Max(0, (readoutRect.Height - textSize.Height) / 2) - 3,
                Math.Min(Width, textSize.Width + 24),
                Math.Min(readoutRect.Height, textSize.Height + 6));
            g.FillRectangle(readoutFill, fillRect);
        }
        TextRenderer.DrawText(g, readout, readoutFont, readoutRect, Theme.Muted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }
}
