using System.ComponentModel;
using System.Runtime.InteropServices;
using GestureFlow.Core;
namespace GestureFlow.Windows;

public sealed class WindowsIntegration : IWindowsIntegration
{
    public bool IsAvailable => OperatingSystem.IsWindows() && Environment.Is64BitProcess;
    public void Execute(MediaCommand command)
    {
        if (!IsAvailable) throw new PlatformNotSupportedException("Требуется Windows x64.");
        ushort key = command switch
        {
            MediaCommand.VolumeDown => 0xAE,
            MediaCommand.VolumeUp => 0xAF,
            MediaCommand.Mute => 0xAD,
            MediaCommand.PlayPause => 0xB3,
            MediaCommand.NextTrack => 0xB0,
            MediaCommand.PreviousTrack => 0xB1,
            _ => throw new ArgumentOutOfRangeException(nameof(command))
        };
        var down = new Input { Type = 1, Keyboard = new KeyboardInput { VirtualKey = key, Flags = 1 } };
        var up = new Input { Type = 1, Keyboard = new KeyboardInput { VirtualKey = key, Flags = 3 } };
        uint sent = SendInput(2, [down, up], Marshal.SizeOf<Input>());
        if (sent != 2)
        {
            int error = Marshal.GetLastWin32Error();
            // Best effort release if only the down event was accepted.
            if (sent == 1) SendInput(1, [up], Marshal.SizeOf<Input>());
            throw new Win32Exception(error, "Windows не приняла медиакоманду полностью. Возможна блокировка ввода или разница уровней доступа.");
        }
    }
    [StructLayout(LayoutKind.Explicit, Size = 40)]
    struct Input
    {
        [FieldOffset(0)] public uint Type;
        [FieldOffset(8)] public KeyboardInput Keyboard;
        [FieldOffset(8)] public MouseInput Mouse;
    }
    [StructLayout(LayoutKind.Sequential)]
    struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }
    public bool LeftButtonHeld => IsAvailable && GetAsyncKeyState(1) < 0;
    public bool EscapeHeld => IsAvailable && GetAsyncKeyState(0x1B) < 0;
    public void MovePointer(double x, double y)
    {
        if (!IsAvailable) throw new PlatformNotSupportedException("Требуется Windows x64.");
        if (!double.IsFinite(x) || !double.IsFinite(y)) throw new ArgumentException("Некорректные координаты.");
        int width = GetSystemMetrics(0), height = GetSystemMetrics(1);
        if (width <= 0 || height <= 0) throw new InvalidOperationException("Экран недоступен.");
        if (!SetCursorPos((int)Math.Round(Math.Clamp(x, 0, 1) * (width - 1)), (int)Math.Round(Math.Clamp(y, 0, 1) * (height - 1))))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Не удалось переместить курсор.");
    }
    public void SetLeftButton(bool down)
    {
        if (!IsAvailable) throw new PlatformNotSupportedException("Требуется Windows x64.");
        var input = new Input { Type = 0, Mouse = new MouseInput { Flags = down ? 2u : 4u } };
        if (SendInput(1, [input], Marshal.SizeOf<Input>()) != 1) throw new Win32Exception(Marshal.GetLastWin32Error(), "Не удалось изменить состояние левой кнопки.");
    }
    public void Scroll(int delta)
    {
        if (!IsAvailable) throw new PlatformNotSupportedException("Требуется Windows x64.");
        if (delta == 0) return;
        if (delta % 120 != 0 || Math.Abs((long)delta) > 360) throw new ArgumentOutOfRangeException(nameof(delta));
        var input = new Input { Type = 0, Mouse = new MouseInput { Flags = 0x0800, Data = unchecked((uint)delta) } };
        if (SendInput(1, [input], Marshal.SizeOf<Input>()) != 1) throw new Win32Exception(Marshal.GetLastWin32Error(), "Не удалось отправить прокрутку.");
    }
    public void MovePointerPixels(int x, int y)
    {
        if (!IsAvailable) throw new PlatformNotSupportedException("Требуется Windows x64.");
        if (!SetCursorPos(x, y)) throw new Win32Exception(Marshal.GetLastWin32Error());
    }
    public void Click(MouseClickKind kind = MouseClickKind.Left)
    {
        if (!IsAvailable) throw new PlatformNotSupportedException("Требуется Windows x64.");
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        var down = new Input { Type = 0, Mouse = new MouseInput { Flags = kind == MouseClickKind.Right ? 8u : 2u } };
        var up = new Input { Type = 0, Mouse = new MouseInput { Flags = kind == MouseClickKind.Right ? 16u : 4u } };
        Input[] events = kind == MouseClickKind.Double ? [down, up, down, up] : [down, up];
        uint sent = SendInput((uint)events.Length, events, Marshal.SizeOf<Input>());
        if (sent != events.Length)
        {
            int error = Marshal.GetLastWin32Error(); if (sent % 2 == 1) SendInput(1, [up], Marshal.SizeOf<Input>());
            throw new Win32Exception(error, "Не удалось отправить клик полностью.");
        }
    }
    [StructLayout(LayoutKind.Sequential)]
    struct MouseInput { public int X, Y; public uint Data, Flags, Time; public UIntPtr ExtraInfo; }
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll", SetLastError = true)]
    static extern uint SendInput(uint count, [In] Input[] inputs, int size);
}






