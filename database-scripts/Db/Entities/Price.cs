using Db.Enums;

namespace Db.Entities;

public class Price : BaseEntity
{
    public Guid TenantsId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public Guid EventsId { get; set; }
    public Event Event { get; set; } = null!;

    public required string Name { get; set; }
    public PricingType PricingType { get; set; } = PricingType.TicketTier;

    public int BasePriceCents { get; set; }

    public int PerAttendeeCents { get; set; }

    public bool IsAllInclusive { get; set; }

    public Guid? FeeFormulasId { get; set; }
    public FeeFormula? FeeFormula { get; set; }

    public int? MaxQuantity { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<PriceRule> PriceRules { get; set; } = [];
}
