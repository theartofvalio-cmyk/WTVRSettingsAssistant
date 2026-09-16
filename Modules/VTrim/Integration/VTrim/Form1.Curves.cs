using System.Drawing;
using System.Drawing.Drawing2D;
using System.ComponentModel;
using System.Windows.Forms;

namespace HOTASTrimUtility;

public partial class Form1
{
    // Retains the uploaded 1.3.0 response model and serialized profile fields.
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
        RegisterResponsiveScrollSurface(parent, layout, 900);
        RefreshCurveEditors();
    }

    private Control CreateCurveCard(AxisTarget axis)
    {
        Panel card = CreateSectionPanel(Point.Empty, Size.Empty);
        card.Dock = DockStyle.Fill; card.Margin = new Padding(0, 0, 0, 8);
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2,
            BackColor = Theme.Panel, Padding = new Padding(12), Margin = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
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
        var preview = new AxisCurvePreview
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 16, 6),
            MinimumSize = new Size(220, 150),
            InputLabel = VT("Curves.Input"),
            OutputLabel = VT("Curves.Output")
        };
        layout.Controls.Add(preview, 0, 1);
        var controls = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 5, BackColor = Theme.Panel,
            Margin = Padding.Empty
        };
        controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64));
        controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
        for (int i = 0; i < 5; i++) controls.RowStyles.Add(new RowStyle(SizeType.Percent, 20));
        NumericUpDown Add(int row, string key, decimal min, decimal max, decimal value, bool decimals = false)
        {
            controls.Controls.Add(I18n(new Label
            {
                Dock = DockStyle.Fill, ForeColor = Theme.Muted,
                TextAlign = ContentAlignment.MiddleLeft, UseMnemonic = false
            }, key), 0, row);
            var number = new NumericUpDown
            {
                Minimum = min, Maximum = max, Value = value,
                DecimalPlaces = decimals ? 1 : 0, Increment = decimals ? 0.1m : 1m,
                Dock = DockStyle.Fill, Margin = new Padding(2, 3, 2, 3),
                BackColor = Theme.Control, ForeColor = Theme.Text, TextAlign = HorizontalAlignment.Right
            };
            controls.Controls.Add(number, 1, row);
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
        void Changed()
        {
            if (_refreshingCurveEditors) return;
            AxisResponse response = ResponseFor(axis);
            response.Curve = (int)editor.Curve.Value;
            response.Deadzone = (double)editor.Deadzone.Value;
            response.InputRange = (double)editor.InputRange.Value;
            response.OutputRange = (double)editor.OutputRange.Value;
            response.Normalize(); preview.SetResponse(response); ResetAutomaticInstructorHold(); SaveBindings();
        }
        foreach (NumericUpDown number in new[] { editor.Curve, editor.Deadzone, editor.InputRange, editor.OutputRange })
            number.ValueChanged += (_, _) => Changed();
        _toolTip.SetToolTip(editor.Curve, VT("Curves.CurveTooltip"));
        _toolTip.SetToolTip(editor.OutputRange, VT("Curves.OutputTooltip"));
        layout.Controls.Add(controls, 1, 0); layout.SetRowSpan(controls, 2);
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
    private AxisResponse _response = new();
    private double _input;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string InputLabel { get; set; } = "Input";
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string OutputLabel { get; set; } = "Output";
    public AxisCurvePreview()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Inner;
    }
    internal void SetResponse(AxisResponse response) { _response = response; Invalidate(); }
    internal void SetInput(double input)
    {
        input = double.IsFinite(input) ? Math.Clamp(input, -1, 1) : 0;
        if (Math.Abs(input - _input) < 0.00001) return;
        _input = input; Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (Width < 60 || Height < 60) return;
        Graphics g = e.Graphics;
        g.Clear(BackColor); g.SmoothingMode = SmoothingMode.AntiAlias;
        const int readoutHeight = 38;
        float left = 18;
        float top = 10;
        float w = Width - 36;
        float h = Math.Max(40, Height - top - readoutHeight - 14);
        PointF Map(double x, double y) => new(left + (float)((x + 1) * 0.5 * w), top + (float)((1 - y) * 0.5 * h));
        using var grid = new Pen(Theme.Border, 1);
        using var reference = new Pen(Theme.Muted, 1) { DashStyle = DashStyle.Dash };
        using var curve = new Pen(Theme.AccentLight, 2);
        using var dot = new SolidBrush(Theme.Text);
        g.DrawRectangle(grid, left, top, w, h);
        PointF a = Map(-1, 0), b = Map(1, 0), c = Map(0, -1), d = Map(0, 1);
        g.DrawLine(grid, a, b); g.DrawLine(grid, c, d);
        g.DrawLine(reference, Map(-1, -1), Map(1, 1));
        var points = new PointF[201];
        for (int i = 0; i < points.Length; i++)
        { double x = i / 100.0 - 1; points[i] = Map(x, _response.Apply(x)); }
        g.DrawLines(curve, points);
        PointF p = Map(_input, _response.Apply(_input));
        g.FillEllipse(dot, p.X - 4, p.Y - 4, 8, 8);
        using Font readoutFont = new("Segoe UI Semibold", Math.Clamp(Font.Size, 10F, 12F), FontStyle.Regular, GraphicsUnit.Point);
        int readoutTop = (int)Math.Round(top + h - readoutHeight - 4);
        Rectangle readoutRect = new(0, Math.Max((int)top + 4, readoutTop), Width, readoutHeight);
        using (Brush readoutFill = new SolidBrush(Color.FromArgb(160, BackColor)))
        {
            string readout = $"{InputLabel} {_input * 100:+0.0;-0.0;0.0}%    {OutputLabel} {_response.Apply(_input) * 100:+0.0;-0.0;0.0}%";
            Size textSize = TextRenderer.MeasureText(readout, readoutFont);
            Rectangle fillRect = new(
                Math.Max(0, Width / 2 - textSize.Width / 2 - 12),
                readoutRect.Top + Math.Max(0, (readoutRect.Height - textSize.Height) / 2) - 3,
                Math.Min(Width, textSize.Width + 24),
                Math.Min(readoutRect.Height, textSize.Height + 6));
            g.FillRectangle(readoutFill, fillRect);
        }
        TextRenderer.DrawText(g, $"{InputLabel} {_input * 100:+0.0;-0.0;0.0}%    {OutputLabel} {_response.Apply(_input) * 100:+0.0;-0.0;0.0}%",
            readoutFont, readoutRect, Theme.Muted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }
}

