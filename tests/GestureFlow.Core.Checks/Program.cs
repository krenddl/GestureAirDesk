using GestureFlow.Core;
static void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("PASS: " + name); }
static HandObservation Pose(GestureKind kind)
{
    var p = Enumerable.Range(0, 21).Select(_ => new HandPoint(0, 0, 0)).ToArray();
    p[0] = new(0, 0, 0); p[1] = new(-.7f, -.3f, 0); p[2] = new(-1, -.6f, 0); p[3] = new(-1.3f, -.9f, 0); p[4] = new(-1.6f, -1.2f, 0);
    for (int f = 0; f < 4; f++)
    {
        int i = 5 + f * 4; float x = -.6f + f * .4f;
        p[i] = new(x, -1, 0); p[i + 1] = new(x, -1.6f, 0);
        var curled = kind is GestureKind.Fist or GestureKind.ThumbsUp || (kind == GestureKind.ThreeFingers && f == 3) || (kind == GestureKind.Shaka && f < 3) || (kind == GestureKind.Victory && f > 1);
        p[i + 2] = curled ? new(x + .1f, -1.1f, 0) : new(x, -2, 0);
        p[i + 3] = curled ? new(x + .1f, -.8f, 0) : new(x, -2.4f, 0);
    }
    if (kind == GestureKind.Fist) p[4] = new(-.7f, -1.1f, 0);
    if (kind == GestureKind.ThumbsUp)
    {
        p[2] = new(-1.4f, -.9f, 0);
        p[3] = new(-1.4f, -1.45f, 0);
        p[4] = new(-1.4f, -2f, 0);
    }
    if (kind == GestureKind.Victory)
    {
        for (int joint = 1; joint <= 3; joint++)
        {
            p[5 + joint] = p[5 + joint] with { X = p[5 + joint].X - joint * .1f };
            p[9 + joint] = p[9 + joint] with { X = p[9 + joint].X + joint * .1f };
        }
    }
    if (kind == GestureKind.Pinch) { p[4] = new(-.65f, -2.3f, 0); }
    return new(p, .99f);
}
var open = Pose(GestureKind.OpenPalm); var fist = Pose(GestureKind.Fist); var pinch = Pose(GestureKind.Pinch);
foreach (var (pose, kind) in new[] { (open, GestureKind.OpenPalm), (fist, GestureKind.Fist), (pinch, GestureKind.Pinch), (Pose(GestureKind.Victory), GestureKind.Victory), (Pose(GestureKind.ThumbsUp), GestureKind.ThumbsUp), (Pose(GestureKind.ThreeFingers), GestureKind.ThreeFingers), (Pose(GestureKind.Shaka), GestureKind.Shaka) })
{
    Check(GestureClassifier.Classify(pose) == kind, "классификация " + kind);
    var transformed = new HandObservation(pose.Points.Select(p => new HandPoint(50 - p.Y * 3, 20 + p.X * 3, p.Z * 3)).ToArray(), .99f);
    Check(GestureClassifier.Classify(transformed) == kind, "масштаб и поворот " + kind);
}
var thumbsUp = Pose(GestureKind.ThumbsUp);
var thumbsDown = thumbsUp with
{
    Points = thumbsUp.Points.Select(p => p with { X = -p.X, Y = -p.Y }).ToArray()
};
Check(GestureClassifier.Classify(thumbsDown) == GestureKind.ThumbsDown, "классификация большого пальца вниз");
var brightness = new PalmBrightnessGesture();
CameraFrame BrightnessFrame(float x, float y, GestureKind kind = GestureKind.OpenPalm) =>
    new([], "", 1, new(kind, kind, 1, 0),
        open with { Points = open.Points.Select(p => p with { X = p.X + x, Y = p.Y + y }).ToArray() }, 640, 480);
