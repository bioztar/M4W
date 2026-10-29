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
        if (!Native.SendCtrlV()) log.Error("paste: SendInput was rejected (target may be elevated)");
    }
}
