namespace GestureFlow.Core;

/// <summary>One click per dwell. Initial activation, loss and a fired click require new movement.</summary>
public sealed class DwellClickProcessor
{
    bool enabled, waiting = true;
    (double X,double Y)? anchor;
    TimeSpan? last, since;
    int samples;
    public double Progress { get; private set; }
    public bool WaitingForMovement => waiting;
    public void Configure(bool value) { enabled=value; Reset(); }
    public void Reset() { anchor=null;last=null;since=null;samples=0;waiting=true;Progress=0; }
    public bool Observe(double x,double y,TimeSpan now,double radius=20)
    {
        if(!enabled) return false;
        if(!double.IsFinite(x)||!double.IsFinite(y)||!double.IsFinite(radius)||radius<=0) { Reset();return false; }
        if(last.HasValue && (now<=last.Value || now-last.Value>TimeSpan.FromMilliseconds(300))) Reset();
        last=now;
        if(anchor==null) { anchor=(x,y);return false; }
        var dx=x-anchor.Value.X;var dy=y-anchor.Value.Y;
        var distance=Math.Sqrt(dx*dx+dy*dy);
        if(waiting) {
            if(distance<=radius*2) return false;
            waiting=false;anchor=(x,y);since=now;samples=1;return false;
        }
        if(distance>radius) { anchor=(x,y);since=now;samples=1;Progress=0;return false; }
        samples++;
        Progress=Math.Clamp((now-since!.Value).TotalMilliseconds/900,0,1);
        if(Progress<1 || samples<3)return false;
        waiting=true;anchor=(x,y);since=null;samples=0;Progress=0;return true;
    }
}
