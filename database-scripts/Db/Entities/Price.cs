using Db.Enums;

namespace Db.Entities;

/// <summary>
/// A priceable defined by the Pricing Module — the single source of truth for
/// pricing on a sellable (ticket tier, table, or add-on). The effective fee is
/// resolved server-side from <see cref="FeeFormulasId"/> when set, otherwise the
/// owning tenant's default fee formula. Time/inventory variation is expressed via
/// <see cref="PriceRule"/> children evaluated by app.resolve_price().
/// </summary>
public class Price : BaseEntity
{
    public Guid TenantsId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public Guid EventsId { get; set; }
    public Event Event { get; set; } = null!;

    public required string Name { get; set; }
    public PricingType PricingType { get; set; } = PricingType.TicketTier;

    public int BasePriceCents { get; set; }

    /// <summary>Per-attendee surcharge for tables (ignored when all-inclusive).</summary>
    public int PerAttendeeCents { get; set; }

    /// <summary>Table mode: one flat price covers all seats (no per-attendee charge).</summary>
    public bool IsAllInclusive { get; set; }

    /// <summary>Developer-only override of the fee formula; null = tenant default.</summary>
    public Guid? FeeFormulasId { get; set; }
    public FeeFormula? FeeFormula { get; set; }

    /// <summary>For AddOn prices: the base ticket/table price this layers on.</summary>
    public Guid? ParentPricesId { get; set; }
    public Price? ParentPrice { get; set; }

    public int? MaxQuantity { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<PriceRule> PriceRules { get; set; } = [];
}
