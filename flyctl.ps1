param(
    [Parameter(Position=0)]
    [string]$Command = "show"
)

# Hide the PowerShell window immediately
Add-Type -Name Window -Namespace Console -MemberDefinition '
[DllImport("Kernel32.dll")]
public static extern IntPtr GetConsoleWindow();

[DllImport("user32.dll")]
public static extern bool ShowWindow(IntPtr hWnd, Int32 nCmdShow);
'

$consolePtr = [Console.Window]::GetConsoleWindow()
[Console.Window]::ShowWindow($consolePtr, 0) | Out-Null  # 0 = SW_HIDE

# Add Windows API definitions
Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;

public class FlyMenuAPI {
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
  public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct COPYDATASTRUCT {
      public IntPtr dwData;
    public int cbData;
        public IntPtr lpData;
    }
    
    public const uint WM_COPYDATA = 0x004A;
}
"@

# Find the FlyMenu window by enumerating all windows
$foundHandle = [IntPtr]::Zero

$callback = {
    param($hWnd, $lParam)
    
    $sb = New-Object System.Text.StringBuilder 256
    [FlyMenuAPI]::GetWindowText($hWnd, $sb, $sb.Capacity) | Out-Null
    $title = $sb.ToString()
    
    if ($title -eq "FlyMenuReceiverWindow") {
        $script:foundHandle = $hWnd
        return $false  # Stop enumeration
    }
    
    return $true  # Continue enumeration
}

[FlyMenuAPI]::EnumWindows($callback, [IntPtr]::Zero) | Out-Null

if ($foundHandle -eq [IntPtr]::Zero) {
    # Don't show error in hidden mode, just exit silently
    exit 1
}

# Prepare the message data
$messageBytes = [Text.Encoding]::UTF8.GetBytes($Command + "`0")

# Allocate memory for the message
$dataPtr = [Runtime.InteropServices.Marshal]::AllocHGlobal($messageBytes.Length)

try {
    # Copy message to unmanaged memory
    [Runtime.InteropServices.Marshal]::Copy($messageBytes, 0, $dataPtr, $messageBytes.Length)
 
    # Create COPYDATASTRUCT
    $cds = New-Object FlyMenuAPI+COPYDATASTRUCT
    $cds.dwData = [IntPtr]::Zero
    $cds.cbData = $messageBytes.Length
    $cds.lpData = $dataPtr
    
    # Allocate memory for COPYDATASTRUCT
    $cdsSize = [Runtime.InteropServices.Marshal]::SizeOf([type][FlyMenuAPI+COPYDATASTRUCT])
    $cdsPtr = [Runtime.InteropServices.Marshal]::AllocHGlobal($cdsSize)
    
    try {
  # Copy struct to unmanaged memory
        [Runtime.InteropServices.Marshal]::StructureToPtr($cds, $cdsPtr, $false)
        
        # Send the message
        $result = [FlyMenuAPI]::SendMessage($foundHandle, [FlyMenuAPI]::WM_COPYDATA, [IntPtr]::Zero, $cdsPtr)
  
   if ($result -ne [IntPtr]::Zero) {
            exit 0
      } else {
            exit 1
  }
    } finally {
        [Runtime.InteropServices.Marshal]::FreeHGlobal($cdsPtr)
    }
} finally {
    [Runtime.InteropServices.Marshal]::FreeHGlobal($dataPtr)
}