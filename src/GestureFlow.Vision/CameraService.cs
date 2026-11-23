using GestureFlow.Core;
using System.Diagnostics;
using OpenCvSharp;
namespace GestureFlow.Vision;

public sealed class CameraService : ICameraService
{
    public Task<IReadOnlyList<CameraDevice>> DiscoverAsync() => Task.Run<IReadOnlyList<CameraDevice>>(() =>
    {
        var result = new List<CameraDevice>();
        for (int i = 0; i < 6; i++) { using var c = new VideoCapture(); if (c.Open(i, VideoCaptureAPIs.DSHOW)) result.Add(new(i)); }
        return result;
    });
    // Capture, inference and native resources share a single owning worker.
    public Task RunAsync(int index, Action<CameraFrame> frame, CancellationToken token, bool recognize = false, TrackingPreset preset=TrackingPreset.Balanced) => Task.Run(() =>
    {
        using var c = new VideoCapture();
        if (!c.Open(index, VideoCaptureAPIs.DSHOW))
            throw new IOException("Не удалось открыть камеру. Проверьте разрешения Windows и другие приложения.");
        c.Set(VideoCaptureProperties.FrameWidth, 640); c.Set(VideoCaptureProperties.FrameHeight, 480);
        OnnxHandTracker? tracker = null;
        string recognition = "Распознавание отключено";
        try
        {
            if (recognize)
            {
                try
                {
                    tracker = new OnnxHandTracker(Path.Combine(AppContext.BaseDirectory, "models"));
                    tracker.Validate();
                    recognition = "Эксперимент: рука не найдена";
                }
                catch (Exception ex)
                {
                    tracker?.Dispose(); tracker = null;
                    recognition = "Распознавание недоступно: " + ex.Message;
                }
            }
            var processor = new HandGestureProcessor(preset);
            var clock = Stopwatch.StartNew();
            using var mat = new Mat(); int failures = 0;
            while (!token.IsCancellationRequested)
            {
                if (!c.Read(mat) || mat.Empty())
                {
                    if (++failures >= 15) throw new IOException("Камера перестала передавать кадры.");
                    Thread.Sleep(50); continue;
                }
                failures = 0; int hands = 0;
                GestureSnapshot? gesture = null; HandObservation? visibleHand = null;
                if (tracker != null)
                {
                    try
                    {
                        var result = tracker.Detect(mat);
                        var processed = processor.Process(result, clock.Elapsed);
                        gesture = processed.Gesture; visibleHand = processed.Hand;
                        if (processed.Hand != null) { OnnxHandTracker.Draw(mat, processed.Hand); hands = 1; }
                        recognition = result == null ? "Эксперимент: рука не найдена" : $"Эксперимент: 21 точка · уверенность {result.Confidence:P0}";
                    }
                    catch (Exception ex)
                    {
                        processor.ResetTracking(); tracker.Dispose(); tracker = null; recognition = "Распознавание остановлено: " + ex.Message;
                    }
                }
                if (token.IsCancellationRequested) break;
                frame(new(mat.ToBytes(".jpg"), recognition, hands, gesture, visibleHand, mat.Width, mat.Height)); Thread.Sleep(30);
            }
        }
        finally { tracker?.Dispose(); }
    });
}




