using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Threading;
using GestureFlow.Core;

namespace GestureFlow.Desktop;

public sealed partial class MainWindow
{
    const int HotkeyMessage = 0x0312;
    const uint HotkeyModifiers = 0x4007; // No repeat + Ctrl + Alt + Shift
    const int HotkeyBaseId = 0x4750;
    static readonly (uint Key, string Label)[] Hotkeys =
    {
        ('0', "Остановить всё"), ('1', "Камера с распознаванием"),
        ('2', "Управление мышью"), ('3', "Правила жестов"),
        ('4', "Способ клика: задержка / щипок"),
        ('5', "Действие мыши: левый / правый / двойной клик / перетаскивание"),
        ('6', "Включить или выключить прокрутку")
    };
    readonly HashSet<int> registeredHotkeys = new();
    readonly List<string> unavailableHotkeys = new();
    IntPtr hotkeyWindow;
    bool startCameraAfterDiscovery;

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);

    [DllImport("user32.dll")]
    static extern bool UnregisterHotKey(IntPtr window, int id);

    void RegisterGlobalHotkeys()
    {
        if (!OperatingSystem.IsWindows() || TryGetPlatformHandle() is not { } handle) return;
        hotkeyWindow = handle.Handle;
        Win32Properties.AddWndProcHookCallback(this, HandleHotkey);
        for (int i = 0; i < Hotkeys.Length; i++)
        {
            int id = HotkeyBaseId + i;
            if (RegisterHotKey(hotkeyWindow, id, HotkeyModifiers, Hotkeys[i].Key))
                registeredHotkeys.Add(id);
            else
                unavailableHotkeys.Add($"{HotkeyLabel(Hotkeys[i].Key)} (код Windows {Marshal.GetLastWin32Error()})");
        }
    }

    void UnregisterGlobalHotkeys()
    {
        if (hotkeyWindow == IntPtr.Zero) return;
        foreach (int id in registeredHotkeys) UnregisterHotKey(hotkeyWindow, id);
        registeredHotkeys.Clear();
        Win32Properties.RemoveWndProcHookCallback(this, HandleHotkey);
        hotkeyWindow = IntPtr.Zero;
    }

    IntPtr HandleHotkey(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message != HotkeyMessage) return IntPtr.Zero;
        int id = unchecked((int)wParam.ToInt64());
        if (registeredHotkeys.Contains(id))
        {
            handled = true;
            Dispatcher.UIThread.Post(() => RunHotkey(id - HotkeyBaseId));
        }
        return IntPtr.Zero;
    }

    async void RunHotkey(int action)
    {
        if (closeRequested) return;
        switch (action)
        {
            case 0:
                StopEverything();
                break;
            case 1:
                if (cts != null || startCameraAfterDiscovery)
                {
                    StopEverything();
                    break;
                }
                if (busy) break;
                recognitionToggle.IsChecked = true;
                if (devices.SelectedItem is not CameraDevice)
                {
                    startCameraAfterDiscovery = true;
                    await Discover();
                    if (!startCameraAfterDiscovery || closeRequested) break;
                    startCameraAfterDiscovery = false;
                }
                await Start();
                break;
            case 2:
                mouseEnabled.IsChecked = mouseEnabled.IsChecked != true;
                break;
            case 3:
                automationEnabled.IsChecked = automationEnabled.IsChecked != true;
                break;
            case 4:
                if (clickTrigger.SelectedIndex == 1)
                {
                    if (clickKind.SelectedIndex == 3) clickKind.SelectedIndex = (int)MouseClickKind.Left;
                    clickTrigger.SelectedIndex = 0;
                }
                else clickTrigger.SelectedIndex = 1;
                ShowStripShortcut(clickTrigger.SelectedIndex == 1 ? "Клик: щипок" : "Клик: задержка");
                break;
            case 5:
                clickKind.SelectedIndex = (clickKind.SelectedIndex + 1) % 4;
                ShowStripShortcut(clickKind.SelectedItem as string ?? "Действие мыши");
                break;
            case 6:
                wheelEnabled.IsChecked = wheelEnabled.IsChecked != true;
                ShowStripShortcut(wheelEnabled.IsChecked == true ? "Прокрутка: вкл." : "Прокрутка: выкл.");
                break;
        }
        RefreshShell();
    }

    static string HotkeyLabel(uint key) => $"Ctrl+Alt+Shift+{(char)key}";

    Control BuildHotkeyHelp()
    {
        var lines = new StackPanel { Spacing = 6 };
        lines.Children.Add(Copy("Глобальные горячие клавиши", 17));
        foreach (var hotkey in Hotkeys)
            lines.Children.Add(Copy($"{HotkeyLabel(hotkey.Key)}   {hotkey.Label}"));
        lines.Children.Add(Copy("Работают в других окнах, пока GestureFlow запущен, в том числе свёрнутым.", 13, true));
        if (unavailableHotkeys.Count > 0)
            lines.Children.Add(Copy($"Windows не зарегистрировала: {string.Join(", ", unavailableHotkeys)}.", 13, true));
        return lines;
    }
}