using System.Text;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using GestureFlow.Core;

namespace GestureFlow.Desktop;

public sealed partial class MainWindow
{
    Button? exportHistoryButton;
    bool exportingHistory;

    Control BuildHistoryExport()
    {
        var feedback = Copy("", 13, true);
        var button = new Button { Content = "Выгрузить CSV", IsEnabled = history.Count > 0 && !exportingHistory };
        button.Classes.Add("primary");
        exportHistoryButton = button;
        button.Click += async (_, _) =>
        {
            if (exportingHistory || history.Count == 0) return;
            var snapshot = history.ToArray();
            exportingHistory = true;
            button.IsEnabled = false;
            try
            {
                var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Выгрузить историю GestureFlow",
                    SuggestedFileName = $"GestureFlow-history-{DateTime.Now:yyyy-MM-dd}",
                    DefaultExtension = "csv",
                    FileTypeChoices = new[] { new FilePickerFileType("Таблица CSV") { Patterns = new[] { "*.csv" } } },
                    ShowOverwritePrompt = true
                });
                if (file == null) return;
                await using (var stream = await file.OpenWriteAsync())
                {
                    if (stream.CanSeek) stream.SetLength(0);
                    await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                    await writer.WriteAsync(SessionHistoryCsv.Format(snapshot));
                }
                feedback.Text = $"Выгружено сеансов: {snapshot.Length}.";
            }
            catch (Exception ex) { feedback.Text = "Не удалось выгрузить историю: " + ex.Message; }
            finally
            {
                exportingHistory = false;
                if (exportHistoryButton != null) exportHistoryButton.IsEnabled = history.Count > 0;
            }
        };
        return new StackPanel { Spacing = 6, Children = { button, feedback } };
    }
}