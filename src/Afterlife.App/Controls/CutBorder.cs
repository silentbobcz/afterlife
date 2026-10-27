using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Afterlife.App.Controls;

/// <summary>
/// Border with chamfered corners (AFTERLIFE identity). Draws fill, outline and an optional accent on the top edge.
/// </summary>
public sealed class CutBorder : Decorator
{
    public static readonly DependencyProperty BackgroundProperty = DependencyProperty.Register(nameof(Background), typeof(Brush), typeof(CutBorder),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty BorderBrushProperty = DependencyProperty.Register(nameof(BorderBrush), typeof(Brush), typeof(CutBorder),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty BorderThicknessProperty = DependencyProperty.Register(nameof(BorderThickness), typeof(double), typeof(CutBorder),
        new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty CutProperty = DependencyProperty.Register(nameof(Cut), typeof(double), typeof(CutBorder),
        new FrameworkPropertyMetadata(8.0, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty CutTopLeftProperty = DependencyProperty.Register(nameof(CutTopLeft), typeof(bool), typeof(CutBorder),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty CutTopRightProperty = DependencyProperty.Register(nameof(CutTopRight), typeof(bool), typeof(CutBorder),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty CutBottomRightProperty = DependencyProperty.Register(nameof(CutBottomRight), typeof(bool), typeof(CutBorder),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
    public bool CutBottomRight { get => (bool)GetValue(CutBottomRightProperty); set => SetValue(CutBottomRightProperty, value); }
    public static readonly DependencyProperty AccentBrushProperty = DependencyProperty.Register(nameof(AccentBrush), typeof(Brush), typeof(CutBorder),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty PaddingProperty = DependencyProperty.Register(nameof(Padding), typeof(Thickness), typeof(CutBorder),
        new FrameworkPropertyMetadata(new Thickness(0), FrameworkPropertyMetadataOptions.AffectsMeasure));

    public Brush? Background { get => (Brush?)GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }
    public Brush? BorderBrush { get => (Brush?)GetValue(BorderBrushProperty); set => SetValue(BorderBrushProperty, value); }
    public double BorderThickness { get => (double)GetValue(BorderThicknessProperty); set => SetValue(BorderThicknessProperty, value); }
    public double Cut { get => (double)GetValue(CutProperty); set => SetValue(CutProperty, value); }
    public bool CutTopLeft { get => (bool)GetValue(CutTopLeftProperty); set => SetValue(CutTopLeftProperty, value); }
    public bool CutTopRight { get => (bool)GetValue(CutTopRightProperty); set => SetValue(CutTopRightProperty, value); }
    public Brush? AccentBrush { get => (Brush?)GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }
    public Thickness Padding { get => (Thickness)GetValue(PaddingProperty); set => SetValue(PaddingProperty, value); }

    protected override Size MeasureOverride(Size constraint)
    {
        var p = Padding;
        var inner = new Size(Math.Max(0, constraint.Width - p.Left - p.Right), Math.Max(0, constraint.Height - p.Top - p.Bottom));
        if (Child is null) return new Size(p.Left + p.Right, p.Top + p.Bottom);
        Child.Measure(inner);
        return new Size(Child.DesiredSize.Width + p.Left + p.Right, Child.DesiredSize.Height + p.Top + p.Bottom);
    }

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        var p = Padding;
        Child?.Arrange(new Rect(p.Left, p.Top, Math.Max(0, arrangeSize.Width - p.Left - p.Right), Math.Max(0, arrangeSize.Height - p.Top - p.Bottom)));
        return arrangeSize;
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;
        var half = BorderThickness / 2;
        var k = Math.Min(Cut, Math.Min(w, h) / 2);
        var tl = CutTopLeft ? k : 0;
        var tr = CutTopRight ? k : 0;
        var br = CutBottomRight ? k : 0;

        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(new Point(half, tl + half), true, true);
            c.LineTo(new Point(tl + half, half), true, false);
            c.LineTo(new Point(w - tr - half, half), true, false);
            c.LineTo(new Point(w - half, tr + half), true, false);
            c.LineTo(new Point(w - half, h - br - half), true, false);
            if (br > 0) c.LineTo(new Point(w - br - half, h - half), true, false);
            c.LineTo(new Point(half, h - half), true, false);
        }
        g.Freeze();
        var pen = BorderBrush is null || BorderThickness <= 0 ? null : new Pen(BorderBrush, BorderThickness) { LineJoin = PenLineJoin.Miter };
        dc.DrawGeometry(Background, pen, g);

        if (AccentBrush is not null)
        {
            var accent = new StreamGeometry();
            using (var c = accent.Open())
            {
                c.BeginFigure(new Point(1, tl + 1), false, false);
                c.LineTo(new Point(tl + 1, 1), true, false);
                c.LineTo(new Point(w - tr - 1, 1), true, false);
                c.LineTo(new Point(w - 1, tr + 1), true, false);
            }
            accent.Freeze();
            dc.DrawGeometry(null, new Pen(AccentBrush, 2) { LineJoin = PenLineJoin.Miter }, accent);
        }
    }
}
