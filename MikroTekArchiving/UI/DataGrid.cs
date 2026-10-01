using System.Globalization;
using System.Linq.Expressions;

namespace MikroTek.Archiving.UI;

public enum ColType { Text, Money, Number, Date, Badge, Html }

/// <summary>One column of a DataGrid. Built through DataGrid.For(rows).Text(...).Money(...).</summary>
public class GridColumn
{
    public string Header { get; init; } = "";
    public ColType Type { get; init; } = ColType.Text;
    public bool Sortable { get; init; } = true;
    public bool Total { get; init; }
    public bool Nowrap { get; init; }
    public bool HideOnMobile { get; init; }
    public string? Width { get; init; }
    public string? CssClass { get; init; }
    internal Func<object, object?> Value { get; init; } = _ => null;
    internal Func<object, string?>? Href { get; init; }
    internal Func<object, string?>? Tone { get; init; }

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");

    public object? Raw(object row) => Value(row);
    public string? Link(object row) => Href?.Invoke(row);
    public string BadgeTone(object row) => Tone?.Invoke(row) ?? "gray";

    /// <summary>Text shown in the cell.</summary>
    public string Display(object row) => Value(row) switch
    {
        null => "",
        decimal d when Type == ColType.Money => d.ToString("N2", En),
        decimal d => d.ToString("N0", En),
        int i => i.ToString("N0", En),
        DateTime dt => dt.ToString("MM/dd/yyyy", Inv),
        var v => v.ToString() ?? ""
    };

    /// <summary>Value the grid script sorts by (numbers and ISO dates sort correctly).</summary>
    public string SortKey(object row) => Value(row) switch
    {
        null => "",
        decimal d => d.ToString(Inv),
        int i => i.ToString(Inv),
        DateTime dt => dt.ToString("yyyy-MM-dd HH:mm", Inv),
        var v => v.ToString() ?? ""
    };

    public bool IsNumeric => Type is ColType.Money or ColType.Number;
}

/// <summary>
/// Reusable data grid: server renders a normal table (works without JavaScript),
/// then wwwroot/ui/ui-grid.js adds search, sorting, paging, totals and CSV export.
///
/// Usage in a view:
///   var grid = DataGrid.For(Model)
///       .Text("DV No.", x => x.DvNo, href: x => Url.Action("Details", new { x.Id }))
///       .Date("Date", x => x.Date)
///       .Money("Net", x => x.Net, total: true)
///       .Badge("Status", x => x.Status, x => x.Status == "Paid" ? "green" : "orange")
///       .Build("dvGrid", pageSize: 10);
///   &lt;partial name="UI/_DataGrid" model="grid" /&gt;
/// </summary>
public class DataGrid
{
    public string Id { get; init; } = "grid";
    public List<GridColumn> Columns { get; init; } = [];
    public List<object> Rows { get; init; } = [];
    public int PageSize { get; init; } = 10;
    public bool Searchable { get; init; } = true;
    public bool Exportable { get; init; } = true;
    public string ExportName { get; init; } = "export";
    public string EmptyText { get; init; } = "No records found.";
    public Func<object, string?>? RowActions { get; init; }   // raw HTML per row (buttons)
    public bool Compact { get; init; }

    public static DataGridBuilder<T> For<T>(IEnumerable<T> rows) where T : notnull => new(rows);
}

public class DataGridBuilder<T>(IEnumerable<T> rows) where T : notnull
{
    private readonly List<GridColumn> _cols = [];
    private Func<T, string?>? _actions;

    private static Func<object, TR> W<TR>(Func<T, TR> f) => o => f((T)o);

    public DataGridBuilder<T> Text(string header, Func<T, object?> value, Func<T, string?>? href = null,
        bool nowrap = false, bool hideOnMobile = false, string? width = null, string? css = null)
    { _cols.Add(new() { Header = header, Type = ColType.Text, Value = W(value), Href = href is null ? null : W(href), Nowrap = nowrap, HideOnMobile = hideOnMobile, Width = width, CssClass = css }); return this; }

    public DataGridBuilder<T> Money(string header, Func<T, decimal> value, bool total = false, bool hideOnMobile = false)
    { _cols.Add(new() { Header = header, Type = ColType.Money, Value = o => value((T)o), Total = total, Nowrap = true, HideOnMobile = hideOnMobile }); return this; }

    public DataGridBuilder<T> Number(string header, Func<T, decimal> value, bool total = false, bool hideOnMobile = false)
    { _cols.Add(new() { Header = header, Type = ColType.Number, Value = o => value((T)o), Total = total, Nowrap = true, HideOnMobile = hideOnMobile }); return this; }

    public DataGridBuilder<T> Date(string header, Func<T, DateTime?> value, bool hideOnMobile = false)
    { _cols.Add(new() { Header = header, Type = ColType.Date, Value = o => value((T)o), Nowrap = true, HideOnMobile = hideOnMobile }); return this; }

    /// <summary>Status pill. tone: blue, aqua, orange, violet, green, red, gray.</summary>
    public DataGridBuilder<T> Badge(string header, Func<T, string?> text, Func<T, string> tone, bool hideOnMobile = false)
    { _cols.Add(new() { Header = header, Type = ColType.Badge, Value = o => text((T)o), Tone = o => tone((T)o), Nowrap = true, HideOnMobile = hideOnMobile }); return this; }

    /// <summary>Custom HTML cell (already-encoded). Not sortable by default.</summary>
    public DataGridBuilder<T> Html(string header, Func<T, string> html, bool sortable = false, bool hideOnMobile = false)
    { _cols.Add(new() { Header = header, Type = ColType.Html, Value = o => html((T)o), Sortable = sortable, HideOnMobile = hideOnMobile }); return this; }

    /// <summary>Buttons at the end of each row (raw HTML).</summary>
    public DataGridBuilder<T> Actions(Func<T, string?> html) { _actions = html; return this; }

    public DataGrid Build(string id, int pageSize = 10, bool searchable = true, bool exportable = true,
        string? exportName = null, string emptyText = "No records found.", bool compact = false) => new()
    {
        Id = id, Columns = _cols, Rows = rows.Cast<object>().ToList(), PageSize = pageSize,
        Searchable = searchable, Exportable = exportable, ExportName = exportName ?? id, EmptyText = emptyText,
        RowActions = _actions is null ? null : o => _actions((T)o), Compact = compact
    };
}
