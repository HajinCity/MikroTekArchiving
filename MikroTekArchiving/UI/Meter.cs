namespace MikroTek.Archiving.UI;

/// <summary>A part-to-whole bar: segments side by side with a 2px gap, plus a legend.</summary>
/// <param name="Tone">"s1".."s3" (palette), "muted" (de-emphasis gray), "track" (empty remainder).</param>
public record MeterSegment(string Label, decimal Amount, string Tone);

/// <summary>Usage: &lt;partial name="UI/_Meter" model="new Meter(title, total, segments)" /&gt;</summary>
public record Meter(string? Title, decimal Total, List<MeterSegment> Segments, string? Subtitle = null, bool Money = true);

public record TimelineItem(DateTime When, string Who, string What, string Icon = "bi-dot", string Tone = "blue");

public record EmptyState(string Title, string? Text = null, string Icon = "bi-inbox", string? ActionText = null, string? ActionHref = null);
