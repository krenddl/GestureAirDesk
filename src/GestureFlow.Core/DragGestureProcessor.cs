namespace GestureFlow.Core;

public enum DragTransition { None, Press, Release }
public sealed class DragGestureProcessor
{
    bool held, armed;
    public DragTransition Observe(bool enabled, GestureSnapshot? gesture)
    {
        var pose = gesture?.Stable ?? GestureKind.NoHand;
        if (!enabled) { armed = false; if (held) { held = false; return DragTransition.Release; } return DragTransition.None; }
        if (pose != GestureKind.Pinch)
        {
            if (pose != GestureKind.Unknown) armed = true;
            if (held) { held = false; return DragTransition.Release; }
            return DragTransition.None;
        }
        if (!held && armed) { held = true; armed = false; return DragTransition.Press; }
        return DragTransition.None;
    }
}

