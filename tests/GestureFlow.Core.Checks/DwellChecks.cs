using GestureFlow.Core;
static class DwellChecks
{
    static void Must(bool value,string name) { if(!value)throw new Exception(name);Console.WriteLine("PASS: "+name); }
    public static void Run()
    {
        var d=new DwellClickProcessor();
        Must(!d.Observe(0,0,TimeSpan.Zero),"dwell выключен");
        d.Configure(true);d.Observe(0,0,TimeSpan.Zero);
        for(int t=100;t<=1200;t+=100)Must(!d.Observe(0,0,TimeSpan.FromMilliseconds(t)),"dwell требует движения после включения");
        d.Observe(100,0,TimeSpan.FromMilliseconds(1300));
        for(int t=1400;t<2200;t+=100)Must(!d.Observe(102,1,TimeSpan.FromMilliseconds(t)),"dwell ждёт 900 мс");
        Must(d.Observe(101,1,TimeSpan.FromMilliseconds(2200)),"dwell один клик с допустимым дрожанием");
        for(int t=2300;t<=3400;t+=100)Must(!d.Observe(101,1,TimeSpan.FromMilliseconds(t)),"dwell не повторяет клик без движения");
        d.Observe(180,0,TimeSpan.FromMilliseconds(3500));
        for(int t=3600;t<4400;t+=100)d.Observe(180,0,TimeSpan.FromMilliseconds(t));
        Must(d.Observe(180,0,TimeSpan.FromMilliseconds(4400)),"dwell повторяется после нового движения");
        d.Reset();d.Observe(0,0,TimeSpan.FromMilliseconds(4500));d.Observe(100,0,TimeSpan.FromMilliseconds(4600));
        d.Observe(100,0,TimeSpan.FromMilliseconds(5000));
        for(int t=5100;t<6300;t+=100)Must(!d.Observe(100,0,TimeSpan.FromMilliseconds(t)),"разрыв кадров снимает dwell");
        d.Observe(double.NaN,0,TimeSpan.FromMilliseconds(6400));Must(d.WaitingForMovement && d.Progress==0,"невалидная точка сбрасывает dwell");
        d.Configure(false);Must(!d.Observe(400,0,TimeSpan.FromMilliseconds(6500)),"отключение блокирует dwell");
        var click=new NavigationAutomation(TimeSpan.FromMilliseconds(350));
        var pinch=new GestureSnapshot(GestureKind.Pinch,GestureKind.Pinch,1,1);
        click.Configure(true);click.Observe(null,GestureKind.Pinch,TimeSpan.Zero);
        Must(click.Observe(pinch,GestureKind.Pinch,TimeSpan.FromMilliseconds(10)),"первый щипок");
        click.Observe(null,GestureKind.Pinch,TimeSpan.FromMilliseconds(300));
        Must(click.Observe(pinch,GestureKind.Pinch,TimeSpan.FromMilliseconds(500)),"мышь принимает второй щипок раньше двух секунд");
    }
}
