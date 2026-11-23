namespace GestureFlow.Core;

public sealed class PalmBrightnessGesture
{
    (double X, double Y, TimeSpan Time)? origin;
    bool consumed;

    public void Reset()
    {
        origin = null;
        consumed = false;
    }

    public int Observe(CameraFrame frame, TimeSpan now)
    {
        if (frame.Hand is null || frame.Width <= 0 || frame.Height <= 0 ||
            !GestureClassifier.Valid(frame.Hand) || frame.Gesture?.Candidate != GestureKind.OpenPalm)
        {
            Reset();
            return 0;
        }
        if (consumed || frame.Gesture.Stable != GestureKind.OpenPalm) return 0;

        var wrist = frame.Hand.Points[0];
        double x = (double)wrist.X / frame.Width;
        double y = (double)wrist.Y / frame.Height;
        if (origin is not { } start || now < start.Time ||
            now - start.Time > TimeSpan.FromMilliseconds(850) || Math.Abs(x - start.X) > .1)
        {
            origin = (x, y, now);
            return 0;
        }
        double vertical = y - start.Y;
        if (now - start.Time < TimeSpan.FromMilliseconds(100) || Math.Abs(vertical) < .14)
            return 0;

        consumed = true;
        return vertical < 0 ? 10 : -10;
    }
}