using System.Text.Json;

namespace MikroTek.Archiving.UI;

public enum ChartType { Bar, HBar, StackedBar, Line, Area, Donut }

public record ChartSeries(string Name, IEnumerable<decimal> Values);

public record ChartSpec(
    ChartType Type,
    IEnumerable<string> Labels,
    IEnumerable<ChartSeries> Series,
    string? Title = null,
    string? Subtitle = null,
    bool Money = true,
    int Height = 260,
    string? Id = null)
{
    public string ElementId { get; } = Id ?? "chart" + Guid.NewGuid().ToString("N")[..8];

    public string ToJson() => JsonSerializer.Serialize(new
    {
        type = Type.ToString().ToLowerInvariant(),
        labels = Labels,
        series = Series.Select(s => new { name = s.Name, values = s.Values }),
        money = Money
    });
}
