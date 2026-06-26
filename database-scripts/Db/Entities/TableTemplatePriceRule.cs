using Db.Enums;

namespace Db.Entities;

/// <summary>
/// A catalog-level price rule attached to a <see cref="TableTemplate"/>. When an
/// event table type is created from the template, these are snapshotted into
/// per-event <see cref="PriceRule"/> rows on the table's <see cref="Price"/>,
/// giving catalog table types reusable presale / last-minute / time-window pricing
/// that admins may then override at the event level.
/// </summary>
public class TableTemplatePriceRule : BaseEntity
{
    public Guid TenantsId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public Guid TableTemplatesId { get; set; }
    public TableTemplate TableTemplate { get; set; } = null!;

    public required string Name { get; set; }
    public PriceRuleType RuleType { get; set; } = PriceRuleType.TimeWindow;

    /// <summary>Higher wins; mirrors <see cref="PriceRule.Priority"/>.</summary>
    public int Priority { get; set; }

    public int PriceCents { get; set; }

    public DateTime? ActiveFrom { get; set; }
    public DateTime? ActiveUntil { get; set; }

    public int? MinRemaining { get; set; }
    public int? MaxRemaining { get; set; }

    public bool IsActive { get; set; } = true;
}
