using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace Afterlife.App.Controls;

/// <summary>
/// One shared ~30 fps clock for every looping effect (P14). Runs only while something listens and motion is on,
/// so a hidden or reduced-motion window costs nothing.
/// </summary>
public static class FxClock
{
    private static readonly DispatcherTimer Timer = new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
    private static readonly Stopwatch Watch = Stopwatch.StartNew();
    private static event Action<double>? TickEvent;
    private static int _listeners;

    static FxClock() => Timer.Tick += (_, _) => TickEvent?.Invoke(Now);

    public static double Now => Watch.Elapsed.TotalSeconds;

    public static void Subscribe(Action<double> a) { TickEvent += a; _listeners++; Update(); }
    public static void Unsubscribe(Action<double> a) { TickEvent -= a; _listeners = Math.Max(0, _listeners - 1); Update(); }

    public static void Update()
    {
        var run = _listeners > 0 && Fx.MotionEnabled;
        if (run && !Timer.IsEnabled) Timer.Start();
        else if (!run && Timer.IsEnabled) Timer.Stop();
    }
}

/// <summary>Base for elements that redraw from <see cref="FxClock"/>; they draw a calm still frame when motion is off.</summary>
public abstract class AnimatedElement : FrameworkElement
{
    protected AnimatedElement()
    {
        Loaded += (_, _) => { FxClock.Subscribe(OnTick); Fx.MotionChanged += OnMotionChanged; };
        Unloaded += (_, _) => { FxClock.Unsubscribe(OnTick); Fx.MotionChanged -= OnMotionChanged; };
        IsHitTestVisible = false;
    }

    protected double Time => Fx.MotionEnabled ? FxClock.Now : 0;

    private void OnMotionChanged() => InvalidateVisual();

    private void OnTick(double t)
    {
        if (IsVisible && ShouldRedraw(t)) InvalidateVisual();
    }

    /// <summary>Override to skip redraws while nothing changes (keeps the title cheap).</summary>
    protected virtual bool ShouldRedraw(double t) => true;

    protected static Color A(Color c, double alpha) => Color.FromArgb((byte)Math.Clamp(alpha * 255, 0, 255), c.R, c.G, c.B);

    protected static SolidColorBrush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    public static readonly Color Yc = Color.FromRgb(0xFC, 0xEE, 0x0A);
    public static readonly Color Cc = Color.FromRgb(0x00, 0xF0, 0xFF);
    public static readonly Color Mc = Color.FromRgb(0xFF, 0x2A, 0x6D);
    public static readonly Color Bgc = Color.FromRgb(0x05, 0x05, 0x08);
}

/// <summary>
/// P17/P29: the EDDIES background, ported 1:1 from EDDIES app.css (body::before) and fx.js (canvas fx-bg):
/// magenta glow top right, cyan glow bottom left, 44 px grid, a magenta perspective floor running towards the
/// viewer from 62 % height with cyan rays from 62 % width, a magenta horizon haze and rising data dust.
/// </summary>
public sealed class Backdrop : AnimatedElement
{
    private static readonly Brush GridBrush;
    private (double X, double Y, double V, double S, Color C)[] _parts = [];
    private double _last = -1;
    private double _off;
    private Size _size;

    static Backdrop()
    {
        var pen = new Pen(Frozen(A(Cc, 0.035)), 1);
        pen.Freeze();
        var g = new GeometryGroup();
        g.Children.Add(new LineGeometry(new Point(0, 0.5), new Point(44, 0.5)));
        g.Children.Add(new LineGeometry(new Point(0.5, 0), new Point(0.5, 44)));
        var d = new DrawingBrush(new GeometryDrawing(null, pen, g))
        {
            TileMode = TileMode.Tile, Viewport = new Rect(0, 0, 44, 44), ViewportUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(0, 0, 44, 44), ViewboxUnits = BrushMappingMode.Absolute,
        };
        d.Freeze();
        GridBrush = d;
    }

