namespace GestureFlow.Core;

public sealed class WheelGestureProcessor(double sensitivity=1500)
{
    double? previous;
    TimeSpan? last;
    double remainder;
    public void Reset() { previous = null; last = null; remainder = 0; }
    public int Observe(bool fist, double wristY, TimeSpan time)
    {
        if (!fist || !double.IsFinite(wristY) || wristY < 0 || wristY > 1) { Reset(); return 0; }
        if (previous == null || last == null || time <= last || time - last > TimeSpan.FromMilliseconds(300) ||
            Math.Abs(wristY - previous.Value) > .25)
        {
            previous = wristY; last = time; remainder = 0; return 0;
        }
        remainder += (previous.Value - wristY) * sensitivity;
        previous = wristY; last = time;
        int steps = Math.Clamp((int)(remainder / 120), -3, 3);
        remainder -= steps * 120;
        return steps * 120;
    }
}


