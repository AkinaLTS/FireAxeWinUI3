// P7: pure sizing policy around AppWindow.ResizeClient (physical pixels) and saved client DIPs.
using System;

namespace FireAxe;

public static class WindowGeometry
{
    public static (int Width, int Height) GetClientSize(double? width, double? height,
        double scale, int maxWidth, int maxHeight)
    {
        if (!double.IsFinite(scale) || scale <= 0) scale = 1;
        static double Valid(double? value, double fallback) =>
            value is { } size && double.IsFinite(size) && size > 0 ? size : fallback;
        // A corrupt/oversized setting must not overflow or put the resize handles off screen.
        int Size(double dip, double minimum, int maximum) => (int)Math.Clamp(
            Math.Round(Math.Max(minimum, dip) * scale), 1, Math.Max(1, maximum));
        return (Size(Valid(width, 1145), 640, maxWidth), Size(Valid(height, 700), 480, maxHeight));
    }
}