    private void EnsureParts(double w, double h)
    {
        if (_size == new Size(w, h) && _parts.Length > 0) return;
        _size = new Size(w, h);
        var rnd = new Random(2077);
        _parts = Enumerable.Range(0, Math.Max(20, (int)Math.Round(w / 26))).Select(_ =>
        {
            var c = rnd.NextDouble() < 0.5 ? Cc : rnd.NextDouble() < 0.5 ? Mc : Yc;
            return (rnd.NextDouble() * w, rnd.NextDouble() * h, 0.15 + rnd.NextDouble() * 0.5, rnd.NextDouble() < 0.15 ? 2.0 : 1.0, c);
        }).ToArray();
    }

    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 0 || h <= 0) return;
        EnsureParts(w, h);
        var t = Time * 1000; // ms like requestAnimationFrame
        var dt = _last < 0 ? 33 : Math.Min(100, t - _last);
        _last = t;

        dc.DrawRectangle(Frozen(Bgc), null, new Rect(0, 0, w, h));
        // radial-gradient(900px 500px at 85% -10%, rgba(255,42,109,.143), transparent 60%)
        dc.DrawRectangle(Glow(Mc, 0.143, new Point(w * 0.85, -h * 0.10), 900 * 0.6, 500 * 0.6), null, new Rect(0, 0, w, h));
        // radial-gradient(800px 600px at -10% 110%, rgba(0,240,255,.10), transparent 60%)
        dc.DrawRectangle(Glow(Cc, 0.10, new Point(-w * 0.10, h * 1.10), 800 * 0.6, 600 * 0.6), null, new Rect(0, 0, w, h));
        dc.DrawRectangle(GridBrush, null, new Rect(0, 0, w, h));

        // perspective floor (fx.js frame)
        var horizon = h * 0.62;
        if (Fx.MotionEnabled) _off = (_off + dt * 0.018 / 18) % 1;
        for (int i = 0; i < 18; i++)
        {
            var z = (i + _off) / 18;
            var y = horizon + (h - horizon) * Math.Pow(z, 2.2);
            dc.DrawLine(new Pen(Frozen(A(Mc, 0.033 + z * 0.132)), 1), new Point(0, y), new Point(w, y));
        }
        var cx = w * 0.62;
        for (int i = -14; i <= 14; i++)
            dc.DrawLine(new Pen(Frozen(A(Cc, 0.05 - Math.Abs(i) * 0.002)), 1), new Point(cx + i * 18, horizon), new Point(cx + i * w * 0.16, h));
        var haze = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
        haze.GradientStops.Add(new GradientStop(A(Bgc, 0), 0));
        haze.GradientStops.Add(new GradientStop(A(Mc, 0.066), 0.6));
        haze.GradientStops.Add(new GradientStop(A(Bgc, 0), 1));
        dc.DrawRectangle(haze, null, new Rect(0, horizon - 60, w, 100));

        // data dust rising
        for (int k = 0; k < _parts.Length; k++)
        {
            var p = _parts[k];
            if (Fx.MotionEnabled)
            {
                p.Y -= p.V * dt * 0.06;
                if (p.Y < -4) { p.Y = h + 4; p.X = Random.Shared.NextDouble() * w; }
                _parts[k] = p;
            }
            var a = Math.Max(0.04, 0.12 + 0.25 * Math.Sin(t / 900 + p.X));
            dc.DrawRectangle(Frozen(A(p.C, a)), null, new Rect(p.X, p.Y, p.S, p.S * (p.S > 1 ? 6 : 3)));
        }
    }

    private static Brush Glow(Color c, double alpha, Point center, double rx, double ry)
    {
        var b = new RadialGradientBrush(A(c, alpha), A(c, 0))
        {
            MappingMode = BrushMappingMode.Absolute, Center = center, GradientOrigin = center, RadiusX = rx, RadiusY = ry,
        };
        b.Freeze();
        return b;
    }
}

/// <summary>Scanlines over the whole window (EDDIES); very faint so text stays crisp.</summary>
public sealed class Scanlines : FrameworkElement
{
    private static readonly Brush Lines;

    static Scanlines()
    {
        var d = new DrawingBrush(new GeometryDrawing(new SolidColorBrush(Color.FromArgb(0x17, 0, 0, 0)), null, new RectangleGeometry(new Rect(0, 0, 3, 1))))
        {
            TileMode = TileMode.Tile, Viewport = new Rect(0, 0, 3, 3), ViewportUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(0, 0, 3, 3), ViewboxUnits = BrushMappingMode.Absolute,
        };
        d.Freeze();
        Lines = d;
    }

    public Scanlines() => IsHitTestVisible = false;

