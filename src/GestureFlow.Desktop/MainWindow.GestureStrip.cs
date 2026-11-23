using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace GestureFlow.Desktop;

public sealed partial class MainWindow
{
    const int ExtendedStyle = -20;
    const int TransparentLayeredTool = 0x080800A0; // NOACTIVATE | LAYERED | TOOLWINDOW | TRANSPARENT
    const uint LayeredAlpha = 2;
    readonly CheckBox showGestureStrip = new()
    {
        Content = "Показывать полоску жестов поверх окон",
        IsChecked = true
    };
    Window? gestureStrip;
    TextBlock? stripGesture;
    TextBlock? stripMouse;
    TextBlock? stripRules;
    NativeMenuItem? trayGestureStrip;
    TimeSpan previewGestureStripUntil;
    TimeSpan stripShortcutUntil;
    string stripShortcutText = "";

    void ShowStripShortcut(string text)
    {
        stripShortcutText = text;
        stripShortcutUntil = automationClock.Elapsed + TimeSpan.FromSeconds(3);
    }

    bool StripShortcutVisible => automationClock.Elapsed < stripShortcutUntil;
    bool gestureStripUnavailable;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    static extern IntPtr GetOverlayStyle(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    static extern IntPtr SetOverlayStyle(IntPtr hwnd, int index, IntPtr value);

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint colorKey, byte alpha, uint flags);

    void InitializeGestureStrip()
    {
        showGestureStrip.IsVisible = OperatingSystem.IsWindows();
        showGestureStrip.IsCheckedChanged += (_, _) =>
        {
            if (showGestureStrip.IsChecked == true)
                previewGestureStripUntil = automationClock.Elapsed + TimeSpan.FromSeconds(5);
            RefreshShell();
        };
        Closed += (_, _) => gestureStrip?.Close();
    }

    Window CreateGestureStrip()
    {
        stripGesture = StripLabel("Жест: —");
        stripMouse = StripLabel("Мышь: выкл.");
        stripRules = StripLabel("Правила: выкл.");
        var content = new Grid { ColumnDefinitions = new ColumnDefinitions("2.1*,*,1.05*"), ColumnSpacing = 12 };
        foreach (var (label, index) in new[] { (stripGesture, 0), (stripMouse, 1), (stripRules, 2) })
        {
            Grid.SetColumn(label, index);
            content.Children.Add(label);
        }
        var window = new Window
        {
            Title = "GestureFlow · полоска жестов",
            Width = 480, Height = 40,
            CanResize = false, ShowActivated = false, ShowInTaskbar = false, Topmost = true,
            SystemDecorations = SystemDecorations.None, WindowStartupLocation = WindowStartupLocation.Manual,
            Background = Ink("#382820"),
            Content = new Border
            {
                IsHitTestVisible = false, Padding = new Thickness(14, 0),
                BorderBrush = Ink("#AE674B"), BorderThickness = new Thickness(0, 0, 0, 2),
                Child = content
            }
        };
        return window;
    }

    static bool ConfigureGestureStrip(Window window)
    {
        if (window.TryGetPlatformHandle() is not { } handle) return false;
        var style = GetOverlayStyle(handle.Handle, ExtendedStyle).ToInt64();
        SetOverlayStyle(handle.Handle, ExtendedStyle, new IntPtr(style | TransparentLayeredTool));
        var applied = GetOverlayStyle(handle.Handle, ExtendedStyle).ToInt64();
        return (applied & TransparentLayeredTool) == TransparentLayeredTool &&
            SetLayeredWindowAttributes(handle.Handle, 0, 255, LayeredAlpha);
    }

    static TextBlock StripLabel(string text) => new()
    {
        Text = text, FontSize = 13, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center,
        TextTrimming = TextTrimming.CharacterEllipsis
    };

    void UpdateGestureStrip(bool capturing)
    {
        if (!OperatingSystem.IsWindows() || closeRequested || gestureStripUnavailable) return;
        bool visible = showGestureStrip.IsChecked == true &&
            ((capturing && recognitionToggle.IsChecked == true) || automationClock.Elapsed < previewGestureStripUntil || StripShortcutVisible);
        if (!visible)
        {
            if (gestureStrip?.IsVisible == true) gestureStrip.Hide();
            return;
        }
        var screen = Screens.Primary;
        if (screen == null) return;
        gestureStrip ??= CreateGestureStrip();
        stripGesture!.Text = StripShortcutVisible ? stripShortcutText : capturing ? gestureStatus.Text : "Камера выключена";
        stripMouse!.Text = mouseEnabled.IsChecked == true ? "Мышь: вкл." : "Мышь: выкл.";
        stripRules!.Text = automationEnabled.IsChecked != true ? "Правила: выкл."
            : trainingActive ? "Тренировка" : $"Правила: {activeRules.Length}";
        if (!gestureStrip.IsVisible)
        {
            var area = screen.WorkingArea;
            int width = (int)(gestureStrip.Width * screen.Scaling);
            gestureStrip.Position = new PixelPoint(area.X + Math.Max(0, (area.Width - width) / 2), area.Y + 8);
            gestureStrip.Show();
            if (!ConfigureGestureStrip(gestureStrip))
            {
                gestureStrip.Close();
                gestureStrip = null;
                gestureStripUnavailable = true;
                showGestureStrip.Content = "Windows не разрешила показать полоску жестов";
                showGestureStrip.IsEnabled = false;
            }
        }
    }
}