using System;
using System.Threading.Tasks;
using M4W.Core;

namespace M4W.App;

internal static class Paster
{
    /// <summary>Brings <paramref name="target"/> back to the foreground and, if asked, types Ctrl+V into it.</summary>
    public static async Task RestoreFocusAndPasteAsync(IntPtr target, bool paste, ILog log)
    {
        if (target == IntPtr.Zero || !Native.IsWindow(target))
        {
            log.Warn("paste: previous window is gone, clipboard set but nothing pasted");
            return;
        }

        Native.SetForegroundWindow(target);
        for (var i = 0; i < 15 && Native.GetForegroundWindow() != target; i++)
            await Task.Delay(20);
        if (Native.GetForegroundWindow() != target)
            log.Warn("paste: could not restore focus to previous window");

        if (!paste) return;
        await Task.Delay(40); // let the target window settle before the keystrokes arrive
        await WaitForModifiersReleasedAsync(); // a snippet hotkey's own modifiers (Alt/Shift/Win) may still be held
        if (!Native.SendCtrlV()) log.Error("paste: SendInput was rejected (target may be elevated)");
    }

    /// <summary>
    /// Waits (briefly) for Alt, Shift and Win to be physically up before sending Ctrl+V, so a snippet
    /// fired as e.g. Ctrl+Shift+R doesn't leave Shift held down and turn our Ctrl+V into Ctrl+Shift+V in
    /// the target app. Gives up after a timeout rather than delaying the paste indefinitely.
    /// </summary>
    private static async Task WaitForModifiersReleasedAsync()
    {
        var deadline = Environment.TickCount64 + 300;
        while (IsPhysicallyDown(Native.VK_MENU) || IsPhysicallyDown(Native.VK_SHIFT) ||
               IsPhysicallyDown(Native.VK_LWIN) || IsPhysicallyDown(Native.VK_RWIN))
        {
            if (Environment.TickCount64 >= deadline) return; // paste anyway rather than hang
            await Task.Delay(15);
        }
    }

    private static bool IsPhysicallyDown(int vk) => (Native.GetAsyncKeyState(vk) & 0x8000) != 0;
}
