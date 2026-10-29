using System;
using System.IO;

namespace M4W.App;

internal static class AppPaths
{
    public static string Dir { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "M4W");

    public static string Database => Path.Combine(Dir, "history.db");
    public static string Settings => Path.Combine(Dir, "settings.json");
    public static string Log => Path.Combine(Dir, "m4w.log");
}
