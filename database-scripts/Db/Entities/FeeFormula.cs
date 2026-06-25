namespace Db.Entities;

/// <summary>
/// A developer-defined service-fee formula applied to ticket types / tables.
/// fee = round(price * PercentBps / 10000) + FlatCents, clamped to
/// [MinFeeCents, MaxFeeCents] when set. Platform-global (not tenant scoped):
/// developers write, everyone reads.
/// </summary>
public class FeeFormula : BaseEntity
{
    public required string Name { get; set; }
    public int PercentBps { get; set; }
    public int FlatCents { get; set; }
    public int? MinFeeCents { get; set; }
    public int? MaxFeeCents { get; set; }
    public bool IsActive { get; set; } = true;
}
