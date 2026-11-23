namespace GestureFlow.Core;
public sealed class ScrollPoseGate
{
    TimeSpan? lastFist;
    public void Reset()=>lastFist=null;
    public bool Observe(bool enabled,GestureSnapshot? pose,TimeSpan now)
    {
        if(!enabled||pose==null||pose.Stable==GestureKind.NoHand) { Reset();return false; }
        if(pose.Stable==GestureKind.Fist) { lastFist=now;return true; }
        if(pose.Candidate is GestureKind.OpenPalm or GestureKind.Pinch or GestureKind.Victory or GestureKind.ThumbsUp or GestureKind.ThumbsDown or GestureKind.ThreeFingers or GestureKind.Shaka) { Reset();return false; }
        return lastFist.HasValue && now>=lastFist.Value && now-lastFist.Value<=TimeSpan.FromMilliseconds(150);
    }
}
