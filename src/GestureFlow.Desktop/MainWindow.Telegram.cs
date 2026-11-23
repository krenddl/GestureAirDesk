using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Layout;

namespace GestureFlow.Desktop;

public sealed partial class MainWindow
{
    const string TelegramAction = "Telegram · открыть черновик";
    static readonly Regex TelegramUsername = new(@"\A[A-Za-z0-9_]{1,32}\z", RegexOptions.CultureInvariant);

    string TelegramDraftPath => Path.Combine(Path.GetDirectoryName(settingsStore.FilePath)!, "telegram-draft.json");

    sealed record TelegramDraft(string Username, string Text);

    TelegramDraft? ReadTelegramDraft()
    {
        if (!File.Exists(TelegramDraftPath)) return null;
        var draft = JsonSerializer.Deserialize<TelegramDraft>(File.ReadAllText(TelegramDraftPath));
        if (draft == null || !TelegramUsername.IsMatch(draft.Username ?? "") ||
            string.IsNullOrWhiteSpace(draft.Text) || draft.Text.Length > 500)
            throw new InvalidDataException("Укажите @username получателя и сохраните черновик заново.");
        return draft;
    }

    Control BuildTelegramDraftEditor()
    {
        var recipient = new TextBox { Watermark = "@username получателя", MaxLength = 33 };
        var message = new TextBox
        {
            Watermark = "Текст сообщения", MaxLength = 500, AcceptsReturn = true,
            MinHeight = 66, TextWrapping = Avalonia.Media.TextWrapping.Wrap
        };
        Avalonia.Automation.AutomationProperties.SetName(recipient, "Получатель Telegram");
        Avalonia.Automation.AutomationProperties.SetName(message, "Текст черновика Telegram");
        var feedback = Copy("", 13, true);
        try
        {
            if (ReadTelegramDraft() is { } draft)
            {
                recipient.Text = "@" + draft.Username;
                message.Text = draft.Text;
            }
        }
        catch (Exception ex) { feedback.Text = ex.Message; }

        var save = new Button { Content = "Сохранить черновик" };
        save.Click += (_, _) =>
        {
            var username = (recipient.Text ?? "").Trim();
            if (username.StartsWith('@')) username = username[1..];
            var body = (message.Text ?? "").Trim();
            if (!TelegramUsername.IsMatch(username) || string.IsNullOrWhiteSpace(body))
            {
                feedback.Text = "Укажите @username получателя и введите текст.";
                return;
            }
            try
            {
                var path = TelegramDraftPath;
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(new TelegramDraft(username, body)));
                File.Move(path + ".tmp", path, overwrite: true);
                feedback.Text = "Черновик сохранён.";
            }
            catch (Exception ex) { feedback.Text = "Не удалось сохранить черновик: " + ex.Message; }
        };
        var clear = new Button { Content = "Сбросить черновик" };
        clear.Click += (_, _) =>
        {
            try
            {
                File.Delete(TelegramDraftPath);
                recipient.Text = message.Text = "";
                feedback.Text = "Черновик сброшен.";
            }
            catch (Exception ex) { feedback.Text = "Не удалось сбросить черновик: " + ex.Message; }
        };
        var editor = new StackPanel { Spacing = 8, Children = {
            Copy("Черновик Telegram", 17),
            Copy("GestureFlow подставит текст в чат. Для отправки нажмите «Отправить» в Telegram Desktop.", 13, true),
            recipient, message,
            new WrapPanel { Orientation = Orientation.Horizontal, Children = { save, clear } },
            feedback
        } };
        void UpdateVisibility(object? sender, SelectionChangedEventArgs args) =>
            editor.IsVisible = automationDestination.SelectedItem as string == TelegramAction;
        editor.AttachedToVisualTree += (_, _) =>
        {
            automationDestination.SelectionChanged += UpdateVisibility;
            editor.IsVisible = automationDestination.SelectedItem as string == TelegramAction;
        };
        editor.DetachedFromVisualTree += (_, _) => automationDestination.SelectionChanged -= UpdateVisibility;
        return editor;
    }

    bool OpenTelegramAction()
    {
        try
        {
            if (ReadTelegramDraft() is not { } draft)
            {
                ruleSaveStatus.Text = "Сначала сохраните черновик Telegram.";
                return false;
            }
            var url = $"tg://resolve?domain={Uri.EscapeDataString(draft.Username)}&text={Uri.EscapeDataString(draft.Text)}";
            using var process = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            ruleSaveStatus.Text = "Запрошено открытие чата с черновиком.";
            return true;
        }
        catch (Exception ex)
        {
            ruleSaveStatus.Text = "Не удалось открыть черновик. " + ex.Message;
            return false;
        }
    }
}