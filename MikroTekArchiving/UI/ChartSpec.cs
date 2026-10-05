using System.Text.Json;

namespace MikroTek.Archiving.UI;

public enum ChartType { Bar, HBar, StackedBar, Line, Area, Donut }

/// <summary>One data series. Colors come from the validated palette in order (series 1 = blue, 2 = orange, …).</summary>
public record ChartSeries(string Name, IEnumerable<decimal> Values);

/// <summary>
/// Reusable chart (Chart.js underneath, styled by wwwroot/ui/ui-charts.js).
/// Usage:  &lt;partial name="UI/_Chart" model="new ChartSpec(ChartType.Bar, labels, [new("JHS", jhs), new("SHS", shs)])" /&gt;
/// Every chart also has a "Table" toggle so the numbers can be read without the picture.
/// Highlight = index of one label to emphasise: on a one-series bar/hbar that bar is blue and the
/// rest gray ("this one vs the others"); on a donut that slice is pulled out.
/// </summary>
public record ChartSpec(
    ChartType Type,
    IEnumerable<string> Labels,
    IEnumerable<ChartSeries> Series,
    string? Title = null,
    string? Subtitle = null,
    bool Money = true,
    int Height = 260,
    string? Id = null,
    int? Highlight = null)
{
    public string ElementId { get; } = Id ?? "chart" + Guid.NewGuid().ToString("N")[..8];

    public string ToJson() => JsonSerializer.Serialize(new
    {
        type = Type.ToString().ToLowerInvariant(),
        labels = Labels,
        series = Series.Select(s => new { name = s.Name, values = s.Values }),
        money = Money,
        highlight = Highlight
    });
}
