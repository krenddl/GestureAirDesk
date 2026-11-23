using GestureFlow.Core;
using GestureFlow.Windows;

namespace GestureFlow.Desktop;

public sealed partial class MainWindow
{
    const string BrightnessAction = "Яркость движением ладони";
    readonly PalmBrightnessGesture brightnessGesture = new();
    bool adjustingBrightness;
    bool BrightnessSelected => automationDestination.SelectedItem as string == BrightnessAction;

    void ObserveBrightness(CameraFrame frame)
    {
        if (automationEnabled.IsChecked != true || mouseEnabled.IsChecked == true)
        {
            brightnessGesture.Reset();
            return;
        }
        int step = brightnessGesture.Observe(frame, automationClock.Elapsed);
        if (step == 0) return;
        if (trainingActive) ruleSaveStatus.Text = $"Тренировка: яркость изменилась бы на {step:+0;-0} пунктов.";
        else if (!adjustingBrightness) _ = AdjustBrightnessAsync(step);
    }

    async Task AdjustBrightnessAsync(int step)
    {
        if (!OperatingSystem.IsWindows()) return;
        adjustingBrightness = true;
        try
        {
            int value = await Task.Run(() => OperatingSystem.IsWindows() ? DisplayBrightness.Adjust(step) : throw new PlatformNotSupportedException());
            ruleSaveStatus.Text = $"Яркость экрана: {value}%. Смените позу перед повтором.";
        }
        catch (Exception ex) { ruleSaveStatus.Text = "Не удалось изменить яркость: " + ex.Message; }
        finally { adjustingBrightness = false; }
    }
}