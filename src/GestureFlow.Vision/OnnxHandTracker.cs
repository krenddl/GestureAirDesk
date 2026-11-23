// Adapted pipeline from OpenCV Zoo MP-PalmDet / MP-HandPose (Apache-2.0).
// Changes: C# implementation, one-hand selection, full-frame rotation and exact inverse affine mapping.
// See models/NOTICE.md and the accompanying upstream licenses.
using System.Runtime.InteropServices;
using GestureFlow.Core;
using OpenCvSharp;
using OpenCvSharp.Dnn;
namespace GestureFlow.Vision;

public sealed class OnnxHandTracker : IDisposable
{
    readonly Net palm;
    readonly Net hand;
    readonly Point2f[] anchors;
    public OnnxHandTracker(string directory)
    {
        palm = CvDnn.ReadNetFromOnnx(Path.Combine(directory, "palm_detection_mediapipe_2023feb.onnx")) ?? throw new InvalidDataException("Не загружена модель ладони.");
        try { hand = CvDnn.ReadNetFromOnnx(Path.Combine(directory, "handpose_estimation_mediapipe_2023feb.onnx")) ?? throw new InvalidDataException("Не загружена модель руки."); }
        catch { palm.Dispose(); throw; }
        anchors = Enumerable.Range(0, 24 * 24 * 2).Select(i => new Point2f((i / 2 % 24 + .5f) / 24, (i / 2 / 24 + .5f) / 24))
            .Concat(Enumerable.Range(0, 12 * 12 * 6).Select(i => new Point2f((i / 6 % 12 + .5f) / 12, (i / 6 / 12 + .5f) / 12))).ToArray();
    }
    static Mat Input(Mat bgr, int size)
    {
        using var resized = new Mat();
        using var rgb = new Mat();
        using var normalized = new Mat();
        Cv2.Resize(bgr, resized, new Size(size, size), 0, 0, InterpolationFlags.Area);
        Cv2.CvtColor(resized, rgb, ColorConversionCodes.BGR2RGB);
        rgb.ConvertTo(normalized, MatType.CV_32FC3, 1.0 / 255);
        var data = new float[size * size * 3];
        Marshal.Copy(normalized.Data, data, 0, data.Length);
        var blob = new Mat(new[] { 1, size, size, 3 }, MatType.CV_32FC1);
        Marshal.Copy(data, 0, blob.Data, data.Length);
        return blob;
    }
    static Dictionary<string, float[]> Forward(Net net, Mat input)
    {
        net.SetInput(input);
        var names = net.GetUnconnectedOutLayersNames().Select(n => n ?? throw new InvalidDataException("Пустое имя выхода.")).ToArray();
        var mats = names.Select(_ => new Mat()).ToArray();
        try
        {
            net.Forward(mats, names);
            var output = new Dictionary<string, float[]>();
            for (int i = 0; i < mats.Length; i++)
            {
                var values = new float[checked((int)mats[i].Total())];
                Marshal.Copy(mats[i].Data, values, 0, values.Length);
                if (values.Any(v => !float.IsFinite(v))) throw new InvalidDataException("Модель вернула некорректные числа.");
                output.Add(names[i], values);
            }
            return output;
        }
        finally { foreach (var m in mats) m.Dispose(); }
    }
    public string Validate()
    {
        using var black = new Mat(224, 224, MatType.CV_8UC3, Scalar.Black);
        using var p = Input(black, 192);
        using var h = Input(black, 224);
        var po = Forward(palm, p); var ho = Forward(hand, h);
        ValidatePalm(po); ValidateHand(ho);
        return "Palm: " + string.Join(", ", po.Select(x => $"{x.Key}[{x.Value.Length}]")) +
            "; Hand: " + string.Join(", ", ho.Select(x => $"{x.Key}[{x.Value.Length}]"));
    }
    static void ValidatePalm(Dictionary<string, float[]> o)
    {
        if (o.Count != 2 || !o.Values.Any(x => x.Length == 2016 * 18) || !o.Values.Any(x => x.Length == 2016))
            throw new InvalidDataException("Неподдерживаемые выходы модели ладони.");
    }
    static void ValidateHand(Dictionary<string, float[]> o)
    {
        if (!o.TryGetValue("Identity", out var points) || points.Length != 63 ||
           !o.TryGetValue("Identity_1", out var confidence) || confidence.Length != 1)
            throw new InvalidDataException("Неподдерживаемые выходы модели точек руки.");
    }
    public HandObservation? Detect(Mat frame)
    {
        double scale = 192.0 / Math.Max(frame.Width, frame.Height);
        int width = (int)(frame.Width * scale), height = (int)(frame.Height * scale);
        int left = (192 - width) / 2, top = (192 - height) / 2;
        using var resized = new Mat(); using var padded = new Mat();
        Cv2.Resize(frame, resized, new Size(width, height));
        Cv2.CopyMakeBorder(resized, padded, top, 192 - height - top, left, 192 - width - left, BorderTypes.Constant, Scalar.Black);
        using var input = Input(padded, 192);
        var output = Forward(palm, input); ValidatePalm(output);
        var scores = output.Values.Single(v => v.Length == 2016);
        var boxes = output.Values.Single(v => v.Length == 2016 * 18);
        int best = Array.IndexOf(scores, scores.Max());
        double score = 1 / (1 + Math.Exp(-Math.Clamp(scores[best], -80, 80)));
        if (score < .65) return null;
        var points = new Point2f[7];
        for (int i = 0; i < 7; i++) points[i] = new Point2f(
            (float)((boxes[best * 18 + 4 + i * 2] + anchors[best].X * 192 - left) / scale),
            (float)((boxes[best * 18 + 5 + i * 2] + anchors[best].Y * 192 - top) / scale));
        var angle = 90 - Math.Atan2(-(points[2].Y - points[0].Y), points[2].X - points[0].X) * 180 / Math.PI;
        int side = (int)Math.Ceiling(Math.Sqrt((double)frame.Width * frame.Width + (double)frame.Height * frame.Height));
        using var rotation = Cv2.GetRotationMatrix2D(new Point2f(frame.Width / 2f, frame.Height / 2f), angle, 1);
        rotation.Set(0, 2, rotation.At<double>(0, 2) + (side - frame.Width) / 2.0);
        rotation.Set(1, 2, rotation.At<double>(1, 2) + (side - frame.Height) / 2.0);
        Point2f Map(Point2f pt, Mat m) => new((float)(m.At<double>(0, 0) * pt.X + m.At<double>(0, 1) * pt.Y + m.At<double>(0, 2)),
            (float)(m.At<double>(1, 0) * pt.X + m.At<double>(1, 1) * pt.Y + m.At<double>(1, 2)));
        var rotatedPoints = points.Select(pt => Map(pt, rotation)).ToArray();
        float x1 = rotatedPoints.Min(p => p.X), x2 = rotatedPoints.Max(p => p.X);
        float y1 = rotatedPoints.Min(p => p.Y), y2 = rotatedPoints.Max(p => p.Y);
        float cx = (x1 + x2) / 2, cy = (y1 + y2) / 2 - .4f * (y2 - y1);
        int rx1 = Math.Clamp((int)(cx - (x2 - x1) * 1.5), 0, side), rx2 = Math.Clamp((int)(cx + (x2 - x1) * 1.5), 0, side);
        int ry1 = Math.Clamp((int)(cy - (y2 - y1) * 1.5), 0, side), ry2 = Math.Clamp((int)(cy + (y2 - y1) * 1.5), 0, side);
        if (rx2 - rx1 < 8 || ry2 - ry1 < 8) return null;
        using var rotated = new Mat(); Cv2.WarpAffine(frame, rotated, rotation, new Size(side, side));
        using var crop = new Mat(rotated, new Rect(rx1, ry1, rx2 - rx1, ry2 - ry1));
        int cropSide = Math.Max(crop.Width, crop.Height), px = (cropSide - crop.Width) / 2, py = (cropSide - crop.Height) / 2;
        using var square = new Mat();
        Cv2.CopyMakeBorder(crop, square, py, cropSide - crop.Height - py, px, cropSide - crop.Width - px, BorderTypes.Constant, Scalar.Black);
        using var handInput = Input(square, 224);
        var result = Forward(hand, handInput); ValidateHand(result);
        float confidence = result["Identity_1"][0];
        if (confidence < .8f) return null;
        using var inverse = new Mat(); Cv2.InvertAffineTransform(rotation, inverse);
        var landmarks = result["Identity"]; var mapped = new HandPoint[21];
        for (int i = 0; i < 21; i++)
        {
            var pt = Map(new Point2f(landmarks[i * 3] * cropSide / 224 + rx1 - px, landmarks[i * 3 + 1] * cropSide / 224 + ry1 - py), inverse);
            mapped[i] = new(pt.X, pt.Y, landmarks[i * 3 + 2] * cropSide / 224);
        }
        return new(mapped, confidence);
    }
    public static void Draw(Mat frame, HandObservation observation)
    {
        int[][] fingers = [[0, 1, 2, 3, 4], [0, 5, 6, 7, 8], [5, 9, 10, 11, 12], [9, 13, 14, 15, 16], [13, 17, 18, 19, 20], [0, 17]];
        Point Pt(int i) => new((int)observation.Points[i].X, (int)observation.Points[i].Y);
        foreach (var chain in fingers) for (int i = 1; i < chain.Length; i++) Cv2.Line(frame, Pt(chain[i - 1]), Pt(chain[i]), Scalar.LimeGreen, 2);
        for (int i = 0; i < 21; i++) Cv2.Circle(frame, Pt(i), 3, Scalar.Red, -1);
    }
    public void Dispose() { palm.Dispose(); hand.Dispose(); }
}


