using Db.Entities;
using Microsoft.EntityFrameworkCore;

namespace Db;

public class EventPlatformDbContext(
    DbContextOptions<EventPlatformDbContext> options
) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantStripeProfile> TenantStripeProfiles => Set<TenantStripeProfile>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<EnumDefinition> EnumDefinitions => Set<EnumDefinition>();
    public DbSet<MagicLinkToken> MagicLinkTokens => Set<MagicLinkToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<DeviceSession> DeviceSessions => Set<DeviceSession>();

    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<TableTemplate> TableTemplates => Set<TableTemplate>();
    public DbSet<TableTemplatePriceRule> TableTemplatePriceRules => Set<TableTemplatePriceRule>();

    public DbSet<Event> Events => Set<Event>();
    public DbSet<ScheduleItem> ScheduleItems => Set<ScheduleItem>();
    public DbSet<Performer> Performers => Set<Performer>();
    public DbSet<EventPerformer> EventPerformers => Set<EventPerformer>();
    public DbSet<Sponsor> Sponsors => Set<Sponsor>();
    public DbSet<EventSponsor> EventSponsors => Set<EventSponsor>();
    public DbSet<StaffEventAccess> StaffEventAccesses => Set<StaffEventAccess>();
    public DbSet<CheckInLog> CheckInLogs => Set<CheckInLog>();
    public DbSet<EventTable> EventTables => Set<EventTable>();
    public DbSet<EventTicketType> EventTicketTypes => Set<EventTicketType>();
    public DbSet<FeeFormula> FeeFormulas => Set<FeeFormula>();
    public DbSet<Price> Prices => Set<Price>();
    public DbSet<PriceRule> PriceRules => Set<PriceRule>();
    public DbSet<LayoutObject> LayoutObjects => Set<LayoutObject>();
    public DbSet<FloorPlanTemplate> FloorPlanTemplates => Set<FloorPlanTemplate>();
    public DbSet<FloorPlanTemplateTable> FloorPlanTemplateTables => Set<FloorPlanTemplateTable>();
    public DbSet<FloorPlanTemplateObject> FloorPlanTemplateObjects => Set<FloorPlanTemplateObject>();
    public DbSet<Table> Tables => Set<Table>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BookingLine> BookingLines => Set<BookingLine>();
    public DbSet<BookingTax> BookingTaxes => Set<BookingTax>();
    public DbSet<TaxRateCache> TaxRateCaches => Set<TaxRateCache>();
    public DbSet<StripeTransaction> StripeTransactions => Set<StripeTransaction>();
    public DbSet<StripeTransfer> StripeTransfers => Set<StripeTransfer>();
    public DbSet<StripePayout> StripePayouts => Set<StripePayout>();

    public DbSet<Image> Images => Set<Image>();
    public DbSet<EventImage> EventImages => Set<EventImage>();
    public DbSet<VenueImage> VenueImages => Set<VenueImage>();

    public DbSet<Feedback> Feedbacks => Set<Feedback>();

    public DbSet<TenantSubscription> TenantSubscriptions => Set<TenantSubscription>();
    public DbSet<EventUpgrade> EventUpgrades => Set<EventUpgrade>();
    public DbSet<TenantAddon> TenantAddons => Set<TenantAddon>();
    public DbSet<BillingCharge> BillingCharges => Set<BillingCharge>();

    public DbSet<EmailLog> EmailLogs => Set<EmailLog>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Address>(entity =>
        {
            entity.ToTable("addresses");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Line1).HasMaxLength(256);
            entity.Property(e => e.Line2).HasMaxLength(256);
            entity.Property(e => e.City).HasMaxLength(128);
            entity.Property(e => e.State).HasMaxLength(64);
            entity.Property(e => e.ZipCode).HasMaxLength(16);
        });

        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.ToTable("tenants");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Slug).IsUnique();
            entity.HasIndex(e => e.StripeConnectedAccountId)
                .IsUnique()
                .HasFilter("stripe_connected_account_id IS NOT NULL");
            entity.HasIndex(e => e.ArchivedAt);
            entity.Property(e => e.Slug).HasMaxLength(128).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(256).IsRequired();
            entity.Property(e => e.LegalName).HasMaxLength(256);
            entity.Property(e => e.CountryCode).HasMaxLength(2).HasDefaultValue("US");
            entity.Property(e => e.StripeConnectedAccountId).HasMaxLength(128);
            entity.Property(e => e.StripeChargesEnabled).HasDefaultValue(false);
            entity.Property(e => e.StripePayoutsEnabled).HasDefaultValue(false);
            entity.Property(e => e.StripeDetailsSubmitted).HasDefaultValue(false);
            entity.Property(e => e.StripeRequirementsDue).HasColumnType("jsonb");
            entity.Property(e => e.BrandTokens).HasColumnType("jsonb");
            entity.Property(e => e.Tier).HasMaxLength(32).HasDefaultValue("free");
            entity.Property(e => e.AdvancedReportingEnabled).HasDefaultValue(false);
            entity.HasOne(e => e.DefaultFeeFormula).WithMany()
                .HasForeignKey(e => e.DefaultFeeFormulasId)
                .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.GatewayFeeFormula).WithMany()
                .HasForeignKey(e => e.GatewayFeeFormulasId)
                .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
            entity.Property(e => e.AchEnabled).HasDefaultValue(false);
            entity.HasOne(e => e.AchFeeFormula).WithMany()
                .HasForeignKey(e => e.AchFeeFormulasId)
                .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<TenantStripeProfile>(entity =>
        {
            entity.ToTable("tenant_stripe_profiles");
            entity.HasKey(e => e.TenantsId);
            entity.Property(e => e.TenantsId).HasColumnName("tenants_id");
            entity.Property(e => e.BusinessType).HasMaxLength(20);
            entity.Property(e => e.BusinessUrl).HasMaxLength(512);
            entity.Property(e => e.ProductDescription).HasMaxLength(2048);
            entity.Property(e => e.Mcc).HasMaxLength(4);
            entity.Property(e => e.SupportEmail).HasMaxLength(256);
            entity.HasOne(e => e.Tenant).WithOne().HasForeignKey<TenantStripeProfile>(e => e.TenantsId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users", t =>
            {
                t.HasCheckConstraint("CK_users_DeveloperHasNoTenant",
                    "(role = 99) = (tenants_id IS NULL)");
                t.HasCheckConstraint("CK_users_Role",
                    "role IN (0, 1, 2, 3, 4, 99)");
            });
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TenantsId, e.Email, e.Role }).IsUnique();
            entity.HasIndex(e => e.EmailHash);
            entity.HasIndex(e => e.GoogleSubject)
                .IsUnique()
                .HasFilter("google_subject IS NOT NULL");
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.EmailHash).HasMaxLength(128);
            entity.Property(e => e.FirstName).HasMaxLength(128);
            entity.Property(e => e.LastName).HasMaxLength(128);
            entity.Property(e => e.PasswordHash).HasMaxLength(256);
            entity.Property(e => e.PepperVersion).HasDefaultValue((short)1);
            entity.Property(e => e.GoogleSubject).HasMaxLength(64);
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.EmailVerified).HasDefaultValue(false);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.FailedLoginAttempts).HasDefaultValue(0);
            entity.Property(e => e.OptInLocationEmail).HasDefaultValue(false);
            entity.Property(e => e.HasCompletedOnboarding).HasDefaultValue(false);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantsId)
                .IsRequired(false).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Image).WithMany().HasForeignKey(e => e.ImagesId)
                .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.Address).WithMany().HasForeignKey(e => e.AddressesId)
                .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<StripeTransfer>(entity =>
        {
            entity.ToTable("stripe_transfers");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.StripeTransferId).IsUnique();
            entity.HasIndex(e => e.TenantsId);
            entity.HasIndex(e => e.BookingsId);
            entity.Property(e => e.StripeTransferId).HasMaxLength(128).IsRequired();
            entity.Property(e => e.Currency).HasMaxLength(8).HasDefaultValue("usd");
            entity.Property(e => e.RawEvent).HasColumnType("jsonb");
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantsId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Booking).WithMany().HasForeignKey(e => e.BookingsId)
                .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<StripePayout>(entity =>
        {
            entity.ToTable("stripe_payouts");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.StripePayoutId).IsUnique();
            entity.HasIndex(e => e.TenantsId);
            entity.Property(e => e.StripePayoutId).HasMaxLength(128).IsRequired();
            entity.Property(e => e.Currency).HasMaxLength(8).HasDefaultValue("usd");
            entity.Property(e => e.Status).HasMaxLength(32).IsRequired();
            entity.Property(e => e.RawEvent).HasColumnType("jsonb");
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantsId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Invitation>(entity =>
        {
            entity.ToTable("invitations", t =>
            {
                t.HasCheckConstraint("CK_invitations_Status",
                    "status IN ('Pending','Accepted','Revoked','Expired')");
                t.HasCheckConstraint("CK_invitations_Role",
                    "role IN (1, 2, 3, 99)");
            });
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TokenHash).IsUnique();
            entity.HasIndex(e => e.Email);
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.TokenHash).HasMaxLength(128);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantsId)
                .IsRequired(false).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.InvitedBy)
                .WithMany()
                .HasForeignKey(e => e.InvitedByUsersId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Event)
                .WithMany()
                .HasForeignKey(e => e.EventId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AppSetting>(entity =>
        {
            entity.ToTable("app_settings");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Key).IsUnique();
            entity.Property(e => e.Key).HasMaxLength(128);
            entity.Property(e => e.Value).HasMaxLength(4096);
            entity.Property(e => e.Description).HasMaxLength(512);
        });

        modelBuilder.Entity<EnumDefinition>(entity =>
        {
            entity.ToTable("enum_definitions");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.EnumType, e.EnumValue }).IsUnique();
            entity.Property(e => e.EnumType).HasMaxLength(128);
            entity.Property(e => e.EnumValue).HasMaxLength(128);
            entity.Property(e => e.UsedIn).HasMaxLength(256);
            entity.Property(e => e.Description).HasMaxLength(512);
        });

        modelBuilder.Entity<MagicLinkToken>(entity =>
        {
            entity.ToTable("magic_link_tokens", t =>
            {
                t.HasCheckConstraint("CK_magic_link_tokens_Usage",
                    "(is_used = false AND used_at IS NULL) OR (is_used = true AND used_at IS NOT NULL)");
            });
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TokenHash).IsUnique();
            entity.HasIndex(e => e.Email);
            entity.HasIndex(e => e.ExpiresAt);
            entity.Property(e => e.TokenHash).HasMaxLength(128);
            entity.Property(e => e.Email).HasMaxLength(256);
        });

        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.ToTable("password_reset_tokens", t =>
            {
                t.HasCheckConstraint("CK_password_reset_tokens_Usage",
                    "(is_used = false AND used_at IS NULL) OR (is_used = true AND used_at IS NOT NULL)");
            });
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TokenHash).IsUnique();
            entity.HasIndex(e => e.UsersId);
            entity.HasIndex(e => e.ExpiresAt);
            entity.Property(e => e.TokenHash).HasMaxLength(128);
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UsersId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DeviceSession>(entity =>
        {
            entity.ToTable("device_sessions");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SessionHash).IsUnique();
            entity.HasIndex(e => new { e.ExpiresAt, e.RevokedAt })
                .HasFilter("revoked_at IS NULL");
            entity.Property(e => e.SessionHash).HasMaxLength(128);
            entity.Property(e => e.DeviceFingerprint).HasMaxLength(256);
            entity.Property(e => e.DeviceName).HasMaxLength(256);
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UsersId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Venue>(entity =>
        {
            entity.ToTable("venues");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantsId);
            entity.HasIndex(e => e.Name);
            entity.Property(e => e.Name).HasMaxLength(256);
            entity.Property(e => e.Description).HasMaxLength(4096);
            entity.Property(e => e.ImagePath).HasMaxLength(512);
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.Website).HasMaxLength(512);

            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantsId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Address).WithMany().HasForeignKey(e => e.AddressesId)
                .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<TableTemplate>(entity =>
        {
            entity.ToTable("table_templates", t =>
            {
                t.HasCheckConstraint("CK_table_templates_DefaultShape",
                    "default_shape IN ('Round','Rectangle','Square','Cocktail')");
                t.HasCheckConstraint("CK_table_templates_DefaultCapacity",
                    "default_capacity > 0");
                t.HasCheckConstraint("CK_table_templates_DefaultPriceCents",
                    "default_price_cents >= 0");
            });
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantsId);
            entity.HasIndex(e => new { e.TenantsId, e.Name }).IsUnique();
            entity.HasIndex(e => new { e.TenantsId, e.DefaultColor })
                .IsUnique()
                .HasFilter("default_color IS NOT NULL");
            entity.Property(e => e.Name).HasMaxLength(128);
            entity.Property(e => e.DefaultShape).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.DefaultColor).HasMaxLength(20);
            entity.Property(e => e.DefaultWidth).HasColumnType("numeric(10,2)").HasDefaultValue(80);
            entity.Property(e => e.DefaultHeight).HasColumnType("numeric(10,2)").HasDefaultValue(80);
            entity.Property(e => e.DefaultIsAllInclusive).HasDefaultValue(true);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantsId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TableTemplatePriceRule>(entity =>
        {
            entity.ToTable("table_template_price_rules", t =>
            {
                t.HasCheckConstraint("CK_table_template_price_rules_RuleType",
                    "rule_type IN ('Presale','LastMinute','TimeWindow','Dynamic')");
                t.HasCheckConstraint("CK_table_template_price_rules_PriceCents", "price_cents >= 0");
                t.HasCheckConstraint("CK_table_template_price_rules_Window",
                    "active_from IS NULL OR active_until IS NULL OR active_until > active_from");
            });
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantsId);
            entity.HasIndex(e => new { e.TableTemplatesId, e.Priority });
            entity.Property(e => e.Name).HasMaxLength(128).IsRequired();
            entity.Property(e => e.RuleType).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.Priority).HasDefaultValue(0);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantsId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.TableTemplate).WithMany(tt => tt.PriceRules)
                .HasForeignKey(e => e.TableTemplatesId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EventTable>(entity =>
        {
            entity.ToTable("event_tables", t =>
            {
                t.HasCheckConstraint("CK_event_tables_Shape",
                    "shape IN ('Round','Rectangle','Square','Cocktail')");
                t.HasCheckConstraint("CK_event_tables_Capacity", "capacity > 0");
                t.HasCheckConstraint("CK_event_tables_PriceCents", "price_cents >= 0");
            });
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantsId);
            entity.HasIndex(e => new { e.EventsId, e.Label });
            entity.Property(e => e.Label).HasMaxLength(128);
            entity.Property(e => e.Shape).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.Color).HasMaxLength(20);
            entity.Property(e => e.DefaultWidth).HasColumnType("numeric(10,2)");
            entity.Property(e => e.DefaultHeight).HasColumnType("numeric(10,2)");
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantsId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Event).WithMany().HasForeignKey(e => e.EventsId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.TableTemplate).WithMany(tt => tt.EventTables)
                .HasForeignKey(e => e.TableTemplatesId)
                .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.FeeFormula).WithMany().HasForeignKey(e => e.FeeFormulasId)
                .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.Price).WithMany().HasForeignKey(e => e.PricesId)
                .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<TenantSubscription>(entity =>
        {
            entity.ToTable("tenant_subscriptions", t =>
            {
                t.HasCheckConstraint("CK_tenant_subscriptions_Tier",
                    "tier IN ('starter','professional','business','enterprise')");
                t.HasCheckConstraint("CK_tenant_subscriptions_Status",
                    "status IN ('trial','active','past_due','canceled','expired')");
            });
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantsId);
            
            entity.HasIndex(e => e.TenantsId).IsUnique()
                .HasDatabaseName("IX_tenant_subscriptions_live")
                .HasFilter("status IN ('trial','active','past_due')");
            entity.Property(e => e.Tier).HasMaxLength(32);
            entity.Property(e => e.Status).HasMaxLength(16).HasDefaultValue("active");
            entity.Property(e => e.PendingTier).HasMaxLength(32);
            entity.Property(e => e.StripeSubscriptionId).HasMaxLength(128);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantsId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EventUpgrade>(entity =>
        {
            entity.ToTable("event_upgrades", t =>
            {
                t.HasCheckConstraint("CK_event_upgrades_Tier",
                    "tier IN ('starter_event','pro_event','business_event','enterprise_event')");
                t.HasCheckConstraint("CK_event_upgrades_Status",
                    "status IN ('active','canceled','refunded')");
            });
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantsId);
            entity.HasIndex(e => e.EventsId).IsUnique()
                .HasDatabaseName("IX_event_upgrades_live")
                .HasFilter("status = 'active'");
            entity.Property(e => e.Tier).HasMaxLength(32);
            entity.Property(e => e.Status).HasMaxLength(16).HasDefaultValue("active");
            entity.Property(e => e.StripePaymentIntentId).HasMaxLength(128);
            entity.HasOne(e => e.Event).WithMany().HasForeignKey(e => e.EventsId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantsId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TenantAddon>(entity =>
        {
            entity.ToTable("tenant_addons", t =>
            {
                t.HasCheckConstraint("CK_tenant_addons_Type",
                    "type IN ('custom_domain','advanced_analytics','sms','extra_manager')");
                t.HasCheckConstraint("CK_tenant_addons_BillingPeriod",
                    "billing_period IN ('monthly','annual')");
                t.HasCheckConstraint("CK_tenant_addons_Status", "status IN ('active','canceled')");
                t.HasCheckConstraint("CK_tenant_addons_Quantity", "quantity > 0");
            });
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantsId);
            entity.Property(e => e.Type).HasMaxLength(32);
            entity.Property(e => e.BillingPeriod).HasMaxLength(16).HasDefaultValue("monthly");
            entity.Property(e => e.Status).HasMaxLength(16).HasDefaultValue("active");
            entity.Property(e => e.Quantity).HasDefaultValue(1);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantsId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BillingCharge>(entity =>
        {
            entity.ToTable("billing_charges", t =>
            {
                t.HasCheckConstraint("CK_billing_charges_Kind",
                    "kind IN ('subscription','proration','pay_per_event','addon','setup_fee','refund')");
            });
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantsId);
            entity.HasIndex(e => e.CreatedAt);
            entity.Property(e => e.Kind).HasMaxLength(32);
            entity.Property(e => e.Reference).HasMaxLength(64);
            entity.Property(e => e.Description).HasMaxLength(512);
            entity.Property(e => e.StripePaymentIntentId).HasMaxLength(128);
            
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantsId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FeeFormula>(entity =>
        {
            entity.ToTable("fee_formulas");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(128).IsRequired();
            entity.Property(e => e.PercentBps).HasDefaultValue(0);
            entity.Property(e => e.FlatCents).HasDefaultValue(0);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<EventTicketType>(entity =>
        {
            entity.ToTable("event_ticket_types", t =>
            {
                t.HasCheckConstraint("CK_event_ticket_types_PriceCents", "price_cents >= 0");
                t.HasCheckConstraint("CK_event_ticket_types_MaxQuantity",
                    "max_quantity IS NULL OR max_quantity > 0");
                t.HasCheckConstraint("CK_event_ticket_types_Capacity",
                    "capacity IS NULL OR capacity > 0");
                t.HasCheckConstraint("CK_event_ticket_types_SortOrder", "sort_order >= 0");
            });
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantsId);
            entity.HasIndex(e => new { e.EventsId, e.Label });
            entity.HasIndex(e => new { e.EventsId, e.SortOrder });
            entity.Property(e => e.Label).HasMaxLength(128);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantsId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Event).WithMany().HasForeignKey(e => e.EventsId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.FeeFormula).WithMany().HasForeignKey(e => e.FeeFormulasId)
                .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.Price).WithMany().HasForeignKey(e => e.PricesId)
                .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Price>(entity =>
        {
            entity.ToTable("prices", t =>
            {
                t.HasCheckConstraint("CK_prices_PricingType",
                    "pricing_type IN ('TicketTier','Table')");
                t.HasCheckConstraint("CK_prices_BasePriceCents", "base_price_cents >= 0");
                t.HasCheckConstraint("CK_prices_PerAttendeeCents", "per_attendee_cents >= 0");
                t.HasCheckConstraint("CK_prices_MaxQuantity",
                    "max_quantity IS NULL OR max_quantity > 0");
            });
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantsId);
            entity.HasIndex(e => e.EventsId);
            entity.Property(e => e.Name).HasMaxLength(128).IsRequired();
            entity.Property(e => e.PricingType).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.PerAttendeeCents).HasDefaultValue(0);
            entity.Property(e => e.IsAllInclusive).HasDefaultValue(false);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantsId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Event).WithMany().HasForeignKey(e => e.EventsId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.FeeFormula).WithMany().HasForeignKey(e => e.FeeFormulasId)
                .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<PriceRule>(entity =>
        {
            entity.ToTable("price_rules", t =>
            {
                t.HasCheckConstraint("CK_price_rules_RuleType",
                    "rule_type IN ('Presale','LastMinute','TimeWindow','Dynamic')");
                t.HasCheckConstraint("CK_price_rules_PriceCents", "price_cents >= 0");
                t.HasCheckConstraint("CK_price_rules_Capacity",
                    "capacity IS NULL OR capacity > 0");
                t.HasCheckConstraint("CK_price_rules_Window",
                    "active_from IS NULL OR active_until IS NULL OR active_until > active_from");

                t.HasCheckConstraint("CK_price_rules_Scope",
                    "(scope = 'Price' AND prices_id IS NOT NULL AND events_id IS NULL) "
                    + "OR (scope = 'Event' AND events_id IS NOT NULL AND prices_id IS NULL)");
            });
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantsId);
            entity.HasIndex(e => new { e.PricesId, e.Priority });
            entity.HasIndex(e => new { e.EventsId, e.Scope, e.Priority });
            entity.Property(e => e.Name).HasMaxLength(128).IsRequired();
            entity.Property(e => e.Scope).HasConversion<string>().HasMaxLength(20)
                .HasDefaultValue(Db.Enums.PriceRuleScope.Price);
            entity.Property(e => e.RuleType).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.Priority).HasDefaultValue(0);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantsId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Price).WithMany(p => p.PriceRules).HasForeignKey(e => e.PricesId)
                .IsRequired(false).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Event).WithMany().HasForeignKey(e => e.EventsId)
                .IsRequired(false).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LayoutObject>(entity =>
        {
            entity.ToTable("layout_objects", t =>
            {
                t.HasCheckConstraint("CK_layout_objects_ObjectType",
                    "object_type IN ('Entry','Exit','Stage')");
                t.HasCheckConstraint("CK_layout_objects_PosX", "pos_x >= 0");
                t.HasCheckConstraint("CK_layout_objects_PosY", "pos_y >= 0");
            });
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantsId);
            entity.HasIndex(e => e.EventsId);
            entity.Property(e => e.ObjectType).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.Label).HasMaxLength(64);
            entity.Property(e => e.Color).HasMaxLength(20);
            entity.Property(e => e.PosX).HasColumnType("numeric(10,2)");
            entity.Property(e => e.PosY).HasColumnType("numeric(10,2)");
            entity.Property(e => e.Width).HasColumnType("numeric(10,2)").HasDefaultValue(80);
            entity.Property(e => e.Height).HasColumnType("numeric(10,2)").HasDefaultValue(80);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantsId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Event).WithMany().HasForeignKey(e => e.EventsId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FloorPlanTemplate>(entity =>
        {
            entity.ToTable("floor_plan_templates");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantsId);
            entity.Property(e => e.Name).HasMaxLength(128).IsRequired();
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantsId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FloorPlanTemplateTable>(entity =>
        {
            entity.ToTable("floor_plan_template_tables", t =>
            {
                t.HasCheckConstraint("CK_fpt_tables_Shape",
                    "shape IN ('Round','Rectangle','Square','Cocktail')");
            });
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantsId);
            entity.HasIndex(e => e.FloorPlanTemplatesId);
            entity.Property(e => e.Label).HasMaxLength(20);
            entity.Property(e => e.TypeLabel).HasMaxLength(128);
            entity.Property(e => e.Shape).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.Color).HasMaxLength(20);
            entity.Property(e => e.PosX).HasColumnType("numeric(10,2)");
            entity.Property(e => e.PosY).HasColumnType("numeric(10,2)");
            entity.Property(e => e.Width).HasColumnType("numeric(10,2)").HasDefaultValue(80);
            entity.Property(e => e.Height).HasColumnType("numeric(10,2)").HasDefaultValue(80);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantsId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.FloorPlanTemplate).WithMany(f => f.Tables)
                .HasForeignKey(e => e.FloorPlanTemplatesId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FloorPlanTemplateObject>(entity =>
        {
            entity.ToTable("floor_plan_template_objects", t =>
            {
                t.HasCheckConstraint("CK_fpt_objects_ObjectType",
                    "object_type IN ('Entry','Exit','Stage')");
            });
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantsId);
            entity.HasIndex(e => e.FloorPlanTemplatesId);
            entity.Property(e => e.ObjectType).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.Label).HasMaxLength(64);
            entity.Property(e => e.Color).HasMaxLength(20);
            entity.Property(e => e.PosX).HasColumnType("numeric(10,2)");
            entity.Property(e => e.PosY).HasColumnType("numeric(10,2)");
            entity.Property(e => e.Width).HasColumnType("numeric(10,2)").HasDefaultValue(80);
            entity.Property(e => e.Height).HasColumnType("numeric(10,2)").HasDefaultValue(80);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantsId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.FloorPlanTemplate).WithMany(f => f.Objects)
                .HasForeignKey(e => e.FloorPlanTemplatesId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Event>(entity =>
        {
            entity.ToTable("events", t =>
            {
                t.HasCheckConstraint("CK_events_Status",
                    "status IN ('Draft','Published','Completed','Cancelled')");
                t.HasCheckConstraint("CK_events_Category",
                    "category IS NULL OR category IN ('Music','Business','Social','Dining','Tech','Arts','Family','Sports')");
                t.HasCheckConstraint("CK_events_LayoutMode", "layout_mode IN ('Grid','Open')");
                t.HasCheckConstraint("CK_events_EventType", "event_type IN ('Open','Table','Both')");
                t.HasCheckConstraint("CK_events_DateRange", "end_date > start_date");
                t.HasCheckConstraint("CK_events_PublishLifecycle",
                    "status <> 'Published' OR published_at IS NOT NULL");
                t.HasCheckConstraint("CK_events_DraftNoPublishDate",
                    "status <> 'Draft' OR published_at IS NULL");
                t.HasCheckConstraint("CK_events_CompletedRequiresPublish",
                    "status <> 'Completed' OR published_at IS NOT NULL");
            });
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantsId);
            entity.HasIndex(e => e.Slug).IsUnique();
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => e.StartDate);
            entity.HasIndex(e => new { e.Status, e.StartDate });
            entity.Property(e => e.Title).HasMaxLength(256);
            entity.Property(e => e.Slug).HasMaxLength(300);
            entity.Property(e => e.Description).HasMaxLength(8192);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.Category).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.ImagePath).HasMaxLength(512);
            entity.Property(e => e.LayoutMode).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.EventType).HasConversion<string>().HasMaxLength(20)
                .HasDefaultValue(Db.Enums.EventType.Open);
            entity.Property(e => e.FeesIncluded).HasDefaultValue(false);
            entity.Property(e => e.AchEnabled).HasDefaultValue(false);
            entity.Property(e => e.Meta).HasColumnType("jsonb").HasDefaultValueSql("'[]'::jsonb");
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantsId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Venue).WithMany(v => v.Events).HasForeignKey(e => e.VenuesId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedByUsersId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.FeeFormula).WithMany().HasForeignKey(e => e.FeeFormulasId)
                .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
