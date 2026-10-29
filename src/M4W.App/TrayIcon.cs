using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace M4W.App;

/// <summary>
/// The tray icon is M4W's one permanent affordance: while the process runs the icon is there, and a
/// left click always opens the popup. Menu actions are raised as events; AppHost does the work.
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    private const string Tooltip = "M4W — clipboard history";

    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _pause = new("Pause capture") { CheckOnClick = true };
    private readonly ToolStripMenuItem _autostart = new("Start with Windows") { CheckOnClick = true };
    private readonly Icon _activeIcon = Render(Color.FromArgb(0x4C, 0x8D, 0xFF));
    private readonly Icon _pausedIcon = Render(Color.FromArgb(0x80, 0x80, 0x88));

    public TrayIcon(bool autostartEnabled)
    {
        var open = new ToolStripMenuItem("Open history") { Font = new Font(SystemFonts.MenuFont!, FontStyle.Bold) };
        var clear = new ToolStripMenuItem("Clear history (unpinned)");
        var about = new ToolStripMenuItem("About M4W");
        var quit = new ToolStripMenuItem("Quit");
        _autostart.Checked = autostartEnabled;

        open.Click += (_, _) => OpenRequested?.Invoke();
        clear.Click += (_, _) => ClearRequested?.Invoke();
        about.Click += (_, _) => AboutRequested?.Invoke();
        quit.Click += (_, _) => QuitRequested?.Invoke();
        _pause.CheckedChanged += (_, _) => PauseChanged?.Invoke(_pause.Checked);
        _autostart.CheckedChanged += (_, _) => AutostartChanged?.Invoke(_autostart.Checked);

        var menu = new ContextMenuStrip();
        menu.Items.AddRange([open, _pause, clear, _autostart, new ToolStripSeparator(), about, quit]);

        _icon = new NotifyIcon { Icon = _activeIcon, Text = Tooltip, ContextMenuStrip = menu, Visible = true };
        _icon.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) OpenRequested?.Invoke(); };
    }

    public event Action? OpenRequested;
    public event Action? ClearRequested;
    public event Action? AboutRequested;
    public event Action? QuitRequested;
    public event Action<bool>? PauseChanged;
    public event Action<bool>? AutostartChanged;

    public void SetPaused(bool paused)
    {
        _icon.Icon = paused ? _pausedIcon : _activeIcon;
        _icon.Text = paused ? "M4W — capture paused" : Tooltip;
    }

    public void SetAutostartChecked(bool value) => _autostart.Checked = value;

    public void ShowBalloon(string message) =>
        _icon.ShowBalloonTip(4000, "M4W", message, ToolTipIcon.Warning);

    public void Dispose()
    {
        _icon.Visible = false; // otherwise a ghost icon lingers in the tray until hovered
        _icon.Dispose();
        _activeIcon.Dispose();
        _pausedIcon.Dispose();
    }

    private static Icon Render(Color background)
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        using (var fill = new SolidBrush(background))
        using (var font = new Font("Segoe UI", 15f, FontStyle.Bold, GraphicsUnit.Pixel))
        using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);
            using var path = RoundedRect(new Rectangle(1, 1, 30, 30), 7);
            g.FillPath(fill, path);
            g.DrawString("M", font, Brushes.White, new RectangleF(0, 1, 32, 32), format);
        }

        var handle = bmp.GetHicon();
        try { return (Icon)Icon.FromHandle(handle).Clone(); }
        finally { Native.DestroyIcon(handle); }
    }

    private static GraphicsPath RoundedRect(Rectangle r, int radius)
    {
        var d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.Left, r.Top, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
