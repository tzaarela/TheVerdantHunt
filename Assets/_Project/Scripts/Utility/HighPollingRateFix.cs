// HighPollingRateFix.cs
// Fixes performance degradation with high polling rate mice (2kHz+) in Unity standalone builds.
// Uses RIDEV_NOLEGACY to suppress expensive legacy WM_MOUSE* message translation,
// then manually injects mouse state via WinAPI + Input System events.

using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public class HighPollingRateFix: MonoBehaviour
{
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR

    [StructLayout(LayoutKind.Sequential)] struct RAWINPUTDEVICE { public ushort usUsagePage, usUsage; public uint dwFlags; public IntPtr hwndTarget; }
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int x, y; }

    [DllImport("user32.dll", SetLastError = true)] static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] d, int c, int s);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int k);

    static readonly int kDevSize = Marshal.SizeOf<RAWINPUTDEVICE>();
    Vector2 _lastPos;

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
        ApplyFix();
    }

    void OnApplicationFocus(bool f) { if (f) ApplyFix(); }

    void Update()
    {
        if (Mouse.current == null) return;
        if (!GetCursorPos(out POINT pt)) return;

        var pos = new Vector2(pt.x, Screen.height - 1 - pt.y);
        var delta = pos - _lastPos;

        bool lmb = (GetAsyncKeyState(0x01) & 0x8000) != 0;
        bool rmb = (GetAsyncKeyState(0x02) & 0x8000) != 0;
        bool mmb = (GetAsyncKeyState(0x04) & 0x8000) != 0;

        // ---------------------------------------------------------------
        // Safe variant (official Input System API, no special settings)
        // ---------------------------------------------------------------
        using (StateEvent.From(Mouse.current, out var eventPtr))
        {
            Mouse.current.position.WriteValueIntoEvent(pos, eventPtr);
            Mouse.current.delta.WriteValueIntoEvent(delta, eventPtr);
            Mouse.current.leftButton.WriteValueIntoEvent(lmb ? 1f : 0f, eventPtr);
            Mouse.current.rightButton.WriteValueIntoEvent(rmb ? 1f : 0f, eventPtr);
            Mouse.current.middleButton.WriteValueIntoEvent(mmb ? 1f : 0f, eventPtr);
            InputSystem.QueueEvent(eventPtr);
        }

        // ---------------------------------------------------------------
        // Unsafe variant — uncomment this block and comment out the safe
        // variant above. Direct memory write via pointer arithmetic,
        // may be slightly faster but needs profiling to confirm.
        // Requires "Allow 'unsafe' Code" in Player Settings.
        // ---------------------------------------------------------------
        // var state = new MouseState();
        // state.WithButton(MouseButton.Left, lmb);
        // state.WithButton(MouseButton.Right, rmb);
        // state.WithButton(MouseButton.Middle, mmb);
        // unsafe
        // {
        //     Vector2* p = (Vector2*)((byte*)&state + 0);  // position at offset 0
        //     Vector2* d = (Vector2*)((byte*)&state + 8);  // delta at offset 8
        //     *p = pos;
        //     *d = delta;
        //     InputSystem.QueueStateEvent(Mouse.current, state);
        // }

        _lastPos = pos;
    }

    void ApplyFix()
    {
        var d = new RAWINPUTDEVICE[1];
        d[0].usUsagePage = 0x01;
        d[0].usUsage     = 0x02;
        d[0].dwFlags     = 0x30; // RIDEV_NOLEGACY
        d[0].hwndTarget  = IntPtr.Zero;
        RegisterRawInputDevices(d, 1, kDevSize);
    }

#endif
}