using MikroTek.Services.DTO;

namespace MikroTek.Services.Interface
{
    /// <summary>Suppliers, job order workers and employees paid through DVs (Setup → Payees).</summary>
    public interface IPayees
    {
        Task<List<PayeeDto>> GetAllAsync();
        Task<ServiceResult> SaveAsync(SavePayeeRequest request, long byUserId);
    }

    /// <summary>The cover-sheet checklists per transaction type (Setup → Checklist Templates).</summary>
    public interface IChecklistTemplates
    {
        Task<List<ChecklistTemplateDto>> GetAllAsync();
        Task<ChecklistTemplateDto?> GetAsync(long id);
        Task<ServiceResult> SaveAsync(SaveChecklistTemplateRequest request, long byUserId);
        /// <summary>DVs already encoded keep their own copy of the checklist.</summary>
        Task<ServiceResult> DeleteAsync(long id, long byUserId);

        /// <summary>
        /// Puts back the standard checklists: a missing one is added; one with the same name gets the standard documents
        /// it lacks (its own extra documents are kept, after them). Returns how many were added and completed.
        /// </summary>
        Task<(int Added, int Completed)> RestoreStandardAsync(long byUserId);
    }

    /// <summary>School details and signatories printed on reports (Setup → School &amp; Signatories).</summary>
    public interface ISchoolProfile
    {
        Task<SchoolProfileDto> GetAsync();
        Task<ServiceResult> SaveAsync(SchoolProfileDto profile, long byUserId);
    }

    /// <summary>MOOE cash advances.</summary>
    public interface ICashAdvances
    {
        /// <summary>Every cash advance with its encoded DVs, newest first.</summary>
        Task<List<CashAdvanceDto>> GetAllAsync();
        Task<CashAdvanceDto?> GetAsync(long id);
        /// <summary>Adds (Id = 0) or updates. Result.Id = the cash advance's Id.</summary>
        Task<ServiceResult> SaveAsync(SaveCashAdvanceRequest request, long byUserId);
        Task<ServiceResult> SetLiquidatedAsync(long id, bool liquidated, long byUserId);
        /// <summary>Only a cash advance with no DVs can be deleted.</summary>
        Task<ServiceResult> DeleteAsync(long id, long byUserId);
    }

    /// <summary>Disbursement vouchers.</summary>
    public interface IDisbursements
    {
        /// <summary>Every DV, oldest first. Drafts (uploaded, not yet encoded) only when asked for.</summary>
        Task<List<DvDto>> GetAllAsync(bool includeDrafts = false);
        Task<DvDto?> GetAsync(long id);

        /// <summary>Adds (Id = 0) or updates an encoded DV; a draft becomes a real DV. Result.Id = the DV's Id.</summary>
        Task<ServiceResult> SaveAsync(SaveDvRequest request, long byUserId);

        /// <summary>Saves only the checklist marks, dates and remarks of an existing DV.</summary>
        Task<ServiceResult> SaveChecklistAsync(long id, List<ChecklistInput> marks, string? remarks, long byUserId);

        /// <summary>Starts an empty DV for a scan that was just uploaded. Result.Id = the draft's Id.</summary>
        Task<ServiceResult> CreateDraftAsync(long cashAdvanceId, long? templateId, long byUserId);

        /// <summary>Deletes the DV with its lines, checklist and scanned files.</summary>
        Task<ServiceResult> DeleteAsync(long id, long byUserId);

        /// <summary>The next free DV number of each cash advance: cash advance Id → "JHS2026-03-054".</summary>
        Task<Dictionary<long, string>> GetNextDvNumbersAsync();

        Task<ArchiveAlerts> GetAlertsAsync();
    }
}
