namespace MikroTek.Archiving.UI;

/// <summary>
/// A KPI / stat card. Render with: &lt;partial name="UI/_StatCard" model="new StatCard(...)" /&gt;
/// Variants: "icon" (round icon + big number, default), "plain" (number only), "compact" (small, inline icon).
/// Tones: blue, aqua, orange, violet, green, red, gray.
/// </summary>
public record StatCard(
    string Label,
    string Value,
    string Icon = "bi-graph-up",
    string Tone = "blue",
    string? Hint = null,
    decimal? Trend = null,          // e.g. 12.5 = +12.5% ; shown with arrow
    bool TrendUpIsGood = true,
    string Variant = "icon",
    string? Href = null,
    decimal[]? Spark = null);       // optional sparkline values
