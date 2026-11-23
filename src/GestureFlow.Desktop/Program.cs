using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
namespace GestureFlow.Desktop;

internal static class Program
{
    internal static EventWaitHandle? Activation;
    [STAThread] public static void Main(string[] args)
    {
        using var signal=new EventWaitHandle(false,EventResetMode.AutoReset,@"Local\GestureFlow.AirDesk.Activate" + DataDirectory.InstanceSuffix);
        Activation=signal;
        using var instance=new Mutex(false,@"Local\GestureFlow.AirDesk.Instance" + DataDirectory.InstanceSuffix);
        bool owner;
        try { owner=instance.WaitOne(0); } catch(AbandonedMutexException) { owner=true; }
        if(!owner) { signal.Set();return; }
        try { AppBuilder.Configure<App>().UsePlatformDetect().StartWithClassicDesktopLifetime(args); }
        finally { Activation=null;instance.ReleaseMutex(); }
    }
}
public sealed class App : Application
{
    public override void Initialize()
    {
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
        var fluent = new FluentTheme();
        fluent.Palettes[Avalonia.Styling.ThemeVariant.Light] = new ColorPaletteResources { Accent = Avalonia.Media.Color.Parse("#984829") };
        Styles.Add(fluent);
        Styles.Add(new Avalonia.Markup.Xaml.Styling.StyleInclude(new Uri("avares://GestureFlow.Desktop/"))
        {
            Source = new Uri("avares://GestureFlow.Desktop/Styles.axaml")
        });
    }
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d) {
            var window=new MainWindow();d.MainWindow=window;
            var timer=new Avalonia.Threading.DispatcherTimer { Interval=TimeSpan.FromMilliseconds(200) };
            timer.Tick+=(_,_)=> {
                if(Program.Activation?.WaitOne(0)!=true)return;
                if(window.WindowState==Avalonia.Controls.WindowState.Minimized)window.WindowState=Avalonia.Controls.WindowState.Normal;
                window.Show();window.Activate();
            };
            timer.Start();window.Closed+=(_,_)=>timer.Stop();
        }
        base.OnFrameworkInitializationCompleted();
    }
}