#pragma warning disable CS8603
            entity.HasGeneratedTsVectorColumn(e => e.SearchVector, "english", e => new { e.Title, Description = e.Description! })
                  .HasIndex(e => e.SearchVector).HasMethod("GIN");
#pragma warning restore CS8603
        });

        modelBuilder.Entity<ScheduleItem>(entity =>
        {
            entity.ToTable("schedule_items", t =>
            {
                t.HasCheckConstraint("CK_schedule_items_TypeCategory",
                    "type_category IN ('Performance','Break','Intermission','DJ Set','Networking','Other')");
                t.HasCheckConstraint("CK_schedule_items_TimeRange", "end_time > start_time");
            });
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantsId);
            entity.HasIndex(e => new { e.EventsId, e.StartTime });
            entity.Property(e => e.Title).HasMaxLength(256).IsRequired();
            entity.Property(e => e.TypeCategory).HasMaxLength(32).IsRequired();
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantsId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Event).WithMany().HasForeignKey(e => e.EventsId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StaffEventAccess>(entity =>
        {
            entity.ToTable("staff_event_access");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.StaffUserId, e.EventId }).IsUnique();
            entity.HasIndex(e => e.EventId);
            entity.HasOne(e => e.StaffUser).WithMany().HasForeignKey(e => e.StaffUserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Event).WithMany().HasForeignKey(e => e.EventId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.AssignedByAdmin).WithMany()
                .HasForeignKey(e => e.AssignedByAdminId)
                .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<CheckInLog>(entity =>
        {
            entity.ToTable("checkin_logs");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.EventId);
            entity.HasIndex(e => e.StaffUserId);
            entity.HasIndex(e => e.BookingId);
            entity.HasIndex(e => e.TicketId);
            entity.HasOne(e => e.Event).WithMany().HasForeignKey(e => e.EventId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.StaffUser).WithMany().HasForeignKey(e => e.StaffUserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Booking).WithMany().HasForeignKey(e => e.BookingId)
                .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.Ticket).WithMany().HasForeignKey(e => e.TicketId)
                .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
            entity.Property(e => e.Method).HasMaxLength(20).HasDefaultValue("qr_scan");
            entity.Property(e => e.Status).HasMaxLength(10).HasDefaultValue("success");
            entity.Property(e => e.FailureReason).HasMaxLength(60);
        });

        modelBuilder.Entity<Table>(entity =>
        {
            entity.ToTable("tables", t =>
            {
                t.HasCheckConstraint("CK_tables_Status",
                    "status IN ('Available','Locked','Booked')");
                t.HasCheckConstraint("CK_tables_LockedRequiresOwner",
                    "status <> 'Locked' OR (locked_by_users_id IS NOT NULL AND lock_expires_at IS NOT NULL)");
                t.HasCheckConstraint("CK_tables_AvailableNoLock",
                    "status <> 'Available' OR (locked_by_users_id IS NULL AND lock_expires_at IS NULL)");
                t.HasCheckConstraint("CK_tables_PosX", "pos_x >= 0");
                t.HasCheckConstraint("CK_tables_PosY", "pos_y >= 0");
            });
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantsId);
            entity.HasIndex(e => e.EventsId);
            entity.HasIndex(e => new { e.EventsId, e.Label }).IsUnique();
            
            entity.HasIndex(e => new { e.EventsId, e.Status });
            entity.Property(e => e.Label).HasMaxLength(20);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20)
                .HasDefaultValue(Db.Enums.TableStatus.Available);
            entity.Property(e => e.ShapeOverride).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.ColorOverride).HasMaxLength(20);
            entity.Property(e => e.PosX).HasColumnType("numeric(10,2)");
            entity.Property(e => e.PosY).HasColumnType("numeric(10,2)");
            entity.Property(e => e.Width).HasColumnType("numeric(10,2)").HasDefaultValue(80);
            entity.Property(e => e.Height).HasColumnType("numeric(10,2)").HasDefaultValue(80);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantsId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.EventTable).WithMany(et => et.Tables)
                .HasForeignKey(e => e.EventTablesId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Event).WithMany().HasForeignKey(e => e.EventsId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.LockedByUser).WithMany().HasForeignKey(e => e.LockedByUsersId)
                .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Booking>(entity =>
        {
            entity.ToTable("bookings", t =>
            {
                t.HasCheckConstraint("CK_bookings_Status",
                    "status IN ('Pending','Paid','CheckedIn','Cancelled','Refunded','Expired')");
                t.HasCheckConstraint("CK_bookings_SubtotalCents", "subtotal_cents >= 0");
                t.HasCheckConstraint("CK_bookings_FeeCents", "fee_cents >= 0");
                t.HasCheckConstraint("CK_bookings_TotalCents", "total_cents >= 0");
                t.HasCheckConstraint("CK_bookings_TotalFormula",
                    "total_cents = subtotal_cents + fee_cents");
                t.HasCheckConstraint("CK_bookings_SeatsReserved",
                    "seats_reserved IS NULL OR seats_reserved > 0");
            });
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantsId);
            
            entity.HasIndex(e => new { e.EventsId, e.UsersId, e.BookingNumber }).IsUnique();
            entity.HasIndex(e => e.QrToken).IsUnique().HasFilter("qr_token IS NOT NULL");
            entity.HasIndex(e => e.Status);
            entity.Property(e => e.SalesChannel).HasMaxLength(32).HasDefaultValue("direct");
            entity.HasIndex(e => e.UsersId);
            entity.HasIndex(e => new { e.UsersId, e.CreatedAt });
            entity.HasIndex(e => new { e.EventsId, e.Status });
            entity.Property(e => e.BookingNumber).HasMaxLength(20);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.QrToken).HasMaxLength(128);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantsId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UsersId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Event).WithMany().HasForeignKey(e => e.EventsId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Booking>(entity =>
        {
            entity.Property(e => e.TaxRate).HasPrecision(6, 5);
        });

        modelBuilder.Entity<BookingTax>(entity =>
        {
            entity.ToTable("booking_taxes", t =>
            {
                t.HasCheckConstraint("CK_booking_taxes_TaxableAmountCents", "taxable_amount_cents >= 0");
                t.HasCheckConstraint("CK_booking_taxes_TaxAmountCents", "tax_amount_cents >= 0");
            });
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantsId);
            entity.HasIndex(e => e.BookingsId).IsUnique();
            entity.HasIndex(e => e.State);
            entity.Property(e => e.ZipCode).HasMaxLength(16);
            entity.Property(e => e.State).HasMaxLength(64);
            entity.Property(e => e.County).HasMaxLength(128);
            entity.Property(e => e.City).HasMaxLength(128);
            entity.Property(e => e.ApiResponseId).HasMaxLength(128);
            entity.Property(e => e.CombinedRate).HasPrecision(6, 5);
            entity.Property(e => e.StateRate).HasPrecision(6, 5);
            entity.Property(e => e.CountyRate).HasPrecision(6, 5);
            entity.Property(e => e.CityRate).HasPrecision(6, 5);
            entity.Property(e => e.LocalRate).HasPrecision(6, 5);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantsId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Booking).WithMany().HasForeignKey(e => e.BookingsId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TaxRateCache>(entity =>
        {
            entity.ToTable("tax_rate_cache");
            entity.HasKey(e => e.ZipCode);
            entity.HasIndex(e => e.State);
            entity.Property(e => e.ZipCode).HasMaxLength(16);
            entity.Property(e => e.State).HasMaxLength(64);
            entity.Property(e => e.County).HasMaxLength(128);
            entity.Property(e => e.City).HasMaxLength(128);
            entity.Property(e => e.ApiResponseId).HasMaxLength(128);
            entity.Property(e => e.StateRate).HasPrecision(6, 5);
            entity.Property(e => e.CountyRate).HasPrecision(6, 5);
            entity.Property(e => e.CityRate).HasPrecision(6, 5);
            entity.Property(e => e.LocalRate).HasPrecision(6, 5);
            entity.Property(e => e.CombinedRate).HasPrecision(6, 5);
        });

        modelBuilder.Entity<Event>(entity =>
        {
            entity.Property(e => e.TaxRateOverride).HasPrecision(6, 5);
        });

        modelBuilder.Entity<BookingLine>(entity =>
        {
            entity.ToTable("booking_lines", t =>
            {
                t.HasCheckConstraint("CK_booking_lines_Kind", "kind IN ('Ticket','Table')");
                t.HasCheckConstraint("CK_booking_lines_Ref",
                    "(kind = 'Ticket' AND event_ticket_types_id IS NOT NULL AND tables_id IS NULL) "
                    + "OR (kind = 'Ticket' AND tables_id IS NOT NULL AND event_ticket_types_id IS NULL) "
                    + "OR (kind = 'Table' AND tables_id IS NOT NULL AND event_ticket_types_id IS NULL)");
                t.HasCheckConstraint("CK_booking_lines_Seats", "seats > 0");
                t.HasCheckConstraint("CK_booking_lines_SubtotalCents", "subtotal_cents >= 0");
                t.HasCheckConstraint("CK_booking_lines_FeeCents", "fee_cents >= 0");
                t.HasCheckConstraint("CK_booking_lines_TotalFormula",
                    "total_cents = subtotal_cents + fee_cents");
                t.HasCheckConstraint("CK_booking_lines_TicketStatus",
                    "status IN ('Unassigned','Invited','Claimed','CheckedIn')");
            });
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantsId);
            entity.HasIndex(e => e.BookingsId);
            entity.HasIndex(e => e.EventTicketTypesId);
            entity.HasIndex(e => e.TablesId);
            entity.HasIndex(e => e.EventsId);
            entity.HasIndex(e => e.GuestUsersId);
            entity.HasIndex(e => e.QrToken).IsUnique().HasFilter("qr_token IS NOT NULL");
            entity.HasIndex(e => e.InviteTokenHash).IsUnique().HasFilter("invite_token_hash IS NOT NULL");
            entity.HasIndex(e => new { e.EventsId, e.TicketCode }).IsUnique().HasFilter("ticket_code IS NOT NULL");
            entity.HasIndex(e => new { e.BookingsId, e.SeatNumber }).IsUnique().HasFilter("seat_number IS NOT NULL");

            entity.Property(e => e.Kind).HasMaxLength(20);
            entity.Property(e => e.AppliedRuleName).HasMaxLength(128);
            entity.Property(e => e.Currency).HasMaxLength(8).HasDefaultValue("usd");
            entity.Property(e => e.TicketCode).HasMaxLength(20);
            entity.Property(e => e.QrToken).HasMaxLength(128);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.InviteTokenHash).HasMaxLength(128);
            entity.Property(e => e.InvitedEmail).HasMaxLength(256);

            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantsId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Booking).WithMany(b => b.Lines)
                .HasForeignKey(e => e.BookingsId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Event).WithMany().HasForeignKey(e => e.EventsId)
                .IsRequired(false).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.EventTicketType).WithMany().HasForeignKey(e => e.EventTicketTypesId)
                .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.Table).WithMany().HasForeignKey(e => e.TablesId)
                .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.Price).WithMany().HasForeignKey(e => e.PricesId)
                .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.GuestUser).WithMany().HasForeignKey(e => e.GuestUsersId)
                .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<StripeTransaction>(entity =>
        {
            entity.ToTable("stripe_transactions", t =>
            {
                t.HasCheckConstraint("CK_stripe_transactions_Status",
                    "status IN ('RequiresConfirmation','Succeeded','Failed','Refunded')");
                t.HasCheckConstraint("CK_stripe_transactions_AmountCents", "amount_cents >= 0");
                t.HasCheckConstraint("CK_stripe_transactions_Currency", "currency IN ('usd')");
                t.HasCheckConstraint("CK_stripe_transactions_RefundLifecycle",
                    "status <> 'Refunded' OR refunded_at IS NOT NULL");
                t.HasCheckConstraint("CK_stripe_transactions_PaidLifecycle",
                    "status NOT IN ('Succeeded','Refunded') OR paid_at IS NOT NULL");
                t.HasCheckConstraint("CK_stripe_transactions_PendingNoPaidDate",
                    "status NOT IN ('RequiresConfirmation','Failed') OR paid_at IS NULL");
                t.HasCheckConstraint("CK_stripe_transactions_NotRefundedNoRefundDate",
                    "status = 'Refunded' OR refunded_at IS NULL");
                t.HasCheckConstraint("CK_stripe_transactions_TransferAmount",
                    "transfer_amount_cents IS NULL OR transfer_amount_cents >= 0");
                t.HasCheckConstraint("CK_stripe_transactions_StripeFees",
                    "stripe_fees_cents IS NULL OR stripe_fees_cents >= 0");
                t.HasCheckConstraint("CK_stripe_transactions_TotalCharged",
                    "total_charged_cents IS NULL OR total_charged_cents >= 0");
            });
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantsId);
            entity.HasIndex(e => e.PaymentIntentId).IsUnique();
            entity.HasIndex(e => new { e.Status, e.PaidAt });
            entity.Property(e => e.PaymentIntentId).HasMaxLength(128);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(30);
            entity.Property(e => e.Currency).HasMaxLength(3);
            entity.Property(e => e.RefundId).HasMaxLength(128);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantsId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Booking).WithOne(b => b.StripeTransaction)
                .HasForeignKey<StripeTransaction>(e => e.BookingsId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Image>(entity =>
        {
            entity.ToTable("images");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantsId);
            entity.HasIndex(e => new { e.EntityType, e.EntityId });
            entity.Property(e => e.EntityType).HasMaxLength(20);
            entity.Property(e => e.StorageKey).HasMaxLength(500);
            entity.Property(e => e.OriginalName).HasMaxLength(255);
            entity.Property(e => e.UploaderType).HasMaxLength(255);
            entity.Property(e => e.AltText).HasMaxLength(512);
            entity.Property(e => e.Caption).HasMaxLength(1024);
            entity.Property(e => e.ContentType).HasMaxLength(64);
            entity.Property(e => e.Checksum).HasMaxLength(128);
            entity.Property(e => e.Tag).HasMaxLength(50).HasDefaultValue("Generic");
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantsId)
                .IsRequired(false).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<VenueImage>(entity =>
        {
            entity.ToTable("venue_images");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantsId);
            entity.HasIndex(e => new { e.VenuesId, e.ImagesId }).IsUnique();
            entity.HasIndex(e => new { e.VenuesId, e.SortOrder });
            entity.HasIndex(e => e.VenuesId)
                .IsUnique()
                .HasFilter("is_primary = true");
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantsId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Venue).WithMany().HasForeignKey(e => e.VenuesId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Image).WithMany().HasForeignKey(e => e.ImagesId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EventImage>(entity =>
        {
            entity.ToTable("event_images", t =>
            {
                t.HasCheckConstraint("CK_event_images_Type",
                    "type IN ('event_image','event_thumbnail')");
            });
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Type).HasMaxLength(32).HasDefaultValue("event_image");
            entity.HasIndex(e => e.TenantsId);
            entity.HasIndex(e => new { e.EventsId, e.ImagesId }).IsUnique();
            entity.HasIndex(e => new { e.EventsId, e.SortOrder });
            entity.HasIndex(e => new { e.EventsId, e.Type })
                .IsUnique()
                .HasFilter("is_primary = true");
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantsId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Event).WithMany().HasForeignKey(e => e.EventsId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Image).WithMany().HasForeignKey(e => e.ImagesId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EmailLog>(entity =>
        {
            entity.ToTable("email_logs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Timestamp).HasDefaultValueSql("now()");
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.TenantsId);
            entity.Property(e => e.Recipient).HasMaxLength(256);
            entity.Property(e => e.Subject).HasMaxLength(512);
            entity.Property(e => e.Status).HasMaxLength(20);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("audit_logs", t =>
            {
                t.HasCheckConstraint("CK_audit_logs_ActorType",
                    "actor_type IN ('User','Admin','Developer','System')");
            });
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.ActorType).HasConversion<string>().HasMaxLength(16);
            entity.Property(e => e.EventType).HasMaxLength(128);
            entity.Property(e => e.Action).HasMaxLength(128);
            entity.Property(e => e.SubjectType).HasMaxLength(64);
            entity.Property(e => e.MetadataJson).HasColumnType("jsonb");
            entity.Property(e => e.Ip).HasMaxLength(45);
            entity.HasIndex(e => e.TenantsId);
            entity.HasIndex(e => new { e.ActorType, e.ActorId, e.CreatedAt });
            entity.HasIndex(e => new { e.SubjectType, e.SubjectId, e.CreatedAt });
            entity.HasIndex(e => new { e.TenantsId, e.EventsId, e.CreatedAt });
        });

        modelBuilder.Entity<Feedback>(entity =>
        {
            entity.ToTable("feedbacks");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.Type).HasMaxLength(20);
            entity.Property(e => e.Message).HasMaxLength(2000);
            entity.Property(e => e.UserAgent).HasMaxLength(512);
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.Property(e => e.Diagnostics).HasColumnType("jsonb");
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantsId)
                .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UsersId)
                .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(e => e.TenantsId);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.Type);
        });

        modelBuilder.Entity<Performer>(entity =>
        {
            entity.ToTable("performers");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantsId);
            entity.HasIndex(e => e.Slug).IsUnique();
            entity.HasIndex(e => e.Name);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Slug).HasMaxLength(220).IsRequired();
            entity.Property(e => e.PrimaryImagePath).HasMaxLength(512);
            entity.Property(e => e.Meta).HasColumnType("jsonb").HasDefaultValueSql("'[]'::jsonb");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantsId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EventPerformer>(entity =>
        {
            entity.ToTable("event_performers");
            entity.HasKey(e => new { e.EventsId, e.PerformersId });
            entity.HasIndex(e => e.TenantsId);
            entity.HasIndex(e => new { e.EventsId, e.SortOrder });
            entity.HasIndex(e => e.PerformersId);
            entity.Property(e => e.SortOrder).HasDefaultValue(0);
            entity.Property(e => e.EventMeta).HasColumnType("jsonb").HasDefaultValueSql("'[]'::jsonb");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantsId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Event).WithMany().HasForeignKey(e => e.EventsId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Performer).WithMany().HasForeignKey(e => e.PerformersId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Sponsor>(entity =>
        {
            entity.ToTable("sponsors");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantsId);
            entity.HasIndex(e => e.Slug).IsUnique();
            entity.HasIndex(e => e.Name);
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Slug).HasMaxLength(220).IsRequired();
            entity.Property(e => e.PrimaryImagePath).HasMaxLength(512);
            entity.Property(e => e.Meta).HasColumnType("jsonb").HasDefaultValueSql("'[]'::jsonb");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantsId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EventSponsor>(entity =>
        {
            entity.ToTable("event_sponsors");
            entity.HasKey(e => new { e.EventsId, e.SponsorsId });
            entity.HasIndex(e => e.TenantsId);
            entity.HasIndex(e => new { e.EventsId, e.SortOrder });
            entity.HasIndex(e => e.SponsorsId);
            entity.Property(e => e.SortOrder).HasDefaultValue(0);
            entity.Property(e => e.EventMeta).HasColumnType("jsonb").HasDefaultValueSql("'[]'::jsonb");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantsId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Event).WithMany().HasForeignKey(e => e.EventsId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Sponsor).WithMany().HasForeignKey(e => e.SponsorsId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var tableName = entityType.GetTableName();
            if (tableName is null)
            {
                continue;
            }
            var primaryKey = entityType.FindPrimaryKey();
            if (primaryKey is null)
            {
                continue;
            }
            foreach (var property in primaryKey.Properties)
            {
                if (property.Name == "Id")
                {
                    property.SetColumnName($"{tableName}_id");
                    if (property.ClrType == typeof(Guid) && property.GetDefaultValueSql() is null)
                    {
                        property.SetDefaultValueSql("gen_random_uuid()");
                    }
                }
            }
        }
    }
}
