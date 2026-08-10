using System.Runtime.InteropServices;
using System.Text;

namespace FlyCtl;

/// <summary>
/// Fast command-line tool to send commands to FlyMenu via WM_COPYDATA.
/// Usage:
///   flyctl [command1] [command2]
///
/// If two commands are supplied, FlyCtl behaves like a "double-tap":
///   - First invocation runs command1 and stores a timestamp in flyctl.ini
///   - A second invocation within DoubleClickWindowMs runs command2 instead
///     of command1 (still touching the timestamp).
///
/// Example: flyctl "latest" "Next Desktop"
///   Single press  -> switch to latest desktop
///   Quick re-press -> Next Desktop
/// </summary>
class Program
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct COPYDATASTRUCT
    {
        public IntPtr dwData;
        public int cbData;
        public IntPtr lpData;
    }

    private const uint WM_COPYDATA = 0x004A;
    private const string WINDOW_TITLE = "FlyMenuReceiverWindow";
    private const int DefaultDoubleClickWindowMs = 400;
    private static IntPtr foundWindowHandle = IntPtr.Zero;

    static int Main(string[] args)
    {
        var cfg = ReadConfig();

        // Parse up to two commands. Defaults come from flyctl.ini (defaultCommand1/2),
        // falling back to "show" if nothing is configured.
        string cmd1 = args.Length > 0 ? args[0] : (cfg.defaultCmd1 ?? "show");
        string? cmd2 = args.Length > 1 ? args[1] : (args.Length == 0 ? cfg.defaultCmd2 : null);

        // Second-tap logic: only relevant when a second command was supplied.
        string command = cmd1;
        if (cmd2 != null)
        {
            var now = DateTimeOffset.UtcNow;
            if (cfg.hasLast && (now - cfg.last).TotalMilliseconds <= cfg.windowMs)
            {
                command = cmd2;
            }
            WriteLastInvocation(now, cfg.windowMs, cfg.defaultCmd1, cfg.defaultCmd2);
        }

        if (!FindFlyMenuWindow())
        {
            Console.Error.WriteLine("Error: FlyMenu is not running (receiver window not found)");
            Console.Error.WriteLine("Tip: Make sure FlyMenu.exe is running in the system tray");
            return 1;
        }

        if (SendCommandToFlyMenu(command))
        {
            return 0;
        }
        else
        {
            Console.Error.WriteLine("Warning: Message sent but receiver returned zero");
            return 1;
        }
    }

    // ---------- flyctl.ini (timestamp store) ----------

    private static string IniPath =>
        Path.Combine(AppContext.BaseDirectory, "flyctl.ini");

    private record struct IniConfig(DateTimeOffset last, bool hasLast, int windowMs, string? defaultCmd1, string? defaultCmd2);

    private static IniConfig ReadConfig()
    {
        DateTimeOffset ts = default;
        bool hasTs = false;
        int windowMs = DefaultDoubleClickWindowMs;
        string? def1 = null;
        string? def2 = null;
        try
        {
            if (!File.Exists(IniPath)) return new IniConfig(default, false, windowMs, null, null);
            foreach (var raw in File.ReadAllLines(IniPath))
            {
                var line = raw.Trim();
                if (TryGetValue(line, "lastInvocation=", out var lastVal))
                {
                    if (DateTimeOffset.TryParse(lastVal, System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                        out var parsed))
                    {
                        ts = parsed;
                        hasTs = true;
                    }
                }
                else if (TryGetValue(line, "doubleClickWindowMs=", out var msVal))
                {
                    if (int.TryParse(msVal, System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture, out var parsedMs) && parsedMs >= 0)
                    {
                        windowMs = parsedMs;
                    }
                }
                else if (TryGetValue(line, "defaultCommand1=", out var d1))
                {
                    def1 = Unquote(d1);
                }
                else if (TryGetValue(line, "defaultCommand2=", out var d2))
                {
                    def2 = Unquote(d2);
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"flyctl.ini read failed: {ex.Message}");
        }
        return new IniConfig(ts, hasTs, windowMs, def1, def2);
    }

    private static bool TryGetValue(string line, string key, out string value)
    {
        if (line.StartsWith(key, StringComparison.OrdinalIgnoreCase))
        {
            value = line.Substring(key.Length).Trim();
            return true;
        }
        value = string.Empty;
        return false;
    }

    private static string Unquote(string s)
    {
        if (s.Length >= 2 && s[0] == '"' && s[^1] == '"')
            return s.Substring(1, s.Length - 2);
        return s;
    }

    private static void WriteLastInvocation(DateTimeOffset now, int windowMs, string? defaultCmd1, string? defaultCmd2)
    {
        try
        {
            var iso = now.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
            var sb = new StringBuilder();
            sb.AppendLine("[flyctl]");
            sb.AppendLine("doubleClickWindowMs=" + windowMs.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (defaultCmd1 != null) sb.AppendLine("defaultCommand1=\"" + defaultCmd1 + "\"");
            if (defaultCmd2 != null) sb.AppendLine("defaultCommand2=\"" + defaultCmd2 + "\"");
            sb.AppendLine("lastInvocation=" + iso);
            File.WriteAllText(IniPath, sb.ToString());
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"flyctl.ini write failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Finds the FlyMenu receiver window by enumerating all windows
    /// </summary>
    private static bool FindFlyMenuWindow()
    {
        foundWindowHandle = IntPtr.Zero;

        // Enumerate all windows to find FlyMenuReceiverWindow
        EnumWindows((hWnd, lParam) =>
        {
            var sb = new StringBuilder(256);
            GetWindowText(hWnd, sb, sb.Capacity);
            string title = sb.ToString();

            if (title == WINDOW_TITLE)
            {
                foundWindowHandle = hWnd;
                return false; // Stop enumeration
            }

            return true; // Continue enumeration
        }, IntPtr.Zero);

        return foundWindowHandle != IntPtr.Zero;
    }

    /// <summary>
    /// Sends a command to FlyMenu via WM_COPYDATA
    /// </summary>
    private static bool SendCommandToFlyMenu(string command)
    {
        // Prepare message with null terminator
        byte[] messageBytes = Encoding.UTF8.GetBytes(command + "\0");
        IntPtr dataPtr = Marshal.AllocHGlobal(messageBytes.Length);

        try
        {
            // Copy message to unmanaged memory
            Marshal.Copy(messageBytes, 0, dataPtr, messageBytes.Length);

            // Create COPYDATASTRUCT
            COPYDATASTRUCT cds = new COPYDATASTRUCT
            {
                dwData = IntPtr.Zero,
                cbData = messageBytes.Length,
                lpData = dataPtr
            };

            // Allocate memory for struct
            IntPtr cdsPtr = Marshal.AllocHGlobal(Marshal.SizeOf<COPYDATASTRUCT>());

            try
            {
                // Copy struct to unmanaged memory
                Marshal.StructureToPtr(cds, cdsPtr, false);

                // Send WM_COPYDATA message
                IntPtr result = SendMessage(foundWindowHandle, WM_COPYDATA, IntPtr.Zero, cdsPtr);

                return result != IntPtr.Zero;
            }
            finally
            {
                Marshal.FreeHGlobal(cdsPtr);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(dataPtr);
        }
    }
}
