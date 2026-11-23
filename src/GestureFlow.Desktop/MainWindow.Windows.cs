using Avalonia.Controls;
using GestureFlow.Core;
using GestureFlow.Windows;

namespace GestureFlow.Desktop;

public sealed partial class MainWindow
{
    readonly CheckBox windowGestureEnabled = new() { Content = "Управлять выбранным окном жестом на этот запуск" };
    readonly ComboBox windowGesture = new() { ItemsSource = new[] { "Открытая ладонь", "Кулак", "Щипок" }, SelectedIndex = 1, Width = 260 };
    readonly ComboBox windowCommand = new() { ItemsSource = new[] { "Свернуть", "Развернуть", "Восстановить" }, SelectedIndex = 0, Width = 260 };
    readonly NavigationAutomation windowRule = new();
    WindowTarget? armedWindow;
    TimeSpan lastWindowFrame;

    void InitializeWindowGestures()
    {
        windowGestureEnabled.IsCheckedChanged += (_, _) =>
        {
            windowRule.Configure(false);
            armedWindow = null;
            if (windowGestureEnabled.IsChecked != true) return;
            if (cts == null || cts.IsCancellationRequested || recognitionToggle.IsChecked != true || !IsActive)
            {
                windowGestureEnabled.IsChecked = false;
                windowStatus.Text = "Сначала запустите камеру с распознаванием и вернитесь в активное окно GestureFlow.";
                return;
            }
            if (windowPicker.SelectedItem is not WindowTarget target)
            {
                windowGestureEnabled.IsChecked = false;
                windowStatus.Text = "Сначала выберите окно.";
                return;
            }
            try { windowManager.State(target); }
            catch (Exception ex)
            {
                windowGestureEnabled.IsChecked = false;
                windowStatus.Text = "Ошибка: " + ex.Message;
                return;
            }
            mouseEnabled.IsChecked = false;
            airPointer.Stop();
            automationEnabled.IsChecked = false;
            mediaGestureEnabled.IsChecked = false;
            palmEnabled.IsChecked = false;
            SetPalmOpen(false);
            armedWindow = target;
            lastWindowFrame = automationClock.Elapsed;
            windowRule.Configure(true);
            windowStatus.Text = "Готово: " + target.Title + ". Сначала отпустите выбранный жест. После команды правило выключится.";
        };
        windowPicker.SelectionChanged += (_, _) => windowGestureEnabled.IsChecked = false;
        windowGesture.SelectionChanged += (_, _) => windowGestureEnabled.IsChecked = false;
        windowCommand.SelectionChanged += (_, _) => windowGestureEnabled.IsChecked = false;
    }

    void AddWindowGestureControls(StackPanel panel)
    {
        foreach (var control in new Control[] { windowGesture, windowCommand, windowGestureEnabled })
        {
            Detach(control);
            panel.Children.Add(control);
        }
        panel.Children.Add(new TextBlock
        {
            Text = "1. Запустите камеру с распознаванием. 2. Выберите окно, жест и команду. 3. Включите правило и отпустите жест, затем покажите его. Одна команда выключает правило. Уход из GestureFlow, смена выбора, Стоп камеры и удержание Escape отключают его. Включение и цель не сохраняются.",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        });
    }

    void CheckWindowGestureStop()
    {
        if (windowGestureEnabled.IsChecked == true &&
            (mouseIntegration.EscapeHeld || !IsActive || cts == null || cts.IsCancellationRequested ||
             automationClock.Elapsed - lastWindowFrame > TimeSpan.FromMilliseconds(500)))
        {
            windowGestureEnabled.IsChecked = false;
            windowStatus.Text = "Правило окна остановлено.";
        }
    }

    void ObserveWindowGesture(GestureSnapshot? gesture)
    {
        if (windowGestureEnabled.IsChecked != true) return;
        // Check timeout before accepting a delayed frame; a stalled stream must not resume input.
        CheckWindowGestureStop();
        if (windowGestureEnabled.IsChecked != true) return;
        lastWindowFrame = automationClock.Elapsed;
        var trigger = windowGesture.SelectedIndex switch { 0 => GestureKind.OpenPalm, 1 => GestureKind.Fist, _ => GestureKind.Pinch };
        if (!windowRule.Observe(gesture, trigger, automationClock.Elapsed)) return;
        var target = armedWindow;
        var command = windowCommand.SelectedIndex switch { 0 => WindowAction.Minimize, 1 => WindowAction.Maximize, _ => WindowAction.Restore };
        windowGestureEnabled.IsChecked = false;
        if (target == null) return;
        try
        {
            windowManager.Execute(target, command);
            windowStatus.Text = "Команда отправлена: " + target.Title + " → " + windowCommand.SelectedItem + ". Правило выключено.";
        }
        catch (Exception ex) { windowStatus.Text = "Ошибка команды окна: " + ex.Message; }
    }
}

