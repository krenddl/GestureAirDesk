using Avalonia;
using Avalonia.Platform;
using GestureFlow.Windows;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using GestureFlow.Core;
using GestureFlow.Vision;
using System.Diagnostics;
namespace GestureFlow.Desktop;

public sealed partial class MainWindow : Window
{
    readonly CameraService camera = new();
    readonly WindowManager windowManager=new();
    readonly ComboBox windowPicker=new(){Width=600};
    readonly TextBlock windowStatus=new(){Text="Обновите список и выберите окно.",TextWrapping=Avalonia.Media.TextWrapping.Wrap};
    Window? windowDemo;
    void RefreshWindows(){
        windowGestureEnabled.IsChecked=false;
        var previous=(windowPicker.SelectedItem as WindowTarget)?.Handle;
        var windows=windowManager.List(this.TryGetPlatformHandle()?.Handle??IntPtr.Zero);
        windowPicker.ItemsSource=windows;windowPicker.SelectedItem=windows.FirstOrDefault(w=>w.Handle==previous);
    }
    readonly ControlProfileStore profileStore=new(DataDirectory.File("profiles.json"));
    readonly List<AppSettings> savedProfiles=new();
    readonly ListBox profilePicker = new() { MinHeight = 125, MaxHeight = 300 };
    bool profilesWritable=true;
    readonly ComboBox trackingPreset=new(){ItemsSource=new[]{"Сбалансированный","Плавный","Быстрый"},SelectedIndex=0,Width=300};
    readonly TextBlock activePreset=new(){Text="Камера не запущена. Отклик задаётся в Настройках.",TextWrapping=Avalonia.Media.TextWrapping.Wrap};
    readonly ComboBox mouseDisplay=new(){Width=360};
    sealed record DisplayChoice(int Number,PixelRect Bounds,bool Primary){
        public override string ToString()=>$"Экран {Number}: {Bounds.Width}×{Bounds.Height}"+(Primary?" · основной":"");
    }
    void RefreshDisplays(){
        mouseEnabled.IsChecked=false;
        var previous=(mouseDisplay.SelectedItem as DisplayChoice)?.Bounds;
        var choices=Screens.All.Select((s,i)=>new DisplayChoice(i+1,s.Bounds,s.IsPrimary)).ToArray();
        mouseDisplay.ItemsSource=choices;
        mouseDisplay.SelectedItem=choices.FirstOrDefault(c=>c.Bounds==previous)??choices.FirstOrDefault(c=>c.Primary)??choices.FirstOrDefault();
    }
    readonly DragGestureProcessor drag = new();
    bool systemButtonHeld;
    (int X, int Y)? lastGesturePointer;
    (int X, int Y) pinchDragOffset;
    TimeSpan lastMouseFrame;
    readonly ComboBox clickKind = new() { ItemsSource = new[] { "Левый клик", "Правый клик", "Двойной клик", "Перетаскивание" }, SelectedIndex = 0, Width = 260 };
    MouseClickKind SelectedClick => (MouseClickKind)Math.Max(0, clickKind.SelectedIndex);
    readonly GestureFlow.Windows.WindowsIntegration mouseIntegration = new();
    readonly CheckBox mouseEnabled = new() { Content = "Управлять мышью Windows (эксперимент)" };
    readonly TextBlock mouseStatus = new() { Text = "Мышь Windows выключена.", TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    readonly NavigationAutomation mouseClick = new(TimeSpan.FromMilliseconds(350));
    readonly DwellClickProcessor dwellClick = new();
    readonly MotionContinuityGuard motionGuard = new();
    bool autoMousePending;
    readonly CheckBox autoStartMouse=new() { Content="Включать мышь после запуска камеры" };
    readonly ComboBox clickTrigger = new() { ItemsSource=new[]{"Задержка курсора","Щипок"},SelectedIndex=0 };
    readonly ProgressBar dwellProgress = new() { Minimum=0,Maximum=1,Height=4,Value=0 };
    readonly WheelGestureProcessor wheel = new(3000);
    readonly ScrollPoseGate scrollPose=new();
    readonly CheckBox wheelEnabled = new() { Content = "Прокрутка кулаком в режиме мыши" };
    readonly AirDeskPointerView airPointer = new();
    readonly TextBlock ruleSaveStatus = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    readonly CheckBox mediaGestureEnabled = new() { Content = "Разрешить медиажесты в других окнах" };
    readonly IWindowsIntegration windowsIntegration = new GestureFlow.Windows.WindowsIntegration();
    readonly TextBlock mediaStatus = new() { Text = "Команда не отправлялась.", TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    readonly SessionHistoryStore historyStore = new(DataDirectory.File("history.json"));
    readonly List<SessionSummary> history = new();
    StackPanel? historyTable;
    string historyError = "";
    bool historyWritable = true;
    DateTimeOffset sessionStarted;
    readonly CheckBox automationEnabled = new() { Content = "Включить правило на этот запуск" };
    readonly ComboBox automationGesture = new() { ItemsSource = new[] { "Открытая ладонь", "Кулак", "Щипок", "V-жест", "Большой палец вверх", "Три пальца", "Шака", "Большой палец вниз" }, SelectedIndex = 2, Width = 260 };
    const string NextTrackAction = "Медиа: следующий трек";
    const string PreviousTrackAction = "Медиа: предыдущий трек";
    readonly ComboBox automationDestination = new() { ItemsSource = new[] { "Камера", "Жесты", "Аналитика", "Настройки", "AirDesk/Palm Menu", "Медиа: тише", "Медиа: громче", "Медиа: звук вкл/выкл", "Медиа: пауза", TelegramAction, OpenExplorerAction, OpenCalculatorAction, BrightnessAction, NextTrackAction, PreviousTrackAction, TelegramPhotoAction }, SelectedIndex = 2, Width = 260 };
    readonly TextBlock automationLog = new() { Text = "Срабатываний пока нет.", TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    readonly Queue<string> automationEntries = new();
    readonly Stopwatch automationClock = Stopwatch.StartNew();
    readonly PalmMenuState palmState = new();
    readonly CheckBox palmEnabled = new() { Content = "Открывать Palm Menu жестом ладони" };
    readonly Border palmPanel = new() { IsVisible = false, Padding = new Thickness(16), Margin = new Thickness(12), BorderThickness = new Thickness(1), BorderBrush = Avalonia.Media.Brushes.DimGray };
    readonly TextBlock palmHint = new() { Text = "Панель закрыта", TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    readonly CheckBox recognitionToggle = new() { Content = "Рука и жесты · экспериментальный режим" };
    readonly TextBlock recognitionStatus = new() { Text = "Распознавание отключено", TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    string lastRecognition = "отключено";
    readonly TextBlock gestureStatus = new() { Text = "Жест: —", TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    readonly TextBlock gestureDetails = new() { Text = "Камера не запущена.", TextWrapping = Avalonia.Media.TextWrapping.Wrap };

    long gestureConfirmations;
    readonly SettingsStore settingsStore = new();
    AppSettings settings = new();
    readonly TextBlock settingsStatus = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    readonly TextBlock metrics = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    readonly CheckBox mirror = new() { Content = "Зеркальное отображение камеры" };
    readonly TextBox profile = new() { Watermark = "Название профиля", MaxLength = 60, Width = 180, HorizontalAlignment = HorizontalAlignment.Left };
    readonly Stopwatch session = new();
    long frames; double fps; long intervalFrames; long intervalStart;
    bool restoringControlSettings;
    bool closeRequested;
    readonly ComboBox devices = new() { Width = 180 };
    readonly Button search = new() { Content = "Найти камеры" }, start = new() { Content = "Старт", IsEnabled = false }, stop = new() { Content = "Стоп", IsEnabled = false };
    readonly TextBlock status = new() { Text = "Нажмите «Найти камеры».", TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    readonly Image image = new() { Height = 420, Stretch = Avalonia.Media.Stretch.Uniform };
    readonly ContentControl page = new();
    readonly StackPanel cameraPage = new() { Spacing = 16 };
    CancellationTokenSource? cts; Task? operation; bool busy, closing; int pending;
    public MainWindow()
    {
        TransparencyLevelHint = new[] { WindowTransparencyLevel.None };
        SystemDecorations=SystemDecorations.None;
        TransparencyBackgroundFallback = Avalonia.Media.Brush.Parse("#F2F3F6");
        Background = Avalonia.Media.Brushes.Transparent;
        WindowState = Avalonia.Controls.WindowState.Normal; WindowStartupLocation=WindowStartupLocation.CenterScreen; Title = "GestureFlow AirDesk · 0.37"; Width = 1180; Height = 692; MinWidth = 980; MinHeight = 640;
        try { history.AddRange(PostgreSqlProfiles.Enabled ? PostgreSqlHistory.Load(historyStore) : historyStore.Load()); } catch (Exception ex) { historyWritable = false; historyError = "Не удалось прочитать историю; файл не будет перезаписан: " + ex.Message; }
        RefreshHistory();
        try { settings = settingsStore.Load(); } catch (Exception ex) { settingsStatus.Text = "Не удалось загрузить настройки: " + ex.Message; }
        automationGesture.SelectedIndex = settings.AutomationGesture; automationDestination.SelectedIndex = settings.AutomationAction;
        LoadRules();
        training.IsCheckedChanged += (_, _) => SetTrainingMode();
        if(PostgreSqlProfiles.Enabled) { profileStore.DatabaseLoad=PostgreSqlProfiles.LoadProfiles; profileStore.DatabaseSave=PostgreSqlProfiles.SaveProfiles; }
        try{savedProfiles.AddRange(profileStore.Load());}catch(Exception ex){profilesWritable=false;settingsStatus.Text="Профили не прочитаны; файл не будет перезаписан: "+ex.Message;}
        RefreshProfiles();
        autoStartMouse.IsChecked=settings.AutoStartMouse;clickKind.SelectedIndex=settings.ClickMode;clickTrigger.SelectedIndex=settings.ClickMode==3?1:settings.ClickTrigger;wheelEnabled.IsChecked=settings.WheelEnabled;
        airPointer.LoadArea(settings.PointerInsetX,settings.PointerInsetY);
        airPointer.Started += () => { windowGestureEnabled.IsChecked=false; mouseEnabled.IsChecked = false; automationEnabled.IsChecked = false; mediaGestureEnabled.IsChecked = false; palmEnabled.IsChecked = false; SetPalmOpen(false); };
        airPointer.SaveArea += (x, y) =>
        {
            var updated = settings with { PointerInsetX = x, PointerInsetY = y };
            try { settingsStore.Save(updated); settings = updated; airPointer.AreaSaved("Область сохранена."); }
            catch (Exception ex) { airPointer.AreaSaved("Не удалось сохранить: " + ex.Message); }
        };
        InitializeWindowGestures();
        Opened += (_, _) => { RefreshDisplays(); RegisterGlobalHotkeys(); CreateTrayMenu(); };
        Closed += (_, _) => { DisposeTrayMenu(); UnregisterGlobalHotkeys(); };
        mouseDisplay.SelectionChanged+=(_,_)=>mouseEnabled.IsChecked=false;
        autoStartMouse.IsCheckedChanged+=(_,_)=> { if(autoStartMouse.IsChecked!=true)autoMousePending=false;SaveControlPreferences(); };
        clickTrigger.SelectionChanged += (_, _) =>
        {
            if (clickKind.SelectedIndex == 3 && clickTrigger.SelectedIndex != 1)
                clickTrigger.SelectedIndex = 1;
            else
                ResetMouseForClickChange();
        };
        clickKind.SelectionChanged += (_, _) =>
        {
            if (clickKind.SelectedIndex == 3 && clickTrigger.SelectedIndex != 1)
                clickTrigger.SelectedIndex = 1;
            else
                ResetMouseForClickChange();
        };
        wheelEnabled.IsCheckedChanged += (_, _) => { scrollPose.Reset();wheel.Reset();SaveControlPreferences(); };
        mouseEnabled.IsCheckedChanged += (_, _) =>
        {
            if (mouseEnabled.IsChecked == true)
            {
                windowGestureEnabled.IsChecked=false;
                if(clickKind.SelectedIndex==3 && clickTrigger.SelectedIndex==0) { mouseEnabled.IsChecked=false;mouseStatus.Text="Для перетаскивания выберите щипок.";return; }
                if(mouseDisplay.SelectedItem==null){mouseEnabled.IsChecked=false;mouseStatus.Text="Выберите доступный экран.";return;}
                if (cts == null || recognitionToggle.IsChecked != true) { mouseEnabled.IsChecked = false; mouseStatus.Text = "Сначала запустите камеру с распознаванием."; return; }
                airPointer.Stop(); if (!CanShareAutomationWithMouse()) automationEnabled.IsChecked = false; palmEnabled.IsChecked = false; SetPalmOpen(false);
                motionGuard.Reset();lastGesturePointer = null;lastMouseFrame = automationClock.Elapsed; drag.Observe(false, null); wheel.Reset(); mouseClick.Configure(true); dwellClick.Configure(true);dwellProgress.Value=0; mouseStatus.Text = "Мышь включена. Удерживайте Escape для остановки.";
            }
            else { scrollPose.Reset();autoMousePending=false;motionGuard.Reset();dwellClick.Configure(false);dwellProgress.Value=0; wheel.Reset(); mouseClick.Configure(false); mouseStatus.Text = "Мышь Windows выключена."; ReleaseDrag(); }
        };
        var escapeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        escapeTimer.Tick += (_, _) =>
        {
            if (mouseIntegration.EscapeHeld)
            {
                autoMousePending = false;
                automationEnabled.IsChecked = false;
                mediaGestureEnabled.IsChecked = false;
                mouseEnabled.IsChecked = false;
            }
            CheckWindowGestureStop();
            if (mouseEnabled.IsChecked == true && automationClock.Elapsed - lastMouseFrame > TimeSpan.FromMilliseconds(500))
                PauseWindowsMouse("Жду кадр камеры. Управление возобновится автоматически.");
            if (mouseEnabled.IsChecked != true && systemButtonHeld) ReleaseDrag();
        };
        escapeTimer.Start(); Closed += (_, _) => escapeTimer.Stop();
        airPointer.Navigate += target => Show(target);
        automationEnabled.IsCheckedChanged += (_, _) =>
        {
            bool enabled = automationEnabled.IsChecked == true;
            if (enabled)
            {
                if (!CanShareAutomationWithMouse())
                {
                    autoMousePending = false;
                    mouseEnabled.IsChecked = false;
                }
                windowGestureEnabled.IsChecked = false;
                airPointer.Stop();
                palmEnabled.IsChecked = false;
                SetPalmOpen(false);
            }
            RefreshActiveRules();
            RefreshShell();
        };
        automationGesture.SelectionChanged += (_, _) => ResetAutomationAfterSelection();
        automationDestination.SelectionChanged += (_, _) => ResetAutomationAfterSelection();
        mediaGestureEnabled.IsCheckedChanged += (_, _) => RefreshActiveRules();
        Deactivated += (_, _) => { windowGestureEnabled.IsChecked = false; airPointer.Stop(); };
        trackingPreset.SelectedIndex=settings.TrackingPresetIndex;
        mirror.IsChecked = settings.MirrorPreview; profile.Text = settings.ProfileName;
        mirror.Content="Зеркально отражать изображение и курсор";
        mirror.IsCheckedChanged+=(_,_)=> {
            mouseEnabled.IsChecked=false;airPointer.Stop();windowGestureEnabled.IsChecked=false;
            var next=settings with { MirrorPreview=mirror.IsChecked==true };
            try {
                if(!restoringControlSettings)settingsStore.Save(next);
                settings=next;
                image.RenderTransformOrigin=new RelativePoint(.5,.5,RelativeUnit.Relative);
                image.RenderTransform=new Avalonia.Media.ScaleTransform(settings.MirrorPreview?-1:1,1);
                settingsStatus.Text=settings.MirrorPreview?"Зеркало включено и сохранено.":"Зеркало выключено и сохранено.";
            }
            catch(Exception ex) {
                settingsStatus.Text="Не удалось сохранить зеркало: "+ex.Message;
                Dispatcher.UIThread.Post(()=>mirror.IsChecked=settings.MirrorPreview);
            }
        };
        devices.SelectionChanged += (_, _) => { if (!busy && devices.SelectedItem is CameraDevice d) { settings = settings with { CameraIndex = d.Index }; SaveSettings(); SetBusy(false); } };
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) => { UpdateMetrics(); RefreshShell(); }; timer.Start(); Closed += (_, _) => timer.Stop();
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        BuildCameraPage();
        var shortcuts = new StackPanel { Spacing = 10 };
        shortcuts.Children.Add(new TextBlock { Text = "Palm Menu · демонстрация", FontSize = 20 });
        var shortcutRow = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var destination in new[] { "Камера", "Жесты", "Аналитика", "Настройки" })
        {
            var shortcut = new Button { Content = destination, Margin = new Thickness(4) };
            shortcut.Click += (_, _) => { Show(destination); SetPalmOpen(false); };
            shortcutRow.Children.Add(shortcut);
        }
        var dismiss = new Button { Content = "Закрыть панель", Margin = new Thickness(4) };
        dismiss.Click += (_, _) => SetPalmOpen(false); shortcutRow.Children.Add(dismiss);
        shortcuts.Children.Add(shortcutRow); palmPanel.Child = shortcuts;
        palmEnabled.IsCheckedChanged += (_, _) => { if (palmEnabled.IsChecked != true) SetPalmOpen(false); };
        KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Escape) { autoMousePending=false; windowGestureEnabled.IsChecked=false; mouseEnabled.IsChecked = false; airPointer.Stop(); SetPalmOpen(false); mediaGestureEnabled.IsChecked = false; automationEnabled.IsChecked = false; e.Handled = true; } };
        InitializeGestureStrip();
        Content = BuildShell(); Show("AirDesk/Palm Menu");
        search.Click += async (_, _) => await Discover();
        start.Click += async (_, _) => await Start();
        stop.Click += (_, _) => { autoMousePending=false; windowGestureEnabled.IsChecked=false; mouseEnabled.IsChecked = false; cts?.Cancel(); stop.IsEnabled = false; status.Text = "Остановка…"; };
        Closing += async (_, e) =>
        {
            windowDemo?.Close();mouseEnabled.IsChecked = false; ReleaseDrag();
            if (closing) return;
            if (closeRequested) { e.Cancel = true; return; }
            closeRequested = true;
            automationEnabled.IsChecked = false;
            if (!busy) return;
            e.Cancel = true;
            cts?.Cancel();
            try
            {
                if (operation != null) await operation;

            }
            finally { closing = true; Close(); }
        };
    }
    void Show(string name)
    {
        SelectNavigation(name);
        if(name=="Дополнительно") { ShowAdvanced();return; }
        if (name != "AirDesk/Palm Menu") airPointer.Stop();
        if (name == "Камера") { page.Content = cameraPage; return; }
        var panel = new StackPanel { Spacing = 18 };
        if (name is "Настройки" or "Профили")
        {
            Detach(settingsStatus);
            var parameters = new StackPanel { Spacing = 8 };
            if (name == "Профили")
            {
                foreach (var control in new Control[] { profilePicker, profile, trackingPreset }) Detach(control);
                var apply = new Button { Content = "Применить профиль", IsEnabled = profilePicker.SelectedItem != null };
                void UpdateApply(object? sender, SelectionChangedEventArgs args) => apply.IsEnabled = profilePicker.SelectedItem != null;
                apply.AttachedToVisualTree += (_, _) => profilePicker.SelectionChanged += UpdateApply;
                apply.DetachedFromVisualTree += (_, _) => profilePicker.SelectionChanged -= UpdateApply;
                apply.Click += (_, _) => ApplyProfile();
                var saveProfile = new Button { Content = "Сохранить профиль" };
                saveProfile.Classes.Add("primary");
                saveProfile.Click += (_, _) => { SaveProfile(); if (profilePicker.Parent == null && savedProfiles.Count > 0) Show("Профили"); };
                var saved = Card(new StackPanel { Spacing = 12, Children = {
                    Copy("Сохранённые профили", 18), savedProfiles.Count == 0 ? Copy("Сохранённых профилей пока нет.", 14, true) : profilePicker, apply
                } });
                var edit = Card(new StackPanel { Spacing = 12, Children = {
                    Copy("Настройки профиля", 18), SettingRow("Название", profile),
                    SettingRow("Отклик", trackingPreset), saveProfile, settingsStatus
                } });
                var columns = new Grid { ColumnDefinitions = new ColumnDefinitions("*,1.3*"), ColumnSpacing = 14 };
                saved.VerticalAlignment = VerticalAlignment.Top;
                edit.VerticalAlignment = VerticalAlignment.Top;
                columns.Children.Add(saved);
                Grid.SetColumn(edit, 1);
                columns.Children.Add(edit);
                StackWhenNarrow(columns, edit, "*,1.3*", 740);
                page.Content = columns;
                return;
            }
            Detach(showGestureStrip);
            parameters.Children.Add(showGestureStrip);
            parameters.Children.Add(ToggleRow("Зеркало камеры", mirror));
            parameters.Children.Add(Separator());
            parameters.Children.Add(ToggleRow("Автоматически включать мышь", autoStartMouse));
            parameters.Children.Add(Separator());
            Detach(clickTrigger);
            parameters.Children.Add(SettingRow("Способ клика", clickTrigger));
            var guide = new Button { Content = "Как управлять  →" };
            guide.Click += (_, _) => ShowControlGuide();
            parameters.Children.Add(guide);
            var save = new Button { Content = "Сохранить настройки" };
            save.Click += (_, _) =>
            {
                var nameValue = settings.ProfileName;
                if (string.IsNullOrWhiteSpace(nameValue)) { settingsStatus.Text = "Введите название профиля."; return; }
                settings = settings with { ProfileName = nameValue, MirrorPreview = mirror.IsChecked == true,TrackingPresetIndex=trackingPreset.SelectedIndex,ClickMode=clickKind.SelectedIndex,ClickTrigger=clickTrigger.SelectedIndex,AutoStartMouse=autoStartMouse.IsChecked==true,WheelEnabled=wheelEnabled.IsChecked==true }; SaveSettings();
            };
            save.Classes.Add("primary"); parameters.Children.Add(save); parameters.Children.Add(settingsStatus); parameters.Children.Add(Separator()); parameters.Children.Add(BuildHotkeyHelp()); var settingsCard = Card(parameters); settingsCard.VerticalAlignment = VerticalAlignment.Top; page.Content = settingsCard; return;
        }
        else if (name == "AirDesk/Palm Menu")
        {
            AddAirDeskHero(panel);
            var mousePage = new StackPanel { Spacing = 14 };
            var demoPage = new StackPanel { Spacing = 14, Margin = new Thickness(0, 16) };
            mousePage.Children.Add(new TextBlock { Text = "1. В Камере включите распознавание и нажмите Старт. 2. Выберите действие щипка. 3. Включите управление мышью. Остановка — удерживать Escape.", TextWrapping = Avalonia.Media.TextWrapping.Wrap });
            Detach(mouseDisplay);mousePage.Children.Add(new TextBlock{Text="Экран для управления"});mousePage.Children.Add(mouseDisplay);
            var refreshDisplays=new Button{Content="Обновить экраны"};refreshDisplays.Click+=(_,_)=>RefreshDisplays();mousePage.Children.Add(refreshDisplays);
            Detach(mouseEnabled); Detach(mouseStatus); Detach(wheelEnabled); mousePage.Children.Add(mouseEnabled); mousePage.Children.Add(wheelEnabled); mousePage.Children.Add(mouseStatus);
            Detach(clickKind); mousePage.Children.Add(new TextBlock { Text = "Действие щипка" }); mousePage.Children.Add(clickKind);
            var clickReport = new TextBlock { Text = "Левых: 0 · правых: 0 · двойных: 0", TextWrapping = Avalonia.Media.TextWrapping.Wrap };
            int leftCount = 0, rightCount = 0, doubleCount = 0;
            void Report() => clickReport.Text = $"Левых: {leftCount} · правых: {rightCount} · двойных: {doubleCount}";
            var clickTarget = new Button { Content = "Тестовая площадка клика", Height = 50, Width = 320 };
            clickTarget.Click += (_, _) => { leftCount++; Report(); };
            clickTarget.DoubleTapped += (_, _) => { doubleCount++; Report(); };
            clickTarget.PointerPressed += (_, e) => { if (e.GetCurrentPoint(clickTarget).Properties.IsRightButtonPressed) { rightCount++; Report(); } };
            clickTarget.ContextMenu = new ContextMenu { ItemsSource = new[] { new MenuItem { Header = "Правый клик получен" } } };
            var runTest = new Button { Content = "Проверить выбранный клик на площадке" };
            runTest.Click += async (_, _) =>
            {
                mouseEnabled.IsChecked = false; airPointer.Stop();
                try { var point = clickTarget.PointToScreen(new Point(clickTarget.Bounds.Width / 2, clickTarget.Bounds.Height / 2)); mouseIntegration.MovePointerPixels(point.X, point.Y); if (clickKind.SelectedIndex == 3) { try { mouseIntegration.SetLeftButton(true); systemButtonHeld = true; await Task.Delay(80); mouseIntegration.MovePointerPixels(point.X + 50, point.Y); await Task.Delay(80); } finally { ReleaseDrag(); } await Task.Delay(50); clickReport.Text = mouseIntegration.LeftButtonHeld ? "Кнопка всё ещё нажата — отпустите обычную мышь или нажмите Escape." : "Тест удержания завершён: левая кнопка отпущена."; } else mouseIntegration.Click(SelectedClick); }
                catch (Exception ex) { clickReport.Text = "Ошибка теста: " + ex.Message; }
            };
            mousePage.Children.Add(clickTarget); mousePage.Children.Add(runTest); mousePage.Children.Add(clickReport);
            mousePage.Children.Add(new TextBlock { Text = "Палец двигает курсор выбранного экрана, щипок — выбранный тип клика. Сначала отпустите щипок. Управление работает и в других обычных окнах. Удерживайте Escape для остановки. Камера Стоп также отключает мышь. Для прокрутки включите опцию, сожмите кулак и двигайте руку вверх-вниз. В режиме перетаскивания удерживайте щипок и двигайте руку; разожмите для отпускания.", TextWrapping = Avalonia.Media.TextWrapping.Wrap });
            ArrangeMouseCards(mousePage);
            Detach(airPointer); demoPage.Children.Add(airPointer);
            Detach(palmEnabled); Detach(palmHint); demoPage.Children.Add(palmEnabled); demoPage.Children.Add(palmHint);
            var demo = new Button { Content = "Открыть панель вручную" };
            demo.Click += (_, _) => SetPalmOpen(true); demoPage.Children.Add(demo);
            demoPage.Children.Add(new TextBlock { Text = "Включите управление панелью и распознавание камеры. Подтверждённая ладонь открывает панель, кулак закрывает. Потеря руки и остановка камеры закрывают её. Escape закрывает вручную. Кнопки панели переключают разделы приложения. Выбор щипком доступен в панели указателя выше; системная мышь не перемещается.", TextWrapping = Avalonia.Media.TextWrapping.Wrap });
            Detach(airPointer.CalibrationPanel);
            var areaPage = new StackPanel { Spacing = 14, Margin = new Thickness(0, 16) };
            areaPage.Children.Add(new TextBlock { Text = "Уменьшите центральную область кадра, если неудобно тянуть руку к краям. Изменение области отключает управление. После настройки нажмите «Сохранить область».", TextWrapping = Avalonia.Media.TextWrapping.Wrap });
            areaPage.Children.Add(airPointer.CalibrationPanel);
            var tabs = new TabControl
            {
                ItemsSource = new[]{

            new TabItem{Header="Рабочая область",Content=areaPage},
            new TabItem{Header="Palm Menu",Content=demoPage}
        },
                SelectedIndex = 0
            };
            tabs.SelectionChanged += (_, e) => { if (ReferenceEquals(e.Source, tabs)) { mouseEnabled.IsChecked = false; airPointer.Stop(); SetPalmOpen(false); } };
            panel.Children.Add(mousePage);

            workspaceAdvanced=tabs;
        }
        else if (name == "Автоматизация")
        {
            LoadRules();
            foreach (var control in new Control[] { automationEnabled, automationGesture, automationDestination, automationLog, mediaGestureEnabled, ruleSaveStatus }) Detach(control);
            var windowsLink = new Button { Content = "Команды выбранному окну  →" };
            windowsLink.Click += (_, _) => Show("Окна Windows");
            panel.Children.Add(windowsLink);
            panel.Children.Add(automationEnabled); panel.Children.Add(mediaGestureEnabled);
            automationGesture.IsEnabled = !BrightnessSelected;
            panel.Children.Add(SettingRow("Когда", automationGesture));
            panel.Children.Add(SettingRow("Действие", automationDestination));
            panel.Children.Add(BuildRuleEditorButtons());
            panel.Children.Add(ruleSaveStatus);
            var demo = new Button { Content = "Проверить действие без камеры" };
            demo.Click += (_, _) => RunNavigation(true); panel.Children.Add(demo);
            panel.Children.Add(new TextBlock { Text = "Назначьте разные позы для переходов, Telegram, приложений и медиакоманд. Медиажесты включаются отдельно и работают в других окнах. Escape отключает их. После включения сначала отпустите выбранный жест. Удержание не повторяет действие, пауза между срабатываниями — 2 секунды. Выбор можно сохранить; включение правила и медиакоманд не сохраняется.", TextWrapping = Avalonia.Media.TextWrapping.Wrap });
            panel.Children.Add(automationLog);
            panel.Children.Add(new TextBlock { Text = "Медиа Windows · ручное управление", FontSize = 20 });
            var mediaButtons = new WrapPanel { Orientation = Orientation.Horizontal };
            foreach (var entry in new[] { ("Тише", MediaCommand.VolumeDown), ("Громче", MediaCommand.VolumeUp), ("Звук вкл/выкл", MediaCommand.Mute), ("Воспроизведение / пауза", MediaCommand.PlayPause), ("Предыдущий трек", MediaCommand.PreviousTrack), ("Следующий трек", MediaCommand.NextTrack) })
            {
                var mediaButton = new Button { Content = entry.Item1, Margin = new Thickness(4), IsEnabled = windowsIntegration.IsAvailable };
                mediaButton.Click += (_, _) => { try { windowsIntegration.Execute(entry.Item2); mediaStatus.Text = "Отправлена команда: " + entry.Item1 + ". Подтверждение от проигрывателя не запрашивается."; } catch (Exception ex) { mediaStatus.Text = "Ошибка медиакоманды: " + ex.Message; } };
                mediaButtons.Children.Add(mediaButton);
            }
            panel.Children.Add(mediaButtons); Detach(mediaStatus); panel.Children.Add(mediaStatus);
        }
                else if(name=="Окна Windows"){
            var back = new Button { Content = "←  К автоматизации" }; back.Click += (_, _) => Show("Автоматизация"); panel.Children.Add(back);
            Detach(windowPicker);Detach(windowStatus);
            panel.Children.Add(new TextBlock{Text="Команды применяются к выбранному окну. Ниже можно связать одну команду с жестом на этот запуск.",TextWrapping=Avalonia.Media.TextWrapping.Wrap});
            panel.Children.Add(windowPicker);
            var refresh=new Button{Content="Обновить список окон"};refresh.Click+=(_,_)=>{try{RefreshWindows();windowStatus.Text="Список обновлён.";}catch(Exception ex){windowStatus.Text=ex.Message;}};panel.Children.Add(refresh);
            var actions=new WrapPanel();
            foreach(var entry in new[]{("Свернуть",WindowAction.Minimize),("Развернуть",WindowAction.Maximize),("Восстановить",WindowAction.Restore)}){
                var button=new Button{Content=entry.Item1,Margin=new Thickness(4)};
                button.Click+=async (_,_)=>{
                    if(windowPicker.SelectedItem is not WindowTarget target){windowStatus.Text="Сначала выберите окно.";return;}
                    mouseEnabled.IsChecked=false;
                    try{windowManager.Execute(target,entry.Item2);await Task.Delay(200);windowStatus.Text=target.Title+": "+windowManager.State(target);}
                    catch(Exception ex){windowStatus.Text="Ошибка: "+ex.Message;}
                };actions.Children.Add(button);
            }
            panel.Children.Add(actions);
            var demo=new Button{Content="Создать тестовое окно GestureFlow"};
            demo.Click+=(_,_)=>{
                mouseEnabled.IsChecked=false;
                if(windowDemo==null){
                    windowDemo=new Window{Title="GestureFlow · тестовое окно",Width=500,Height=300,Content=new TextBlock{Text="Окно для проверки сворачивания, разворачивания и восстановления.",Margin=new Thickness(24),TextWrapping=Avalonia.Media.TextWrapping.Wrap}};
                    windowDemo.Closed+=(_,_)=>windowDemo=null;windowDemo.Show();
                }
                RefreshWindows();if(windowPicker.ItemsSource is IEnumerable<WindowTarget> targets)windowPicker.SelectedItem=targets.FirstOrDefault(w=>w.Title=="GestureFlow · тестовое окно");
                Activate();windowStatus.Text="Выбрано тестовое окно.";
            };panel.Children.Add(demo); AddWindowGestureControls(panel); panel.Children.Add(windowStatus);
        }
        else if (name == "Жесты")
        {
            Detach(gestureDetails); panel.Children.Add(gestureDetails);
            panel.Children.Add(new TextBlock { Text = "Открытая ладонь — выпрямите пальцы.\nКулак — согните четыре пальца.\nЩипок — соедините большой и указательный пальцы перед ладонью.\nУдерживайте позу: время зависит от режима отклика, минимум три кадра.\nСейчас это диагностика: системные действия не выполняются.", TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        }
        else if (name == "Аналитика")
        {
            historyTable = new StackPanel();
            RefreshHistory();
            panel.Children.Add(BuildHistoryExport());
            panel.Children.Add(Card(historyTable));
            Detach(metrics);
            UpdateMetrics();
            panel.Children.Add(new Expander { Header = "Текущий сеанс", Content = Card(new StackPanel { Children = { metrics } }) });
        }
        else
        {
            panel.Children.Add(new TextBlock { Text = name == "Жесты" ? "Профиль: " + settings.ProfileName + "\nТочки руки доступны в экспериментальном режиме на странице камеры. Классификация жестов пока отсутствует." : "Заготовка следующего этапа. Функции пока не подключены.", TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        }
        ArrangeSecondaryPage(name, panel);
        page.Content = panel;
    }
    static void Detach(Control control) { if (control.Parent is Panel p) p.Children.Remove(control); }
    void SaveSettings()
    {
        try { settingsStore.Save(settings); settingsStatus.Text = "Настройки сохранены."; }
        catch (Exception ex) { settingsStatus.Text = "Не удалось сохранить настройки: " + ex.Message; }
    }
    void UpdateMetrics() => metrics.Text = $"Профиль: {settings.ProfileName}\nКадров показано: {frames}\nЧастота отображения: {fps:F1} кадр/с\nВремя захвата: {session.Elapsed:hh\\:mm\\:ss}\nРаспознавание: {lastRecognition}\nПодтверждений жестов: {gestureConfirmations}";
    void SetBusy(bool value) { busy = value; recognitionToggle.IsEnabled = !value; search.IsEnabled = !value; devices.IsEnabled = !value; start.IsEnabled = !value && devices.SelectedItem != null; stop.IsEnabled = value && cts != null; }
    async Task Discover()
    {
        if (busy || closeRequested) return;
        SetBusy(true); status.Text = "Поиск камер 0–5…";
        operation = DiscoverCore(); await operation; SetBusy(false);
    }
    async Task DiscoverCore()
    {
        try
        {
            var list = await camera.DiscoverAsync(); devices.ItemsSource = list; devices.SelectedItem = list.FirstOrDefault(d => d.Index == settings.CameraIndex) ?? list.FirstOrDefault();
            if (devices.SelectedItem is CameraDevice selected) { settings = settings with { CameraIndex = selected.Index }; SaveSettings(); }
            status.Text = list.Count > 0 ? "Выберите камеру и нажмите «Старт»." : "Камеры не найдены. Проверьте подключение и разрешения Windows → Камера.";
        }
        catch (Exception ex) { status.Text = Error(ex); }
    }
    async Task Start()
    {
        if (busy || closeRequested || devices.SelectedItem is not CameraDevice device) return;
        autoMousePending = settings.AutoStartMouse && recognitionToggle.IsChecked == true && (automationEnabled.IsChecked != true || CanShareAutomationWithMouse());
        sessionStarted = DateTimeOffset.Now; gestureConfirmations = 0; UpdateGesture(null); frames = 0; fps = 0; intervalFrames = 0; intervalStart = 0; session.Restart();
        lastRecognition = recognitionToggle.IsChecked == true ? "загрузка моделей" : "отключено";
        cts = new(); SetBusy(true); status.Text = "Открытие камеры…";
        operation = Capture(device.Index, cts.Token); await operation; cts.Dispose(); cts = null; SetBusy(false);
    }
    async Task Capture(int index, CancellationToken token)
    {
        string outcome = "Завершён"; var sessionProfile = settings.ProfileName;
        try
        {
            bool recognize = recognitionToggle.IsChecked == true;
            var preset=(TrackingPreset)settings.TrackingPresetIndex;
            activePreset.Text="Отклик текущего сеанса: "+new[]{"Сбалансированный","Плавный","Быстрый"}[settings.TrackingPresetIndex];
            await camera.RunAsync(index, frame =>
            {
                if (Interlocked.Exchange(ref pending, 1) != 0) return;
                Dispatcher.UIThread.Post(() =>
                {
                    try
                    {
                        if (token.IsCancellationRequested) return;
                        using var stream = new MemoryStream(frame.Jpeg); var bitmap = new Bitmap(stream); var old = image.Source as Bitmap; image.Source = bitmap; old?.Dispose();
                        image.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
                        image.RenderTransform = new Avalonia.Media.ScaleTransform(settings.MirrorPreview ? -1 : 1, 1);
                        frames++; intervalFrames++;
                        if(autoMousePending && frame.Hand!=null) { autoMousePending=false;mouseEnabled.IsChecked=true; }
                        var elapsed = session.ElapsedMilliseconds;
                        if (elapsed - intervalStart >= 1000) { fps = intervalFrames * 1000.0 / (elapsed - intervalStart); intervalFrames = 0; intervalStart = elapsed; }
                        status.Text = "Камера работает";
                        recognitionStatus.Text = frame.RecognitionStatus; lastRecognition = frame.RecognitionStatus;
                        bool reserveGesture = ObserveRules(frame, mouseEnabled.IsChecked == true);
                        if (reserveGesture)
                        {
                            lastMouseFrame = automationClock.Elapsed;
                            PauseWindowsMouse("Смените жест, чтобы продолжить управление мышью.");
                        }
                        else UpdateWindowsMouse(frame);
                        if (frame.Gesture != null) gestureConfirmations = frame.Gesture.Confirmations;
                        ObserveWindowGesture(frame.Gesture); airPointer.Update(frame, settings.MirrorPreview); UpdateGesture(frame.Gesture);
                    }
                    catch (Exception ex) { status.Text = Error(ex); cts?.Cancel(); }
                    finally { Interlocked.Exchange(ref pending, 0); }
                });
            }, token, recognize,preset); status.Text = "Камера остановлена.";
        }
        catch (Exception ex) { outcome = "Ошибка захвата"; status.Text = Error(ex); }
        finally { brightnessGesture.Reset(); autoMousePending=false;windowGestureEnabled.IsChecked=false; mouseEnabled.IsChecked = false; airPointer.Stop(); UpdateGesture(null); recognitionStatus.Text = "Захват остановлен"; lastRecognition = "захват остановлен"; session.Stop(); if (frames > 0) SaveSession(new(sessionStarted, sessionProfile, index, session.Elapsed.TotalSeconds, frames, gestureConfirmations, outcome)); fps = 0; var old = image.Source as Bitmap; image.Source = null; old?.Dispose(); }
    }
    static string GestureName(GestureKind kind) => kind switch
    {
        GestureKind.OpenPalm => "Открытая ладонь",
        GestureKind.Fist => "Кулак",
        GestureKind.Pinch => "Щипок",
        GestureKind.Victory => "V-жест",
        GestureKind.ThumbsUp => "Большой палец вверх",
        GestureKind.ThumbsDown => "Большой палец вниз",
        GestureKind.ThreeFingers => "Три пальца",
        GestureKind.Shaka => "Шака",
        GestureKind.NoHand => "Рука не найдена",
        _ => "Не определён"
    };
    void UpdateGesture(GestureSnapshot? gesture)
    {

        if (palmEnabled.IsChecked == true) palmState.Apply(true, gesture); else if (gesture == null) palmState.Close(); RefreshPalm();
        var text = gesture == null ? "Жест: —" : $"Жест: {GestureName(gesture.Stable)}";
        gestureStatus.Text = text;
        if (!StripShortcutVisible && gestureStrip?.IsVisible == true && stripGesture != null && cts is { IsCancellationRequested: false }) stripGesture.Text = text;
        trainingProgress = gesture == null ? "Жду руку." : $"Поза: {GestureName(gesture.Candidate)} · удержание {gesture.HoldProgress:P0}";
        gestureDetails.Text = gesture == null ? "Запустите камеру с включённым экспериментальным режимом." :
            $"{text}\nКандидат: {GestureName(gesture.Candidate)}\nУдержание: {gesture.HoldProgress:P0}\nПодтверждений за сеанс: {gesture.Confirmations}\nМедиажесты работают только при отдельном включении в Автоматизации.";
    }
    void SetPalmOpen(bool open) { if (open) palmState.Open(); else palmState.Close(); RefreshPalm(); }
    void RefreshPalm() { palmPanel.IsVisible = palmState.IsOpen; palmHint.Text = palmState.IsOpen ? "Панель открыта" : "Панель закрыта"; }
    void ResetAutomationAfterSelection() => automationGesture.IsEnabled = !BrightnessSelected;

    void RunNavigation(bool test, string? target = null, GestureKind? trigger = null)
    {
        target ??= automationDestination.SelectedItem as string;
        if (target == null) return;
        if (trainingActive)
        {
            ruleSaveStatus.Text = $"Тренировка: выбрано действие «{target}».";
            return;
        }
        if (target == BrightnessAction)
        {
            ruleSaveStatus.Text = "Для проверки проведите открытой ладонью вверх или вниз перед камерой.";
            return;
        }
        if (target.StartsWith("Медиа:"))
        {
            if (mediaGestureEnabled.IsChecked != true) { ruleSaveStatus.Text = "Включите разрешение медиажестов в разделе «Действия»."; return; }
            try
            {
                var command = target switch { "Медиа: тише" => MediaCommand.VolumeDown, "Медиа: громче" => MediaCommand.VolumeUp, "Медиа: звук вкл/выкл" => MediaCommand.Mute,
                    NextTrackAction => MediaCommand.NextTrack, PreviousTrackAction => MediaCommand.PreviousTrack,
                    "Медиа: пауза" => MediaCommand.PlayPause, _ => throw new ArgumentOutOfRangeException(nameof(target)) };
                windowsIntegration.Execute(command); mediaStatus.Text = "Отправлено: " + target;
            }
            catch (Exception ex) { mediaGestureEnabled.IsChecked = false; mediaStatus.Text = "Ошибка: " + ex.Message; return; }
        }
        if (target == TelegramPhotoAction)
        {
            ruleSaveStatus.Text = "Для фото запустите камеру и покажите назначенный жест.";
            return;
        }
        if (target == TelegramAction && !OpenTelegramAction()) return;
        bool launchesWindowsApp = target is OpenExplorerAction or OpenCalculatorAction;
        if (launchesWindowsApp && !OpenWindowsApp(target)) return;
        automationEntries.Enqueue($"{DateTime.Now:HH:mm:ss} · {(test ? "Тест кнопкой" : GestureName(trigger ?? RuleTrigger(automationGesture.SelectedIndex)))} → {target}");
        while (automationEntries.Count > 10) automationEntries.Dequeue();
        automationLog.Text = string.Join(Environment.NewLine, automationEntries.Reverse());
        if (target != TelegramAction && !launchesWindowsApp && !target.StartsWith("Медиа:")) Show(target); SetPalmOpen(false);
    }
    void SaveSession(SessionSummary summary)
    {
        history.Insert(0, summary); if (history.Count > 30) history.RemoveRange(30, history.Count - 30);
        if (historyWritable)
        {
            try { if (PostgreSqlProfiles.Enabled) PostgreSqlHistory.Save(history); historyStore.Save(history); historyError = ""; }
            catch (Exception ex) { historyError = "Сеанс есть в памяти, но не сохранён: " + ex.Message; }
        }
        RefreshHistory();
    }
    void RefreshHistory()
    {
        if (exportHistoryButton != null) exportHistoryButton.IsEnabled = history.Count > 0 && !exportingHistory;
        if (historyTable is not { } table) return;
        table.Children.Clear();
        if (historyError.Length > 0) table.Children.Add(Copy(historyError, 13, true));
        table.Children.Add(HistoryRow(true, "Начало", "Профиль", "Длительность", "Кадры"));
        if (history.Count == 0) table.Children.Add(Copy("Завершённых сеансов пока нет.", 14, true));
        foreach (var item in history)
        {
            table.Children.Add(Separator());
            var row = HistoryRow(false, item.Started.LocalDateTime.ToString("dd.MM.yyyy HH:mm"),
                item.Profile, TimeSpan.FromSeconds(item.DurationSeconds).ToString(@"hh\:mm\:ss"), item.DisplayedFrames.ToString("N0"));
            ToolTip.SetTip(row, $"Камера {item.CameraIndex} · {item.AverageFps:F1} кадр/с · жестов: {item.GestureConfirmations} · {item.Outcome}");
            table.Children.Add(row);
        }
    }
    void UpdateWindowsMouse(CameraFrame frame)
    {
        if (mouseEnabled.IsChecked != true) return;
        if (mouseIntegration.EscapeHeld) { mouseEnabled.IsChecked = false; return; }
        lastMouseFrame = automationClock.Elapsed;
        if (frame.Hand == null)
        {
            PauseWindowsMouse("Жду руку. Управление возобновится автоматически.");
            return;
        }
        if (!motionGuard.Observe(frame.Hand, automationClock.Elapsed))
        {
            PauseWindowsMouse("Положение руки изменилось. Жду следующий кадр.");
            return;
        }
        var point = frame.Hand?.Points.ElementAtOrDefault(8);
        var projected = point == null ? null : PointerProjection.Project(point, frame.Width, frame.Height, settings.MirrorPreview, settings.PointerInsetX, settings.PointerInsetY);
        if (projected == null) { dwellClick.Reset();dwellProgress.Value=0; ReleaseDrag(); wheel.Reset(); mouseClick.Observe(null, GestureKind.Pinch, automationClock.Elapsed); mouseStatus.Text = "Рука не найдена; курсор остановлен."; return; }
        try
        {
            if(mouseDisplay.SelectedItem is not DisplayChoice selectedDisplay || !Screens.All.Any(s=>s.Bounds==selectedDisplay.Bounds))throw new InvalidOperationException("Выбранный экран недоступен. Обновите список экранов.");
            bool fist = scrollPose.Observe(clickKind.SelectedIndex!=3 && wheelEnabled.IsChecked==true,frame.Gesture,automationClock.Elapsed);
            var wrist = frame.Hand?.Points.ElementAtOrDefault(0);
            int scroll = wheel.Observe(fist, wrist == null || frame.Height <= 0 ? double.NaN : wrist.Y / frame.Height, automationClock.Elapsed);
            if (clickKind.SelectedIndex != 3 && wheelEnabled.IsChecked == true && (fist || frame.Gesture?.Candidate == GestureKind.Fist))
            {
                dwellClick.Reset();dwellProgress.Value=0;mouseClick.Configure(true);
                if (scroll != 0) mouseIntegration.Scroll(scroll);
                mouseStatus.Text = fist ? (scroll==0?"Кулак принят: двигайте кисть вверх/вниз над нужным окном.":$"Прокрутка отправлена: {scroll}.") : "Подтверждение кулака…";
                return;
            }
                        if(mouseDisplay.SelectedItem is not DisplayChoice display || !Screens.All.Any(s=>s.Bounds==display.Bounds))
                throw new InvalidOperationException("Выбранный экран изменился или отключён. Обновите список экранов.");
            var bounds=display.Bounds;
            var pixel=ScreenProjection.Map(projected.Value.X,projected.Value.Y,bounds.X,bounds.Y,bounds.Width,bounds.Height)
                ?? throw new InvalidOperationException("Некорректные границы экрана.");
            bool pinchCandidate = frame.Gesture?.Candidate == GestureKind.Pinch;
            if (!pinchCandidate || clickTrigger.SelectedIndex != 1)
            {
                if (!systemButtonHeld)
                {
                    mouseIntegration.MovePointerPixels(pixel.X, pixel.Y);
                    lastGesturePointer = pixel;
                }
            }
            if (clickKind.SelectedIndex == 3)
            {
                var transition = drag.Observe(true, frame.Gesture);
                if (transition == DragTransition.Press)
                {
                    var anchor = lastGesturePointer ?? pixel;
                    pinchDragOffset = (anchor.X - pixel.X, anchor.Y - pixel.Y);
                    mouseIntegration.SetLeftButton(true);
                    systemButtonHeld = true;
                }
                else if (transition == DragTransition.Release) ReleaseDrag();
                else if (systemButtonHeld)
                {
                    int x = Math.Clamp(pixel.X + pinchDragOffset.X, bounds.X, bounds.Right - 1);
                    int y = Math.Clamp(pixel.Y + pinchDragOffset.Y, bounds.Y, bounds.Bottom - 1);
                    mouseIntegration.MovePointerPixels(x, y);
                }
                mouseStatus.Text = systemButtonHeld ? "Перетаскивание: кнопка удерживается. Escape — отпустить." : "Перетаскивание: отпустите щипок, затем зажмите.";
                return;
            }
            if(clickTrigger.SelectedIndex==0) {
                bool click=dwellClick.Observe(pixel.X,pixel.Y,automationClock.Elapsed,20*RenderScaling);
                dwellProgress.Value=dwellClick.Progress;
                if(click)mouseIntegration.Click(SelectedClick);
                mouseStatus.Text=click?"Клик выполнен. Отведите курсор для следующего.":dwellClick.WaitingForMovement?"Сдвиньте курсор, затем удерживайте на цели.":$"Удерживайте на цели: {dwellClick.Progress:P0}";
            }
            else {
                dwellProgress.Value=0;
                bool click = mouseClick.Observe(frame.Gesture, GestureKind.Pinch, automationClock.Elapsed);
                if (click) mouseIntegration.Click(SelectedClick);
                mouseStatus.Text = click ? "Клик выполнен. Разожмите пальцы." : "Щипок — клик. Разожмите пальцы перед следующим кликом.";
            }
        }
        catch (Exception ex) { mouseEnabled.IsChecked = false; mouseStatus.Text = "Мышь отключена: " + ex.Message; }
    }
    void ResetMouseForClickChange()
    {
        PauseWindowsMouse(mouseEnabled.IsChecked == true ? "Способ клика обновлён. Начните с раскрытой кисти." : "Мышь выключена. Включите управление для клика.");
        SaveControlPreferences();
    }

    void PauseWindowsMouse(string message)
    {
        lastGesturePointer = null;
        motionGuard.Reset();
        dwellClick.Reset();
        dwellProgress.Value = 0;
        scrollPose.Reset();
        wheel.Reset();
        mouseClick.Configure(true);
        ReleaseDrag();
        mouseStatus.Text = message;
    }

    void ReleaseDrag()
    {
        drag.Observe(false, null);
        if (!systemButtonHeld) return;
        try { mouseIntegration.SetLeftButton(false); systemButtonHeld = false; }
        catch (Exception ex) { mouseStatus.Text = "Не удалось отпустить кнопку: " + ex.Message; }
    }
        void RefreshProfiles(){profilePicker.ItemsSource=savedProfiles.Select(p=>p.ProfileName).ToArray();profilePicker.SelectedIndex=savedProfiles.FindIndex(p=>p.ProfileName==settings.ProfileName);}
    void SaveProfile(){
        if(!profilesWritable){settingsStatus.Text="Файл профилей повреждён: запись отключена.";return;}
        var area=airPointer.CurrentArea;
        var snapshot=settings with {ProfileName=profile.Text?.Trim()??"",MirrorPreview=mirror.IsChecked==true,
            TrackingPresetIndex=trackingPreset.SelectedIndex,AutomationGesture=automationGesture.SelectedIndex,
            AutomationAction=automationDestination.SelectedIndex,PointerInsetX=area.X,PointerInsetY=area.Y,
            ClickMode=clickKind.SelectedIndex,ClickTrigger=clickTrigger.SelectedIndex,AutoStartMouse=autoStartMouse.IsChecked==true,WheelEnabled=wheelEnabled.IsChecked==true};
        var next=savedProfiles.Where(p=>!string.Equals(p.ProfileName,snapshot.ProfileName,StringComparison.OrdinalIgnoreCase)).Append(snapshot).ToArray();
        try{profileStore.Save(next);savedProfiles.Clear();savedProfiles.AddRange(next);RefreshProfiles();profilePicker.SelectedItem=snapshot.ProfileName;settingsStatus.Text="Профиль сохранён: "+snapshot.ProfileName;}
        catch(Exception ex){settingsStatus.Text="Не удалось сохранить профиль: "+ex.Message;}
    }
    void SaveControlPreferences() {
        if(restoringControlSettings||clickKind.SelectedIndex<0||clickTrigger.SelectedIndex<0)return;
        var next=settings with {ClickMode=clickKind.SelectedIndex,ClickTrigger=clickTrigger.SelectedIndex,AutoStartMouse=autoStartMouse.IsChecked==true,WheelEnabled=wheelEnabled.IsChecked==true};
        try { settingsStore.Save(next);settings=next; }
        catch(Exception ex) { mouseStatus.Text="Не удалось сохранить управление: "+ex.Message; }
    }
    void ApplyProfile(){
        if(busy||cts!=null){settingsStatus.Text="Сначала остановите камеру.";return;}
        var selected=savedProfiles.FirstOrDefault(p=>p.ProfileName==(profilePicker.SelectedItem as string));
        if(selected==null){settingsStatus.Text="Выберите сохранённый профиль.";return;}
        try{
            restoringControlSettings=true;settingsStore.Save(selected);mouseEnabled.IsChecked=false;airPointer.Stop();automationEnabled.IsChecked=false;mediaGestureEnabled.IsChecked=false;palmEnabled.IsChecked=false;
            settings=selected; LoadRules(); profile.Text=settings.ProfileName;mirror.IsChecked=settings.MirrorPreview;
            trackingPreset.SelectedIndex=settings.TrackingPresetIndex;autoStartMouse.IsChecked=settings.AutoStartMouse;clickKind.SelectedIndex=settings.ClickMode;clickTrigger.SelectedIndex=settings.ClickMode==3?1:settings.ClickTrigger;wheelEnabled.IsChecked=settings.WheelEnabled;
            automationGesture.SelectedIndex=settings.AutomationGesture;automationDestination.SelectedIndex=settings.AutomationAction;
            airPointer.LoadArea(settings.PointerInsetX,settings.PointerInsetY);
            if(devices.ItemsSource is IEnumerable<CameraDevice> available)devices.SelectedItem=available.FirstOrDefault(d=>d.Index==settings.CameraIndex);
            SetBusy(false);settingsStatus.Text="Применён профиль: "+settings.ProfileName+". Управление выключено.";
        }catch(Exception ex){settingsStatus.Text="Не удалось применить профиль: "+ex.Message;}
        finally { restoringControlSettings=false; }
    }
    static string Error(Exception ex) => $"Ошибка камеры: {ex.Message}\nПроверьте доступ к камере, закройте другие приложения и установите Visual C++ Redistributable x64.";
}



















































