using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Layout;
using GestureFlow.Core;

namespace GestureFlow.Desktop;

public sealed partial class MainWindow
{
    sealed record GestureRule(int Gesture, int Action, bool Enabled);
    sealed record ActiveRule(GestureRule Rule, GestureKind Trigger, string Target, NavigationAutomation Gate);

    List<GestureRule> rules = new();
    ActiveRule[] activeRules = [];
    string? rulesProfile;
    string? rulesLoadError;
    StackPanel? ruleList;
    int? editingRule;
    Button? submitRuleButton;
    Button? cancelRuleEditButton;
    readonly CheckBox training = new() { Content = "Тренировка без выполнения команд" };
    bool trainingActive;
    string trainingProgress = "";
    TextBlock? ruleAssignmentHint;
    string RulesPath => Path.Combine(Path.GetDirectoryName(settingsStore.FilePath)!, "gesture-rules.json");

    Dictionary<string, List<GestureRule>> ReadRuleFile()
    {
        if (!File.Exists(RulesPath)) return new(StringComparer.OrdinalIgnoreCase);
        var stored = JsonSerializer.Deserialize<Dictionary<string, List<GestureRule>>>(File.ReadAllText(RulesPath))
            ?? throw new InvalidDataException("Файл правил пуст.");
        return new(stored, StringComparer.OrdinalIgnoreCase);
    }

    void LoadRules()
    {
        if (StringComparer.OrdinalIgnoreCase.Equals(rulesProfile, settings.ProfileName)) return;
        rulesProfile = settings.ProfileName;
        SetEditingRule(null);
        rulesLoadError = null;
        rules = [];
        try
        {
            if (ReadRuleFile().TryGetValue(settings.ProfileName, out var saved))
            {
                if (saved == null || saved.Any(r => r == null || !ValidRule(r)) ||
                    saved.Select(RulePose).Distinct().Count() != saved.Count)
                    throw new InvalidDataException("Некорректный список правил.");
                rules = saved;
            }
            else
            {
                var legacy = new GestureRule(settings.AutomationGesture, settings.AutomationAction, true);
                if (ValidRule(legacy)) rules.Add(legacy);
            }
        }
        catch (Exception ex) { rulesLoadError = "Не удалось загрузить правила: " + ex.Message; }
        RefreshActiveRules();
        RefreshRuleList();
    }

    string? RuleTarget(int index) => automationDestination.ItemsSource is string[] choices &&
        index >= 0 && index < choices.Length ? choices[index] : null;

    bool ValidRule(GestureRule rule) => rule.Gesture is >= 0 and <= 7 && RuleTarget(rule.Action) != null;
    int RulePose(GestureRule rule) => RuleTarget(rule.Action) == BrightnessAction ? 0 : rule.Gesture;
    static GestureKind RuleTrigger(int index) => index switch
    {
        0 => GestureKind.OpenPalm, 1 => GestureKind.Fist, 2 => GestureKind.Pinch,
        3 => GestureKind.Victory, 4 => GestureKind.ThumbsUp, 5 => GestureKind.ThreeFingers,
        6 => GestureKind.Shaka, 7 => GestureKind.ThumbsDown, _ => GestureKind.Unknown
    };

    void RefreshActiveRules()
    {
        bool enabled = automationEnabled.IsChecked == true;
        activeRules = rules.Where(rule => rule.Enabled).Select(rule =>
        {
            var gate = new NavigationAutomation();
            gate.Configure(enabled);
            return new ActiveRule(rule, RuleTrigger(RulePose(rule)), RuleTarget(rule.Action)!, gate);
        }).ToArray();
        brightnessGesture.Reset();
    }

