namespace HOTASTrimUtility;

internal static class CompactAircraftStripLayout
{
    public const int Slots = 3;

    public static int CardWidth(int viewportWidth, int gap, int preferredWidth) =>
        Math.Min(preferredWidth, Math.Max(1, (viewportWidth - gap * (Slots - 1)) / Slots));

    public static int LeftOffset(int viewportWidth, int gap, int cardWidth, int cardCount)
    {
        int shown = Math.Clamp(cardCount, 0, Slots);
        int rowWidth = shown * cardWidth + Math.Max(0, shown - 1) * gap;
        return Math.Max(0, (viewportWidth - rowWidth) / 2);
    }
}
