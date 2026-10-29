using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using M4W.Core;

namespace M4W.App;

/// <summary>
/// The single place to edit snippets, the main popup hotkey, and every other setting. Nothing here
/// touches disk or the live hotkey registrations until Save: Cancel discards every edit, including
/// snippet adds/deletes, which only exist in <see cref="_drafts"/> until then.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly AppSettings _original;
    private readonly SnippetService _snippets;
    private readonly ObservableCollection<SnippetDraft> _drafts;
    private SnippetDraft? _selected;
    private bool _loadingDetail;

    internal SettingsWindow(AppSettings settings, SnippetService snippets, IReadOnlySet<string> unavailableSnippetIds)
    {
        _original = settings;
        _snippets = snippets;
        InitializeComponent();

        PopupHotkeyBox.Text = settings.Hotkey;
        MaxItemsBox.Text = settings.MaxItems.ToString();
        FuzzyBox.IsChecked = settings.FuzzyByDefault;
        AutoPasteBox.IsChecked = settings.AutoPaste;
        RespectPrivateBox.IsChecked = settings.RespectPrivateFlag;
        IdleOffBox.Text = settings.TypingIdleOffMinutes.ToString();
        SkipPasswordBox.IsChecked = settings.TypingSkipPasswordFields;
        MinTypedBox.Text = settings.MinTypedChars.ToString();
        IdleFlushBox.Text = settings.IdleFlushSeconds.ToString();

        _drafts = new ObservableCollection<SnippetDraft>(snippets.Items.Select(s => SnippetDraft.From(s, unavailableSnippetIds.Contains(s.Id))));
        SnippetList.ItemsSource = _drafts;
        RefreshDetail();
    }

    /// <summary>Raised once, after Save has validated and applied everything (settings persisted by the caller; snippets already committed to <see cref="SnippetService"/>).</summary>
    internal event Action<AppSettings>? SettingsSaved;

    private void OnAddSnippet(object sender, RoutedEventArgs e)
    {
        var draft = new SnippetDraft { Id = Guid.NewGuid().ToString("N") };
        _drafts.Add(draft);
        SnippetList.SelectedItem = draft;
    }

    private void OnDeleteSnippet(object sender, RoutedEventArgs e)
    {
        if (SnippetList.SelectedItem is SnippetDraft draft) _drafts.Remove(draft);
    }

    private void OnSnippetSelected(object sender, SelectionChangedEventArgs e)
    {
        _selected = SnippetList.SelectedItem as SnippetDraft;
        RefreshDetail();
    }

    private void RefreshDetail()
    {
        SnippetDetail.IsEnabled = _selected is not null;
        _loadingDetail = true;
        SnippetNameBox.Text = _selected?.Name ?? "";
        SnippetHotkeyBox.Text = _selected?.HotkeyText ?? "";
        SnippetSecretBox.IsChecked = _selected?.IsSecret ?? false;
        SnippetEnabledBox.IsChecked = _selected?.Enabled ?? true;
        RefreshValueBox();
        _loadingDetail = false;
    }

    private bool _revealed;

    private void RefreshValueBox()
    {
        var masked = _selected is { IsSecret: true } && !_revealed;
        SnippetValueBox.IsReadOnly = masked;
        SnippetValueBox.Text = _selected is null ? "" : masked ? (_selected.Value.Length == 0 ? "" : "••••••••") : _selected.Value;
        RevealButton.Content = _revealed ? "Hide" : "Reveal";
    }

    private void OnRevealToggle(object sender, RoutedEventArgs e)
    {
        _revealed = !_revealed;
        RefreshValueBox();
    }

    private void OnSnippetNameChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loadingDetail && _selected is not null) _selected.Name = SnippetNameBox.Text;
    }

    private void OnSnippetValueChanged(object sender, TextChangedEventArgs e)
    {
        // Only commit real text, never the masked placeholder — see RefreshValueBox.
        if (!_loadingDetail && _selected is not null && !SnippetValueBox.IsReadOnly) _selected.Value = SnippetValueBox.Text;
    }

    private void OnSnippetSecretChanged(object sender, RoutedEventArgs e)
    {
        if (_loadingDetail || _selected is null) return;
        _selected.IsSecret = SnippetSecretBox.IsChecked == true;
        _revealed = false;
        RefreshValueBox();
    }

    private void OnSnippetEnabledChanged(object sender, RoutedEventArgs e)
    {
        if (!_loadingDetail && _selected is not null) _selected.Enabled = SnippetEnabledBox.IsChecked == true;
    }

    /// <summary>Shared by the popup-hotkey box and the per-snippet hotkey box: press keys, see the canonical text.</summary>
    private void OnHotkeyCapture(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return;
        if (!TryKeyToHotkeyText(key, out var keyText)) return;

        var mods = HotkeyModifiers.None;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) mods |= HotkeyModifiers.Ctrl;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) mods |= HotkeyModifiers.Alt;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) mods |= HotkeyModifiers.Shift;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)) mods |= HotkeyModifiers.Win;
        if (mods == HotkeyModifiers.None) return; // Hotkey requires at least one modifier

        var text = new Hotkey(mods, keyText).ToText();
        var box = (TextBox)sender;
        box.Text = text;
        if (box == SnippetHotkeyBox && _selected is not null) _selected.HotkeyText = text;
    }

    private static bool TryKeyToHotkeyText(Key key, out string text)
    {
        if (key >= Key.A && key <= Key.Z) { text = key.ToString(); return true; }
        if (key >= Key.D0 && key <= Key.D9) { text = ((int)(key - Key.D0)).ToString(); return true; }
        if (key >= Key.F1 && key <= Key.F24) { text = key.ToString().ToUpperInvariant(); return true; }
        text = "";
        return false;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => Close();

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (!TryParseInt(MaxItemsBox.Text, AppSettings.MinItems, AppSettings.MaxItemsCeiling, out var maxItems, "Max history items") ||
            !TryParseInt(IdleOffBox.Text, AppSettings.MinTypingIdleOffMinutes, AppSettings.MaxTypingIdleOffMinutes, out var idleOff, "Idle auto-off") ||
            !TryParseInt(MinTypedBox.Text, AppSettings.MinMinTypedChars, AppSettings.MaxMinTypedChars, out var minTyped, "Minimum typed characters") ||
            !TryParseInt(IdleFlushBox.Text, AppSettings.MinIdleFlushSeconds, AppSettings.MaxIdleFlushSeconds, out var idleFlush, "Idle flush seconds"))
            return;

        var draftSnippets = _drafts.Select(d => d.ToSnippet()).ToArray();
        var issues = SnippetConflicts.Find(draftSnippets, PopupHotkeyBox.Text);
        if (issues.Count > 0)
        {
            ValidationText.Text = string.Join(" · ", issues.Select(DescribeIssue));
            return;
        }
        ValidationText.Text = "";

        var newSettings = _original with
        {
            Hotkey = PopupHotkeyBox.Text,
            MaxItems = maxItems,
            FuzzyByDefault = FuzzyBox.IsChecked == true,
            AutoPaste = AutoPasteBox.IsChecked == true,
            RespectPrivateFlag = RespectPrivateBox.IsChecked == true,
            TypingIdleOffMinutes = idleOff,
            TypingSkipPasswordFields = SkipPasswordBox.IsChecked == true,
            MinTypedChars = minTyped,
            IdleFlushSeconds = idleFlush,
        };

        var keptIds = draftSnippets.Select(s => s.Id).ToHashSet();
        foreach (var removed in _snippets.Items.Where(s => !keptIds.Contains(s.Id)).ToArray()) _snippets.Delete(removed.Id);
        foreach (var s in draftSnippets) _snippets.Put(s);

        SettingsSaved?.Invoke(newSettings);
        Close();
    }

    private bool TryParseInt(string text, int min, int max, out int value, string fieldName)
    {
        if (int.TryParse(text, out value) && value >= min && value <= max) return true;
        ValidationText.Text = $"{fieldName} must be a number between {min} and {max}.";
        value = 0;
        return false;
    }

    private static string DescribeIssue(HotkeyIssue issue) => issue.Kind switch
    {
        HotkeyIssueKind.Unparseable => $"'{issue.OwnerName}': hotkey \"{issue.HotkeyText}\" is not valid",
        HotkeyIssueKind.DuplicatesPopupHotkey => $"'{issue.OwnerName}': hotkey clashes with the popup hotkey",
        HotkeyIssueKind.DuplicatesAnotherSnippet => $"'{issue.OwnerName}': hotkey clashes with another snippet",
        _ => $"'{issue.OwnerName}': hotkey problem",
    };
}
