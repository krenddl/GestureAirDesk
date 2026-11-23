using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using GestureFlow.Core;
using System.Diagnostics;
namespace GestureFlow.Desktop;

public sealed class AirDeskPointerView : StackPanel
{
    readonly CheckBox enabled = new() { Content = "Включить указатель внутри этой панели" };
    readonly Canvas board = new() { Width = 500, Height = 240, Background = Brushes.Black, ClipToBounds = true };
    readonly Ellipse cursor = new() { Width = 18, Height = 18, Fill = Brushes.Cyan, IsHitTestVisible = false, IsVisible = false };
    readonly TextBlock hint = new() { Text = "Указатель выключен.", TextWrapping = TextWrapping.Wrap };
    readonly Button[] targets;
    public StackPanel CalibrationPanel { get; } = new() { Spacing = 14 };
    readonly Slider insetX = new() { Minimum = 0, Maximum = .4, Value = 0, Width = 300 };
    readonly Slider insetY = new() { Minimum = 0, Maximum = .4, Value = 0, Width = 300 };
    readonly TextBlock areaLabel = new() { TextWrapping = TextWrapping.Wrap };
    readonly TextBlock areaStatus = new() { TextWrapping = TextWrapping.Wrap };
    public (double X,double Y) CurrentArea=>(insetX.Value,insetY.Value);
    public event Action<double, double>? SaveArea;
    public event Action? Started;
    public void LoadArea(double x, double y) { insetX.Value = x; insetY.Value = y; RefreshArea(); }
    public void AreaSaved(string message) => areaStatus.Text = message;
    void RefreshArea() { areaLabel.Text = $"Отступ от каждого края: горизонталь {insetX.Value:P0}, вертикаль {insetY.Value:P0}. Рабочая область: {1 - 2 * insetX.Value:P0} × {1 - 2 * insetY.Value:P0} кадра."; Stop(); }
    readonly NavigationAutomation selection = new();
    readonly Stopwatch clock = Stopwatch.StartNew();
    public event Action<string>? Navigate;
    public AirDeskPointerView()
    {
        Spacing = 12;
        Children.Add(new TextBlock { Text = "Указатель AirDesk", FontSize = 22 }); Children.Add(enabled);
        CalibrationPanel.Children.Add(new TextBlock { Text = "Рабочая область: отступ по горизонтали / вертикали" });
        CalibrationPanel.Children.Add(insetX); CalibrationPanel.Children.Add(insetY); CalibrationPanel.Children.Add(areaLabel);
        var areaButtons = new WrapPanel();
        var save = new Button { Content = "Сохранить область", Margin = new Thickness(4) };
        save.Click += (_, _) => SaveArea?.Invoke(insetX.Value, insetY.Value);
        var reset = new Button { Content = "Весь кадр", Margin = new Thickness(4) };
        reset.Click += (_, _) => LoadArea(0, 0);
        areaButtons.Children.Add(save); areaButtons.Children.Add(reset); CalibrationPanel.Children.Add(areaButtons); CalibrationPanel.Children.Add(areaStatus);
        insetX.ValueChanged += (_, _) => RefreshArea(); insetY.ValueChanged += (_, _) => RefreshArea(); RefreshArea();
        var names = new[] { "Камера", "Жесты", "Аналитика", "Настройки" };
        targets = names.Select((name, i) =>
        {
            var b = new Button { Content = name, Width = 240, Height = 110 };
            Canvas.SetLeft(b, i % 2 * 250); Canvas.SetTop(b, i / 2 * 120);
            b.Click += (_, _) => Choose(name); board.Children.Add(b); return b;
        }).ToArray();
        board.Children.Add(cursor); Children.Add(board); Children.Add(hint);
        Children.Add(new TextBlock { Text = "Указательный палец — наведение, подтверждённый щипок — выбор. Сначала отпустите щипок. После выбора указатель выключается. Кнопки можно проверить мышью без камеры.", TextWrapping = TextWrapping.Wrap });
        enabled.IsCheckedChanged += (_, _) => { if (enabled.IsChecked == true) Started?.Invoke(); selection.Configure(enabled.IsChecked == true); cursor.IsVisible = false; hint.Text = enabled.IsChecked == true ? "Жду руку и кадры камеры." : "Указатель выключен."; };
    }
    public void Stop() { enabled.IsChecked = false; cursor.IsVisible = false; }
    void Choose(string name) { Stop(); Navigate?.Invoke(name); }
    public void Update(CameraFrame frame, bool mirror)
    {
        if (enabled.IsChecked != true) return;
        var point = frame.Hand?.Points.ElementAtOrDefault(8);
        var projected = point == null ? null : PointerProjection.Project(point, frame.Width, frame.Height, mirror, insetX.Value, insetY.Value);
        if (projected == null)
        {
            cursor.IsVisible = false; hint.Text = "Рука не найдена.";
            selection.Observe(null, GestureKind.Pinch, clock.Elapsed); return;
        }
        var p = projected.Value; int target = PointerProjection.Target(p.X, p.Y);
        Canvas.SetLeft(cursor, p.X * 482); Canvas.SetTop(cursor, p.Y * 222); cursor.IsVisible = true;
        for (int i = 0; i < targets.Length; i++) targets[i].BorderBrush = i == target ? Brushes.Cyan : Brushes.Gray;
        hint.Text = "Наведение: " + targets[target].Content;
        if (selection.Observe(frame.Gesture, GestureKind.Pinch, clock.Elapsed)) Choose((string)targets[target].Content!);
    }
}




