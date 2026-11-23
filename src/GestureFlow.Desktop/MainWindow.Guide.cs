using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
namespace GestureFlow.Desktop;

public sealed partial class MainWindow
{
    void ShowControlGuide()
    {
        autoMousePending = false; mouseEnabled.IsChecked = false; ReleaseDrag();
        var panel = new StackPanel { Spacing = 16, Margin = new Thickness(24) };
        panel.Children.Add(Copy("Как управлять компьютером", 24));
        foreach (var item in new[] {
            ("1. Подключите камеру", "Камера → Найти камеры → выбрать устройство. Включите «Рука и жесты», затем нажмите «Старт». Сама камера автоматически не запускается."),
            ("2. Подготовьте руку", "Держите одну кисть целиком в кадре при хорошем освещении. На видео должны появиться точки. Если их нет, проверьте освещение, расстояние и сообщение на странице камеры."),
            ("3. Двигайте курсор", "Откройте «Управление», выберите экран и включите управление. Указательный палец перемещает курсор. При потере руки ввод приостанавливается."),
            ("4. Нажимайте задержкой", "Выберите «Задержка курсора» и «Левый клик». Сдвиньте курсор на цель и удерживайте около 0,9 секунды. Индикатор покажет прогресс. Для следующего клика сначала отведите курсор."),
            ("5. Прокручивайте страницу", "Включите «Прокрутка», наведите курсор на нужную страницу, покажите кулак и двигайте кисть вверх или вниз. Раскрытая ладонь прекращает прокрутку. В режиме перетаскивания прокрутка недоступна."),
            ("6. Останавливайте действия", "Escape или «Остановить всё» прекращают управление. В «Камера» кнопка «Стоп» освобождает устройство. Перед сменой профиля остановите камеру."),
            ("Если что-то не получается", "Откройте «⋯» рядом с заголовком «Мышь»: там есть тестовая площадка для клика и колеса. Сначала проверьте команды кнопками, затем повторите рукой. Распознавание пока рассчитано на одну кисть; две руки и расширенные макросы не заявлены как готовые функции.")
        }) {
            var card = new StackPanel { Spacing = 6 };
            card.Children.Add(Copy(item.Item1, 18)); card.Children.Add(Copy(item.Item2));
            panel.Children.Add(Card(card));
        }
        var dialog = new Window { Title = "GestureFlow — инструкция", Width = 680, Height = 720,
            Background = Ink("#FAFAFC"), MinWidth = 420, MinHeight = 320, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        var cameraButton = new Button { Content = "Перейти к камере" };
        cameraButton.Click += (_, _) => { Show("Камера"); dialog.Close(); };
        var close = new Button { Content = "Понятно" }; close.Click += (_, _) => dialog.Close();
        actions.Children.Add(cameraButton); actions.Children.Add(close); panel.Children.Add(actions);
        dialog.Content = new ScrollViewer { Content = panel, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        _ = dialog.ShowDialog(this);
    }
}