namespace GestureFlow.Core;

/// <summary>Controls only the in-app demonstration panel; never performs OS actions.</summary>
public sealed class PalmMenuState
{
    public bool IsOpen { get; private set; }
    public void Open() => IsOpen = true;
    public void Close() => IsOpen = false;
    public void Apply(bool enabled, GestureSnapshot? gesture)
    {
        if (!enabled || gesture == null || gesture.Stable == GestureKind.NoHand) { Close(); return; }
        if (gesture.Stable == GestureKind.OpenPalm) Open();
        else if (gesture.Stable == GestureKind.Fist) Close();
    }
}

