namespace MikroTek.Domain
{
    /// <summary>The kind of UACS account (first digit of the code: 1 Asset, 2 Liability, 3 Equity, 4 Income, 5 Expense).</summary>
    public enum AccountGroup
    {
        Asset,
        Liability,
        Equity,
        Income,
        Expense
    }
}