    protected override void OnRender(DrawingContext dc) => dc.DrawRectangle(Lines, null, new Rect(0, 0, ActualWidth, ActualHeight));
}

/// <summary>
/// P13: the AFTERLIFE name drawn like the EDDIES logo: Chakra Petch Bold in yellow with a soft glow, two raster
/// cuts through the letters, a cyan/magenta bar underneath and short cyan (top) and magenta (bottom) glitch slices
/// (EDDIES keyframes glitch-a 4.2 s and glitch-b 3.7 s). Every 6 s a BREACH-style white flash runs over it.
/// </summary>
public sealed class GlitchTitle : AnimatedElement
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(GlitchTitle),
        new FrameworkPropertyMetadata("AFTERLIFE", FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((GlitchTitle)d)._geometry = null));
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(nameof(Size), typeof(double), typeof(GlitchTitle),
        new FrameworkPropertyMetadata(44.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((GlitchTitle)d)._geometry = null));
    public static readonly DependencyProperty SpacingProperty = DependencyProperty.Register(nameof(Spacing), typeof(double), typeof(GlitchTitle),
        new FrameworkPropertyMetadata(4.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((GlitchTitle)d)._geometry = null));

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public double Size { get => (double)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    public double Spacing { get => (double)GetValue(SpacingProperty); set => SetValue(SpacingProperty, value); }

    private Geometry? _geometry;
    private Rect _ink;
    private int _lastState = -1;

    public GlitchTitle()
    {
        Effect = new DropShadowEffect { Color = Yc, BlurRadius = 18, ShadowDepth = 0, Opacity = 0.35 };
    }

    private void Build()
    {
        if (_geometry is not null) return;
        var family = TryFindResource("DisplayFont") as FontFamily ?? new FontFamily("Segoe UI");
        var tf = new Typeface(family, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var group = new GeometryGroup { FillRule = FillRule.Nonzero };
        double x = 0;
        foreach (var ch in Text ?? "")
        {
            var ft = new FormattedText(ch.ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, tf, Size, Brushes.White, dpi);
            group.Children.Add(ft.BuildGeometry(new Point(x, 0)));
            x += ft.WidthIncludingTrailingWhitespace + Spacing;
        }
        _ink = group.Bounds.IsEmpty ? new Rect(0, 0, 1, 1) : group.Bounds;
        // raster cuts at the EDDIES wordmark positions (24 % and 52 % of the letter height)
        var cuts = new GeometryGroup();
        cuts.Children.Add(new RectangleGeometry(new Rect(_ink.Left - 2, _ink.Top + _ink.Height * 0.238, _ink.Width + 4, Math.Max(1.5, _ink.Height * 0.071))));
        cuts.Children.Add(new RectangleGeometry(new Rect(_ink.Left - 2, _ink.Top + _ink.Height * 0.524, _ink.Width + 4, Math.Max(1, _ink.Height * 0.048))));
        var cut = Geometry.Combine(group, cuts, GeometryCombineMode.Exclude, null);
        cut.Freeze();
        _geometry = cut;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        Build();
        return new Size(_ink.Right + 8, _ink.Bottom + 6);
    }

    /// <summary>Discrete states only, so the title redraws a few times per cycle instead of 30 times a second.</summary>
    private static int State(double t)
    {
        var a = (t % 4.2) / 4.2;
        var b = (t % 3.7) / 3.7;
        var f = (t % 6.0) / 6.0;
        var sa = a is >= 0.93 and < 0.95 ? 1 : a is >= 0.95 and < 0.97 ? 2 : 0;
        var sb = b is >= 0.89 and < 0.91 ? 1 : b is >= 0.91 and < 0.93 ? 2 : 0;
        var sf = f is >= 0.09 and < 0.11 ? 1 : f is >= 0.11 and < 0.14 ? 2 : 0;
        return sa + sb * 3 + sf * 9;
    }

    protected override bool ShouldRedraw(double t)
    {
        var s = State(t);
        if (s == _lastState) return false;
        _lastState = s;
        return true;
    }

    protected override void OnRender(DrawingContext dc)
    {
        Build();
        var g = _geometry!;
        var t = Time;
        var s = Fx.MotionEnabled ? State(t) : 0;
        var sa = s % 3;
        var sb = s / 3 % 3;
        var sf = s / 9;

        if (sf == 2)
        {
            Offset(dc, g, Cc, -2, 0, 0.8, null);
            Offset(dc, g, Mc, 2, 0, 0.8, null);
        }
        dc.DrawGeometry(Frozen(sf == 1 ? Colors.White : Yc), null, g);
        if (sf == 1)
        {
            Offset(dc, g, Cc, -4, 0, 0.6, null);
            Offset(dc, g, Mc, 4, 0, 0.6, null);
        }

        // P34: no underline

        // glitch slices
        var top = new RectangleGeometry(new Rect(_ink.Left - 6, _ink.Top - 4, _ink.Width + 12, _ink.Height * 0.40 + 4));
        var bottom = new RectangleGeometry(new Rect(_ink.Left - 6, _ink.Top + _ink.Height * 0.55, _ink.Width + 12, _ink.Height * 0.45 + 4));
        if (sa == 1) Offset(dc, g, Cc, -3, -1, 0.9, top);
        if (sa == 2) Offset(dc, g, Cc, 2, 1, 0.9, top);
        if (sb == 1) Offset(dc, g, Mc, 3, 1, 0.9, bottom);
        if (sb == 2) Offset(dc, g, Mc, -2, 0, 0.9, bottom);
    }

    private static void Offset(DrawingContext dc, Geometry g, Color c, double dx, double dy, double alpha, Geometry? clip)
    {
        if (clip is not null) dc.PushClip(clip);
        dc.PushTransform(new TranslateTransform(dx, dy));
        dc.DrawGeometry(Frozen(A(c, alpha)), null, g);
        dc.Pop();
        if (clip is not null) dc.Pop();
    }
}

/// <summary>
/// P13: BREACH corner brackets around the logo mark. They snap in from 1.8× size, hold and fade (2.4 s loop),
/// one corner after another. Still frame: no frame at all, as the user preferred over a static blue frame.
/// </summary>
public sealed class CornerBrackets : AnimatedElement
{
    private static readonly double[] Delays = { 0, 0.15, 0.3, 0.45 };

    protected override void OnRender(DrawingContext dc)
    {
        if (!Fx.MotionEnabled) return;
        var w = ActualWidth;
        var h = ActualHeight;
        var t = Time;
        const double len = 12;
        var corners = new[]
        {
            (new Point(0, 0), 1, 1), (new Point(w, 0), -1, 1), (new Point(w, h), -1, -1), (new Point(0, h), 1, -1),
        };
        for (int i = 0; i < 4; i++)
        {
            var p = ((t - Delays[i]) % 2.4 + 2.4) % 2.4 / 2.4;
            double opacity, scale;
            if (p < 0.3) { var k = p / 0.3; opacity = k; scale = 1.8 - 0.8 * k; }
            else if (p < 0.8) { opacity = 1; scale = 1; }
            else { opacity = 1 - (p - 0.8) / 0.2; scale = 1; }
            var (pt, sx, sy) = corners[i];
            var center = new Point(w / 2, h / 2);
            var cp = new Point(center.X + (pt.X - center.X) * (1 + (scale - 1) * 0.25), center.Y + (pt.Y - center.Y) * (1 + (scale - 1) * 0.25));
            var pen = new Pen(Frozen(A(Cc, opacity)), 2) { StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat };
            var l = len * scale;
            var geo = new StreamGeometry();
            using (var c = geo.Open())
            {
                c.BeginFigure(new Point(cp.X, cp.Y + sy * l), false, false);
                c.LineTo(cp, true, false);
                c.LineTo(new Point(cp.X + sx * l, cp.Y), true, false);
            }
            dc.DrawGeometry(null, pen, geo);
        }
    }
}

/// <summary>
/// P16: NETDECK-style network scan. Nodes linked by traces; a scan line sweeps across, nodes it passes ping with
/// a ring and packets run along the traces. <see cref="Active"/> makes it faster and brighter while scanning.
/// </summary>
public sealed class NetScan : AnimatedElement
{
    public static readonly DependencyProperty ActiveProperty = DependencyProperty.Register(nameof(Active), typeof(bool), typeof(NetScan),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty NodesProperty = DependencyProperty.Register(nameof(Nodes), typeof(int), typeof(NetScan),
        new FrameworkPropertyMetadata(9, FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((NetScan)d)._layout = null));
    public static readonly DependencyProperty SeedProperty = DependencyProperty.Register(nameof(Seed), typeof(int), typeof(NetScan),
        new FrameworkPropertyMetadata(7, FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((NetScan)d)._layout = null));

    public bool Active { get => (bool)GetValue(ActiveProperty); set => SetValue(ActiveProperty, value); }
    public int Nodes { get => (int)GetValue(NodesProperty); set => SetValue(NodesProperty, value); }
    public int Seed { get => (int)GetValue(SeedProperty); set => SetValue(SeedProperty, value); }

    private (Point[] Nodes, (int A, int B)[] Edges)? _layout;
    private Size _layoutSize;

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        _layout = null;
    }

    private (Point[] Nodes, (int A, int B)[] Edges) Layout(double w, double h)
    {
        if (_layout is { } l && _layoutSize == new Size(w, h)) return l;
        var rnd = new Random(Seed);
        var n = Math.Max(3, Nodes);
        var pts = new Point[n];
        for (int i = 0; i < n; i++)
        {
            var col = (i + 0.5) / n;
            pts[i] = new Point(w * (0.06 + 0.88 * col) + (rnd.NextDouble() - 0.5) * w / n * 0.6, h * (0.18 + 0.64 * rnd.NextDouble()));
        }
        var edges = new List<(int, int)>();
        for (int i = 0; i < n - 1; i++) edges.Add((i, i + 1));
        for (int i = 0; i < n - 2; i += 2) if (rnd.NextDouble() > 0.35) edges.Add((i, i + 2));
        _layout = (pts, edges.ToArray());
        _layoutSize = new Size(w, h);
        return _layout.Value;
    }

    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 0 || h <= 0) return;
        var (nodes, edges) = Layout(w, h);
        var t = Time;
        var active = Active;
        var speed = active ? 0.9 : 0.25;
        var scanX = ((t * speed) % 1.2 - 0.1) * w;
        var lineAlpha = active ? 0.45 : 0.22;

        // traces (right angles like circuit lines)
        foreach (var (a, b) in edges)
        {
            var p = nodes[a];
            var q = nodes[b];
            var mid = new Point(q.X, p.Y);
            var pen = new Pen(Frozen(A(Cc, lineAlpha)), 1);
            dc.DrawLine(pen, p, mid);
            dc.DrawLine(pen, mid, q);
            if (!Fx.MotionEnabled) continue;
            // packet
            var k = ((t * (active ? 1.4 : 0.5) + a * 0.37) % 1.0);
            var len1 = Math.Abs(mid.X - p.X);
            var len2 = Math.Abs(q.Y - mid.Y);
            var d = k * (len1 + len2);
            var pos = d <= len1 ? new Point(p.X + Math.Sign(mid.X - p.X) * d, p.Y) : new Point(mid.X, mid.Y + Math.Sign(q.Y - mid.Y) * (d - len1));
            dc.DrawRectangle(Frozen(A(a % 3 == 0 ? Mc : Yc, active ? 0.95 : 0.6)), null, new Rect(pos.X - 1.5, pos.Y - 1.5, 3, 3));
        }

        // nodes + ping rings when the scan line passes
        for (int i = 0; i < nodes.Length; i++)
        {
            var p = nodes[i];
            var since = Fx.MotionEnabled ? (scanX - p.X) / (w * speed) : 99;
            var hit = since is >= 0 and < 0.5;
            var size = 5.0;
            var fill = hit ? Yc : Cc;
            dc.DrawRectangle(Frozen(A(Bgc, 1)), new Pen(Frozen(A(fill, hit ? 1 : 0.7)), 1.2), new Rect(p.X - size / 2, p.Y - size / 2, size, size));
            if (hit)
            {
                var r = 3 + since * 26;
                dc.DrawEllipse(null, new Pen(Frozen(A(Cc, 0.8 * (1 - since * 2))), 1), p, r, r);
                dc.DrawRectangle(Frozen(Yc), null, new Rect(p.X - 1.5, p.Y - 1.5, 3, 3));
            }
        }

        // scan line
        if (Fx.MotionEnabled && scanX > 0 && scanX < w)
        {
            dc.DrawRectangle(new LinearGradientBrush(A(Cc, 0), A(Cc, active ? 0.28 : 0.14), 0), null, new Rect(Math.Max(0, scanX - 40), 0, Math.Min(40, scanX), h));
            dc.DrawRectangle(Frozen(A(Cc, active ? 0.9 : 0.5)), null, new Rect(scanX, 0, 1.5, h));
        }
    }
}

