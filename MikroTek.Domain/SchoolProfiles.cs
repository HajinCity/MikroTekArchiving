using MikroTek.Domain.Custom;

namespace MikroTek.Domain
{
    /// <summary>The school's details and signatories printed on reports. There is only one row.</summary>
    public class SchoolProfiles : AuditableEntity
    {
        public string SchoolName { get; set; } = string.Empty;
        public string? Region { get; set; }
        public string? Division { get; set; }
        public string? District { get; set; }
        public string? Address { get; set; }
        public string? JhsBankAccount { get; set; }
        public string? ShsBankAccount { get; set; }
        public string? ResponsibilityCenter { get; set; }
        public string? FundCluster { get; set; }       
        public string? HeadName { get; set; }
        public string? HeadPosition { get; set; }    
        public string? BookkeeperName { get; set; }
        public string? BookkeeperPosition { get; set; }      
        public string? ValidatorName { get; set; }
        public string? ValidatorPosition { get; set; }       
        public string? CashUnitName { get; set; }
        public string? CashUnitPosition { get; set; }
    }
}