    bool SaveRules(List<GestureRule> proposed)
    {
        if (rulesLoadError != null)
        {
            ruleSaveStatus.Text = rulesLoadError;
            return false;
        }
        try
        {
            var stored = ReadRuleFile();
            stored[settings.ProfileName] = proposed;
            Directory.CreateDirectory(Path.GetDirectoryName(RulesPath)!);
            File.WriteAllText(RulesPath + ".tmp", JsonSerializer.Serialize(stored));
            File.Move(RulesPath + ".tmp", RulesPath, overwrite: true);
            rules = proposed;
            RefreshActiveRules();
            RefreshRuleList();
            return true;
        }
        catch (Exception ex)
        {
            ruleSaveStatus.Text = "Не удалось сохранить правила: " + ex.Message;
            return false;
        }
    }

    void SetEditingRule(int? index)
    {
        editingRule = index;
        if (submitRuleButton != null)
            submitRuleButton.Content = index.HasValue ? "Сохранить изменения" : "Добавить правило";
        if (cancelRuleEditButton != null)
            cancelRuleEditButton.IsVisible = index.HasValue;
        UpdateRuleAssignmentHint();
    }

    Control BuildRuleEditorButtons()
    {
        var submit = new Button();
        submit.Classes.Add("primary");
        submit.Click += (_, _) => SaveSelectedRule();
        var cancel = new Button { Content = "Отменить редактирование" };
        cancel.Click += (_, _) => SetEditingRule(null);
        submitRuleButton = submit;
        cancelRuleEditButton = cancel;
        SetEditingRule(editingRule);
        return new WrapPanel { Orientation = Orientation.Horizontal, Children = { submit, cancel } };
    }

    void SaveSelectedRule()
    {
        var rule = new GestureRule(automationGesture.SelectedIndex, automationDestination.SelectedIndex,
            editingRule is int edited ? rules[edited].Enabled : true);
        if (!ValidRule(rule)) return;
        for (int i = 0; i < rules.Count; i++)
        {
            if (i == editingRule || RulePose(rules[i]) != RulePose(rule)) continue;
            ruleSaveStatus.Text = $"Жест «{GestureName(RuleTrigger(RulePose(rule)))}» уже занят другим правилом.";
            return;
        }
        var proposed = rules.ToList();
        if (editingRule is int index) proposed[index] = rule;
        else proposed.Add(rule);
        if (!SaveRules(proposed)) return;
        ruleSaveStatus.Text = editingRule.HasValue ? "Изменения сохранены." : "Правило добавлено.";
        SetEditingRule(null);
    }

    Control BuildRuleList()
    {
        ruleList = new StackPanel { Spacing = 6 };
        RefreshRuleList();
        return ruleList;
    }

