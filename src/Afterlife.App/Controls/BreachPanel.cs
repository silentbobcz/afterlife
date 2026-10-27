using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace Afterlife.App.Controls;

/// <summary>
/// "Breach protocol" while AFTERLIFE writes into the game (P9): a code matrix with a moving selection,
/// a buffer that fills with progress, and the live log of written files. Decorative only; the real
/// progress and result are always also in the text and in the footer.
/// </summary>
public sealed class BreachPanel : Border
{
    private static readonly string[] Codes = ["1C", "55", "BD", "E9", "7A", "FF"];
    private const int Cols = 6, Rows = 5, BufferSlots = 8;

    private readonly TextBlock[,] _cells = new TextBlock[Rows, Cols];
    private readonly Border[] _buffer = new Border[BufferSlots];
    private readonly TextBlock[] _bufferText = new TextBlock[BufferSlots];
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private int _row, _col, _tick;
    private bool _alongRow = true;

    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(nameof(IsActive), typeof(bool), typeof(BreachPanel),
        new PropertyMetadata(false, (d, _) => ((BreachPanel)d).UpdateTimer()));
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(BreachPanel), new PropertyMetadata(""));
    public static readonly DependencyProperty AccentProperty = DependencyProperty.Register(nameof(Accent), typeof(Brush), typeof(BreachPanel), new PropertyMetadata(Brushes.Cyan));
    public static readonly DependencyProperty LinesProperty = DependencyProperty.Register(nameof(Lines), typeof(IEnumerable), typeof(BreachPanel));
    public static readonly DependencyProperty DoneProperty = DependencyProperty.Register(nameof(Done), typeof(int), typeof(BreachPanel),
        new PropertyMetadata(0, (d, _) => ((BreachPanel)d).UpdateBuffer()));
    public static readonly DependencyProperty TotalProperty = DependencyProperty.Register(nameof(Total), typeof(int), typeof(BreachPanel),
        new PropertyMetadata(0, (d, _) => ((BreachPanel)d).UpdateBuffer()));

    public bool IsActive { get => (bool)GetValue(IsActiveProperty); set => SetValue(IsActiveProperty, value); }
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public Brush Accent { get => (Brush)GetValue(AccentProperty); set => SetValue(AccentProperty, value); }
    public IEnumerable? Lines { get => (IEnumerable?)GetValue(LinesProperty); set => SetValue(LinesProperty, value); }
    public int Done { get => (int)GetValue(DoneProperty); set => SetValue(DoneProperty, value); }
    public int Total { get => (int)GetValue(TotalProperty); set => SetValue(TotalProperty, value); }

    private Brush Res(string key) => (Brush)Application.Current.Resources[key];
    private FontFamily Mono => (FontFamily)Application.Current.Resources["MonoFont"];

    public BreachPanel()
    {
        Focusable = false;
        IsHitTestVisible = false;
        _timer.Tick += (_, _) => Step();
        Child = BuildContent();
        UpdateBuffer();
        Unloaded += (_, _) => _timer.Stop();
        Loaded += (_, _) => UpdateTimer();
    }

    private UIElement BuildContent()
    {
        var root = new CutBorder
        {
            Background = Res("Bg2"),
            CutTopLeft = true,
            CutTopRight = true,
            Cut = 10,
            Padding = new Thickness(18, 14, 18, 14),
        };
        root.SetBinding(CutBorder.BorderBrushProperty, new System.Windows.Data.Binding(nameof(Accent)) { Source = this });
        root.SetBinding(CutBorder.AccentBrushProperty, new System.Windows.Data.Binding(nameof(Accent)) { Source = this });

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var head = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        var label = new TextBlock { Text = "BREACH PROTOCOL", FontFamily = Mono, FontWeight = FontWeights.Bold, FontSize = 13, Foreground = Res("Y") };
        var title = new TextBlock { FontFamily = Mono, FontSize = 12, Margin = new Thickness(16, 0, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis };
        title.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(Title)) { Source = this });
        title.SetBinding(TextBlock.ForegroundProperty, new System.Windows.Data.Binding(nameof(Accent)) { Source = this });
        head.Children.Add(label);
        head.Children.Add(title);
        Grid.SetColumnSpan(head, 2);
        grid.Children.Add(head);

        // code matrix
        var matrix = new UniformGrid { Rows = Rows, Columns = Cols, Margin = new Thickness(0, 0, 24, 0) };
        var rnd = new Random();
        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Cols; c++)
            {
                var tb = new TextBlock
                {
                    Text = Codes[rnd.Next(Codes.Length)], FontFamily = Mono, FontSize = 15, FontWeight = FontWeights.Bold,
                    Foreground = Res("T3"), Width = 34, Height = 26, TextAlignment = TextAlignment.Center, Padding = new Thickness(0, 3, 0, 0),
                };
                _cells[r, c] = tb;
                matrix.Children.Add(tb);
            }
        var matrixFrame = new Border { BorderBrush = Res("Line2"), BorderThickness = new Thickness(1), Padding = new Thickness(8), Child = matrix, VerticalAlignment = VerticalAlignment.Top };
        Grid.SetRow(matrixFrame, 1);
        Grid.SetRowSpan(matrixFrame, 2);
        grid.Children.Add(matrixFrame);

        // buffer
        var bufferPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        bufferPanel.Children.Add(new TextBlock { Text = "BUFFER", FontFamily = Mono, FontSize = 11, Foreground = Res("T2"), Margin = new Thickness(0, 0, 0, 4) });
        var slots = new StackPanel { Orientation = Orientation.Horizontal };
        for (int i = 0; i < BufferSlots; i++)
        {
            var text = new TextBlock { FontFamily = Mono, FontSize = 12, FontWeight = FontWeights.Bold, Foreground = Res("Bg"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var b = new Border { Width = 30, Height = 24, Margin = new Thickness(0, 0, 4, 0), BorderBrush = Res("Line2"), BorderThickness = new Thickness(1), Child = text };
            _buffer[i] = b;
            _bufferText[i] = text;
            slots.Children.Add(b);
        }
        bufferPanel.Children.Add(slots);
        Grid.SetRow(bufferPanel, 1);
        Grid.SetColumn(bufferPanel, 1);
        grid.Children.Add(bufferPanel);

        // log
        var log = new ItemsControl { Focusable = false };
        log.SetBinding(ItemsControl.ItemsSourceProperty, new System.Windows.Data.Binding(nameof(Lines)) { Source = this });
        var lineFactory = new FrameworkElementFactory(typeof(TextBlock));
        lineFactory.SetValue(TextBlock.FontFamilyProperty, Mono);
        lineFactory.SetValue(TextBlock.FontSizeProperty, 11.5);
        lineFactory.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        lineFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Left"));
        lineFactory.SetBinding(TextBlock.ForegroundProperty, new System.Windows.Data.Binding("Tone") { Converter = (System.Windows.Data.IValueConverter)Application.Current.Resources["ToneBrush"] });
        log.ItemTemplate = new DataTemplate { VisualTree = lineFactory };
        Grid.SetRow(log, 2);
        Grid.SetColumn(log, 1);
        grid.Children.Add(log);

        root.Child = grid;
        return root;
    }

    private void UpdateTimer()
    {
        if (IsActive && IsLoaded && Fx.MotionEnabled) _timer.Start();
        else _timer.Stop();
        if (!IsActive) Paint(highlight: false);
    }

    private void Step()
    {
        if (_cells[0, 0] is null) return;
        _tick++;
        if (_alongRow) _col = (_col + 1) % Cols; else _row = (_row + 1) % Rows;
        if (_tick % 7 == 0) _alongRow = !_alongRow; // breach protocol alternates row and column
        if (_tick % 3 == 0)
        {
            var r = Random.Shared.Next(Rows); var c = Random.Shared.Next(Cols);
            _cells[r, c].Text = Codes[Random.Shared.Next(Codes.Length)];
        }
        Paint(highlight: true);
    }

    private void Paint(bool highlight)
    {
        if (_cells[0, 0] is null) return;
        var dim = Res("T3"); var band = Res("TagText"); var hot = Res("Y"); var bandBg = Res("Sel");
        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Cols; c++)
            {
                var inBand = highlight && (_alongRow ? r == _row : c == _col);
                var cell = _cells[r, c];
                cell.Foreground = highlight && r == _row && c == _col ? hot : inBand ? band : dim;
                cell.Background = inBand ? bandBg : Brushes.Transparent;
            }
    }

    private void UpdateBuffer()
    {
        if (_buffer[0] is null) return;
        var filled = Total <= 0 ? 0 : (int)Math.Round(Math.Clamp(Done / (double)Total, 0, 1) * BufferSlots);
        for (int i = 0; i < BufferSlots; i++)
        {
            var on = i < filled;
            _buffer[i].Background = on ? Res("Y") : Brushes.Transparent;
            _buffer[i].BorderBrush = on ? Res("Y") : Res("Line2");
            _bufferText[i].Text = on ? Codes[(i * 7 + 3) % Codes.Length] : "";
        }
    }
}
