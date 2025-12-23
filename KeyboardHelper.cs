using InputSimulatorStandard;
using InputSimulatorStandard.Native;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

public static class KeyboardHelper
{

    /// <summary>
    /// Parses and executes keyboard shortcuts from string format.
    /// Supports two formats:
    /// 1. KeyPress(VK_F3) - Single key press
    /// 2. ModifiedKeyStroke(CONTROL MENU SHIFT, ESCAPE VK_K) - Modifiers + keys
    /// </summary>
    public static void ExecuteShortcut(string shortcutString)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(shortcutString))
            {
                throw new ArgumentException("Shortcut string cannot be empty");
            }

            shortcutString = shortcutString.Trim();

            // Parse KeyPress format
            if (shortcutString.StartsWith("KeyPress(", StringComparison.OrdinalIgnoreCase))
            {
                ExecuteKeyPress(shortcutString);
            }
            // Parse ModifiedKeyStroke format
            else if (shortcutString.StartsWith("ModifiedKeyStroke(", StringComparison.OrdinalIgnoreCase))
            {
                ExecuteModifiedKeyStroke(shortcutString);
            }
            else
            {
                throw new ArgumentException($"Unknown shortcut format: {shortcutString}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to execute shortcut '{shortcutString}': {ex.Message}");
        }
    }

    /// <summary>
    /// Executes KeyPress format: KeyPress(F3) or KeyPress(VK_F3)
    /// </summary>
    private static void ExecuteKeyPress(string shortcutString)
    {
        // Extract content between parentheses
        var content = ExtractContent(shortcutString, "KeyPress");
        var keyCode = ParseKeyCode(content.Trim());

        var simulator = new InputSimulator();
        simulator.Keyboard.KeyPress(keyCode);
    }

    /// <summary>
    /// Executes ModifiedKeyStroke format: ModifiedKeyStroke(CONTROL MENU SHIFT, ESCAPE VK_K)
    /// Everything before comma = modifiers array
    /// Everything after comma = keys array
    /// </summary>
    private static void ExecuteModifiedKeyStroke(string shortcutString)
    {
        // Extract content between parentheses
        var content = ExtractContent(shortcutString, "ModifiedKeyStroke");

        // Split by comma to separate modifiers from keys
        var parts = content.Split(',');
        if (parts.Length != 2)
        {
            throw new ArgumentException($"ModifiedKeyStroke requires exactly 2 arguments separated by comma: {shortcutString}");
        }

        var modifiersPart = parts[0].Trim();
        var keysPart = parts[1].Trim();

        // Parse modifiers
        var modifiers = ParseKeyCodeArray(modifiersPart);
        if (modifiers.Length == 0)
        {
            throw new ArgumentException($"No modifiers specified in: {shortcutString}");
        }

        // Parse keys
        var keys = ParseKeyCodeArray(keysPart);
        if (keys.Length == 0)
        {
            throw new ArgumentException($"No keys specified in: {shortcutString}");
        }

        var simulator = new InputSimulator();

        // Call appropriate overload based on number of keys
        if (keys.Length == 1)
        {
            simulator.Keyboard.ModifiedKeyStroke(modifiers, keys[0]);
        }
        else
        {
            simulator.Keyboard.ModifiedKeyStroke(modifiers, keys);
        }
    }

    /// <summary>
    /// Extracts content between parentheses from a function call string.
    /// Example: "KeyPress(F3)" returns "F3"
    /// </summary>
    private static string ExtractContent(string input, string functionName)
    {
        var startIndex = input.IndexOf('(');
        var endIndex = input.LastIndexOf(')');

        if (startIndex == -1 || endIndex == -1 || endIndex <= startIndex)
        {
            throw new ArgumentException($"Invalid format for {functionName}: {input}");
        }

        return input.Substring(startIndex + 1, endIndex - startIndex - 1);
    }

    /// <summary>
    /// Parses a single key code string (e.g., "F3" or "VK_F3")
    /// </summary>
    private static VirtualKeyCode ParseKeyCode(string keyString)
    {
        if (string.IsNullOrWhiteSpace(keyString))
        {
            throw new ArgumentException("Key code cannot be empty");
        }

        keyString = keyString.Trim();

        // Try to parse as VirtualKeyCode enum
        if (Enum.TryParse<VirtualKeyCode>(keyString, ignoreCase: true, out var code))
        {
            return code;
        }

        // Try with VK_ prefix if not already present
        if (!keyString.StartsWith("VK_"))
        {
            if (Enum.TryParse<VirtualKeyCode>($"VK_{keyString}", ignoreCase: true, out var codeWithPrefix))
            {
                return codeWithPrefix;
            }
        }

        throw new ArgumentException($"Unknown key code: {keyString}");
    }

    /// <summary>
    /// Parses a space-separated list of key codes.
    /// Example: "CONTROL MENU SHIFT" returns array of 3 VirtualKeyCodes
    /// </summary>
    private static VirtualKeyCode[] ParseKeyCodeArray(string keyString)
    {
        if (string.IsNullOrWhiteSpace(keyString))
        {
            return Array.Empty<VirtualKeyCode>();
        }

        var keyCodes = new List<VirtualKeyCode>();
        var keyTokens = keyString.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var token in keyTokens)
        {
            keyCodes.Add(ParseKeyCode(token));
        }

        return keyCodes.ToArray();
    }

    /* Example methods demonstrating keyboard input simulation
    */
    public static void PressTheSpacebar()
    {
        var simulator = new InputSimulator();
        simulator.Keyboard.KeyPress(VirtualKeyCode.SPACE);
    }
    public static void ShoutHello()
    {
        var simulator = new InputSimulator();

        // Simulate each key stroke
        simulator.Keyboard.KeyDown(VirtualKeyCode.SHIFT);
        simulator.Keyboard.KeyPress(VirtualKeyCode.VK_H);
        simulator.Keyboard.KeyPress(VirtualKeyCode.VK_E);
        simulator.Keyboard.KeyPress(VirtualKeyCode.VK_L);
        simulator.Keyboard.KeyPress(VirtualKeyCode.VK_L);
        simulator.Keyboard.KeyPress(VirtualKeyCode.VK_O);
        simulator.Keyboard.KeyPress(VirtualKeyCode.VK_1);
        simulator.Keyboard.KeyUp(VirtualKeyCode.SHIFT);

        // Alternatively you can simulate text entry to acheive the same end result
        simulator.Keyboard.TextEntry("HELLO!");
    }

    public static void SimulateSomeModifiedKeystrokes()
    {
        var simulator = new InputSimulator();

        // CTRL-C (effectively a copy command in many situations)
        simulator.Keyboard.ModifiedKeyStroke(VirtualKeyCode.CONTROL, VirtualKeyCode.VK_C);

        // You can simulate chords with multiple modifiers
        // For example CTRL-K-C whic is simulated as
        // CTRL-down, K, C, CTRL-up
        simulator.Keyboard.ModifiedKeyStroke(VirtualKeyCode.CONTROL, new[] { VirtualKeyCode.VK_K, VirtualKeyCode.VK_C });

        // You can simulate complex chords with multiple modifiers and key presses
        // For example CTRL-ALT-SHIFT-ESC-K which is simulated as
        // CTRL-down, ALT-down, SHIFT-down, press ESC, press K, SHIFT-up, ALT-up, CTRL-up
        simulator.Keyboard.ModifiedKeyStroke(
            new[] { VirtualKeyCode.CONTROL, VirtualKeyCode.MENU, VirtualKeyCode.SHIFT },
            new[] { VirtualKeyCode.ESCAPE, VirtualKeyCode.VK_K });
    }
}

