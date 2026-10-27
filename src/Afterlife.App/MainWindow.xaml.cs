using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Afterlife.App.Controls;
using Afterlife.App.ViewModels;
using Afterlife.Core;

namespace Afterlife.App;

public partial class MainWindow : Window
{
    private static readonly string[] Accepted = [".zip", ".7z", ".rar", ".archive"];
    private const string RowFormat = "AFTERLIFE.ModRows";
    private readonly MainViewModel _vm = new();

    public MainWindow(string? startLink = null)
    {
        InitializeComponent();
        DataContext = _vm;
        _vm.ScrollToSelected += () => Dispatcher.BeginInvoke(() => { if (_vm.SelectedMod is { } r) ModsList.ScrollIntoView(r); },
            System.Windows.Threading.DispatcherPriority.Background);
        SourceInitialized += (_, _) => { UseDarkTitleBar(); FitMinimumToScreen(); };
        Loaded += async (_, _) =>
        {
            if (startLink is not null) await _vm.HandleExternalAsync(startLink);
            await _vm.StartAsync();
        };
        Closed += (_, _) => _vm.Dispose();
        DragOver += OnDragOver;
        DragLeave += (_, _) => SetDragOver(false);
        Drop += OnDrop;
        _vm.ScanCompleted += OnScanCompleted;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    /// <summary>A second AFTERLIFE start handed over an nxm:// link (or just wants the window in front).</summary>
    public async void OnExternalMessage(string message)
    {
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;
        await _vm.HandleExternalAsync(message);
    }

    private static IEnumerable<string> DroppedFiles(DragEventArgs e) =>
        e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] files
            ? files.Where(f => File.Exists(f) && Accepted.Contains(Path.GetExtension(f).ToLowerInvariant()))
            : [];

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(RowFormat) || e.Data.GetDataPresent(BlockFormat)) return; // reordering is handled by the list
        var ok = _vm.Overlay is null && DroppedFiles(e).Any();
        e.Effects = ok ? DragDropEffects.Copy : DragDropEffects.None;
        SetDragOver(ok);
        e.Handled = true;
    }

    private void SetDragOver(bool on)
    {
        if (_vm.IsDragOver == on) return;
        _vm.IsDragOver = on;
        Fx.Lift(DropZone, on);
    }

    // P4/P14: scan sweep over the header, decode of the titles, core table checked off one by one.
    private void OnScanCompleted()
    {
        Dispatcher.BeginInvoke(() =>
        {
            Fx.Sweep(HeaderSweep, Header.ActualWidth + HeaderSweep.ActualWidth);
            Fx.Stagger(CoreList);
            Fx.Replay();
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    // ---------------- selection (P22) ----------------
    private void OnSelectAllClick(object sender, RoutedEventArgs e) => _vm.SetAllChecked(!_vm.AllChecked);

    /// <summary>Ctrl+click / Shift+click on a row toggle its checkbox; a plain click only shows details.</summary>
    private int _anchor = -1;

    /// <summary>P76: double-click on the mod name renames it.</summary>
    private void OnModNameMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2 || sender is not FrameworkElement { DataContext: ModRowVm row }) return;
        e.Handled = true;
        _ = _vm.RenameModAsync(row.PackageId);
    }

    /// <summary>P101: double-click on a category name renames it.</summary>
    private void OnCategoryNameMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2 || sender is not FrameworkElement { DataContext: CollectionChapterVm { IsCategory: true } h }) return;
        e.Handled = true;
        _vm.RenameCategory(h.Slug);
    }

    /// <summary>P101: double-click on a profile name renames it (Mods off cannot be renamed).</summary>
    private void OnProfileNameMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2 || sender is not FrameworkElement { DataContext: ProfileVm p }) return;
        e.Handled = true;
        if (_vm.SelectedProfile != p) _vm.SelectedProfile = p;
        if (_vm.RenameProfileCommand.CanExecute(null)) _vm.RenameProfileCommand.Execute(null);
    }

    private void OnRowPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBoxItem { DataContext: ModRowVm row }) return;
        if (IsInside<System.Windows.Controls.Primitives.ToggleButton>(e.OriginalSource as DependencyObject)) return; // checkbox / toggle handle themselves
        var rows = ModsList.Items.OfType<ModRowVm>().ToList();
        var index = rows.IndexOf(row);
        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0 && _anchor >= 0)
        {
            var (a, b) = (Math.Min(_anchor, index), Math.Max(_anchor, index));
            for (int i = a; i <= b; i++) rows[i].IsChecked = true;
            e.Handled = true;
        }
        else if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            row.IsChecked = !row.IsChecked;
            _anchor = index;
            e.Handled = true;
        }
        else
        {
            _anchor = index;
            _dragStart = e.GetPosition(ModsList);
            _dragRow = row;
        }
    }

    /// <summary>P103: a click into the column header (MOD · TAGS, VERSION, …) unchecks all mods.</summary>
    private void OnColumnHeaderClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject d && IsInside<CheckBox>(d)) return;
        if (_vm.ClearSelectionCommand.CanExecute(null)) _vm.ClearSelectionCommand.Execute(null);
        _anchor = -1;
    }

    private void OnRowCheckClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ModRowVm row }) _anchor = ModsList.Items.IndexOf(row);
    }

    private static bool IsInside<T>(DependencyObject? d) where T : DependencyObject
    {
        while (d is not null && d is not ListBoxItem)
        {
            if (d is T) return true;
            d = d is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
        }
        return false;
    }

    // ---------------- load order by drag and drop (P25) ----------------
    private Point _dragStart;
    private ModRowVm? _dragRow;
    private ModRowVm? _hintRow;

    private void OnRowMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragRow is null || !_dragRow.CanDrag) return;
        var d = e.GetPosition(ModsList) - _dragStart;
        if (Math.Abs(d.Y) < SystemParameters.MinimumVerticalDragDistance * 2) return;
        var moved = _dragRow.IsChecked
            ? ModsList.Items.OfType<ModRowVm>().Where(r => r.IsChecked && r.CanDrag).Select(r => r.PackageId).ToList()
            : [_dragRow.PackageId];
        var data = new DataObject(RowFormat, moved);
        _dragRow = null;
        try { DragDrop.DoDragDrop(ModsList, data, DragDropEffects.Move); }
        finally { SetHint(null, 0); }
    }

    private void OnListDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(BlockFormat)) { OnBlockDragOver(e); return; }
        if (!e.Data.GetDataPresent(RowFormat)) return;
        var moved = e.Data.GetData(RowFormat) as List<string> ?? [];
        var header = HeaderAt(e);
        bool ok;
        if (header is not null)
        {
            // P95: onto a MODS or category header: the mods go to its end
            ok = !_vm.IsModsOff && _vm.CanDropOnChapter(moved, ChapterKeyOf(header));
            SetHint(null, 0);
        }
        else
        {
            var (row, after) = RowAt(e);
            ok = row is not null && !row.IsCore && row.Overview.Package.LayerOfModId is null && !_vm.IsModsOff && _vm.WhyNotDrop(moved, row.Chapter) is null;
            SetHint(ok ? row : null, after ? 1 : -1);
        }
        e.Effects = ok ? DragDropEffects.Move : DragDropEffects.None;
        AutoScroll(e);
        e.Handled = true;
    }

    private async void OnListDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(BlockFormat)) { await OnBlockDropAsync(e); return; }
        if (!e.Data.GetDataPresent(RowFormat)) return;
        e.Handled = true;
        SetHint(null, 0);
        if (e.Data.GetData(RowFormat) is not List<string> moved) return;
        if (HeaderAt(e) is { } header)
        {
            var key = ChapterKeyOf(header);
            if (_vm.CanDropOnChapter(moved, key)) await _vm.MoveModsToChapterAsync(moved, key);
            return;
        }
        var (row, after) = RowAt(e);
        if (row is null) return;
        await _vm.MoveModsAsync(moved, row.PackageId, after);
    }

    // ---------------- blocks by drag and drop (P97) ----------------
    private const string BlockFormat = "AFTERLIFE.Block";
    private Point _blockStart;
    private string? _blockKey;

    private void OnBlockHandleDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string key } || _vm.IsModsOff) return;
        _blockKey = key;
        _blockStart = e.GetPosition(ModsList);
        e.Handled = true;
    }

    private void OnBlockHandleMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _blockKey is null) { _blockKey = null; return; }
        var d = e.GetPosition(ModsList) - _blockStart;
        if (Math.Abs(d.Y) < SystemParameters.MinimumVerticalDragDistance * 2) return;
        var data = new DataObject(BlockFormat, _blockKey);
        _blockKey = null;
        DragDrop.DoDragDrop(ModsList, data, DragDropEffects.Move);
    }

    private static bool IsCategoryKey(string key) => key.StartsWith(BlockLayout.CategoryPrefix, StringComparison.Ordinal);

    /// <summary>The block a drop lands on: the header under the mouse or the block of the row under it.</summary>
    private (string? Target, bool After) BlockTargetAt(DragEventArgs e, string moving)
    {
        var movingCategory = IsCategoryKey(moving);
        if (HeaderElementAt(e) is { DataContext: CollectionChapterVm header } el)
        {
            if (header.BlockKey is null) return (null, false); // CORE MODS
            // a category dropped on the MODS header goes first inside MODS
            if (movingCategory && header.IsPlain) return (header.BlockKey, false);
            return (header.BlockKey, e.GetPosition(el).Y > el.ActualHeight / 2);
        }
        var (row, _) = RowAt(e);
        if (row is null || row.IsCore) return (null, false);
        var section = row.Section;
        if (!movingCategory && IsCategoryKey(section)) section = BlockLayout.ModsBlock;
        // onto a row: after the target block when the moved one comes from above, before it when from below
        var from = _vm.BlockRankOf(moving);
        var to = _vm.BlockRankOf(section);
        return (section, from < to);
    }

    private void OnBlockDragOver(DragEventArgs e)
    {
        var moving = e.Data.GetData(BlockFormat) as string;
        string? target = null;
        if (moving is not null) (target, _) = BlockTargetAt(e, moving);
        var ok = moving is not null && target is not null && target != moving && !_vm.IsModsOff
                 && (!IsCategoryKey(moving) || IsCategoryKey(target) || target == BlockLayout.ModsBlock);
        e.Effects = ok ? DragDropEffects.Move : DragDropEffects.None;
        SetHint(null, 0);
        AutoScroll(e);
        e.Handled = true;
    }

    private async Task OnBlockDropAsync(DragEventArgs e)
    {
        e.Handled = true;
        if (e.Data.GetData(BlockFormat) is not string moving) return;
        var (target, after) = BlockTargetAt(e, moving);
        if (target is null || target == moving) return;
        await _vm.MoveBlockAsync(moving, target, after);
    }

    private FrameworkElement? HeaderElementAt(DragEventArgs e)
    {
        var hit = ModsList.InputHitTest(e.GetPosition(ModsList)) as DependencyObject;
        FrameworkElement? found = null;
        while (hit is not null && hit is not ListBoxItem && hit is not GroupItem)
        {
            if (hit is FrameworkElement { DataContext: CollectionChapterVm } fe) found = fe;
            hit = hit is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(hit) : LogicalTreeHelper.GetParent(hit);
        }
        return found;
    }

    private CollectionChapterVm? HeaderAt(DragEventArgs e) => HeaderElementAt(e)?.DataContext as CollectionChapterVm;

    private static string ChapterKeyOf(CollectionChapterVm h) => h.Kind switch
    {
        ChapterKind.Plain => MainViewModel.PlainChapter,
        ChapterKind.Category => MainViewModel.CategoryChapterPrefix + h.Slug,
        ChapterKind.Core => MainViewModel.CoreChapter,
        _ => MainViewModel.CollectionChapterPrefix + h.Name,
    };

    private (ModRowVm? Row, bool After) RowAt(DragEventArgs e)
    {
        var hit = ModsList.InputHitTest(e.GetPosition(ModsList)) as DependencyObject;
        while (hit is not null && hit is not ListBoxItem) hit = VisualTreeHelper.GetParent(hit);
        if (hit is not ListBoxItem { DataContext: ModRowVm row } item) return (null, false);
        return (row, e.GetPosition(item).Y > item.ActualHeight / 2);
    }

    private void SetHint(ModRowVm? row, int where)
    {
        if (_hintRow is not null && _hintRow != row) _hintRow.DropHint = 0;
        _hintRow = row;
        if (row is not null) row.DropHint = where;
    }

    private void AutoScroll(DragEventArgs e)
    {
        var sv = FindChild<ScrollViewer>(ModsList);
        if (sv is null) return;
        var y = e.GetPosition(ModsList).Y;
        if (y < 24) sv.ScrollToVerticalOffset(sv.VerticalOffset - 12);
        else if (y > ModsList.ActualHeight - 24) sv.ScrollToVerticalOffset(sv.VerticalOffset + 12);
    }

    private static T? FindChild<T>(DependencyObject d) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(d); i++)
        {
            var c = VisualTreeHelper.GetChild(d, i);
            if (c is T t) return t;
            if (FindChild<T>(c) is { } r) return r;
        }
        return null;
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(RowFormat) || e.Data.GetDataPresent(BlockFormat)) return;
        SetDragOver(false);
        var files = DroppedFiles(e).ToList();
        if (files.Count == 0 || _vm.Overlay is not null) return;
        await _vm.ImportFilesAsync(files);
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _vm.Overlay?.CloseCommand is { } close && close.CanExecute(null))
        {
            close.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.A && Keyboard.Modifiers == ModifierKeys.Control && _vm.Overlay is null && Keyboard.FocusedElement is not TextBox)
        {
            _vm.SetAllChecked(!_vm.AllChecked);
            e.Handled = true;
        }
    }

    /// <summary>
    /// P93: the minimum is 1612 × 910, 30 % more than before; on a small or zoomed screen
    /// (1920 × 1080 at 150 % gives about 1280 × 720) it is cut to the work area, so the window always fits.
    /// </summary>
    private void FitMinimumToScreen()
    {
        var area = SystemParameters.WorkArea;
        MinWidth = Math.Min(1612, area.Width);
        MinHeight = Math.Min(910, area.Height);
        Width = Math.Min(Math.Max(Width, MinWidth), area.Width);
        Height = Math.Min(Math.Max(Height, MinHeight), area.Height);
    }

    // Dark title bar on Windows 10 20H1+ / 11 (DWMWA_USE_IMMERSIVE_DARK_MODE = 20).
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    private void UseDarkTitleBar()
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int on = 1;
            DwmSetWindowAttribute(hwnd, 20, ref on, sizeof(int));
        }
        catch { /* older Windows: keep the default title bar */ }
    }
}
