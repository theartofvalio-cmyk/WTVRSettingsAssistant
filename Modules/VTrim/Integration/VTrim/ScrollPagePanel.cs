namespace HOTASTrimUtility;

internal sealed class ScrollPagePanel : Panel
{
    public ScrollPagePanel()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint, true);
    }

    protected override void OnScroll(ScrollEventArgs e)
    {
        base.OnScroll(e);
        // Scrolling copies existing pixels; nested owner-drawn controls must
        // repaint too, otherwise old headers remain over the newly exposed rows.
        Invalidate(true);
        Update();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        Invalidate(true);
        Update();
    }
}
