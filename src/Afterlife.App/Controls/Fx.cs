using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace Afterlife.App.Controls;

/// <summary>
/// Decorative motion from the design (summary "Naznačené animace"): scan sweep over the header, progressive
/// check-off of frameworks, data travelling along requirement lines, lifted drop zone.
/// P14: looping effects (EDDIES, BREACH, NETDECK) run all the time; only the Reduce motion setting turns them off.
/// The Windows "animation effects" switch is no longer used: on many PCs it is off and the window looked static.
/// </summary>
public static class Fx
{
    private static bool _userReduceMotion;

    public static bool MotionEnabled => !_userReduceMotion;

    /// <summary>Raised when Reduce motion is switched; looping effects start or stop.</summary>
    public static event Action? MotionChanged;

    public static void SetReduceMotion(bool reduce)
    {
        _userReduceMotion = reduce;
        FxClock.Update();
        MotionChanged?.Invoke();
    }

    // ---------------- looping storyboard effects (P14) ----------------
    /// <summary>
    /// Flicker: BREACH echo marks (opacity 1 → .25 → 1 → .4 → 1, 3.2 s). Blink: terminal cursor (1 s).
    /// Beam: a light bar crossing its container every 5 s (BREACH header). Mark: EDDIES logo mark skew glitch (6 s).
    /// Pulse: slow breathing glow for the live dot.
    /// </summary>
    public static readonly DependencyProperty LoopProperty = DependencyProperty.RegisterAttached(
        "Loop", typeof(string), typeof(Fx), new PropertyMetadata(null, OnLoopChanged));
    public static string? GetLoop(DependencyObject d) => (string?)d.GetValue(LoopProperty);
    public static void SetLoop(DependencyObject d, string? v) => d.SetValue(LoopProperty, v);

    public static readonly DependencyProperty DelayProperty = DependencyProperty.RegisterAttached(
        "Delay", typeof(double), typeof(Fx), new PropertyMetadata(0.0));
    public static double GetDelay(DependencyObject d) => (double)d.GetValue(DelayProperty);
    public static void SetDelay(DependencyObject d, double v) => d.SetValue(DelayProperty, v);

