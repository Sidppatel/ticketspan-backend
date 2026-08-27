namespace Db.Entities;

public class FeeFormula : BaseEntity
{
    public required string Name { get; set; }
    public int PercentBps { get; set; }
    public int FlatCents { get; set; }
    public int? MaxFeeCents { get; set; }
    public bool IsActive { get; set; } = true;
}
