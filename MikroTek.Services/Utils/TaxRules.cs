using MikroTek.Domain;

namespace MikroTek.Services.Utils
{
    /// <summary>The withholding taxes of a DV when "Automatic" is chosen (the DV form computes the same in the browser).</summary>
    public record TaxBreakdown(decimal TaxBase, decimal Tax1, decimal Tax3, decimal Tax5)
    {
        public decimal Total => Tax1 + Tax3 + Tax5;
    }

    /// <summary>
    /// Automatic withholding by the payee's tax type:
    ///  - Non-VAT: 1% and 3% of the gross amount.
    ///  - VAT: tax base = gross ÷ 1.12 (without the 12% VAT), then 1% and 5% of the tax base.
    ///  - Exempt: nothing withheld.
    /// Every withheld amount is credited to Due to BIR.
    /// </summary>
    public static class TaxRules
    {
        public const string DueToBirCode = "2020101000";
        public const string DueToBirTitle = "Due to BIR";
        public const decimal VatDivisor = 1.12m;

        public static TaxBreakdown Compute(decimal gross, PayeeTaxType type) => type switch
        {
            PayeeTaxType.NonVat => new(gross, Round(gross * 0.01m), Round(gross * 0.03m), 0),
            PayeeTaxType.Vat => Vat(Round(gross / VatDivisor)),
            _ => new(gross, 0, 0, 0)
        };

        private static TaxBreakdown Vat(decimal taxBase) => new(taxBase, Round(taxBase * 0.01m), 0, Round(taxBase * 0.05m));

        /// <summary>To the centavo, .5 rounded up (as on the bookkeeper's computation).</summary>
        public static decimal Round(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);
    }
}
