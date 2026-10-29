using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using M4W.Core;

namespace M4W.App;

public partial class PopupWindow : Window
{
    private readonly HistoryService _history;
    private bool _suppressRefresh;

    internal PopupWindow(HistoryService history, bool fuzzyByDefault)
    {
        _history = history;
        InitializeComponent();
        _suppressRefresh = true;
        ExactToggle.IsChecked = !fuzzyByDefault;
        _suppressRefresh = false;
        Deactivated += (_, _) => { if (IsVisible) Hide(); };
    }

    /// <summary>Raised when the user picks an item (Enter or double-click). The popup has already hidden itself.</summary>
    internal event Action<ClipEntry>? ItemChosen;

    /// <summary>
    /// Shows the popup and takes focus. Returns false (popup hidden again) if Windows would not let us
    /// activate: a topmost window that has no focus can't be typed into or dismissed with Esc.
    /// </summary>
    internal bool ShowAtCursor()
    {
        _suppressRefresh = true;
        SearchBox.Text = string.Empty;
        _suppressRefresh = false;
        Refresh(selectFirst: true);
        PlaceNearCursor();
        Show();
        Activate();
        if (!IsActive) Native.ForceForeground(new WindowInteropHelper(this).Handle);
        if (!IsActive)
        {
            Hide();
            return false;
        }
        SearchBox.Focus();
        Keyboard.Focus(SearchBox);
        return true;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var alt = (Keyboard.Modifiers & ModifierKeys.Alt) != 0;
        e.Handled = true;
        switch (key)
        {
            case Key.Escape: Hide(); break;
            case Key.Down: Move(1); break;
            case Key.Up: Move(-1); break;
            case Key.PageDown: Move(8); break;
            case Key.PageUp: Move(-8); break;
            case Key.Enter: ChooseSelected(); break;
            case Key.P when alt: TogglePinSelected(); break;
            case Key.E when alt: ExactToggle.IsChecked = ExactToggle.IsChecked != true; break;
            // Delete removes the item, unless the user is editing the query (caret before the end of the text).
            case Key.Delete when SearchBox.CaretIndex >= SearchBox.Text.Length: DeleteSelected(); break;
            default: e.Handled = false; break;
        }
        base.OnPreviewKeyDown(e);
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e) => Refresh(selectFirst: true);
    private void OnModeChanged(object sender, RoutedEventArgs e) => Refresh(selectFirst: true);
    private void OnListDoubleClick(object sender, MouseButtonEventArgs e) => ChooseSelected();

    private void Refresh(bool selectFirst)
    {
        if (_suppressRefresh || !IsInitialized) return;
        var keep = ResultList.SelectedIndex;
        IReadOnlyList<PopupItem> rows = _history
            .Search(SearchBox.Text, exact: ExactToggle.IsChecked == true)
            .Select(entry => new PopupItem(entry, _history.LoadImage))
            .ToArray();
        ResultList.ItemsSource = rows;
        if (rows.Count == 0) return;
        ResultList.SelectedIndex = selectFirst ? 0 : Math.Clamp(keep, 0, rows.Count - 1);
        ResultList.ScrollIntoView(ResultList.SelectedItem);
    }

    private void Move(int delta)
    {
        var count = ResultList.Items.Count;
        if (count == 0) return;
        ResultList.SelectedIndex = Math.Clamp(ResultList.SelectedIndex + delta, 0, count - 1);
        ResultList.ScrollIntoView(ResultList.SelectedItem);
    }

    private ClipEntry? Selected => (ResultList.SelectedItem as PopupItem)?.Entry;

    private void ChooseSelected()
    {
        if (Selected is not { } entry) return;
        Hide();
        ItemChosen?.Invoke(entry);
    }

    private void TogglePinSelected()
    {
        if (Selected is not { } entry) return;
        _history.TogglePin(entry.Id);
        Refresh(selectFirst: false);
    }

    private void DeleteSelected()
    {
        if (Selected is not { } entry) return;
        _history.Delete(entry.Id);
        Refresh(selectFirst: false);
    }

    private void PlaceNearCursor()
    {
        if (!Native.GetCursorPos(out var cursor)) return;
        var area = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(cursor.X, cursor.Y)).WorkingArea;
        var scale = VisualTreeHelper.GetDpi(this).DpiScaleX;
        var widthPx = Width * scale;
        var heightPx = Height * scale;
        var x = Math.Clamp(cursor.X, area.Left, Math.Max(area.Left, area.Right - widthPx));
        var y = Math.Clamp(cursor.Y, area.Top, Math.Max(area.Top, area.Bottom - heightPx));
        Left = x / scale;
        Top = y / scale;
    }
}
