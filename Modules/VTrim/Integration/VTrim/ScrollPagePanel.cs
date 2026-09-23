namespace HOTASTrimUtility;

internal sealed class ScrollPagePanel : Panel
{
    public ScrollPagePanel()
    {
        AutoScroll = true;
        DoubleBuffered = true;
        HorizontalScroll.Enabled = false;
        HorizontalScroll.Visible = false;
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        UpdateStyles();
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);

        // Vertical scrolling is allowed, horizontal scrolling never is. A stale
        // AutoScrollMinSize width can make WinForms create a sideways scrollbar
        // after DPI/responsive relayout, so always clear the width component.
        HorizontalScroll.Enabled = false;
        HorizontalScroll.Visible = false;
        if (AutoScrollMinSize.Width != 0)
            AutoScrollMinSize = new Size(0, AutoScrollMinSize.Height);

        foreach (Control child in Controls)
        {
            if (child.Dock != DockStyle.Top) continue;
            int width = Math.Max(1, ClientSize.Width - Padding.Horizontal - SystemInformation.VerticalScrollBarWidth - 2);
            if (child.Width != width) child.Width = width;
        }
    }

    protected override void OnScroll(ScrollEventArgs e)
    {
        base.OnScroll(e);
        // Do not call Update() here. Forcing synchronous paints on every scroll
        // tick was the cause of disappearing/reappearing child controls. Let the
        // normal double-buffered paint cycle coalesce the invalidations.
        Invalidate(false);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        // Native sliders/trackbars retain mouse capture while they are being
        // dragged. Scrolling their parent at the same time makes WinForms move
        // child HWNDs while they are mid-paint, which can leave stretched frames.
        if (HasCapturedDescendant(this))
        {
            if (e is HandledMouseEventArgs handled) handled.Handled = true;
            return;
        }
        base.OnMouseWheel(e);
        Invalidate(false);
    }

    private static bool HasCapturedDescendant(Control root)
    {
        foreach (Control child in root.Controls)
        {
            if (child.Capture || HasCapturedDescendant(child)) return true;
        }
        return false;
    }
}
