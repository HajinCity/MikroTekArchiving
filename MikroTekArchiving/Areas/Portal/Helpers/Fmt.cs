using System.Globalization;

namespace MikroTek.Archiving.Areas.Portal.Helpers;

/// <summary>Number/date formatting used across the portal and print-outs.</summary>
public static class Fmt
{
    private static readonly CultureInfo Ph = CultureInfo.GetCultureInfo("en-US");

    public static string Peso(decimal v) => "₱" + v.ToString("N2", Ph);
    public static string Num(decimal v) => v.ToString("N2", Ph);
    /// <summary>Accounting style: negatives in parentheses, zero as a dash.</summary>
    public static string Acct(decimal v) => v == 0 ? "-" : v < 0 ? $"({(-v).ToString("N2", Ph)})" : v.ToString("N2", Ph);
    public static string Date(DateTime? d) => d?.ToString("MM/dd/yyyy", Ph) ?? "";
    public static string LongDate(DateTime? d) => d?.ToString("MMMM d, yyyy", Ph) ?? "";
}
