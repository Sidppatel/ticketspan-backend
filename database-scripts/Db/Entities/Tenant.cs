namespace Db.Entities;

public class Tenant : BaseEntity
{
    public required string Slug { get; set; }
    public required string Name { get; set; }
    public string? LegalName { get; set; }
    public string CountryCode { get; set; } = "US";
    public string? StripeConnectedAccountId { get; set; }
    public bool StripeChargesEnabled { get; set; }
    public bool StripePayoutsEnabled { get; set; }
    public bool StripeDetailsSubmitted { get; set; }
    public DateTime? StripeOnboardedAt { get; set; }
    public string? StripeRequirementsDue { get; set; }

    public string? Phone { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Zip { get; set; }

    public Guid? LogoImagesId { get; set; }
    public string? BrandPrimary { get; set; }
    public string? BrandSecondary { get; set; }
    public string? BrandAccent { get; set; }
    public string? BrandBackground { get; set; }
    public string? BrandText { get; set; }
    public string? BrandButton { get; set; }
    public string? BrandHighlight { get; set; }

    public DateTime? ArchivedAt { get; set; }

    /// <summary>
    /// Subscription tier: free | starter | professional | business | enterprise.
    /// Professional and above include Advanced Reporting.
    /// </summary>
    public string Tier { get; set; } = "free";

    /// <summary>
    /// Developer-only override that grants Advanced Reporting regardless of tier
    /// (beta tests, grace periods, partnerships). Settable only by role 99.
    /// </summary>
    public bool AdvancedReportingEnabled { get; set; }

    /// <summary>
    /// Tenant-level default fee formula auto-applied to every price unless a price
    /// overrides it. Settable only by developers (role 99).
    /// </summary>
    public Guid? DefaultFeeFormulasId { get; set; }
    public FeeFormula? DefaultFeeFormula { get; set; }

    /// <summary>
    /// Tenant-level gateway (payment-processing) fee formula. Applied to the full
    /// charged amount (selling + platform fee + tax) to model the processor cost as
    /// a separate buyer-facing line. Null = no gateway fee. Reuses the FeeFormula
    /// shape (percent_bps + flat_cents). Settable only by developers (role 99).
    /// </summary>
    public Guid? GatewayFeeFormulasId { get; set; }
    public FeeFormula? GatewayFeeFormula { get; set; }
}
