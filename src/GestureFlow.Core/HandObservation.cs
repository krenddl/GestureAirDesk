namespace GestureFlow.Core;

public record HandPoint(float X, float Y, float Z);
public record HandObservation(IReadOnlyList<HandPoint> Points, float Confidence);
public record CameraFrame(byte[] Jpeg, string RecognitionStatus, int Hands, GestureSnapshot? Gesture = null, HandObservation? Hand = null, int Width = 0, int Height = 0);



