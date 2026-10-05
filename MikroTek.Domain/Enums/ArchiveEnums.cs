namespace MikroTek.Domain
{
    public enum PayeeKind
    {
        Supplier,
        JobOrderWorker,
        Employee
    }

    /// <summary>How the payee is taxed (decides the withholding columns of the Alphalist).</summary>
    public enum PayeeTaxType
    {
        NonVat,
        Vat,
        Exempt
    }

    public enum CashAdvanceStatus
    {
        /// <summary>DVs can still be charged to it.</summary>
        Open,
        /// <summary>Fully reported to the Division; kept for reference.</summary>
        Liquidated
    }

    /// <summary>One line of a DV's supporting-documents checklist.</summary>
    public enum ChecklistMark
    {
        /// <summary>Not yet attached (shown blank on the cover sheet).</summary>
        Missing,
        /// <summary>Attached (✓).</summary>
        Attached,
        /// <summary>Not needed for this DV (NA).</summary>
        NotApplicable
    }
}
