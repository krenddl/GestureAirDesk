using System.Runtime.InteropServices;
using System.Text;
using System.ComponentModel;
namespace GestureFlow.Windows;
public enum WindowAction { Minimize, Maximize, Restore }
public sealed record WindowTarget(IntPtr Handle,uint ProcessId,string Title){
    public override string ToString()=>Title;
}
public sealed class WindowManager
{
    public IReadOnlyList<WindowTarget> List(IntPtr exclude)
    {
        if(!OperatingSystem.IsWindows())return [];
        var result=new List<WindowTarget>();
        if(!EnumWindows((handle,_)=>{
            if(handle==exclude||!IsWindowVisible(handle)||GetWindow(handle,4)!=IntPtr.Zero)return true;
            var text=new StringBuilder(1024);GetWindowText(handle,text,text.Capacity);
            if(text.Length==0)return true;
            GetWindowThreadProcessId(handle,out var pid);
            result.Add(new(handle,pid,text.ToString()));return true;
        },IntPtr.Zero))throw new Win32Exception(Marshal.GetLastWin32Error());
        return result.OrderBy(w=>w.Title).ToArray();
    }
    void Validate(WindowTarget target){
        if(!OperatingSystem.IsWindows()||!IsWindow(target.Handle))throw new InvalidOperationException("Окно недоступно. Обновите список.");
        GetWindowThreadProcessId(target.Handle,out var pid);
        if(pid!=target.ProcessId)throw new InvalidOperationException("Окно изменилось. Обновите список.");
    }
    public void Execute(WindowTarget target,WindowAction action){
        Validate(target);
        int command=action switch{WindowAction.Minimize=>6,WindowAction.Maximize=>3,WindowAction.Restore=>9,_=>throw new ArgumentOutOfRangeException(nameof(action))};
        if(!ShowWindowAsync(target.Handle,command))throw new Win32Exception(Marshal.GetLastWin32Error(),"Команда окна не принята.");
    }
    public string State(WindowTarget target){
        Validate(target);return IsIconic(target.Handle)?"свёрнуто":IsZoomed(target.Handle)?"развёрнуто":"обычный размер";
    }
    delegate bool EnumCallback(IntPtr hwnd,IntPtr parameter);
    [DllImport("user32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]static extern bool EnumWindows(EnumCallback callback,IntPtr parameter);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetWindowText(IntPtr hwnd,StringBuilder text,int max);
    [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint pid);
    [DllImport("user32.dll")]static extern IntPtr GetWindow(IntPtr hwnd,uint command);
    [DllImport("user32.dll")][return:MarshalAs(UnmanagedType.Bool)]static extern bool IsWindow(IntPtr hwnd);
    [DllImport("user32.dll")][return:MarshalAs(UnmanagedType.Bool)]static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")][return:MarshalAs(UnmanagedType.Bool)]static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")][return:MarshalAs(UnmanagedType.Bool)]static extern bool IsZoomed(IntPtr hwnd);
    [DllImport("user32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]static extern bool ShowWindowAsync(IntPtr hwnd,int command);
}

