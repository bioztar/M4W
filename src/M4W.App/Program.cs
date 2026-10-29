using System;
using System.Threading;
using System.Windows;
using M4W.Core;

namespace M4W.App;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        var log = new FileLog(AppPaths.Log);

        // Session-local names: one M4W per Windows session, which is what an RDP reconnect resumes.
        // The event is created first so a second launch can always signal, even mid-startup.
        using var showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\M4W.ShowPopup");
        using var mutex = new Mutex(true, @"Local\M4W.SingleInstance", out var isFirst);
        if (!isFirst)
        {
            Native.AllowSetForegroundWindow(Native.ASFW_ANY); // so the running copy may take focus
            showSignal.Set(); // second launch: ask the running instance to open its popup
            return 0;
        }

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.DispatcherUnhandledException += (_, e) =>
        {
            log.Error("unhandled UI exception", e.Exception);
            e.Handled = true;
        };

        AppHost? host = null;
        try
        {
            host = new AppHost(log);
            host.Start(showSignal);
            return app.Run();
        }
        catch (Exception ex)
        {
            log.Error("fatal startup/run error", ex);
            MessageBox.Show($"M4W could not start:\n{ex.Message}\n\nDetails: {AppPaths.Log}", "M4W", MessageBoxButton.OK, MessageBoxImage.Error);
            return 1;
        }
        finally
        {
            host?.Dispose();
            mutex.ReleaseMutex();
        }
    }
}
