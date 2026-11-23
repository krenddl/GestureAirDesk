namespace GestureFlow.Core;
public enum TrackingPreset { Balanced, Smooth, Fast }
public sealed record TrackingOptions(double SmoothingMs,double HoldMs)
{
    public static TrackingOptions For(TrackingPreset preset)=>preset switch {
        TrackingPreset.Balanced=>new(90,180),
        TrackingPreset.Smooth=>new(140,300),
        TrackingPreset.Fast=>new(50,120),
        _=>throw new ArgumentOutOfRangeException(nameof(preset))
    };
}