    void RefreshRuleList()
    {
        if (ruleList == null) return;
        ruleList.Children.Clear();
        ruleList.Children.Add(Copy($"Правила профиля «{settings.ProfileName}»", 17));
        if (rulesLoadError != null) ruleList.Children.Add(Copy(rulesLoadError, 13, true));
        if (rules.Count == 0) ruleList.Children.Add(Copy("Добавьте первое правило.", 13, true));
        for (int i = 0; i < rules.Count; i++)
        {
            int index = i;
            var rule = rules[index];
            var action = RuleTarget(rule.Action);
            string pose = action == BrightnessAction ? "Ладонь вверх / вниз" : GestureName(RuleTrigger(rule.Gesture));
            var toggle = new CheckBox { Content = Copy($"{pose} → {action}"), IsChecked = rule.Enabled };
            toggle.IsCheckedChanged += (_, _) =>
            {
                var changed = rules.ToList();
                changed[index] = rule with { Enabled = toggle.IsChecked == true };
                if (!SaveRules(changed)) RefreshRuleList();
            };
            var edit = new Button { Content = "Изменить" };
            Avalonia.Automation.AutomationProperties.SetName(edit, $"Изменить правило: {pose}");
            edit.Click += (_, _) =>
            {
                automationGesture.SelectedIndex = rule.Gesture;
                automationDestination.SelectedIndex = rule.Action;
                SetEditingRule(index);
                automationDestination.Focus();
            };
            var test = new Button { Content = "Проверить" };
            Avalonia.Automation.AutomationProperties.SetName(test, $"Проверить правило: {pose}");
            test.Click += (_, _) =>
            {
                RunNavigation(true, action, RuleTrigger(RulePose(rule)));
                ruleSaveStatus.BringIntoView();
            };
            var remove = new Button { Content = "Удалить" };
            Avalonia.Automation.AutomationProperties.SetName(remove, $"Удалить правило: {pose}");
            remove.Click += (_, _) =>
            {
                var changed = rules.ToList();
                changed.RemoveAt(index);
                if (!SaveRules(changed)) return;
                if (editingRule == index) SetEditingRule(null);
                else if (editingRule is int edited && edited > index) SetEditingRule(edited - 1);
                ruleSaveStatus.Text = "Правило удалено.";
            };
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
            row.Children.Add(toggle);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { edit, test, remove } };
            Grid.SetColumn(buttons, 1);
            row.Children.Add(buttons);
            ruleList.Children.Add(row);
        }
        ruleList.Children.Add(Copy("При включённой мыши отдельные позы доступны для Telegram, запуска приложений и разрешённых медиажестов. Для яркости выключите мышь.", 13, true));
    }

    bool CanShareMouse(ActiveRule rule) =>
        (rule.Target is TelegramAction or OpenExplorerAction or OpenCalculatorAction ||
         (mediaGestureEnabled.IsChecked == true && rule.Target.StartsWith("Медиа:", StringComparison.Ordinal))) &&
        (rule.Trigger is GestureKind.Victory or GestureKind.ThumbsUp or GestureKind.ThumbsDown or GestureKind.ThreeFingers or GestureKind.Shaka);

    bool CanShareAutomationWithMouse() => !trainingActive && activeRules.Any(CanShareMouse);

    bool ObserveRules(CameraFrame frame, bool usingMouse)
    {
        ObservePendingTelegramPhoto(frame, usingMouse);
        if (automationEnabled.IsChecked != true) return false;
        bool reserveMouse = false;
        foreach (var rule in activeRules)
        {
            if (usingMouse && !CanShareMouse(rule)) continue;
            if (rule.Target == BrightnessAction)
            {
                ObserveBrightness(frame);
                continue;
            }
            if (usingMouse && (frame.Gesture?.Candidate == rule.Trigger || frame.Gesture?.Stable == rule.Trigger))
                reserveMouse = true;
            if (!rule.Gate.Observe(frame.Gesture, rule.Trigger, automationClock.Elapsed)) continue;
            if (trainingActive)
                ruleSaveStatus.Text = $"Тренировка: «{rule.Target}» сработало бы.";
            else
            {
                if (rule.Target == TelegramPhotoAction)
                    QueueTelegramPhoto();
                else
                    RunNavigation(false, rule.Target, rule.Trigger);
            }
        }
        return reserveMouse;
    }

    void UpdateRuleAssignmentHint()
    {
        if (ruleAssignmentHint == null) return;
        var rule = new GestureRule(automationGesture.SelectedIndex, automationDestination.SelectedIndex, true);
        int pose = RulePose(rule);
        ruleAssignmentHint.Text = rules.Where((_, index) => index != editingRule).Any(existing => RulePose(existing) == pose)
            ? "Эта поза уже назначена в текущем профиле."
            : pose == 1 && wheelEnabled.IsChecked == true ? "Кулак используется для прокрутки при включённой мыши."
            : pose == 2 && (clickTrigger.SelectedIndex == 1 || clickKind.SelectedIndex == 3)
                ? "Щипок используется для клика или перетаскивания при включённой мыши."
            : "";
    }

    void SetTrainingMode()
    {
        trainingActive = training.IsChecked == true;
        if (trainingActive)
        {
            autoMousePending = false;
            mouseEnabled.IsChecked = false;
            automationEnabled.IsChecked = true;
        }
        else automationEnabled.IsChecked = false;
        RefreshActiveRules();
        RefreshShell();
    }
}