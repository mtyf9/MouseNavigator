using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Vanara.PInvoke;

public static class MouseKeyboardSimulator
{
    private static readonly User32.INPUT[] _singleKeyDownUp = new User32.INPUT[2];

    public static void SimulateShortcut(params User32.VK[] keys)
    {
        if (keys is null || keys.Length == 0)
        {
            return;
        }

        var inputs = new List<User32.INPUT>(keys.Length * 2);

        foreach (var key in keys)
        {
            inputs.Add(CreateKeyInput(key, isKeyUp: false));
        }

        for (var i = keys.Length - 1; i >= 0; i--)
        {
            inputs.Add(CreateKeyInput(keys[i], isKeyUp: true));
        }

        var inputArray = inputs.ToArray();
        var sent = User32.SendInput((uint)inputArray.Length, inputArray, Marshal.SizeOf<User32.INPUT>());
        if (sent == 0)
        {
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    public static void KeyPress(User32.VK key)
    {
        _singleKeyDownUp[0] = CreateKeyInput(key, isKeyUp: false);
        _singleKeyDownUp[1] = CreateKeyInput(key, isKeyUp: true);

        var sent = User32.SendInput((uint)_singleKeyDownUp.Length, _singleKeyDownUp, Marshal.SizeOf<User32.INPUT>());
        if (sent == 0)
        {
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    private static User32.INPUT CreateKeyInput(User32.VK key, bool isKeyUp)
    {
        return new User32.INPUT
        {
            type = User32.INPUTTYPE.INPUT_KEYBOARD,
            ki = new User32.KEYBDINPUT
            {
                wVk = (ushort)key,
                dwFlags = isKeyUp ? User32.KEYEVENTF.KEYEVENTF_KEYUP : 0,
            },
        };
    }

    public static void TestShowDesktop()
    {
        SimulateShortcut(User32.VK.VK_LWIN, User32.VK.VK_D);
    }
}
