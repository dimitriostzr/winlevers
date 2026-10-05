using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinLevers.App.Services;
using WinLevers.Presentation.Reporting;

namespace WinLevers.App.Controls;

/// <summary>A set of states as one bar, each state's share of it in that state's colour.</summary>
/// <remarks>
/// The counts stay printed beside it: the bar is the shape of them, never a
/// replacement, so colour is never the only thing carrying a number. Star
/// columns do the proportions, so nothing here depends on the host's width.
/// </remarks>
public sealed partial class SegmentedBar : UserControl
{
    /// <summary>The shares to draw, in order.</summary>
    public static readonly DependencyProperty SharesProperty = DependencyProperty.Register(
        nameof(Shares),
        typeof(IReadOnlyList<ValueShare>),
        typeof(SegmentedBar),
        new PropertyMetadata(null, (d, _) => ((SegmentedBar)d).Rebuild()));

    /// <summary>Whether to print a legend under the bar.</summary>
    public static readonly DependencyProperty ShowLegendProperty = DependencyProperty.Register(
        nameof(ShowLegend),
        typeof(bool),
        typeof(SegmentedBar),
        new PropertyMetadata(false, (d, _) => ((SegmentedBar)d).Rebuild()));

    private static readonly StateToneToBrushConverter Tones = new();

    /// <summary>Creates the bar.</summary>
    public SegmentedBar() => InitializeComponent();

    /// <summary>The shares to draw, in order.</summary>
    public IReadOnlyList<ValueShare>? Shares
    {
        get => (IReadOnlyList<ValueShare>?)GetValue(SharesProperty);
        set => SetValue(SharesProperty, value);
    }

    /// <summary>Whether to print a legend under the bar.</summary>
    public bool ShowLegend
    {
        get => (bool)GetValue(ShowLegendProperty);
        set => SetValue(ShowLegendProperty, value);
    }

    private void Rebuild()
    {
        Bar.ColumnDefinitions.Clear();
        Bar.Children.Clear();

        var shares = Shares?.Where(s => s.Count > 0).ToList() ?? [];

        for (var i = 0; i < shares.Count; i++)
        {
            var share = shares[i];
            var first = i == 0;
            var last = i == shares.Count - 1;

            Bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(share.Count, GridUnitType.Star) });

            // Rounded at the ends of the bar only, with a sliver of surface
            // between segments so two neighbours never read as one.
            var segment = new Border
            {
                Background = (Brush)Tones.Convert(share.Tone, typeof(Brush), null!, string.Empty),
                CornerRadius = new CornerRadius(first ? 3 : 0, last ? 3 : 0, last ? 3 : 0, first ? 3 : 0),
                Margin = new Thickness(0, 0, last ? 0 : 2, 0),
            };

            ToolTipService.SetToolTip(segment, $"{share.Label} · {share.Count} · {share.Percent}%");
            Grid.SetColumn(segment, i);
            Bar.Children.Add(segment);
        }

        Legend.ItemsSource = ShowLegend ? shares : null;
        Legend.Visibility = ShowLegend && shares.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}
