namespace GestureFlow.Core;

public sealed class NavigationAutomation
{
    readonly TimeSpan cooldown;
    public NavigationAutomation(TimeSpan? cooldown=null) {
        this.cooldown=cooldown??TimeSpan.FromSeconds(2);
        if(this.cooldown<TimeSpan.Zero)throw new ArgumentOutOfRangeException(nameof(cooldown));
    }
    GestureKind last = GestureKind.NoHand;
    TimeSpan? fired;
    bool enabled;
    public void Configure(bool value) { enabled = value; last = GestureKind.Unknown; fired = null; armed = false; }
    bool armed;
    public bool IsArmed => enabled && armed;
    // Releasing the configured gesture is required before arming, including after enabling.
    public bool Observe(GestureSnapshot? gesture, GestureKind trigger, TimeSpan now)
    {
        var current = gesture?.Stable ?? GestureKind.NoHand;
        if (!enabled) { last = current; return false; }
        if (current != trigger) { if (current != GestureKind.Unknown) armed = true; last = current; return false; }
        bool edge = last != trigger; last = current;
        if (!armed || !edge) return false;
        armed = false;
        if (fired.HasValue && now - fired.Value < cooldown) return false;
        fired = now; return true;
    }
}



