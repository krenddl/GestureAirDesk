using System.Diagnostics;

namespace GestureFlow.Desktop;

public sealed partial class MainWindow
{
    const string OpenExplorerAction = "Открыть Проводник";
    const string OpenCalculatorAction = "Открыть Калькулятор";

    bool OpenWindowsApp(string target)
    {
        try
        {
            var executable = target == OpenExplorerAction
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe")
                : Path.Combine(Environment.SystemDirectory, "calc.exe");
            using var process = Process.Start(new ProcessStartInfo(executable) { UseShellExecute = true });
            ruleSaveStatus.Text = $"Команда выполнена: {target}.";
            return true;
        }
        catch (Exception ex)
        {
            ruleSaveStatus.Text = $"Не удалось выполнить «{target}»: {ex.Message}";
            return false;
        }
    }
}