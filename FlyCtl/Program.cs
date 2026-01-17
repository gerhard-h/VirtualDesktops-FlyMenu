using System.Runtime.InteropServices;
using System.Text;

namespace FlyCtl;

/// <summary>
/// Fast command-line tool to send commands to FlyMenu via WM_COPYDATA
/// Usage: flyctl [command]
/// Example: flyctl show
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
    private static IntPtr foundWindowHandle = IntPtr.Zero;

    static int Main(string[] args)
    {
        // Default to "show" if no arguments
        string command = args.Length > 0 ? string.Join(" ", args) : "show";

        // Find the FlyMenu receiver window
        if (!FindFlyMenuWindow())
        {
            Console.Error.WriteLine("Error: FlyMenu is not running (receiver window not found)");
            Console.Error.WriteLine("Tip: Make sure FlyMenu.exe is running in the system tray");
            return 1;
        }

        // Send the command
        if (SendCommandToFlyMenu(command))
        {
            return 0; // Success
        }
        else
        {
            Console.Error.WriteLine("Warning: Message sent but receiver returned zero");
            return 1;
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
