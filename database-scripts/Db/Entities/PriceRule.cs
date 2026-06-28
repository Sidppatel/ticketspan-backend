using Db.Enums;

namespace Db.Entities;

/// <summary>
/// A priority-ordered override driving presale, last-minute, time-window and
/// dynamic pricing. Two scopes: <see cref="PriceRuleScope.Price"/> targets a single
/// <see cref="Price"/> (a tier or table type); <see cref="PriceRuleScope.Event"/>
/// applies to every price of an event. At checkout app.resolve_price() prefers a
/// matching per-price rule, falling back to an event-wide rule, then the base price.
/// </summary>
public class PriceRule : BaseEntity
{
    public Guid TenantsId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    /// <summary>Whether this rule targets one price or the whole event.</summary>
    public PriceRuleScope Scope { get; set; } = PriceRuleScope.Price;

    /// <summary>Set when <see cref="Scope"/> is Price; null for event-wide rules.</summary>
    public Guid? PricesId { get; set; }
    public Price? Price { get; set; }

    /// <summary>Set when <see cref="Scope"/> is Event; null for per-price rules.</summary>
    public Guid? EventsId { get; set; }
    public Event? Event { get; set; }

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

    /// <summary>The discount applies only to this many people/seats. Null = no seat cap.</summary>
    public int? Capacity { get; set; }

    public bool IsActive { get; set; } = true;
}
