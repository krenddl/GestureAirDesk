using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media.Imaging;

namespace GestureFlow.Desktop;

public sealed partial class MainWindow
{
    const string TelegramPhotoAction = "Telegram · подготовить фото";
    string TelegramPhotoRecipientPath =>
        Path.Combine(Path.GetDirectoryName(settingsStore.FilePath)!, "telegram-photo-recipient.txt");

    bool preparingTelegramPhoto;
    CancellationTokenSource? telegramPhotoSession;
    TimeSpan telegramPhotoDue;
    int telegramPhotoSeconds;

    void QueueTelegramPhoto()
    {
        if (preparingTelegramPhoto || telegramPhotoSession != null || cts == null) return;
        telegramPhotoSession = cts;
        telegramPhotoDue = automationClock.Elapsed + TimeSpan.FromSeconds(3);
        telegramPhotoSeconds = 3;
        ruleSaveStatus.Text = "Фото через 3 с. Уберите руку из кадра.";
    }

    void ObservePendingTelegramPhoto(GestureFlow.Core.CameraFrame frame, bool usingMouse)
    {
        if (telegramPhotoSession == null) return;
        if (telegramPhotoSession != cts || telegramPhotoSession.IsCancellationRequested ||
            automationEnabled.IsChecked != true || trainingActive || usingMouse ||
            !activeRules.Any(rule => rule.Target == TelegramPhotoAction))
        {
            telegramPhotoSession = null;
            ruleSaveStatus.Text = "Съёмка отменена.";
            return;
        }

        var remaining = telegramPhotoDue - automationClock.Elapsed;
        if (remaining > TimeSpan.Zero)
        {
            int seconds = (int)Math.Ceiling(remaining.TotalSeconds);
            if (seconds != telegramPhotoSeconds)
            {
                telegramPhotoSeconds = seconds;
                ruleSaveStatus.Text = $"Фото через {seconds} с. Уберите руку из кадра.";
            }
            return;
        }

        telegramPhotoSession = null;
        _ = PrepareTelegramPhotoAsync(frame.Jpeg);
    }

    Control BuildTelegramPhotoEditor()
    {
        var recipient = new TextBox { Watermark = "@username для фото", MaxLength = 33 };
        var status = Copy("", 13, true);
        try
        {
            if (File.Exists(TelegramPhotoRecipientPath))
                recipient.Text = "@" + File.ReadAllText(TelegramPhotoRecipientPath).Trim();
        }
        catch (Exception ex)
        {
            status.Text = "Не удалось прочитать получателя: " + ex.Message;
        }

        var save = new Button { Content = "Сохранить получателя" };
        save.Click += (_, _) =>
        {
            var name = (recipient.Text ?? "").Trim().TrimStart('@');
            if (!TelegramUsername.IsMatch(name))
            {
                status.Text = "Укажите @username получателя.";
                return;
            }

            try
            {
                var path = TelegramPhotoRecipientPath;
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path + ".tmp", name);
                File.Move(path + ".tmp", path, overwrite: true);
                status.Text = "Получатель фото сохранён.";
            }
            catch (Exception ex)
            {
                status.Text = "Не удалось сохранить получателя: " + ex.Message;
            }
        };

        var editor = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                Copy("Фото в Telegram", 17),
                Copy("Через 3 секунды после жеста приложение копирует свежий кадр в буфер и открывает чат. Вставьте фото (Ctrl+V) и подтвердите отправку в Telegram.", 13, true),
                recipient, save, status
            }
        };
        void UpdateVisibility(object? sender, SelectionChangedEventArgs args) =>
            editor.IsVisible = automationDestination.SelectedItem as string == TelegramPhotoAction;

        editor.AttachedToVisualTree += (_, _) =>
        {
            automationDestination.SelectionChanged += UpdateVisibility;
            editor.IsVisible = automationDestination.SelectedItem as string == TelegramPhotoAction;
        };
        editor.DetachedFromVisualTree += (_, _) => automationDestination.SelectionChanged -= UpdateVisibility;
        return editor;
    }

    async Task PrepareTelegramPhotoAsync(byte[] jpeg)
    {
        if (preparingTelegramPhoto) return;
        preparingTelegramPhoto = true;
        try
        {
            var name = File.Exists(TelegramPhotoRecipientPath)
                ? File.ReadAllText(TelegramPhotoRecipientPath).Trim()
                : "";
            if (!TelegramUsername.IsMatch(name))
            {
                ruleSaveStatus.Text = "Сначала сохраните @username получателя фото.";
                return;
            }

            var clipboard = Clipboard ?? throw new InvalidOperationException("Буфер обмена недоступен.");
            using var stream = new MemoryStream(jpeg, writable: false);
            using var bitmap = new Bitmap(stream);
            var data = new DataTransfer();
            data.Add(DataTransferItem.Create(DataFormat.Bitmap, bitmap));
            await clipboard.SetDataAsync(data);
            await clipboard.FlushAsync();

            var link = $"tg://resolve?domain={Uri.EscapeDataString(name)}";
            using var process = Process.Start(new ProcessStartInfo(link) { UseShellExecute = true });
            ruleSaveStatus.Text = $"Кадр скопирован. В чате @{name} вставьте фото (Ctrl+V) и подтвердите отправку.";
        }
        catch (Exception ex)
        {
            ruleSaveStatus.Text = "Не удалось подготовить фото: " + ex.Message;
        }
        finally
        {
            preparingTelegramPhoto = false;
        }
    }
}
