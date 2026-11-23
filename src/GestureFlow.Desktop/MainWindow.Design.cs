using GestureFlow.Core;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Data;
using Avalonia.Controls.Primitives;

namespace GestureFlow.Desktop;

public sealed partial class MainWindow
{
    const int PageInset = 20;
    const int SidebarWidth = 158;
    const int PanelGap = 16;
    const int PreviewHeight = 350;
    ScrollViewer? pageScroll;
    TextBlock? ruleState;

    readonly Dictionary<string, Button> navigationButtons = new();
    readonly TextBlock shellLocation = new();
    Avalonia.Controls.Shapes.Path? cameraStatusIcon;
    Line? cameraOffSlash;
    readonly TextBlock shellCamera = new() { FontSize = 13 };
    TextBlock? shellControl;
    readonly StackPanel cameraPlaceholder = new() { Spacing = 10, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    StackPanel? workspaceEmpty;
    Control? workspaceAdvanced; Control? clickTesting;
    static IBrush Ink(string color) => Brush.Parse(color);
    static TextBlock Copy(string text, double size = 14, bool muted = false)
    {
        var label = new TextBlock { Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap };
        if (muted) label.Classes.Add("muted");
        return label;
    }
    static Border Card(Control child)
    {
        var result = new Border { Child = child }; result.Classes.Add("card"); return result;
    }
    static TextBlock Eyebrow(string text) => Copy(text, 12, true);
    static Avalonia.Controls.Shapes.Path LineIcon(string geometry, double size = 20) => new()
    {
        Data = Geometry.Parse(geometry), Stroke = Ink("#697386"), StrokeThickness = 1.6,
        Width = size, Height = size, Stretch = Stretch.Uniform
    };
    static Border Separator() => new() { Height = 1, Background = Ink("#E2D3C5") };
    Control BuildShell()
    {
        var side = new DockPanel { Margin = new Thickness(8, 16, 8, 12) };
        var nav = new StackPanel { Spacing = 6 };
        foreach (var entry in new[]
        {
            ("AirDesk/Palm Menu", "Управление", "M4 1 L4 21 L10 15 L15 23 L19 20 L13 13 L22 12 Z"),
            ("Камера", "Камера", "M2 6 L6 6 L8 3 L15 3 L17 6 L21 6 L21 20 L2 20 Z M16 13 A5 5 0 1 1 6 13 A5 5 0 1 1 16 13"),
            ("Жесты", "Жесты", "M6 18 L3 11 Q2 8 5 10 L7 12 L7 4 Q8 1 9 4 L9 10 L10 2 Q11 0 12 3 L12 10 L14 4 Q16 2 16 5 L15 12 L17 9 Q20 7 19 11 L17 17 Q13 22 6 18 Z"),
            ("Автоматизация", "Действия", "M12 1 L3 12 L10 12 L8 21 L19 9 L12 9 Z"),
            ("Профили", "Профили", "M2 7 L11 2 L20 7 L11 12 Z M2 12 L11 17 L20 12 M2 17 L11 22 L20 17"),
            ("Аналитика", "История", "M22 12 A10 10 0 1 1 2 12 A10 10 0 1 1 22 12 M12 5 L12 12 L17 15"),
            ("Настройки", "Настройки", "M9 2 L15 2 L16 5 L19 6 L22 6 L23 12 L20 14 L19 17 L19 20 L13 22 L11 19 L8 18 L5 19 L2 14 L4 11 L5 8 L5 5 Z M16 12 A4 4 0 1 1 8 12 A4 4 0 1 1 16 12")
        })
        {
            var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            content.Children.Add(LineIcon(entry.Item3,17));
            content.Children.Add(new TextBlock { Text = entry.Item2, FontSize=15, VerticalAlignment = VerticalAlignment.Center });
            var button = new Button { Content = content };
            Avalonia.Automation.AutomationProperties.SetName(button, entry.Item2);
            button.Classes.Add("nav"); var destination = entry.Item1; button.Click += (_, _) => Show(destination);
            navigationButtons[destination] = button; nav.Children.Add(button);
        }
        side.Children.Add(nav);
        var sidebar = new Border { Background = Ink("#EFE2D3"), BorderBrush = Ink("#80FFFFFF"), BorderThickness = new Thickness(0,0,1,0), Child = side };
        var main = new DockPanel { Background = Ink("#FAF4EC") };
        var top = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(PageInset, 16, PageInset, 12) };
        shellLocation.FontSize = 29; shellLocation.FontWeight = FontWeight.SemiBold; shellLocation.VerticalAlignment = VerticalAlignment.Center;
        top.Children.Add(shellLocation);
        shellCamera.VerticalAlignment = VerticalAlignment.Center; shellCamera.Margin = new Thickness(8,0,20,0);
        var cameraIndicator=new StackPanel { Orientation=Orientation.Horizontal,Spacing=10,VerticalAlignment=VerticalAlignment.Center };
        cameraStatusIcon = LineIcon("M2 5 L13 5 L13 17 L2 17 Z M13 9 L20 5 L20 17 L13 13", 20);
        cameraOffSlash = new Line
        {
            StartPoint = new Point(2, 2),
            EndPoint = new Point(20, 20),
            Stroke = Ink("#737D8B"),
            StrokeThickness = 1.6
        };
        var cameraIconSlot = new Grid { Width = 22, Height = 22, Children = { cameraStatusIcon, cameraOffSlash } };
        cameraIndicator.Children.Add(cameraIconSlot);
        cameraIndicator.Children.Add(shellCamera);
        var stopAll = new Button();
        SetActionContent(stopAll, "Остановить всё", "M1 1 H11 V11 H1 Z");
        stopAll.Classes.Add("danger"); stopAll.Click += (_, _) => StopEverything(); Grid.SetColumn(stopAll,1); top.Children.Add(stopAll);
        var bar = new Border { Child = top };
        DockPanel.SetDock(bar,Dock.Top); main.Children.Add(bar);
        page.Margin = new Thickness(PageInset, 4, PageInset, 18);
        pageScroll = new ScrollViewer { Content = page, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        main.Children.Add(pageScroll);
        var root = new Grid { ColumnDefinitions = new ColumnDefinitions($"{SidebarWidth},*") };
        root.Children.Add(sidebar); Grid.SetColumn(main,1); root.Children.Add(main);
        var outer = new DockPanel(); DockPanel.SetDock(palmPanel,Dock.Bottom); outer.Children.Add(palmPanel); outer.Children.Add(root);
                var title = new Grid { Height=46,ColumnDefinitions=new ColumnDefinitions("*,Auto"),Background=Ink("#F7EEE4") };
        title.PointerPressed+=(_,e)=> { if(e.GetPosition(title).X < title.Bounds.Width - 150 && e.GetCurrentPoint(title).Properties.IsLeftButtonPressed) BeginMoveDrag(e); };
        var caption=new StackPanel { Orientation=Orientation.Horizontal,Spacing=8,Margin=new Thickness(12,0),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center };
        caption.Children.Add(LineIcon("M6 18 L3 11 Q2 8 5 10 L7 12 L7 4 Q8 1 9 4 L9 10 L10 2 Q11 0 12 3 L12 10 L14 4 Q16 2 16 5 L15 12 L17 9 Q20 7 19 11 L17 17 Q13 22 6 18 Z",22));
        caption.Children.Add(new TextBlock { Text="GestureFlow AirDesk",FontSize=17,FontWeight=FontWeight.SemiBold });
        caption.Children.Add(cameraIndicator); title.Children.Add(caption);
        var chrome=new StackPanel { Orientation=Orientation.Horizontal,Spacing=0,Margin=new Thickness(0,0,4,0),VerticalAlignment=VerticalAlignment.Center };
        foreach(var command in new[]{("Свернуть","−"),("Развернуть","□"),("Закрыть","×")}) {
            var button=new Button { Content=command.Item2,Width=34,Height=38,MinHeight=0,Padding=new Thickness(0),HorizontalContentAlignment=HorizontalAlignment.Center,
                Background=Brushes.Transparent,BorderThickness=new Thickness(0) };
            Avalonia.Automation.AutomationProperties.SetName(button,command.Item1);button.Classes.Add("windowControl");
            var action=command.Item1;button.Click+=(_,_)=> { if(action=="Свернуть")WindowState=WindowState.Minimized;else if(action=="Развернуть")WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;else Close(); };
            chrome.Children.Add(button);
        }
        Grid.SetColumn(chrome,1); title.Children.Add(chrome);
        var frame=new DockPanel();DockPanel.SetDock(title,Dock.Top);frame.Children.Add(title);frame.Children.Add(outer);
                
        RefreshShell();return new Border { Child = frame, Background = Ink("#FAF4EC"), BorderBrush = Ink("#E2D3C5"), BorderThickness = new Thickness(1) };
    }
    void SelectNavigation(string name)
    {
        pageScroll?.ScrollToHome();
        var selected = name switch { "Окна Windows" => "Автоматизация", "Дополнительно" => "AirDesk/Palm Menu", _ => name };
        foreach (var (key, button) in navigationButtons)
        {
            var active = key == selected;
            if (active) button.Classes.Add("selected"); else button.Classes.Remove("selected");
            if (button.Content is StackPanel content && content.Children[0] is Avalonia.Controls.Shapes.Path icon)
                icon.Stroke = active ? Brushes.White : Ink("#5E4A3D");
        }
        shellLocation.Text = name switch { "AirDesk/Palm Menu" => "Управление", "Автоматизация" => "Действия", "Аналитика" => "История", _ => name };
        RefreshShell();
    }
    void RefreshShell()
    {
        UpdateTrayMenu();
        UpdateRuleAssignmentHint();
        bool capturing = cts != null && session.IsRunning && !cts.IsCancellationRequested;
        if (cameraStatusIcon != null)
            cameraStatusIcon.Stroke = Ink(capturing ? "#28785A" : "#737D8B");
        if (cameraOffSlash != null)
            cameraOffSlash.IsVisible = !capturing;
        if (ruleState != null)
        {
            ruleState.Text = automationEnabled.IsChecked != true ? "Правила выключены."
                : !capturing ? "Запустите камеру с распознаванием руки."
                : recognitionToggle.IsChecked != true ? "Остановите камеру, включите распознавание руки и запустите снова."
                : trainingActive ? $"Тренировка · {trainingProgress}"
                : $"Включённых правил: {activeRules.Length}. {gestureStatus.Text}";
        }
        UpdateGestureStrip(capturing);
        shellCamera.Text = capturing ? "Камера включена" : "Камера выключена";
        shellCamera.Foreground = Ink(capturing ? "#246749" : "#64584E");
        bool controlling = mouseEnabled.IsChecked == true || windowGestureEnabled.IsChecked == true || automationEnabled.IsChecked == true;
        if (shellControl != null) shellControl.Text = controlling ? "● Управление активно" : "Управление выключено";
        if (shellControl != null) shellControl.Foreground = Ink(controlling ? "#246749" : "#64584E");
        cameraPlaceholder.IsVisible = image.Source == null;
        if(workspaceEmpty != null) workspaceEmpty.IsVisible = image.Source == null;
    }
    void StopEverything()
    {
        startCameraAfterDiscovery = false;
        autoMousePending=false;windowGestureEnabled.IsChecked = false; mouseEnabled.IsChecked = false;
        automationEnabled.IsChecked = false; mediaGestureEnabled.IsChecked = false;
        palmEnabled.IsChecked = false; airPointer.Stop(); SetPalmOpen(false);
        ReleaseDrag(); cts?.Cancel(); windowStatus.Text = "Правило окна остановлено.";
        status.Text = cts == null ? "Камера выключена. Управление остановлено." : "Остановка…"; RefreshShell();
    }
    void BuildCameraPage()
    {
        cameraPage.Spacing = 18;


        var setup = new StackPanel { Spacing = 10 };
        setup.Children.Add(Copy("Устройство камеры", 17));
        devices.PlaceholderText = "Выберите камеру";
        devices.Width = double.NaN;
        devices.HorizontalAlignment = HorizontalAlignment.Stretch;
        setup.Children.Add(devices);
        setup.Children.Add(search);
        
        setup.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { start, stop } });
        recognitionToggle.Content = "Распознавать руку";
        setup.Children.Add(recognitionToggle);
        status.MaxWidth = 245;
        setup.Children.Add(status);
        recognitionStatus.Classes.Add("muted");
        setup.Children.Add(recognitionStatus);
        activePreset.Classes.Add("muted");
        var viewport = new Grid { Height = PreviewHeight }; image.Height = PreviewHeight; viewport.Children.Add(image);
        cameraPlaceholder.Children.Add(LineIcon("M1 5 L14 5 L14 18 L1 18 Z M14 9 L22 5 L22 18 L14 14",32));
        cameraPlaceholder.Children.Add(Copy("Нет изображения",18));
        cameraPlaceholder.Children.Add(Copy("Найдите камеру и нажмите «Старт».",12,true));
        viewport.Children.Add(cameraPlaceholder);
        var frame = new Border { Child = viewport, Background = Ink("#F0E5DA"), CornerRadius = new CornerRadius(10), ClipToBounds = true };
        var cameraGesture = Copy("");
        cameraGesture.Bind(TextBlock.TextProperty, new Binding("Text") { Source = gestureStatus });
        var cameraPreview = new StackPanel { Spacing = 8, Children = {
            frame, new Border { Child = cameraGesture, Background = Ink("#F1E5D9"), Padding = new Thickness(10), CornerRadius = new CornerRadius(7) }
        } };
        var columns = new Grid { ColumnDefinitions = new ColumnDefinitions("*,290"), ColumnSpacing = PanelGap };
        columns.Children.Add(cameraPreview);
        var options = new Border { Child = setup };
        options.VerticalAlignment = VerticalAlignment.Top;
        Grid.SetColumn(options, 1);
        columns.Children.Add(options);
        StackWhenNarrow(columns, options, "*,290", 650);
        cameraPage.Children.Add(columns);
        start.Classes.Add("primary"); stop.Classes.Add("danger");
    }
    void AddAirDeskHero(StackPanel panel)
    {
        panel.Spacing = 14;
    }
    static Control ToggleRow(string label, CheckBox source)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), MinHeight = 52 };
        row.Children.Add(new TextBlock { Text=label, FontSize=15, VerticalAlignment=VerticalAlignment.Center });
        var toggle = new ToggleButton { VerticalAlignment=VerticalAlignment.Center }; toggle.Classes.Add("switch");
        bool updating=false;
        void Sync(object? sender,Avalonia.Interactivity.RoutedEventArgs e) {
            if(updating)return;
            updating=true;try { toggle.IsChecked=source.IsChecked; } finally { updating=false; }
        }
        toggle.IsChecked=source.IsChecked;
        toggle.IsCheckedChanged+=(_,_)=> {
            if(updating)return;
            updating=true;
            try { source.IsChecked=toggle.IsChecked; toggle.IsChecked=source.IsChecked; }
            finally { updating=false; }
        };
        toggle.AttachedToVisualTree+=(_,_)=> { source.IsCheckedChanged+=Sync;Sync(null,new Avalonia.Interactivity.RoutedEventArgs()); };
        toggle.DetachedFromVisualTree+=(_,_)=>source.IsCheckedChanged-=Sync;
        Avalonia.Automation.AutomationProperties.SetName(toggle,label);
        Grid.SetColumn(toggle,1); row.Children.Add(toggle); return row;
    }
    static void SetActionContent(Button button, string label, string geometry)
    {
        var icon = new Avalonia.Controls.Shapes.Path
        {
            Data = Geometry.Parse(geometry),
            Width = 12,
            Height = 12,
            Stretch = Stretch.Uniform,
            VerticalAlignment = VerticalAlignment.Center
        };
        icon.Bind(Avalonia.Controls.Shapes.Shape.FillProperty, new Binding(nameof(Button.Foreground)) { Source = button });
        var text = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center };
        button.Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { icon, text } };
        Avalonia.Automation.AutomationProperties.SetName(button, label);
    }

    void ArrangeMouseCards(StackPanel mousePage)
    {
        var controls=mousePage.Children.ToArray(); mousePage.Children.Clear();
        var setup = new StackPanel();
        var enable = new Button { HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center, MinHeight = 43 };
        enable.Classes.Add("primary");
        void UpdateEnable(object? sender, Avalonia.Interactivity.RoutedEventArgs args) =>
            SetActionContent(enable, mouseEnabled.IsChecked == true ? "Выключить управление" : "Включить управление",
                mouseEnabled.IsChecked == true ? "M1 1 H11 V11 H1 Z" : "M2 0 L12 6 L2 12 Z");
        enable.AttachedToVisualTree += (_, _) =>
        {
            mouseEnabled.IsCheckedChanged += UpdateEnable;
            UpdateEnable(null, new Avalonia.Interactivity.RoutedEventArgs());
        };
        enable.DetachedFromVisualTree += (_, _) => mouseEnabled.IsCheckedChanged -= UpdateEnable;
        enable.Click += (_, _) => mouseEnabled.IsChecked = mouseEnabled.IsChecked != true;
        enable.Margin = new Thickness(0, 0, 0, 8);
        setup.Children.Add(enable);
        
        Detach(clickTrigger);Detach(dwellProgress);
        ToolTip.SetTip(clickTrigger, HotkeyLabel('4'));
        setup.Children.Add(SettingRow("Способ клика", clickTrigger));
        setup.Children.Add(Separator());
        ToolTip.SetTip(clickKind, HotkeyLabel('5'));
        setup.Children.Add(SettingRow("Действие", clickKind));
        setup.Children.Add(Separator());
        var scrollRow = ToggleRow("Прокрутка", wheelEnabled);
        ToolTip.SetTip(scrollRow, HotkeyLabel('6'));
        setup.Children.Add(scrollRow);
        var more = new Button { Content = "Дополнительно  →" };
        more.Click += (_, _) => Show("Дополнительно");
        more.Classes.Add("quiet");
        var help = new Button { Content = "Как управлять  →" };
        help.Click += (_, _) => ShowControlGuide();
        help.Classes.Add("quiet");
        mouseDisplay.Width=double.NaN; mouseDisplay.HorizontalAlignment=HorizontalAlignment.Stretch;
        clickTrigger.Classes.Add("controlChoice");
        clickKind.Classes.Add("controlChoice");
        clickKind.ClearValue(WidthProperty);
        clickKind.HorizontalAlignment = HorizontalAlignment.Right;
        clickTrigger.HorizontalAlignment = HorizontalAlignment.Right;
        mouseStatus.Classes.Add("muted"); mouseStatus.FontSize=12;
        var clickHint=Copy("",12,true);clickHint.Bind(TextBlock.TextProperty,new Binding("Text") { Source=mouseStatus });
        setup.Children.Add(clickHint);

        
        var preview=new Image { Height=PreviewHeight,Stretch=Stretch.Uniform };
        preview.Bind(Image.SourceProperty,new Binding("Source") { Source=image });
        preview.Bind(Visual.RenderTransformProperty,new Binding("RenderTransform") { Source=image });
        preview.RenderTransformOrigin=new RelativePoint(.5,.5,RelativeUnit.Relative);
        var viewport=new Grid { Height=PreviewHeight,ClipToBounds=true,Background=Ink("#F1E5D9") };
        viewport.Children.Add(preview);
        var openCamera = new Button { Content = "Открыть камеру  →", HorizontalAlignment = HorizontalAlignment.Center };
        openCamera.Classes.Add("primary");
        openCamera.Click += (_, _) => Show("Камера");
        workspaceEmpty = new StackPanel
        {
            Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            
        };
        var demo = new Grid();
        using (var asset = Avalonia.Platform.AssetLoader.Open(new Uri("avares://GestureFlow.Desktop/Assets/control-demo.png")))
            demo.Children.Add(new Image { Source = new Avalonia.Media.Imaging.Bitmap(asset), Stretch = Stretch.UniformToFill });
        var overlay = new Border
        {
            Background = Ink("#EEFAF4EC"), CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 8),
            Margin = new Thickness(10), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom,
            Child = new StackPanel { Spacing = 4, Children = { Copy("Демо · камера выключена", 12), openCamera } }
        };
        demo.Children.Add(overlay);
        workspaceEmpty.Children.Clear();
        workspaceEmpty.HorizontalAlignment = HorizontalAlignment.Stretch;
        workspaceEmpty.VerticalAlignment = VerticalAlignment.Stretch;
        workspaceEmpty.Children.Add(demo);
        demo.Height = viewport.Height;
        viewport.Children.Add(workspaceEmpty);
        var previewFrame=new Border { Child=viewport,CornerRadius=new CornerRadius(11),ClipToBounds=true };
        var left=new StackPanel { Spacing=10 };left.Children.Add(previewFrame);
        var indicators = new WrapPanel { Orientation = Orientation.Horizontal };
        var handState = Copy("");
        handState.Bind(TextBlock.TextProperty, new Binding("Text") { Source = gestureStatus });
        handState.Margin = new Thickness(0, 0, 14, 0);
        indicators.Children.Add(handState);
        shellControl = new TextBlock { FontSize = 13 };
        indicators.Children.Add(shellControl);
        left.Children.Add(new Border { Child = indicators, Padding = new Thickness(12, 9), CornerRadius = new CornerRadius(8), Background = Ink("#EFE2D3") });
        var columns=new Grid { ColumnDefinitions=new ColumnDefinitions("*,320"), ColumnSpacing = PanelGap };
        columns.Children.Add(left);
        var right = new Grid { RowDefinitions = new RowDefinitions("*,Auto"), RowSpacing = 10 };
        right.Children.Add(setup);
        setup.VerticalAlignment = VerticalAlignment.Top;
        var links = new WrapPanel { Orientation = Orientation.Horizontal, Children = { help, more } };
        Grid.SetRow(links, 1);
        right.Children.Add(links);
        Grid.SetColumn(right,1); columns.Children.Add(right);
        StackWhenNarrow(columns, right, "*,320", 700);
        mousePage.Children.Add(columns);
        Detach(mouseDisplay); var testing=new StackPanel { Spacing=12, Children = { dwellProgress, SettingRow("Экран для управления", mouseDisplay), controls[3] } }; Detach(autoStartMouse);testing.Children.Add(autoStartMouse);
        var saveControl=new Button { Content="Сохранить настройки управления" };
        saveControl.Click+=(_,_)=> {
            var next=settings with { ClickTrigger=clickTrigger.SelectedIndex,ClickMode=clickKind.SelectedIndex,WheelEnabled=wheelEnabled.IsChecked==true };
            try { settingsStore.Save(next);settings=next;mouseStatus.Text="Настройки управления сохранены."; }
            catch(Exception ex) { mouseStatus.Text="Ошибка сохранения: "+ex.Message; }
        };
        testing.Children.Add(saveControl); testing.Children.Add(controls[6]);
        foreach(var index in new[] {9,10,11}) testing.Children.Add(controls[index]);
        if(controls[9] is Button target) { target.Width=double.NaN;target.HorizontalAlignment=HorizontalAlignment.Stretch;target.Height=64; }
        if(controls[10] is Button run)run.Content="Отправить тестовый клик";
        testing.Children.Add(Copy("Проверка прокрутки Windows",18));
        var scrollLines=new StackPanel { Spacing=14 };
        for(int i=1;i<=50;i++)scrollLines.Children.Add(Copy("Строка "+i+" — тестовая область прокрутки",14));
        var scrollArea=new ScrollViewer { Content=scrollLines,Height=180,Background=Ink("#F0F1F4") };
        var scrollReport=Copy("Наведите курсор на область. Для жеста: удерживайте кулак и двигайте кисть вверх/вниз.",12,true);
        testing.Children.Add(scrollArea);
        var scrollButtons=new StackPanel { Orientation=Orientation.Horizontal,Spacing=10 };
        foreach(var delta in new[]{-120,120}) {
            var button=new Button { Content=delta<0?"Тест: вниз":"Тест: вверх" };
            var direction=delta;
            button.Click+=async(_,_)=> {
                mouseEnabled.IsChecked=false;autoMousePending=false;
                try {
                    var before=scrollArea.Offset.Y;
                    var point=scrollArea.PointToScreen(new Point(scrollArea.Bounds.Width/2,scrollArea.Bounds.Height/2));
                    mouseIntegration.MovePointerPixels(point.X,point.Y);mouseIntegration.Click(MouseClickKind.Left);
                    mouseIntegration.Scroll(direction);await Task.Delay(200);
                    scrollReport.Text=$"Смещение области: {before:F0} → {scrollArea.Offset.Y:F0}.";
                }catch(Exception ex){scrollReport.Text="Ошибка: "+ex.Message;}
            };
            scrollButtons.Children.Add(button);
        }
        testing.Children.Add(scrollButtons);testing.Children.Add(scrollReport);
        clickTesting=testing;
        RefreshShell();
    }
    void ShowAdvanced()
    {
        var body=new StackPanel { Spacing=18 };
        var back=new Button { Content="← Управление" };back.Click+=(_,_)=>Show("AirDesk/Palm Menu");body.Children.Add(back);
        if(clickTesting!=null) { Detach(clickTesting);body.Children.Add(Card(clickTesting)); }
        if(workspaceAdvanced!=null) { Detach(workspaceAdvanced);body.Children.Add(workspaceAdvanced); }
        page.Content=body;
    }
    static void StackWhenNarrow(Grid grid, Control second, string wideColumns, double breakpoint)
    {
        bool stacked = false;
        grid.SizeChanged += (_, e) =>
        {
            bool narrow = e.NewSize.Width < breakpoint;
            if (narrow == stacked) return;
            stacked = narrow;
            grid.ColumnDefinitions = new ColumnDefinitions(narrow ? "*" : wideColumns);
            grid.RowDefinitions = new RowDefinitions(narrow ? "Auto,Auto" : "Auto");
            grid.RowSpacing = narrow ? 14 : 0;
            Grid.SetColumn(second, narrow ? 0 : 1);
            Grid.SetRow(second, narrow ? 1 : 0);
        };
    }

    static Control SettingRow(string label, Control value)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12, MinHeight = 52 };
        var title = Copy(label);
        Avalonia.Automation.AutomationProperties.SetLabeledBy(value, title);
        title.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(title);
        value.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(value, 1);
        row.Children.Add(value);
        return row;
    }

    static Control HistoryRow(bool header, params string[] values)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("1.3*,1.1*,*,*"), ColumnSpacing = 12, Margin = new Thickness(0, 10) };
        for (var i = 0; i < values.Length; i++)
        {
            var cell = Copy(values[i]);
            if (header) cell.FontWeight = FontWeight.SemiBold;
            Grid.SetColumn(cell, i);
            row.Children.Add(cell);
        }
        return row;
    }

    void ArrangeSecondaryPage(string name, StackPanel panel)
    {
        if (name == "Жесты")
        {
            var details = panel.Children[0];
            panel.Children.Clear();
            var gestures = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,*,*"),
                RowDefinitions = new RowDefinitions("Auto,Auto,Auto"),
                ColumnSpacing = 12,
                RowSpacing = 12
            };
            var entries = new[]
            {
                ("Ладонь", "Раскройте ладонь и выпрямите пальцы.", "palm"),
                ("Кулак", "Согните пальцы и прижмите большой палец.", "fist"),
                ("Щипок", "Соедините кончики большого и указательного пальцев.", "pinch"),
                ("V-жест", "Разведите указательный и средний пальцы.", "victory"),
                ("Большой палец вверх", "Поднимите большой палец, остальные согните.", "thumbs-up"),
                ("Три пальца", "Выпрямите указательный, средний и безымянный.", "three"),
                ("Шака", "Разведите большой палец и мизинец. Остальные согните.", "shaka"),
                ("Большой палец вниз", "Направьте большой палец вниз, остальные согните.", "thumbs-up")
            };
            for (var i = 0; i < entries.Length; i++)
            {
                var (label, hint, assetName) = entries[i];
                var title = Copy(label, 17);
                title.FontWeight = FontWeight.SemiBold;
                title.TextAlignment = TextAlignment.Center;
                var description = Copy(hint, 13, true);
                description.TextAlignment = TextAlignment.Center;
                using var asset = Avalonia.Platform.AssetLoader.Open(new Uri($"avares://GestureFlow.Desktop/Assets/gesture-{assetName}.png"));
                var illustration = new Image { Source = new Avalonia.Media.Imaging.Bitmap(asset), Height = 116, Stretch = Stretch.Uniform };
                if (label == "Большой палец вниз")
                    illustration.RenderTransform = new RotateTransform(180);
                var gestureIndex = i;
                var assign = new Button { Content = "Назначить действие", HorizontalAlignment = HorizontalAlignment.Center };
                assign.Classes.Add("quiet");
                Avalonia.Automation.AutomationProperties.SetName(assign, $"Назначить действие: {label}");
                assign.Click += (_, _) =>
                {
                    automationEnabled.IsChecked = false;
                    SetEditingRule(null);
                    automationGesture.SelectedIndex = gestureIndex;
                    Show("Автоматизация");
                    automationDestination.Focus();
                };
                var card = Card(new StackPanel { Spacing = 7, Children = { illustration, title, description, assign } });
                Grid.SetColumn(card, i % 3);
                Grid.SetRow(card, i / 3);
                gestures.Children.Add(card);
            }
            panel.Children.Add(gestures);
            var cameraLink = new Button { Content = "Открыть камеру  →" };
            cameraLink.Click += (_, _) => Show("Камера");
            var current = Copy("");
            current.Bind(TextBlock.TextProperty, new Binding("Text") { Source = gestureStatus });
            panel.Children.Add(new Border { Background = Ink("#F1E5D9"), CornerRadius = new CornerRadius(8), Padding = new Thickness(12), Child = current });
            panel.Children.Add(new Expander { Header = "Проверка распознавания", Content = new StackPanel { Spacing = 8, Children = { details, cameraLink } } });
        }
        else if (name == "Автоматизация")
        {
            var items = panel.Children.ToArray();
            panel.Children.Clear();

            var rules = new StackPanel { Spacing = 6, Children = { Copy("Правило жеста", 18) } };
            foreach (var item in items.Skip(1).Take(6)) rules.Children.Add(item);
            var details = new StackPanel { Spacing = 8 };
            foreach (var item in items.Skip(7).Take(3)) details.Children.Add(item);
            rules.Children.Add(new Expander { Header = "Проверка и подсказки", Content = details });
            ruleState = Copy("", 13, true);
            rules.Children.Insert(2, ruleState);
            ruleAssignmentHint = Copy("", 13, true);
            rules.Children.Insert(6, ruleAssignmentHint);
            rules.Children.Insert(7, BuildTelegramDraftEditor());
            rules.Children.Insert(8, BuildTelegramPhotoEditor());
            RefreshShell();
            Detach(training);
            rules.Children.Add(training);
            panel.Children.Add(Card(BuildRuleList()));
            panel.Children.Add(Card(rules));
            var media = new StackPanel { Spacing = 10, Children = { items[0] } };
            foreach (var item in items.Skip(10)) media.Children.Add(item);
            panel.Children.Add(Card(media));
        }
    }
}