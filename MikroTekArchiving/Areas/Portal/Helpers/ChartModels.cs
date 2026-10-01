namespace MikroTek.Archiving.Areas.Portal.Helpers;

/// <summary>One bar in a horizontal bar chart (label, optional small caption, amount).</summary>
public record BarItem(string Label, string? Sub, decimal Amount);

/// <summary>Model for the _HBars partial.</summary>
public record BarChart(string Title, string? Subtitle, List<BarItem> Items, int MaxBars = 6)
{
    /// <summary>Keeps the biggest bars and folds the rest into "Other" so the chart stays readable.</summary>
    public List<BarItem> Display()
    {
        var sorted = Items.Where(i => i.Amount > 0).OrderByDescending(i => i.Amount).ToList();
        if (sorted.Count <= MaxBars) return sorted;
        var top = sorted.Take(MaxBars - 1).ToList();
        var rest = sorted.Skip(MaxBars - 1).ToList();
        top.Add(new BarItem($"Other ({rest.Count} accounts)", null, rest.Sum(r => r.Amount)));
        return top;
    }
}
