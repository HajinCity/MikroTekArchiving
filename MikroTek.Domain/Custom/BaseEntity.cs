namespace MikroTek.Domain.Custom
{    
    public abstract class BaseEntity
    {
        public long Id { get; set; }
    }    
    public abstract class AuditableEntity : BaseEntity
    {
        public DateTime CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public long? UpdatedBy { get; set; }
    }
}
