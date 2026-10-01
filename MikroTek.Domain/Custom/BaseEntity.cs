namespace MikroTek.Domain.Custom
{
    /// <summary>
    /// Parent of every table class. Every table gets the same kind of primary key: a long Id
    /// (SQL bigint, auto-numbered). Because of this, CoreQueries can work with any table by Id.
    /// </summary>
    public abstract class BaseEntity
    {
        public long Id { get; set; }
    }

    /// <summary>
    /// For tables that must record who created and last changed a row (vouchers, cash advances...).
    /// CoreQueries fills these in automatically on save, using its UserId.
    /// </summary>
    public abstract class AuditableEntity : BaseEntity
    {
        public DateTime CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public long? UpdatedBy { get; set; }
    }
}
