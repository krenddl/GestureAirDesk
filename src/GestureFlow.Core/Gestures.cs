namespace GestureFlow.Core;

public enum GestureKind { NoHand, Unknown, OpenPalm, Fist, Pinch, Victory, ThumbsUp, ThreeFingers, Shaka, ThumbsDown }
public sealed record GestureSnapshot(GestureKind Candidate, GestureKind Stable, double HoldProgress, long Confirmations);
public sealed record ProcessedHand(HandObservation? Hand, GestureSnapshot Gesture);

/// <summary>Experimental geometry rules; all distances are relative to palm width.</summary>
public static class GestureClassifier
{
    public static GestureKind Classify(HandObservation hand, GestureKind previous = GestureKind.Unknown)
    {
        if (!Valid(hand)) return GestureKind.NoHand;
        var p = hand.Points;
        double scale = Distance(p[5], p[17]);
        if (scale < 1e-4) return GestureKind.Unknown;
        double pinch = Distance(p[4], p[8]) / scale;
        if (pinch < (previous == GestureKind.Pinch ? .43 : .33) &&
            Distance(p[8], p[0]) > scale * 1.15 && Distance(p[4], p[0]) > scale * 1.05)
            return GestureKind.Pinch;
        int extended = 0, curled = 0;
        for (int mcp = 5; mcp <= 17; mcp += 4)
        {
            double pip = Angle(p[mcp], p[mcp + 1], p[mcp + 2]);
            double dip = Angle(p[mcp + 1], p[mcp + 2], p[mcp + 3]);
            if (pip > 155 && dip > 150 && Distance(p[mcp + 3], p[0]) > Distance(p[mcp + 1], p[0]) * 1.12) extended |= 1 << ((mcp - 5) / 4);
            if (pip < 130 || Distance(p[mcp + 3], p[0]) < Distance(p[mcp + 1], p[0]) * .95) curled |= 1 << ((mcp - 5) / 4);
        }
        if (extended == 0b1111 && Angle(p[2], p[3], p[4]) > 150 && Distance(p[4], p[5]) > scale * .65)
            return GestureKind.OpenPalm;
        if (curled == 0b1111)
        {
            bool thumbExtended = Angle(p[2], p[3], p[4]) > 155 &&
                Distance(p[4], p[0]) > scale * 1.5 &&
                Distance(p[4], p[5]) > scale * .75 &&
                Distance(p[4], p[9]) < Distance(p[4], p[0]);
            if (!thumbExtended) return GestureKind.Fist;
            double thumbX = p[4].X - p[2].X;
            double thumbY = p[4].Y - p[2].Y;
            return thumbY > Math.Abs(thumbX) ? GestureKind.ThumbsDown : GestureKind.ThumbsUp;
        }
        if (extended == 0b0011 && curled == 0b1100 && Distance(p[8], p[12]) > scale * .4)
            return GestureKind.Victory;
        if (extended == 0b0111 && curled == 0b1000)
            return GestureKind.ThreeFingers;
        if (extended == 0b1000 && curled == 0b0111 &&
            Angle(p[2], p[3], p[4]) > 150 && Distance(p[4], p[5]) > scale * .65 &&
            Distance(p[4], p[20]) > scale * 1.4)
            return GestureKind.Shaka;
        return GestureKind.Unknown;
    }
    public static bool Valid(HandObservation hand) => hand.Points.Count == 21 &&
        float.IsFinite(hand.Confidence) && hand.Confidence >= .8f &&
        hand.Points.All(p => float.IsFinite(p.X) && float.IsFinite(p.Y) && float.IsFinite(p.Z));
    public static double Distance(HandPoint a, HandPoint b) =>
        Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2) + Math.Pow(a.Z - b.Z, 2));
    static double Angle(HandPoint a, HandPoint b, HandPoint c)
    {
        double ab = Distance(a, b), cb = Distance(c, b);
        if (ab * cb < 1e-8) return 0;
        double dot = (a.X - b.X) * (c.X - b.X) + (a.Y - b.Y) * (c.Y - b.Y) + (a.Z - b.Z) * (c.Z - b.Z);
        return Math.Acos(Math.Clamp(dot / (ab * cb), -1, 1)) * 180 / Math.PI;
    }
}

/// <summary>One worker owns this instance. No input automation is performed.</summary>
public sealed class HandGestureProcessor
{
    readonly TrackingOptions options;
    public HandGestureProcessor(TrackingPreset preset=TrackingPreset.Balanced){options=TrackingOptions.For(preset);}
    HandObservation? filtered;
    TimeSpan? lastTime;
    TimeSpan candidateSince;
    GestureKind candidate = GestureKind.NoHand, stable = GestureKind.NoHand;
    int samples;
    long confirmations;
    public ProcessedHand Process(HandObservation? observation, TimeSpan time)
    {
        if (observation == null || !GestureClassifier.Valid(observation))
        {
            ResetTracking();
            return new(null, new(GestureKind.NoHand, GestureKind.NoHand, 0, confirmations));
        }
        double dt = lastTime.HasValue ? (time - lastTime.Value).TotalMilliseconds : 0;
        bool discontinuity = filtered == null || dt <= 0 || dt > 250 ||
            GestureClassifier.Distance(observation.Points[0], filtered.Points[0]) >
            Math.Max(1, GestureClassifier.Distance(observation.Points[5], observation.Points[17])) * 1.5;
        if (discontinuity)
        {
            filtered = observation;
            candidate = stable = GestureKind.Unknown;
            samples = 0;
        }
        else
        {
            float alpha = (float)(1 - Math.Exp(-dt / options.SmoothingMs));
            filtered = new(observation.Points.Select((p, i) =>
            {
                var old = filtered!.Points[i];
                return new HandPoint(old.X + alpha * (p.X - old.X), old.Y + alpha * (p.Y - old.Y), old.Z + alpha * (p.Z - old.Z));
            }).ToArray(), observation.Confidence);
        }
        lastTime = time;
        // Use the current fingertip positions for the quick contact gesture.
        var rawGesture = GestureClassifier.Classify(observation, candidate);
        var next = rawGesture == GestureKind.Pinch ? rawGesture : GestureClassifier.Classify(filtered!, candidate);
        if (next == GestureKind.Pinch && rawGesture != GestureKind.Pinch) next = rawGesture;
        if (next != candidate || samples == 0)
        {
            candidate = next; candidateSince = time; samples = 1; stable = GestureKind.Unknown;
        }
        else samples++;
        int requiredSamples = candidate == GestureKind.Pinch ? 2 : 3;
        double holdMs = candidate == GestureKind.Pinch ? Math.Min(options.HoldMs, 45) : options.HoldMs;
        double progress = Math.Clamp((time - candidateSince).TotalMilliseconds / holdMs, 0, 1);
        if (candidate is GestureKind.NoHand or GestureKind.Unknown)
        {
            stable = GestureKind.Unknown; progress = 0;
        }
        else if (progress >= 1 && samples >= requiredSamples && stable != candidate)
        {
            stable = candidate; confirmations++;
        }
        return new(filtered, new(candidate, stable, progress, confirmations));
    }
    public void ResetTracking()
    {
        filtered = null; lastTime = null; candidate = stable = GestureKind.NoHand; samples = 0;
    }
}


