namespace GestureFlow.Core;

public static class PointerProjection
{
    public static (double X, double Y)? Project(HandPoint point, int width, int height, bool mirror, double insetX = 0, double insetY = 0)
    {
        if (!double.IsFinite(insetX) || !double.IsFinite(insetY) || insetX < 0 || insetY < 0 || insetX > .4 || insetY > .4) return null;
        if (width <= 0 || height <= 0 || !float.IsFinite(point.X) || !float.IsFinite(point.Y)) return null;
        double x = Math.Clamp((point.X / width - insetX) / (1 - 2 * insetX), 0, 1), y = Math.Clamp((point.Y / height - insetY) / (1 - 2 * insetY), 0, 1);
        return (mirror ? 1 - x : x, y);
    }
    public static int Target(double x, double y) => (x >= .5 ? 1 : 0) + (y >= .5 ? 2 : 0);
}


