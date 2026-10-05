namespace MikroTek.Archiving.Areas.Portal.Helpers;

/// <summary>
/// The spending categories used in charts, always in this fixed order so each one keeps
/// the same colour everywhere (Supplies is always blue, Repairs always orange, ...).
/// </summary>
public static class SpendTypes
{
    public static readonly string[] Order = ["Supplies", "Repairs", "Utilities", "Travel", "Labor (Job Order)"];

    /// <summary>Short chart name for a DV transaction type, e.g. "Traveling / Transportation Expenses" → "Travel".</summary>
    public static string Of(string dvType) =>
        dvType.StartsWith("Office") ? "Supplies"
        : dvType.StartsWith("Repair") ? "Repairs"
        : dvType.StartsWith("Job") ? "Labor (Job Order)"
        : dvType.StartsWith("Utilities") ? "Utilities"
        : "Travel";
}
