namespace GestureFlow.Core;

public record CameraDevice(int Index) { public override string ToString() => $"Камера {Index}"; }
public enum TrackingState { Disabled, ModelNotConfigured, Tracking }
public record GestureProfile(string Name, bool Enabled = false);
public interface IHandTracker { TrackingState State { get; } }
public enum MediaCommand { VolumeDown, VolumeUp, Mute, PlayPause, NextTrack, PreviousTrack }
public interface IWindowsIntegration { bool IsAvailable { get; } void Execute(MediaCommand command); }
public interface ICameraService
{
    Task<IReadOnlyList<CameraDevice>> DiscoverAsync();
    Task RunAsync(int index, Action<CameraFrame> frame, CancellationToken token, bool recognize = false, TrackingPreset preset=TrackingPreset.Balanced);
}


public enum MouseClickKind { Left, Right, Double }

