using Db.Enums;

namespace Db.Entities;

/// <summary>
/// A priority-ordered override on a <see cref="Price"/> driving presale,
/// last-minute, time-window and dynamic pricing. At checkout app.resolve_price()
/// picks the highest-priority active rule whose time window and inventory
/// conditions match; its <see cref="PriceCents"/> replaces the base price.
/// </summary>
public class PriceRule : BaseEntity
{
    public Guid TenantsId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public Guid PricesId { get; set; }
    public Price Price { get; set; } = null!;

    public required string Name { get; set; }
    public PriceRuleType RuleType { get; set; } = PriceRuleType.TimeWindow;

    /// <summary>Higher wins; first matching rule by descending priority applies.</summary>
    public int Priority { get; set; }

    public int PriceCents { get; set; }

    public DateTime? ActiveFrom { get; set; }
    public DateTime? ActiveUntil { get; set; }

    /// <summary>Inventory gate: rule active only when remaining >= this.</summary>
    public int? MinRemaining { get; set; }

    /// <summary>Inventory gate: rule active only when remaining &lt;= this.</summary>
    public int? MaxRemaining { get; set; }

    public bool IsActive { get; set; } = true;
}
