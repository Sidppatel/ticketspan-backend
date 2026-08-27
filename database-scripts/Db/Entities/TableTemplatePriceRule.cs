using Db.Enums;

namespace Db.Entities;

public class TableTemplatePriceRule : BaseEntity
{
    public Guid TenantsId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public Guid TableTemplatesId { get; set; }
    public TableTemplate TableTemplate { get; set; } = null!;

    public required string Name { get; set; }
    public PriceRuleType RuleType { get; set; } = PriceRuleType.TimeWindow;

    public int Priority { get; set; }

    public int PriceCents { get; set; }

    public DateTime? ActiveFrom { get; set; }
    public DateTime? ActiveUntil { get; set; }

    public int? MinRemaining { get; set; }
    public int? MaxRemaining { get; set; }

    public bool IsActive { get; set; } = true;
}
