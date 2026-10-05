using MikroTek.Domain.Custom;

namespace MikroTek.Domain
{
    /// <summary>
    /// A cover-sheet checklist for one type of transaction ("Job Order", "Office Supplies", ...).
    /// When a DV of that type is encoded, the items are copied onto the DV.
    /// </summary>
    public class ChecklistTemplates : AuditableEntity
    {
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public List<ChecklistTemplateItems> Items { get; set; } = [];
    }

    /// <summary>One required document of a checklist template.</summary>
    public class ChecklistTemplateItems : BaseEntity
    {
        public long TemplateId { get; set; }
        public ChecklistTemplates? Template { get; set; }

        /// <summary>Position on the cover sheet (1 = first).</summary>
        public int SortOrder { get; set; }
        public string Label { get; set; } = string.Empty;
    }
}
