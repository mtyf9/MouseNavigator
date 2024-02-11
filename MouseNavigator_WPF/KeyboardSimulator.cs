using System;
using System.Runtime.InteropServices;

public class KeyboardSimulator
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    private struct INPUT
    {
        public int type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    private const int INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    // Virtual-Key Codes
    public const ushort VK_ALT = 0x12;
    public const ushort VK_TAB = 0x09;
    public const ushort VK_CONTROL = 0x11;
    public const ushort VK_SHIFT = 0x10;
    public const ushort VK_ESCAPE = 0x1B;
    public const ushort VK_LWIN = 0x5B;
    public const ushort VK_D = 0x44;

    public static void SimulateShortcut(params ushort[] keys)
    {
        INPUT[] inputs = new INPUT[keys.Length * 2];
        int inputIndex = 0;

        // Press keys
        for (int i = 0; i < keys.Length; i++)
        {
            inputs[inputIndex].type = INPUT_KEYBOARD;
            inputs[inputIndex].U.ki.wVk = keys[i];
            inputs[inputIndex].U.ki.dwFlags = 0; // KEYEVENTF_EXTENDEDKEY for Alt, Ctrl, etc.
            inputIndex++;
        }

        // Release keys in reverse order
        for (int i = keys.Length - 1; i >= 0; i--)
        {
            inputs[inputIndex].type = INPUT_KEYBOARD;
            inputs[inputIndex].U.ki.wVk = keys[i];
            inputs[inputIndex].U.ki.dwFlags = KEYEVENTF_KEYUP;
            inputIndex++;
        }

        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
    }
}