/// <summary>
/// EDDIES full-window glitch band: every 7–18 s a thin cyan/magenta band jumps sideways for 0.36 s.
/// </summary>
public sealed class GlitchBand : AnimatedElement
{
    private double _next = 6;
    private double _start = -1;
    private double _top;
    private double _height;
    private readonly Random _rnd = new();

    protected override bool ShouldRedraw(double t)
    {
        if (_start < 0 && t >= _next)
        {
            _start = t;
            _top = _rnd.NextDouble() * 0.8;
            _height = 0.02 + _rnd.NextDouble() * 0.06;
        }
        if (_start >= 0 && t - _start > 0.4)
        {
            _start = -1;
            _next = t + 7 + _rnd.NextDouble() * 11;
            return true;
        }
        return _start >= 0;
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (!Fx.MotionEnabled || _start < 0) return;
        var p = (Time - _start) / 0.36;
        if (p is < 0 or > 1) return;
        var dx = p < 0.33 ? -14 : p < 0.66 ? 10 : 0;
        var y = ActualHeight * _top;
        var h = ActualHeight * _height;
        var b = new LinearGradientBrush(A(Cc, 0.07 * (1 - p * 0.6)), A(Mc, 0.10 * (1 - p * 0.6)), 0);
        dc.DrawRectangle(b, null, new Rect(dx, y, ActualWidth, h));
        dc.DrawRectangle(Frozen(A(Cc, 0.35 * (1 - p))), null, new Rect(dx, y, ActualWidth, 1));
    }
}

