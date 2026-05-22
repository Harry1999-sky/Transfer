using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace LanTransfer.Services;

public static class NetworkHelper
{
    private static readonly Random _random = new();

    [DllImport("kernel32.dll")]
    private static extern uint SetThreadExecutionState(uint esFlags);

    private const uint ES_CONTINUOUS = 0x80000000;
    private const uint ES_SYSTEM_REQUIRED = 0x00000001;

    /// <summary>
    /// 阻止系统休眠（传输期间调用）。
    /// </summary>
    public static void PreventSleep()
    {
        try { SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED); } catch { }
    }

    /// <summary>
    /// 恢复系统休眠策略（传输结束后调用）。
    /// </summary>
    public static void AllowSleep()
    {
        try { SetThreadExecutionState(ES_CONTINUOUS); } catch { }
    }

    public static string GetLocalIpAddress()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                return ip.ToString();
        }
        return "127.0.0.1";
    }

    public static int FindAvailablePort(int startPort = 18888)
    {
        for (var port = startPort; port < startPort + 100; port++)
        {
            try
            {
                var listener = new TcpListener(IPAddress.Loopback, port);
                listener.Start();
                listener.Stop();
                return port;
            }
            catch (SocketException) { }
        }
        return startPort;
    }

    public static string GenerateConnectionCode()
    {
        return _random.Next(1000, 10000).ToString();
    }

    /// <summary>
    /// 确保防火墙允许指定端口的入站连接。返回是否成功。
    /// 启动时先清理旧规则再添加新规则，避免崩溃后规则残留或端口变化导致多条规则。
    /// </summary>
    public static bool EnsureFirewallRule(int port)
    {
        var ruleName = "LanTransfer";

        // 如果规则已存在且端口匹配，直接复用
        if (RuleExists(ruleName, port))
            return true;

        // 清理旧规则（端口不匹配或上次崩溃残留）
        DeleteRule(ruleName);

        // 添加新规则
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = $"advfirewall firewall add rule name=\"{ruleName}\" dir=in action=allow protocol=TCP localport={port}",
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true
            };
            var proc = Process.Start(psi);
            proc?.WaitForExit(5000);
            return proc?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool RuleExists(string ruleName, int port)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = $"advfirewall firewall show rule name=\"{ruleName}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            var proc = Process.Start(psi);
            var output = proc?.StandardOutput.ReadToEnd();
            proc?.WaitForExit(3000);
            return output != null && output.Contains(ruleName) && output.Contains(port.ToString());
        }
        catch
        {
            return false;
        }
    }

    private static void DeleteRule(string ruleName)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = $"advfirewall firewall delete rule name=\"{ruleName}\"",
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true
            };
            var proc = Process.Start(psi);
            proc?.WaitForExit(5000);
        }
        catch { }
    }
}