Check(brightness.Observe(BrightnessFrame(300, 250), TimeSpan.Zero) == 0, "яркость: неподвижная ладонь");
Check(brightness.Observe(BrightnessFrame(300, 165), TimeSpan.FromMilliseconds(200)) == 10, "яркость: движение вверх");
Check(brightness.Observe(BrightnessFrame(300, 260), TimeSpan.FromMilliseconds(400)) == 0, "яркость: удержание не повторяет команду");
brightness.Observe(BrightnessFrame(300, 260, GestureKind.Fist), TimeSpan.FromMilliseconds(450));
brightness.Observe(BrightnessFrame(300, 165), TimeSpan.FromMilliseconds(550));
Check(brightness.Observe(BrightnessFrame(300, 250), TimeSpan.FromMilliseconds(750)) == -10, "яркость: движение вниз после смены позы");
brightness.Reset();
brightness.Observe(BrightnessFrame(300, 250), TimeSpan.Zero);
Check(brightness.Observe(BrightnessFrame(410, 165), TimeSpan.FromMilliseconds(200)) == 0, "яркость: диагональный скачок отбрасывается");
var unknown = open with { Points = open.Points.Select(_ => new HandPoint(0, 0, 0)).ToArray() };
Check(GestureClassifier.Classify(unknown) == GestureKind.Unknown, "вырожденная кисть");
var processor = new HandGestureProcessor();
Check(processor.Process(open, TimeSpan.Zero).Gesture.Stable == GestureKind.Unknown, "первый кадр не подтверждает жест");
processor.Process(open, TimeSpan.FromMilliseconds(100));
var held = processor.Process(open, TimeSpan.FromMilliseconds(200));
Check(held.Gesture.Stable == GestureKind.OpenPalm && held.Gesture.Confirmations == 1, "удержание подтверждает жест");
Check(processor.Process(open, TimeSpan.FromMilliseconds(230)).Gesture.Confirmations == 1, "удержание не повторяет событие");
Check(processor.Process(null, TimeSpan.FromMilliseconds(240)).Gesture.Stable == GestureKind.NoHand, "потеря руки снимает жест сразу");
Check(processor.Process(open, TimeSpan.FromMilliseconds(250)).Gesture.Stable == GestureKind.Unknown, "возврат требует нового удержания");
Check(processor.Process(open, TimeSpan.FromMilliseconds(600)).Gesture.Stable == GestureKind.Unknown, "разрыв кадров сбрасывает удержание");
var invalid = open with { Points = open.Points.Take(20).ToArray() };
Check(processor.Process(invalid, TimeSpan.FromMilliseconds(650)).Hand == null, "неверное число точек");
Check(processor.Process(open with { Confidence = .2f }, TimeSpan.FromMilliseconds(700)).Hand == null, "низкая уверенность");
var nan = open with { Points = open.Points.Select((p, i) => i == 0 ? new HandPoint(float.NaN, 0, 0) : p).ToArray() };
Check(processor.Process(nan, TimeSpan.FromMilliseconds(750)).Hand == null, "NaN отклоняется");
var smooth = new HandGestureProcessor(); smooth.Process(open, TimeSpan.Zero);
var shifted = open with { Points = open.Points.Select(p => p with { X = p.X + .1f }).ToArray() };
var smoothed = smooth.Process(shifted, TimeSpan.FromMilliseconds(30));
Check(smoothed.Hand!.Points[0].X > 0 && smoothed.Hand.Points[0].X < .1f, "сглаживание уменьшает скачок");
var jumped = open with { Points = open.Points.Select(p => p with { X = p.X + 10 }).ToArray() };
Check(smooth.Process(jumped, TimeSpan.FromMilliseconds(60)).Hand!.Points[0].X == 10, "резкий перенос сбрасывает фильтр");
var quickPinch = new HandGestureProcessor();
quickPinch.Process(open, TimeSpan.Zero);
Check(quickPinch.Process(pinch, TimeSpan.FromMilliseconds(33)).Gesture.Candidate == GestureKind.Pinch,
    "щипок виден сразу при переходе с ладони");
Check(quickPinch.Process(pinch, TimeSpan.FromMilliseconds(83)).Gesture.Stable == GestureKind.Pinch,
    "щипок подтверждается двумя кадрами");
Check(quickPinch.Process(open, TimeSpan.FromMilliseconds(116)).Gesture.Stable != GestureKind.Pinch,
    "разжатие щипка снимает удержание сразу");
var boundary = pinch with { Points = pinch.Points.Select((p, i) => i == 4 ? new HandPoint(-.2f, -2.4f, 0) : p).ToArray() };
Check(GestureClassifier.Classify(boundary) == GestureKind.Unknown, "щипок не включается на пороге отпускания");
Check(GestureClassifier.Classify(boundary, GestureKind.Pinch) == GestureKind.Pinch, "гистерезис удерживает щипок");
Console.WriteLine("Все проверки Core пройдены.");

