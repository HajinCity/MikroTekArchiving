using MikroTek.Domain.Custom;

namespace MikroTek.Domain
{
    /// <summary>A supplier, job order worker or employee paid through DVs.</summary>
    public class Payees : AuditableEntity
    {
        public string Name { get; set; } = string.Empty;
        public string? Tin { get; set; }
        public string? Address { get; set; }
        public PayeeKind Kind { get; set; } = PayeeKind.Supplier;
        public PayeeTaxType TaxType { get; set; } = PayeeTaxType.NonVat;

        /// <summary>false = no longer offered when encoding (old DVs keep the name).</summary>
        public bool IsActive { get; set; } = true;
    }
}
