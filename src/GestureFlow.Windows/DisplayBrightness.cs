using System.Runtime.Versioning;

namespace GestureFlow.Windows;

[SupportedOSPlatform("windows")]
public static class DisplayBrightness
{
    public static int Adjust(int step)
    {
        var type = Type.GetTypeFromProgID("WbemScripting.SWbemLocator")
            ?? throw new InvalidOperationException("Windows не предоставляет управление яркостью.");
        dynamic locator = Activator.CreateInstance(type)!;
        dynamic service = locator.ConnectServer(".", @"root\wmi");
        foreach (dynamic monitor in service.ExecQuery("SELECT * FROM WmiMonitorBrightness WHERE Active = TRUE"))
        {
            int current = Convert.ToInt32(monitor.Properties_.Item("CurrentBrightness").Value);
            string name = Convert.ToString(monitor.Properties_.Item("InstanceName").Value)!;
            foreach (dynamic method in service.ExecQuery("SELECT * FROM WmiMonitorBrightnessMethods WHERE Active = TRUE"))
            {
                string instance = Convert.ToString(method.Properties_.Item("InstanceName").Value)!;
                if (!string.Equals(name, instance, StringComparison.OrdinalIgnoreCase)) continue;

                var desired = (byte)Math.Clamp(current + step, 0, 100);
                // WMI method signatures are exposed on the class, not the monitor instance.
                dynamic methodClass = service.Get("WmiMonitorBrightnessMethods", 0);
                dynamic signature = methodClass.Methods_.Item("WmiSetBrightness", 0).InParameters
                    ?? throw new InvalidOperationException("Драйвер не предоставил параметры команды яркости.");
                dynamic input = signature.SpawnInstance_(0);
                input.Properties_.Item("Timeout").Value = 0u;
                input.Properties_.Item("Brightness").Value = desired;
                dynamic result = method.ExecMethod_("WmiSetBrightness", input);
                if (result is not null && Convert.ToUInt32(result.Properties_.Item("ReturnValue").Value) != 0)
                    throw new InvalidOperationException("Драйвер отклонил изменение яркости.");
                dynamic updated = service.Get(monitor.Path_.RelPath, 0);
                int applied = Convert.ToInt32(updated.Properties_.Item("CurrentBrightness").Value);
                if (applied == current && desired != current)
                    throw new InvalidOperationException("Экран не подтвердил изменение яркости.");
                return applied;
            }
        }
        throw new InvalidOperationException("Windows не нашла экран с поддержкой управления яркостью.");
    }
}