/// <summary>One scanned game folder with the number of files AFTERLIFE did not install (P27).</summary>
public sealed record ScanFolder(string Label, string Short, int Count);

/// <summary>
/// P27: folder sweep instead of the node graph. Each scanned folder is a row (or a cell in compact mode); a scan
/// head runs over them, the bar of a folder fills when the head passes, folders with unmanaged files light up
/// yellow with their count. Faster and brighter while <see cref="Active"/>.
/// </summary>
public sealed class FolderScan : AnimatedElement
{
    public static readonly DependencyProperty FoldersProperty = DependencyProperty.Register(nameof(Folders), typeof(IEnumerable<ScanFolder>), typeof(FolderScan),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));
    public static readonly DependencyProperty ActiveProperty = DependencyProperty.Register(nameof(Active), typeof(bool), typeof(FolderScan),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
    /// <summary>P42: the sweep runs only while true (hover on the tile, scanning); otherwise a still "checked" frame.</summary>
    public static readonly DependencyProperty RunningProperty = DependencyProperty.Register(nameof(Running), typeof(bool), typeof(FolderScan),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender, (d, e) => { if (e.NewValue is true) ((FolderScan)d)._runStart = FxClock.Now; }));
    public bool Running { get => (bool)GetValue(RunningProperty); set => SetValue(RunningProperty, value); }
    private double _runStart;

    protected override bool ShouldRedraw(double t) => Running;

    public static readonly DependencyProperty CompactProperty = DependencyProperty.Register(nameof(Compact), typeof(bool), typeof(FolderScan),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

    public IEnumerable<ScanFolder>? Folders { get => (IEnumerable<ScanFolder>?)GetValue(FoldersProperty); set => SetValue(FoldersProperty, value); }
    public bool Active { get => (bool)GetValue(ActiveProperty); set => SetValue(ActiveProperty, value); }
    public bool Compact { get => (bool)GetValue(CompactProperty); set => SetValue(CompactProperty, value); }

    private const double RowH = 15;

    private IReadOnlyList<ScanFolder> List => Folders?.ToList() ?? new List<ScanFolder>();

    protected override Size MeasureOverride(Size available)
    {
        var n = Math.Max(1, List.Count);
        return Compact ? new Size(0, 30) : new Size(0, n * RowH + 4);
    }

    private FormattedText Text(string s, double size, Color c, bool bold = false)
    {
        var family = TryFindResource("MonoFont") as FontFamily ?? new FontFamily("Consolas");
        return new FormattedText(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(family, FontStyles.Normal, bold ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal), size, Frozen(c),
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var list = List;
        var w = ActualWidth;
        if (list.Count == 0 || w <= 0) return;
        var t = Time;
        var period = Active ? 1.6 : 5.0;
        // head position 0..1 over all folders, then a pause (25 % of the period)
        var phase = Fx.MotionEnabled && Running ? ((t - _runStart) % period) / period / 0.75 : 1.2;
        var head = phase * list.Count; // folder index the head is on

        if (Compact) { RenderCompact(dc, list, w, head); return; }

        var labelW = 118.0;
        var countW = 40.0;
        var barX = labelW;
        var barW = Math.Max(20, w - labelW - countW - 6);
        for (int i = 0; i < list.Count; i++)
        {
            var f = list[i];
            var y = 2 + i * RowH;
            var passed = head >= i + 1 || phase > 1;
            var onIt = head >= i && head < i + 1 && phase <= 1;
            var found = f.Count > 0;
            var labelColor = found && passed ? Yc : onIt ? Cc : A(Cc, 0.55);
            dc.DrawText(Text(f.Label, 10.5, labelColor), new Point(0, y));
            // track
            dc.DrawRectangle(Frozen(A(Cc, 0.08)), null, new Rect(barX, y + 5, barW, 4));
            // P90: the head runs over the whole bar; a checked folder stays full: cyan when clean, yellow when something was found
            var fill = passed ? 1.0 : onIt ? head - i : 0;
            dc.DrawRectangle(Frozen(found ? Yc : A(Cc, passed ? 0.45 : 0.7)), null, new Rect(barX, y + 5, barW * fill, 4));
            if (onIt)
            {
                var hx = barX + barW * fill;
                dc.DrawRectangle(Frozen(Colors.White), null, new Rect(hx - 1, y + 3, 2, 8));
                dc.DrawRectangle(new LinearGradientBrush(A(Cc, 0), A(Cc, 0.35), 0), null, new Rect(Math.Max(barX, hx - 30), y + 3, Math.Min(30, hx - barX), 8));
            }
            var countText = passed ? (found ? f.Count.ToString(CultureInfo.InvariantCulture) : "ok") : onIt ? "…" : "";
            var ct = Text(countText, 10.5, found ? Yc : A(Cc, 0.7), found);
            dc.DrawText(ct, new Point(w - ct.Width, y));
        }
    }

    private void RenderCompact(DrawingContext dc, IReadOnlyList<ScanFolder> list, double w, double head)
    {
        var gap = 4.0;
        var cellW = (w - gap * (list.Count - 1)) / list.Count;
        for (int i = 0; i < list.Count; i++)
        {
            var f = list[i];
            var x = i * (cellW + gap);
            var passed = head >= i + 1;
            var onIt = head >= i && head < i + 1;
            var found = f.Count > 0;
            var c = found && passed ? Yc : onIt ? Cc : A(Cc, 0.45);
            dc.DrawText(Text(f.Short, 9, c, found && passed), new Point(x, 0));
            dc.DrawRectangle(Frozen(A(Cc, 0.10)), null, new Rect(x, 15, cellW, 5));
            var fill = passed ? 1 : onIt ? head - i : 0;
            dc.DrawRectangle(Frozen(found ? A(Yc, 0.9) : A(Cc, onIt ? 0.9 : 0.5)), null, new Rect(x, 15, cellW * fill, 5));
            if (found && passed)
            {
                var ct = Text(f.Count.ToString(CultureInfo.InvariantCulture), 9, Yc, true);
                dc.DrawText(ct, new Point(x + cellW - ct.Width, 0));
            }
        }
    }
}

/// <summary>
/// P44: light support for the top of the window behind the logo: a slow cyan glow around the mark, a magenta
/// afterglow behind the name, thin circuit traces with data packets running out of the logo, a bottom rule with a
/// travelling highlight and a rare faint glitch slice. Still frame (glow and traces only) when motion is off.
/// </summary>
public sealed class HeaderAura : AnimatedElement
{
    /// <summary>Horizontal centre of the logo, in pixels from the left edge of this element.</summary>
    public double Anchor { get; set; } = 57;

    private static readonly (double Y, double Len, double Speed, double Phase, bool Magenta)[] Traces =
    [
        (0.30, 0.46, 0.11, 0.00, false),
        (0.52, 0.62, 0.075, 0.35, false),
        (0.74, 0.40, 0.14, 0.70, true),
        (0.88, 0.55, 0.06, 0.15, false),
    ];

    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 0 || h <= 0) return;
        var t = Time;
        var cy = h * 0.52;

        // glows: cyan around the mark (slow breathing), magenta afterglow behind the name
        var breathe = 0.5 + 0.5 * Math.Sin(t * Math.PI * 2 / 6.5);
        dc.DrawRectangle(Radial(Cc, 0.07 + 0.05 * breathe), null, new Rect(Anchor - 260, cy - 120, 520, 240));
        dc.DrawRectangle(Radial(Mc, 0.045 + 0.02 * (1 - breathe)), null, new Rect(Anchor + 120, cy - 90, 460, 180));

        // circuit traces leaving the logo to the right, with a short vertical drop at the start
        var x0 = Anchor + 44;
        foreach (var tr in Traces)
        {
            var y = Math.Round(h * tr.Y) + 0.5;
            var len = Math.Max(80, Math.Min(w * tr.Len, w - x0 - 500)); // stop before the core mods table
            var c = tr.Magenta ? Mc : Cc;
            var line = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
            line.GradientStops.Add(new GradientStop(A(c, 0.16), 0));
            line.GradientStops.Add(new GradientStop(A(c, 0.05), 0.55));
            line.GradientStops.Add(new GradientStop(A(c, 0), 1));
            line.Freeze();
            dc.DrawRectangle(line, null, new Rect(x0, y - 0.5, len, 1));
            dc.DrawRectangle(Frozen(A(c, 0.22)), null, new Rect(x0 - 3, y - 1.5, 3, 3)); // solder pad

            if (!Fx.MotionEnabled) continue;
            var p = (t * tr.Speed + tr.Phase) % 1.0;
            var px = x0 + p * len;
            var fade = 1 - p;
            var packet = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
            packet.GradientStops.Add(new GradientStop(A(c, 0), 0));
            packet.GradientStops.Add(new GradientStop(A(c, 0.55 * fade), 1));
            packet.Freeze();
            dc.DrawRectangle(packet, null, new Rect(px - 34, y - 0.5, 34, 1));
            dc.DrawRectangle(Frozen(A(c, 0.8 * fade)), null, new Rect(px, y - 1, 2, 2));
        }

        // bottom rule with a travelling highlight
        var by = h - 0.5;
        var rule = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
        rule.GradientStops.Add(new GradientStop(A(Cc, 0.25), 0));
        rule.GradientStops.Add(new GradientStop(A(Mc, 0.12), 0.5));
        rule.GradientStops.Add(new GradientStop(A(Cc, 0), 1));
        rule.Freeze();
        dc.DrawRectangle(rule, null, new Rect(0, by - 0.5, w, 1));
        if (Fx.MotionEnabled)
        {
            var hx = ((t / 9.0) % 1.0) * (w + 300) - 150;
            var hl = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
            hl.GradientStops.Add(new GradientStop(A(Cc, 0), 0));
            hl.GradientStops.Add(new GradientStop(A(Cc, 0.7), 0.5));
            hl.GradientStops.Add(new GradientStop(A(Cc, 0), 1));
            hl.Freeze();
            dc.DrawRectangle(hl, null, new Rect(hx, by - 1, 150, 2));

            // rare faint glitch slice (about every 7 s, 150 ms)
            var cycle = t % 7.3;
            if (cycle < 0.15)
            {
                var sy = h * (0.2 + 0.6 * ((Math.Floor(t / 7.3) * 0.618) % 1.0));
                dc.DrawRectangle(Frozen(A(Cc, 0.06)), null, new Rect(0, sy, w * 0.6, 3));
                dc.DrawRectangle(Frozen(A(Mc, 0.05)), null, new Rect(14, sy + 3, w * 0.45, 1));
            }
        }
    }

    private static Brush Radial(Color c, double alpha)
    {
        var b = new RadialGradientBrush { GradientOrigin = new Point(0.5, 0.5), Center = new Point(0.5, 0.5), RadiusX = 0.5, RadiusY = 0.5 };
        b.GradientStops.Add(new GradientStop(A(c, alpha), 0));
        b.GradientStops.Add(new GradientStop(A(c, alpha * 0.35), 0.45));
        b.GradientStops.Add(new GradientStop(A(c, 0), 1));
        b.Freeze();
        return b;
    }
}
