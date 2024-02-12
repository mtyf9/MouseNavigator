using System.Threading;
using WindowsInput;
using WindowsInput.Native;

public class MouseKeyboardSimulator
{
    static InputSimulator inputSimulator = new InputSimulator();

    public static void SimulateShortcut(params VirtualKeyCode[] keys)
    {
        //Press keys
        for (int i = 0; i < keys.Length; i++)
        {
            inputSimulator.Keyboard.KeyDown(keys[i]);
        }

        // Release keys in reverse order
        for (int i = keys.Length - 1; i >= 0; i--)
        {
            inputSimulator.Keyboard.KeyUp(keys[i]);
        }
    }

    public static void test()
    {
        //InputSimulator.SimulateTextEntry("Say hello!");

        inputSimulator.Keyboard.KeyDown(VirtualKeyCode.LWIN);
        inputSimulator.Keyboard.KeyPress(VirtualKeyCode.VK_D);
        inputSimulator.Keyboard.KeyUp(VirtualKeyCode.LWIN);
    }
    //public static void SimulateSomeModifiedKeystrokes()
    //{
    //    // CTRL-C (effectively a copy command in many situations)
    //    InputSimulator.SimulateModifiedKeyStroke(VirtualKeyCode.CONTROL, VirtualKeyCode.VK_C);

    //    // You can simulate chords with multiple modifiers
    //    // For example CTRL-K-C whic is simulated as
    //    // CTRL-down, K, C, CTRL-up
    //    InputSimulator.SimulateModifiedKeyStroke(VirtualKeyCode.CONTROL, new[] { VirtualKeyCode.VK_K, VirtualKeyCode.VK_C });

    //    // You can simulate complex chords with multiple modifiers and key presses
    //    // For example CTRL-ALT-SHIFT-ESC-K which is simulated as
    //    // CTRL-down, ALT-down, SHIFT-down, press ESC, press K, SHIFT-up, ALT-up, CTRL-up
    //    InputSimulator.SimulateModifiedKeyStroke(
    //      new[] { VirtualKeyCode.CONTROL, VirtualKeyCode.MENU, VirtualKeyCode.SHIFT },
    //      new[] { VirtualKeyCode.ESCAPE, VirtualKeyCode.VK_K });
    //}


    //[DllImport("user32.dll", SetLastError = true)]
    //private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    //private struct INPUT
    //{
    //    public int type;
    //    public InputUnion U;
    //}

    //[StructLayout(LayoutKind.Explicit)]
    //private struct InputUnion
    //{
    //    [FieldOffset(0)] public MOUSEINPUT mi;
    //    [FieldOffset(0)] public KEYBDINPUT ki;
    //    [FieldOffset(0)] public HARDWAREINPUT hi;
    //}

    //private struct MOUSEINPUT
    //{
    //    public int dx;
    //    public int dy;
    //    public uint mouseData;
    //    public uint dwFlags;
    //    public uint time;
    //    public IntPtr dwExtraInfo;
    //}

    //private struct KEYBDINPUT
    //{
    //    public ushort wVk;
    //    public ushort wScan;
    //    public uint dwFlags;
    //    public uint time;
    //    public IntPtr dwExtraInfo;
    //}

    //private struct HARDWAREINPUT
    //{
    //    public uint uMsg;
    //    public ushort wParamL;
    //    public ushort wParamH;
    //}

    //private const int INPUT_KEYBOARD = 1;
    //private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    //private const uint KEYEVENTF_KEYUP = 0x0002;

    //// Virtual-Key Codes
    //public const ushort VK_ALT = 0x12;
    //public const ushort VK_TAB = 0x09;
    //public const ushort VK_CONTROL = 0x11;
    //public const ushort VK_SHIFT = 0x10;
    //public const ushort VK_ESCAPE = 0x1B;
    //public const ushort VK_LWIN = 0x5B;
    //public const ushort VK_D = 0x44;

    //public static void SimulateShortcut(params ushort[] keys)
    //{
    //    INPUT[] inputs = new INPUT[keys.Length * 2];
    //    int inputIndex = 0;

    //    // Press keys
    //    for (int i = 0; i < keys.Length; i++)
    //    {
    //        inputs[inputIndex].type = INPUT_KEYBOARD;
    //        inputs[inputIndex].U.ki.wVk = keys[i];
    //        inputs[inputIndex].U.ki.dwFlags = 0; // KEYEVENTF_EXTENDEDKEY for Alt, Ctrl, etc.
    //        inputIndex++;
    //    }

    //    // Release keys in reverse order
    //    for (int i = keys.Length - 1; i >= 0; i--)
    //    {
    //        inputs[inputIndex].type = INPUT_KEYBOARD;
    //        inputs[inputIndex].U.ki.wVk = keys[i];
    //        inputs[inputIndex].U.ki.dwFlags = KEYEVENTF_KEYUP;
    //        inputIndex++;
    //    }

    //    SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
    //}
}