var palm = new PalmMenuState();
palm.Apply(false, new(GestureKind.OpenPalm, GestureKind.OpenPalm, 1, 1));
Check(!palm.IsOpen, "панель не открывается без включения");
palm.Apply(true, new(GestureKind.OpenPalm, GestureKind.OpenPalm, 1, 1));
Check(palm.IsOpen, "ладонь открывает панель");
palm.Apply(true, new(GestureKind.Unknown, GestureKind.Unknown, 0, 1));
Check(palm.IsOpen, "промежуточная поза не мигает панелью");
palm.Apply(true, new(GestureKind.Fist, GestureKind.Fist, 1, 2));
Check(!palm.IsOpen, "кулак закрывает панель");
palm.Open(); palm.Apply(true, null);
Check(!palm.IsOpen, "остановка закрывает панель");
palm.Open(); palm.Apply(true, new(GestureKind.NoHand, GestureKind.NoHand, 0, 2));
Check(!palm.IsOpen, "потеря руки закрывает панель");
palm.Open(); palm.Close(); Check(!palm.IsOpen, "ручное закрытие");
var navigation = new NavigationAutomation();
var pinched = new GestureSnapshot(GestureKind.Pinch, GestureKind.Pinch, 1, 1);
Check(!navigation.Observe(pinched, GestureKind.Pinch, TimeSpan.Zero), "автоматизация выключена");
navigation.Configure(true);
Check(!navigation.Observe(pinched, GestureKind.Pinch, TimeSpan.Zero), "включение требует отпускания");
navigation.Observe(null, GestureKind.Pinch, TimeSpan.FromMilliseconds(10));
Check(navigation.Observe(pinched, GestureKind.Pinch, TimeSpan.FromSeconds(1)), "новый жест запускает действие");
Check(!navigation.Observe(pinched, GestureKind.Pinch, TimeSpan.FromSeconds(4)), "удержание не повторяет действие");
navigation.Observe(null, GestureKind.Pinch, TimeSpan.FromSeconds(4.1));
Check(navigation.Observe(pinched, GestureKind.Pinch, TimeSpan.FromSeconds(4.2)), "отпускание разрешает повтор");
navigation.Observe(null, GestureKind.Pinch, TimeSpan.FromSeconds(4.3));
Check(!navigation.Observe(pinched, GestureKind.Pinch, TimeSpan.FromSeconds(4.4)), "пауза блокирует частый повтор");
HistoryChecks.Run();
navigation.Configure(true);
navigation.Observe(new(GestureKind.Unknown, GestureKind.Unknown, 0, 0), GestureKind.Pinch, TimeSpan.Zero);
Check(!navigation.Observe(pinched, GestureKind.Pinch, TimeSpan.FromSeconds(1)), "неопределённая поза не разрешает первое действие");
var oldSettings = System.Text.Json.JsonSerializer.Deserialize<AppSettings>("{\"CameraIndex\":0,\"MirrorPreview\":true,\"ProfileName\":\"Old\"}")!;
Check(oldSettings.AutomationGesture == 2 && oldSettings.AutomationAction == 2 && oldSettings.MirrorPreview, "совместимость старых настроек");
var selectedSettings = oldSettings with { AutomationGesture = 1, AutomationAction = 7 };
var restoredSettings = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(System.Text.Json.JsonSerializer.Serialize(selectedSettings))!;
Check(restoredSettings == selectedSettings, "сохранение выбранного правила");
var projected = PointerProjection.Project(new HandPoint(160, 120, 0), 640, 480, false)!.Value;
Check(projected.X == .25 && projected.Y == .25, "проекция указателя");
Check(PointerProjection.Project(new(160, 120, 0), 640, 480, true)!.Value.X == .75, "зеркальный указатель");
Check(PointerProjection.Project(new(-5, 900, 0), 640, 480, false)!.Value == (0, 1), "границы указателя");
Check(PointerProjection.Project(new(float.NaN, 0, 0), 640, 480, false) == null, "NaN указателя");
Check(PointerProjection.Project(new(0, 0, 0), 0, 480, false) == null, "неверный размер кадра");
Check(PointerProjection.Target(.25, .25) == 0 && PointerProjection.Target(.75, .25) == 1 && PointerProjection.Target(.25, .75) == 2 && PointerProjection.Target(.75, .75) == 3, "четыре области выбора");
Check(PointerProjection.Project(new(320, 240, 0), 640, 480, false, .2, .3)!.Value == (.5, .5), "центр рабочей области");
Check(PointerProjection.Project(new(128, 144, 0), 640, 480, false, .2, .3)!.Value.X < .00001, "левый край рабочей области");
Check(PointerProjection.Project(new(640, 480, 0), 640, 480, true, .2, .3)!.Value == (0, 1), "зеркало и ограничение области");
Check(PointerProjection.Project(new(10, 10, 0), 640, 480, false, .5, 0) == null, "некорректная рабочая область");
Check(PointerProjection.Project(new(10, 10, 0), 640, 480, false, double.NaN, 0) == null, "NaN области");
var areaSettings = selectedSettings with { PointerInsetX = .2, PointerInsetY = .3 };
Check(System.Text.Json.JsonSerializer.Deserialize<AppSettings>(System.Text.Json.JsonSerializer.Serialize(areaSettings)) == areaSettings, "сохранение области");
var wheel = new WheelGestureProcessor();
Check(wheel.Observe(true, .5, TimeSpan.Zero) == 0, "первый кадр прокрутки не двигает колесо");
Check(wheel.Observe(true, .4, TimeSpan.FromMilliseconds(100)) == 120, "движение вверх прокручивает вверх");
Check(wheel.Observe(true, .5, TimeSpan.FromMilliseconds(200)) == -120, "движение вниз прокручивает вниз");
Check(wheel.Observe(false, .5, TimeSpan.FromMilliseconds(220)) == 0, "отпускание кулака сбрасывает прокрутку");
Check(wheel.Observe(true, .2, TimeSpan.FromMilliseconds(250)) == 0, "повторный кулак без скачка");
Check(wheel.Observe(true, .21, TimeSpan.FromMilliseconds(260)) == 0, "мелкое движение не прокручивает");
Check(wheel.Observe(true, .8, TimeSpan.FromMilliseconds(280)) == 0, "скачок руки игнорируется");
Check(wheel.Observe(true, .7, TimeSpan.FromMilliseconds(800)) == 0, "разрыв кадров сбрасывает колесо");
Check(wheel.Observe(true, double.NaN, TimeSpan.FromMilliseconds(900)) == 0, "NaN не прокручивает");
var drag = new DragGestureProcessor();
Check(drag.Observe(true, pinched) == DragTransition.None, "перетаскивание требует отпускания");
drag.Observe(true, null);
Check(drag.Observe(true, pinched) == DragTransition.Press, "щипок нажимает кнопку");
Check(drag.Observe(true, pinched) == DragTransition.None, "удержание не повторяет нажатие");
Check(drag.Observe(true, new(GestureKind.Unknown, GestureKind.Unknown, 0, 1)) == DragTransition.Release, "неопределённая поза отпускает кнопку");
Check(drag.Observe(true, pinched) == DragTransition.None, "шум не возобновляет перетаскивание");
drag.Observe(true, null); drag.Observe(true, pinched);
Check(drag.Observe(false, pinched) == DragTransition.Release, "отключение отпускает кнопку");
Check(drag.Observe(false, null) == DragTransition.None, "повторное отключение без события");
Check(ScreenProjection.Map(0,0,0,0,1920,1080)==(0,0),"начало основного экрана");
Check(ScreenProjection.Map(1,1,0,0,1920,1080)==(1919,1079),"край основного экрана");
Check(ScreenProjection.Map(0,0,-1920,-100,1920,1080)==(-1920,-100),"отрицательные координаты монитора");
Check(ScreenProjection.Map(1,1,-1920,-100,1920,1080)==(-1,979),"правый край левого монитора");
Check(ScreenProjection.Map(2,-1,1920,0,1280,720)==(3199,0),"ограничение координат экрана");
Check(ScreenProjection.Map(double.NaN,0,0,0,100,100)==null,"NaN экрана");
Check(ScreenProjection.Map(1,1,int.MaxValue,0,100,100)==null,"переполнение координат");
Check(ScreenProjection.Map(0,0,0,0,0,100)==null,"нулевой размер монитора");
var fastProcessor=new HandGestureProcessor(TrackingPreset.Fast);
var smoothProcessor=new HandGestureProcessor(TrackingPreset.Smooth);
foreach(var t in new[]{0,100,200}){fastProcessor.Process(open,TimeSpan.FromMilliseconds(t));smoothProcessor.Process(open,TimeSpan.FromMilliseconds(t));}
Check(fastProcessor.Process(open,TimeSpan.FromMilliseconds(220)).Gesture.Stable==GestureKind.OpenPalm,"быстрый режим подтверждает раньше");
Check(smoothProcessor.Process(open,TimeSpan.FromMilliseconds(220)).Gesture.Stable==GestureKind.Unknown,"плавный режим ждёт дольше");
Check(smoothProcessor.Process(open,TimeSpan.FromMilliseconds(320)).Gesture.Stable==GestureKind.OpenPalm,"плавный режим подтверждает после выдержки");
var fastFilter=new HandGestureProcessor(TrackingPreset.Fast);var slowFilter=new HandGestureProcessor(TrackingPreset.Smooth);
fastFilter.Process(open,TimeSpan.Zero);slowFilter.Process(open,TimeSpan.Zero);
Check(fastFilter.Process(shifted,TimeSpan.FromMilliseconds(30)).Hand!.Points[0].X>slowFilter.Process(shifted,TimeSpan.FromMilliseconds(30)).Hand!.Points[0].X,"быстрый фильтр меньше задерживает движение");
Check(System.Text.Json.JsonSerializer.Deserialize<AppSettings>(System.Text.Json.JsonSerializer.Serialize(new AppSettings(TrackingPresetIndex:2)))!.TrackingPresetIndex==2,"сохранение отклика");
ProfileChecks.Run();
DwellChecks.Run();
var legacyClick=System.Text.Json.JsonSerializer.Deserialize<AppSettings>("{\"ClickMode\":0}")!;
Check(legacyClick.ClickTrigger==0,"старые настройки выбирают задержку");
var savedClick=new AppSettings(ClickTrigger:1);
Check(System.Text.Json.JsonSerializer.Deserialize<AppSettings>(System.Text.Json.JsonSerializer.Serialize(savedClick))!.ClickTrigger==1,"сохраняется способ клика");
Check(!SettingsValidation.Valid(savedClick with { ClickTrigger=2 }),"неверный способ клика отклоняется");
var motionGuard=new MotionContinuityGuard();
Check(motionGuard.Observe(open,TimeSpan.Zero),"первая рука принимается");
Check(motionGuard.Observe(shifted,TimeSpan.FromMilliseconds(50)),"плавное движение принимается");
Check(!motionGuard.Observe(jumped,TimeSpan.FromMilliseconds(100)),"скачок другой руки отклоняется");
motionGuard.Reset();motionGuard.Observe(open,TimeSpan.Zero);
Check(!motionGuard.Observe(null,TimeSpan.FromMilliseconds(50)),"потеря руки отклоняется");
motionGuard.Reset();motionGuard.Observe(open,TimeSpan.Zero);
Check(!motionGuard.Observe(open,TimeSpan.FromMilliseconds(400)),"разрыв управления отклоняется");
motionGuard.Reset();motionGuard.Observe(open,TimeSpan.FromMilliseconds(100));
Check(!motionGuard.Observe(open,TimeSpan.FromMilliseconds(90)),"обратное время отклоняется");
Check(System.Text.Json.JsonSerializer.Deserialize<AppSettings>("{}")!.AutoStartMouse,"автоматическое включение мыши по умолчанию");
Check(!System.Text.Json.JsonSerializer.Deserialize<AppSettings>(System.Text.Json.JsonSerializer.Serialize(new AppSettings(AutoStartMouse:false)))!.AutoStartMouse,"отключение автозапуска сохраняется");
var returnGuard=new MotionContinuityGuard();
returnGuard.Observe(open,TimeSpan.Zero);returnGuard.Reset();
Check(returnGuard.Observe(open,TimeSpan.FromSeconds(10)),"после паузы рука принимается с новой опорой");
var returnDwell=new DwellClickProcessor();returnDwell.Configure(true);returnDwell.Observe(0,0,TimeSpan.Zero);returnDwell.Observe(100,0,TimeSpan.FromMilliseconds(100));
returnDwell.Reset();returnDwell.Observe(100,0,TimeSpan.FromSeconds(10));
Check(!returnDwell.Observe(100,0,TimeSpan.FromMilliseconds(10100))&&returnDwell.WaitingForMovement,"возврат руки не продолжает старый отсчёт клика");
var scrollGate=new ScrollPoseGate();
var fistPose=new GestureSnapshot(GestureKind.Fist,GestureKind.Fist,1,1);
Check(scrollGate.Observe(true,fistPose,TimeSpan.Zero),"кулак включает прокрутку");
Check(scrollGate.Observe(true,new(GestureKind.Unknown,GestureKind.Unknown,0,1),TimeSpan.FromMilliseconds(100)),"короткое дрожание кулака сохраняет прокрутку");
Check(!scrollGate.Observe(true,new(GestureKind.Unknown,GestureKind.Unknown,0,1),TimeSpan.FromMilliseconds(200)),"долгая неопределённость прекращает прокрутку");
scrollGate.Observe(true,fistPose,TimeSpan.FromMilliseconds(300));
Check(!scrollGate.Observe(true,new(GestureKind.OpenPalm,GestureKind.Unknown,0,1),TimeSpan.FromMilliseconds(310)),"открытая ладонь сразу прекращает прокрутку");
var sensitiveWheel=new WheelGestureProcessor(3000);sensitiveWheel.Observe(true,.5,TimeSpan.Zero);
Check(sensitiveWheel.Observe(true,.45,TimeSpan.FromMilliseconds(100))==120,"умеренное движение даёт шаг прокрутки");
