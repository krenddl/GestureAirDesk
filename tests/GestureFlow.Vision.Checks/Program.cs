using GestureFlow.Vision;
using GestureFlow.Core;
using OpenCvSharp;
if (args.Length != 1) throw new ArgumentException("Укажите каталог models.");
try { using var missing = new OnnxHandTracker(Path.Combine(args[0], "missing")); throw new Exception("Отсутствующая модель не отклонена."); }
catch (OpenCVException) { Console.WriteLine("PASS: отсутствующая модель отклонена."); }
using var tracker = new OnnxHandTracker(args[0]);
Console.WriteLine(tracker.Validate());
using var black = new Mat(480, 640, MatType.CV_8UC3, Scalar.Black);
if (tracker.Detect(black) != null) throw new Exception("Ложное обнаружение на чёрном кадре.");
Console.WriteLine("PASS: чёрный кадр не содержит руки.");
using var sample = Cv2.ImRead("tests/fixtures/upstream-demo.webp");
if (sample.Empty()) throw new Exception("Не удалось прочитать пример OpenCV Zoo.");
using var photo = new Mat(sample, new Rect(0, 25, 490, 365));
foreach (var angle in new[] { 0, 90 })
{
    using var test = new Mat();
    if (angle == 0) photo.CopyTo(test); else Cv2.Rotate(photo, test, RotateFlags.Rotate90Clockwise);
    var observation = tracker.Detect(test) ?? throw new Exception($"Рука не найдена: угол {angle}.");
    if (observation.Points.Count != 21 || observation.Points.Any(p => !float.IsFinite(p.X) || !float.IsFinite(p.Y) || p.X < 0 || p.Y < 0 || p.X > test.Width || p.Y > test.Height)) throw new Exception("Некорректные точки.");
    Console.WriteLine($"PASS: 21 точка; угол {angle}; уверенность {observation.Confidence:F3}");
    if (GestureClassifier.Classify(observation) != GestureKind.OpenPalm) throw new Exception("Открытая ладонь на примере не распознана.");
    Console.WriteLine("PASS: открытая ладонь на реальных выходах ONNX.");
    OnnxHandTracker.Draw(test, observation); Cv2.ImWrite($"tests/fixtures/check-result-{angle}.jpg", test);
}

