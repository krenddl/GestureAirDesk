namespace GestureFlow.Core;

/// <summary>Local preferences for a single user. Camera access always requires an explicit start.</summary>
public sealed record AppSettings(int CameraIndex = 0, bool MirrorPreview = false, string ProfileName = "Основной", int AutomationGesture = 2, int AutomationAction = 2, double PointerInsetX = 0, double PointerInsetY = 0, int TrackingPresetIndex = 0, int ClickMode = 0, bool WheelEnabled = false, int ClickTrigger = 0, bool AutoStartMouse = true);







