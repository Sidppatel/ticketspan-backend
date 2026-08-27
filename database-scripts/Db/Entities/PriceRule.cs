using Db.Enums;

namespace Db.Entities;

public class PriceRule : BaseEntity
{
    public Guid TenantsId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public PriceRuleScope Scope { get; set; } = PriceRuleScope.Price;

    public Guid? PricesId { get; set; }
    public Price? Price { get; set; }

    public Guid? EventsId { get; set; }
    public Event? Event { get; set; }

    public required string Name { get; set; }
    public PriceRuleType RuleType { get; set; } = PriceRuleType.TimeWindow;

    public int Priority { get; set; }

    public int PriceCents { get; set; }

    public DateTime? ActiveFrom { get; set; }
    public DateTime? ActiveUntil { get; set; }

    public int? MinRemaining { get; set; }

    public int? MaxRemaining { get; set; }

    public int? Capacity { get; set; }

    public int? MinQty { get; set; }

    public int? MaxQty { get; set; }

    public GroupDiscountKind? DiscountKind { get; set; }

    public int? DiscountBps { get; set; }

    public bool IsActive { get; set; } = true;
}
