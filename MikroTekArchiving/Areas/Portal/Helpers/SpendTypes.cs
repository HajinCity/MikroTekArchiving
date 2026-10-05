using MikroTek.Services.DTO;

namespace MikroTek.Archiving.Areas.Portal.Helpers;

/// <summary>
/// The spending categories used in charts = the transaction types (checklist names) of the DVs.
/// Always listed A–Z, so a type keeps its place and colour from one chart to the next.
/// </summary>
public static class SpendTypes
{
    public const string Other = "Other types";

    /// <summary>
    /// The types to show for these DVs. A pie stays readable up to about six slices, so with more
    /// types the biggest ones are kept and the rest are shown together as "Other types".
    /// </summary>
    public static List<string> For(IEnumerable<DvDto> dvs, int max = 6)
    {
        var totals = dvs.GroupBy(v => v.TypeOrOther).Select(g => (Type: g.Key, Gross: g.Sum(v => v.Gross))).ToList();
        if (totals.Count <= max) return totals.Select(t => t.Type).OrderBy(t => t).ToList();

        var kept = totals.OrderByDescending(t => t.Gross).Take(max - 1).Select(t => t.Type).OrderBy(t => t).ToList();
        kept.Add(Other);
        return kept;
    }

    /// <summary>Which of the shown types a DV falls under.</summary>
    public static string Of(DvDto dv, List<string> shown) => shown.Contains(dv.TypeOrOther) ? dv.TypeOrOther : Other;
}
