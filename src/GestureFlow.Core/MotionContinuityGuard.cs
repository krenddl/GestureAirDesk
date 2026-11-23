namespace GestureFlow.Core;

/// <summary>Rejects discontinuous control observations; does not identify a person or hand.</summary>
public sealed class MotionContinuityGuard
{
    HandPoint? center;
    double width;
    TimeSpan? last;
    public void Reset() { center=null;last=null;width=0; }
    public bool Observe(HandObservation? hand,TimeSpan now)
    {
        if(hand==null||!GestureClassifier.Valid(hand)) { Reset();return false; }
        var p=hand.Points;
        var size=GestureClassifier.Distance(p[5],p[17]);
        if(size<1e-4) { Reset();return false; }
        var next=new HandPoint((p[0].X+p[5].X+p[17].X)/3,(p[0].Y+p[5].Y+p[17].Y)/3,(p[0].Z+p[5].Z+p[17].Z)/3);
        bool valid=last==null || (now>last.Value && now-last.Value<=TimeSpan.FromMilliseconds(300) &&
            GestureClassifier.Distance(center!,next)<=Math.Min(width,size)*1.5);
        if(!valid) { Reset();return false; }
        center=next;width=size;last=now;return true;
    }
}
