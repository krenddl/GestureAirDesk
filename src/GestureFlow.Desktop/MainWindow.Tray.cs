using Avalonia.Controls;
using Avalonia.Platform;

namespace GestureFlow.Desktop;

public sealed partial class MainWindow
{
    TrayIcon? trayIcon;
    NativeMenuItem? trayCamera;
    NativeMenuItem? trayMouse;
    NativeMenuItem? trayRule;

    void CreateTrayMenu()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var stream = AssetLoader.Open(new Uri("avares://GestureFlow.Desktop/Assets/gestureflow.ico"));
        Icon = new WindowIcon(stream);
        trayCamera = TrayItem("Включить камеру", () => RunHotkey(1));
        trayMouse = TrayItem("Включить мышь", () => RunHotkey(2));
        trayRule = TrayItem("Включить правило жеста", () => RunHotkey(3));
        trayGestureStrip = TrayItem("Скрыть полоску жестов", () => showGestureStrip.IsChecked = showGestureStrip.IsChecked != true);
        var menu = new NativeMenu
        {
            Items =
            {
                TrayItem("Открыть GestureFlow", RestoreMainWindow),
                new NativeMenuItemSeparator(),
                trayCamera, trayMouse, trayRule, trayGestureStrip,
                TrayItem("Остановить всё", StopEverything),
                new NativeMenuItemSeparator(),
                TrayItem("Выход", Close)
            }
        };
        trayIcon = new TrayIcon { Icon = Icon, Menu = menu, ToolTipText = "GestureFlow AirDesk", IsVisible = true };
        trayIcon.Clicked += (_, _) => RestoreMainWindow();
        UpdateTrayMenu();
    }

    static NativeMenuItem TrayItem(string label, Action action)
    {
        var item = new NativeMenuItem(label);
        item.Click += (_, _) => action();
        return item;
    }

    void RestoreMainWindow()
    {
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Show();
        Activate();
    }

    void UpdateTrayMenu()
    {
        if (trayIcon == null || trayCamera == null || trayMouse == null || trayRule == null) return;
        if (trayGestureStrip != null)
        {
            trayGestureStrip.Header = showGestureStrip.IsChecked == true ? "Скрыть полоску жестов" : "Показать полоску жестов";
            trayGestureStrip.IsEnabled = !gestureStripUnavailable;
        }
        bool cameraRequested = cts != null || startCameraAfterDiscovery;
        trayCamera.Header = cameraRequested ? "Остановить камеру" : "Включить камеру";
        trayCamera.IsEnabled = !busy || cameraRequested;
        trayMouse.Header = mouseEnabled.IsChecked == true ? "Выключить мышь" : "Включить мышь";
        trayMouse.IsEnabled = mouseEnabled.IsChecked == true || (cts is { IsCancellationRequested: false } && recognitionToggle.IsChecked == true);
        trayRule.Header = automationEnabled.IsChecked == true ? "Выключить правило жеста" : "Включить правило жеста";
    }

    void DisposeTrayMenu()
    {
        trayIcon?.Dispose();
        trayIcon = null;
    }
}