    private static void OnLoopChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement el || e.NewValue is not string kind) return;
        void Start() => StartLoop(el, kind);
        void Changed() => el.Dispatcher.BeginInvoke(Start);
        el.Loaded += (_, _) => { Start(); MotionChanged += Changed; };
        el.Unloaded += (_, _) => MotionChanged -= Changed;
        if (kind == "Beam") el.SizeChanged += (_, _) => Start();
    }

    private static void StartLoop(FrameworkElement el, string kind)
    {
        var delay = TimeSpan.FromSeconds(GetDelay(el));
        el.BeginAnimation(UIElement.OpacityProperty, null);
        if (!MotionEnabled)
        {
            el.Opacity = kind == "Beam" ? 0 : 1;
            if (el.RenderTransform is TranslateTransform tt0) tt0.BeginAnimation(TranslateTransform.XProperty, null);
            if (el.RenderTransform is TransformGroup) el.RenderTransform = Transform.Identity;
            return;
        }
        switch (kind)
        {
            case "Flicker":
            {
                var k = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromSeconds(3.2), RepeatBehavior = RepeatBehavior.Forever, BeginTime = delay };
                foreach (var (at, v) in new[] { (0.0, 1.0), (0.40, 0.25), (0.44, 1.0), (0.46, 0.4), (0.48, 1.0) })
                    k.KeyFrames.Add(new DiscreteDoubleKeyFrame(v, KeyTime.FromPercent(at)));
                el.BeginAnimation(UIElement.OpacityProperty, k);
                break;
            }
            case "Blink":
            {
                var k = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromSeconds(1), RepeatBehavior = RepeatBehavior.Forever, BeginTime = delay };
                k.KeyFrames.Add(new DiscreteDoubleKeyFrame(1, KeyTime.FromPercent(0)));
                k.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromPercent(0.5)));
                el.BeginAnimation(UIElement.OpacityProperty, k);
                break;
            }
            case "Pulse":
            {
                var a = new DoubleAnimation(1, 0.35, TimeSpan.FromSeconds(1.2)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, BeginTime = delay, EasingFunction = new SineEase() };
                el.BeginAnimation(UIElement.OpacityProperty, a);
                break;
            }
            case "Beam":
            {
                var width = (el.Parent as FrameworkElement)?.ActualWidth ?? 0;
                if (width <= 0) return;
                var tt = el.RenderTransform as TranslateTransform ?? new TranslateTransform();
                el.RenderTransform = tt;
                el.Opacity = 1;
                var k = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromSeconds(5), RepeatBehavior = RepeatBehavior.Forever, BeginTime = delay };
                k.KeyFrames.Add(new LinearDoubleKeyFrame(-el.ActualWidth - 200, KeyTime.FromPercent(0)));
                k.KeyFrames.Add(new LinearDoubleKeyFrame(width + 60, KeyTime.FromPercent(0.55)));
                k.KeyFrames.Add(new DiscreteDoubleKeyFrame(width + 60, KeyTime.FromPercent(1)));
                tt.BeginAnimation(TranslateTransform.XProperty, k);
                break;
            }
            case "Mark":
            {
                var skew = new SkewTransform();
                var move = new TranslateTransform();
                var g = new TransformGroup();
                g.Children.Add(skew);
                g.Children.Add(move);
                el.RenderTransform = g;
                el.RenderTransformOrigin = new Point(0.5, 0.5);
                var d = TimeSpan.FromSeconds(6);
                DoubleAnimationUsingKeyFrames Keys(params (double At, double V)[] keys)
                {
                    var k = new DoubleAnimationUsingKeyFrames { Duration = d, RepeatBehavior = RepeatBehavior.Forever, BeginTime = delay };
                    foreach (var (at, v) in keys) k.KeyFrames.Add(new DiscreteDoubleKeyFrame(v, KeyTime.FromPercent(at)));
                    return k;
                }
                skew.BeginAnimation(SkewTransform.AngleXProperty, Keys((0, 0), (0.95, -8), (0.97, 0)));
                move.BeginAnimation(TranslateTransform.XProperty, Keys((0, 0), (0.95, 2), (0.97, -2), (0.99, 0)));
                move.BeginAnimation(TranslateTransform.YProperty, Keys((0, 0), (0.95, -1), (0.97, 1), (0.99, 0)));
                break;
            }
        }
    }

    // ---------------- EDDIES decode: text resolves from random glyphs ----------------
    private const string Glyphs = "#%&@$!?/\\<>[]{}=+*^~01";

    /// <summary>Runs the decode effect on a TextBlock once (on load, and again on <see cref="Replay"/>).</summary>
    public static readonly DependencyProperty DecodeProperty = DependencyProperty.RegisterAttached(
        "Decode", typeof(bool), typeof(Fx), new PropertyMetadata(false, OnDecodeChanged));
    public static bool GetDecode(DependencyObject d) => (bool)d.GetValue(DecodeProperty);
    public static void SetDecode(DependencyObject d, bool v) => d.SetValue(DecodeProperty, v);

    private static readonly List<WeakReference<TextBlock>> Decoders = new();

    private static void OnDecodeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock tb || e.NewValue is not true) return;
        Decoders.Add(new WeakReference<TextBlock>(tb));
        tb.Loaded += (_, _) => Decode(tb);
    }

    /// <summary>Replays the decode effect on every registered title (after a scan or apply).</summary>
    public static void Replay()
    {
        foreach (var w in Decoders.ToList())
            if (w.TryGetTarget(out var tb)) { if (tb.IsLoaded) Decode(tb); }
            else Decoders.Remove(w);
    }

    private static void Decode(TextBlock tb)
    {
        if (!MotionEnabled) return;
        var final = tb.Tag as string ?? tb.Text;
        tb.Tag = final;
        if (string.IsNullOrEmpty(final)) return;
        var start = FxClock.Now;
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
        timer.Tick += (_, _) =>
        {
            var p = (FxClock.Now - start) / 0.52;
            if (p >= 1) { tb.Text = final; timer.Stop(); return; }
            var chars = final.Select((c, i) => c == ' ' || i < final.Length * p ? c : Glyphs[Random.Shared.Next(Glyphs.Length)]).ToArray();
            tb.Text = new string(chars);
        };
        timer.Start();
    }

    private static readonly IEasingFunction Ease = new CubicEase { EasingMode = EasingMode.EaseOut };

    /// <summary>Sweeps a gradient bar across its container once.</summary>
    public static void Sweep(FrameworkElement bar, double distance)
    {
        if (!MotionEnabled || distance <= 0) return;
        var tt = bar.RenderTransform as TranslateTransform ?? new TranslateTransform();
        bar.RenderTransform = tt;
        bar.Visibility = Visibility.Visible;
        var a = new DoubleAnimation(-bar.ActualWidth, distance, TimeSpan.FromMilliseconds(1100)) { EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
        a.Completed += (_, _) => bar.Visibility = Visibility.Hidden;
        tt.BeginAnimation(TranslateTransform.XProperty, a);
    }

    /// <summary>Short chromatic jitter of the title layers (cyan left, magenta right).</summary>
    public static void Glitch(FrameworkElement cyanLayer, FrameworkElement magentaLayer)
    {
        if (!MotionEnabled) return;
        Jitter(cyanLayer, -1.5, -4.5);
        Jitter(magentaLayer, 1.5, 4.5);
    }

    private static void Jitter(FrameworkElement e, double rest, double peak)
    {
        var tt = e.RenderTransform as TranslateTransform ?? new TranslateTransform(rest, 0);
        e.RenderTransform = tt;
        var k = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(420) };
        k.KeyFrames.Add(new DiscreteDoubleKeyFrame(peak, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(60))));
        k.KeyFrames.Add(new DiscreteDoubleKeyFrame(rest, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(120))));
        k.KeyFrames.Add(new DiscreteDoubleKeyFrame(peak * 0.6, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(260))));
        k.KeyFrames.Add(new DiscreteDoubleKeyFrame(rest, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(320))));
        tt.BeginAnimation(TranslateTransform.XProperty, k);
    }

    /// <summary>Items appear one after another, sliding in from the left (Patch Guard check-off).</summary>
    public static void Stagger(ItemsControl items, int stepMs = 90)
    {
        if (!MotionEnabled) return;
        for (int i = 0; i < items.Items.Count; i++)
        {
            if (items.ItemContainerGenerator.ContainerFromIndex(i) is not UIElement c) continue;
            var tt = new TranslateTransform(-10, 0);
            c.RenderTransform = tt;
            c.Opacity = 0;
            var begin = TimeSpan.FromMilliseconds(250 + i * stepMs);
            c.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)) { BeginTime = begin });
            tt.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(-10, 0, TimeSpan.FromMilliseconds(260)) { BeginTime = begin, EasingFunction = Ease });
        }
    }

    // ---- data travelling along a requirement line ----
    public static readonly DependencyProperty TravelDotProperty = DependencyProperty.RegisterAttached(
        "TravelDot", typeof(bool), typeof(Fx), new PropertyMetadata(false, OnTravelDotChanged));

    public static bool GetTravelDot(DependencyObject d) => (bool)d.GetValue(TravelDotProperty);
    public static void SetTravelDot(DependencyObject d, bool v) => d.SetValue(TravelDotProperty, v);

    private static void OnTravelDotChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Canvas canvas || e.NewValue is not true) return;
        canvas.Loaded += (_, _) =>
        {
            if (!MotionEnabled || canvas.Children.OfType<Rectangle>().FirstOrDefault() is not { } dot) return;
            var h = Math.Max(0, canvas.ActualHeight - dot.Height);
            var a = new DoubleAnimation(0, h, TimeSpan.FromMilliseconds(900))
            {
                RepeatBehavior = new RepeatBehavior(3),
                BeginTime = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 500)),
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
            };
            a.Completed += (_, _) => dot.Opacity = 0;
            dot.Opacity = 1;
            dot.BeginAnimation(Canvas.TopProperty, a);
        };
    }

    /// <summary>Drop zone "lifts" while files are dragged over the window.</summary>
    public static void Lift(FrameworkElement zone, bool up)
    {
        var st = zone.RenderTransform as ScaleTransform ?? new ScaleTransform(1, 1);
        zone.RenderTransform = st;
        zone.RenderTransformOrigin = new Point(0.5, 0.5);
        var to = up ? 1.015 : 1.0;
        if (!MotionEnabled)
        {
            st.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            st.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            st.ScaleX = st.ScaleY = 1;
            return;
        }
        var a = new DoubleAnimation(to, TimeSpan.FromMilliseconds(160)) { EasingFunction = Ease };
        st.BeginAnimation(ScaleTransform.ScaleXProperty, a);
        st.BeginAnimation(ScaleTransform.ScaleYProperty, a);
    }
}
