using Db.Entities;
using Db.Entities.Views;
using Microsoft.EntityFrameworkCore;

namespace Db;

public class EventPlatformDbContext(
    DbContextOptions<EventPlatformDbContext> options
) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<MagicLinkToken> MagicLinkTokens => Set<MagicLinkToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<UserEmailVerificationToken> UserEmailVerificationTokens => Set<UserEmailVerificationToken>();
    public DbSet<DeviceSession> DeviceSessions => Set<DeviceSession>();

    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<TableTemplate> TableTemplates => Set<TableTemplate>();

    public DbSet<Event> Events => Set<Event>();
    public DbSet<Performer> Performers => Set<Performer>();
    public DbSet<EventPerformer> EventPerformers => Set<EventPerformer>();
    public DbSet<Sponsor> Sponsors => Set<Sponsor>();
    public DbSet<EventSponsor> EventSponsors => Set<EventSponsor>();
    public DbSet<UserEvent> UserEvents => Set<UserEvent>();
    public DbSet<EventTable> EventTables => Set<EventTable>();
    public DbSet<EventTicketType> EventTicketTypes => Set<EventTicketType>();
    public DbSet<Table> Tables => Set<Table>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<PurchaseTicket> PurchaseTickets => Set<PurchaseTicket>();
    public DbSet<PurchaseTable> PurchaseTables => Set<PurchaseTable>();
    public DbSet<StripeTransaction> StripeTransactions => Set<StripeTransaction>();
    public DbSet<StripeTransfer> StripeTransfers => Set<StripeTransfer>();
    public DbSet<StripePayout> StripePayouts => Set<StripePayout>();

    public DbSet<Image> Images => Set<Image>();
    public DbSet<EventImage> EventImages => Set<EventImage>();
    public DbSet<VenueImage> VenueImages => Set<VenueImage>();
    public DbSet<PlatformImage> PlatformImages => Set<PlatformImage>();

    public DbSet<Feedback> Feedbacks => Set<Feedback>();

    public DbSet<EmailLog> EmailLogs => Set<EmailLog>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<EventView> EventViews => Set<EventView>();
    public DbSet<PerformerView> PerformerViews => Set<PerformerView>();
    public DbSet<SponsorView> SponsorViews => Set<SponsorView>();
    public DbSet<EventSummaryView> EventSummaryViews => Set<EventSummaryView>();
    public DbSet<TableView> TableViews => Set<TableView>();
    public DbSet<PurchaseView> PurchaseViews => Set<PurchaseView>();
    public DbSet<PurchaseTicketView> PurchaseTicketViews => Set<PurchaseTicketView>();
    public DbSet<VenueView> VenueViews => Set<VenueView>();
    public DbSet<TenantView> TenantViews => Set<TenantView>();
    public DbSet<StripeTransactionView> StripeTransactionViews => Set<StripeTransactionView>();
    public DbSet<UserProfileView> UserProfileViews => Set<UserProfileView>();
    public DbSet<EventTablesSummaryView> EventTablesSummaryViews => Set<EventTablesSummaryView>();
    public DbSet<EventTicketTypeSummaryView> EventTicketTypeSummaryViews => Set<EventTicketTypeSummaryView>();
    public DbSet<UserView> UserViews => Set<UserView>();
    public DbSet<UserEventView> UserEventViews => Set<UserEventView>();
    public DbSet<DeviceSessionView> DeviceSessionViews => Set<DeviceSessionView>();
    public DbSet<InvitationView> InvitationViews => Set<InvitationView>();
    public DbSet<FeedbackView> FeedbackViews => Set<FeedbackView>();
    public DbSet<EventImageView> EventImageViews => Set<EventImageView>();
    public DbSet<VenueImageView> VenueImageViews => Set<VenueImageView>();
    public DbSet<PlatformImageView> PlatformImageViews => Set<PlatformImageView>();
    public DbSet<BusinessLogView> BusinessLogViews => Set<BusinessLogView>();
    public DbSet<SystemLogView> SystemLogViews => Set<SystemLogView>();
    public DbSet<DeveloperLogView> DeveloperLogViews => Set<DeveloperLogView>();
    public DbSet<SiteVisitView> SiteVisitViews => Set<SiteVisitView>();
    public DbSet<AdminDashboardStatsView> AdminDashboardStatsViews => Set<AdminDashboardStatsView>();
    public DbSet<TopEventRevenueView> TopEventRevenueViews => Set<TopEventRevenueView>();
    public DbSet<PurchasesByStatusView> PurchasesByStatusViews => Set<PurchasesByStatusView>();
    public DbSet<EventsByCategoryView> EventsByCategoryViews => Set<EventsByCategoryView>();
    public DbSet<EventTableStatsView> EventTableStatsViews => Set<EventTableStatsView>();
    public DbSet<EventFacetsView> EventFacetsViews => Set<EventFacetsView>();

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
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users", t =>
            {
                t.HasCheckConstraint("CK_users_DeveloperHasNoTenant",
                    "(role = 99) = (tenants_id IS NULL)");
                t.HasCheckConstraint("CK_users_Role",
                    "role IN (0, 1, 2, 3, 99)");
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
            entity.HasIndex(e => e.PurchasesId);
            entity.Property(e => e.StripeTransferId).HasMaxLength(128).IsRequired();
            entity.Property(e => e.Currency).HasMaxLength(8).HasDefaultValue("usd");
            entity.Property(e => e.RawEvent).HasColumnType("jsonb");
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantsId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Purchase).WithMany().HasForeignKey(e => e.PurchasesId)
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

        modelBuilder.Entity<UserEmailVerificationToken>(entity =>
        {
            entity.ToTable("user_email_verification_tokens");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TokenHash).IsUnique();
            entity.HasIndex(e => e.UsersId);
            entity.HasIndex(e => e.ExpiresAt);
            entity.Property(e => e.TokenHash).HasMaxLength(128);
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
            entity.Property(e => e.Name).HasMaxLength(128);
            entity.Property(e => e.DefaultShape).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.DefaultColor).HasMaxLength(20);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantsId)
                .OnDelete(DeleteBehavior.Restrict);
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
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantsId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Event).WithMany().HasForeignKey(e => e.EventsId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.TableTemplate).WithMany(tt => tt.EventTables)
                .HasForeignKey(e => e.TableTemplatesId)
                .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<EventTicketType>(entity =>
        {
            entity.ToTable("event_ticket_types", t =>
            {
                t.HasCheckConstraint("CK_event_ticket_types_PriceCents", "price_cents >= 0");
                t.HasCheckConstraint("CK_event_ticket_types_MaxQuantity",
                    "max_quantity IS NULL OR max_quantity > 0");
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
                t.HasCheckConstraint("CK_events_DateRange", "end_date > start_date");
                t.HasCheckConstraint("CK_events_MaxCapacity",
                    "max_capacity IS NULL OR max_capacity > 0");
                t.HasCheckConstraint("CK_events_GridDimensions",
                    "(grid_rows IS NULL OR grid_rows > 0) AND (grid_cols IS NULL OR grid_cols > 0)");
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
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantsId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Venue).WithMany(v => v.Events).HasForeignKey(e => e.VenuesId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.CreatedByUser).WithMany().HasForeignKey(e => e.CreatedByUsersId)
                .OnDelete(DeleteBehavior.Restrict);
#pragma warning disable CS8603
            entity.HasGeneratedTsVectorColumn(e => e.SearchVector, "english", e => new { e.Title, Description = e.Description! })
                  .HasIndex(e => e.SearchVector).HasMethod("GIN");
#pragma warning restore CS8603
        });

        modelBuilder.Entity<UserEvent>(entity =>
        {
            entity.ToTable("user_events");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UsersId, e.EventsId }).IsUnique();
            entity.HasIndex(e => e.EventsId);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UsersId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Event).WithMany().HasForeignKey(e => e.EventsId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.AssignedByUser).WithMany()
                .HasForeignKey(e => e.AssignedByUsersId)
                .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
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
                t.HasCheckConstraint("CK_tables_GridRow", "grid_row >= 0");
                t.HasCheckConstraint("CK_tables_GridCol", "grid_col >= 0");
            });
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantsId);
            entity.HasIndex(e => e.EventsId);
            entity.HasIndex(e => new { e.EventsId, e.Label }).IsUnique();
            entity.HasIndex(e => new { e.EventsId, e.GridRow, e.GridCol }).IsUnique();
            entity.HasIndex(e => new { e.EventsId, e.Status });
            entity.Property(e => e.Label).HasMaxLength(20);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20)
                .HasDefaultValue(Db.Enums.TableStatus.Available);
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

        modelBuilder.Entity<Purchase>(entity =>
        {
            entity.ToTable("purchases", t =>
            {
                t.HasCheckConstraint("CK_purchases_Status",
                    "status IN ('Pending','Paid','CheckedIn','Cancelled','Refunded','Expired')");
                t.HasCheckConstraint("CK_purchases_SubtotalCents", "subtotal_cents >= 0");
                t.HasCheckConstraint("CK_purchases_FeeCents", "fee_cents >= 0");
                t.HasCheckConstraint("CK_purchases_TotalCents", "total_cents >= 0");
                t.HasCheckConstraint("CK_purchases_TotalFormula",
                    "total_cents = subtotal_cents + fee_cents");
                t.HasCheckConstraint("CK_purchases_SeatsReserved",
                    "seats_reserved IS NULL OR seats_reserved > 0");
            });
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantsId);
            entity.HasIndex(e => e.PurchaseNumber).IsUnique();
            entity.HasIndex(e => e.QrToken).IsUnique().HasFilter("qr_token IS NOT NULL");
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.UsersId);
            entity.HasIndex(e => new { e.UsersId, e.CreatedAt });
            entity.HasIndex(e => new { e.EventsId, e.Status });
            entity.Property(e => e.PurchaseNumber).HasMaxLength(20);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.QrToken).HasMaxLength(128);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantsId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UsersId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Event).WithMany().HasForeignKey(e => e.EventsId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Table).WithMany().HasForeignKey(e => e.TablesId)
                .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.EventTicketType).WithMany().HasForeignKey(e => e.EventTicketTypesId)
                .IsRequired(false).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<PurchaseTicket>(entity =>
        {
            entity.ToTable("purchase_tickets", t =>
            {
                t.HasCheckConstraint("CK_purchase_tickets_Status",
                    "status IN ('Unassigned','Invited','Claimed','CheckedIn')");
                t.HasCheckConstraint("CK_purchase_tickets_SeatNumber", "seat_number > 0");
            });
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantsId);
            entity.HasIndex(e => e.QrToken).IsUnique();
            entity.HasIndex(e => e.InviteTokenHash).IsUnique()
                .HasFilter("invite_token_hash IS NOT NULL");
            entity.HasIndex(e => new { e.PurchasesId, e.SeatNumber }).IsUnique();
            entity.HasIndex(e => e.GuestUsersId);
            entity.Property(e => e.TicketCode).HasMaxLength(20);
            entity.Property(e => e.QrToken).HasMaxLength(128);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.InviteTokenHash).HasMaxLength(128);
            entity.Property(e => e.InvitedEmail).HasMaxLength(256);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantsId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Purchase).WithMany(b => b.Tickets)
                .HasForeignKey(e => e.PurchasesId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.GuestUser).WithMany()
                .HasForeignKey(e => e.GuestUsersId).IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<PurchaseTable>(entity =>
        {
            entity.ToTable("purchase_tables");
            entity.HasKey(e => new { e.PurchasesId, e.TablesId });
            entity.HasIndex(e => e.TablesId);
            entity.HasIndex(e => e.TenantsId);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantsId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Purchase).WithMany()
                .HasForeignKey(e => e.PurchasesId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Table).WithMany()
                .HasForeignKey(e => e.TablesId).OnDelete(DeleteBehavior.Cascade);
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
                t.HasCheckConstraint("CK_stripe_transactions_TaxAmount",
                    "tax_amount_cents IS NULL OR tax_amount_cents >= 0");
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
            entity.Property(e => e.TaxCalculationId).HasMaxLength(128);
            entity.Property(e => e.TaxTransactionId).HasMaxLength(128);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantsId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Purchase).WithOne(b => b.StripeTransaction)
                .HasForeignKey<StripeTransaction>(e => e.PurchasesId)
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

        modelBuilder.Entity<PlatformImage>(entity =>
        {
            entity.ToTable("platform_images");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ImagesId).IsUnique();
            entity.HasIndex(e => e.SortOrder);
            entity.HasIndex(e => e.Tag);
            entity.Property(e => e.Tag).HasMaxLength(64);
            entity.HasOne(e => e.Image).WithMany().HasForeignKey(e => e.ImagesId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EventImage>(entity =>
        {
            entity.ToTable("event_images");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantsId);
            entity.HasIndex(e => new { e.EventsId, e.ImagesId }).IsUnique();
            entity.HasIndex(e => new { e.EventsId, e.SortOrder });
            entity.HasIndex(e => e.EventsId)
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

        modelBuilder.Entity<EventView>(entity =>
        {
            entity.ToView("vw_events");
            entity.HasKey(e => e.EventId);
            entity.Property(e => e.Performers).HasColumnType("jsonb");
            entity.Property(e => e.Sponsors).HasColumnType("jsonb");
        });
        modelBuilder.Entity<PerformerView>(entity =>
        {
            entity.ToView("vw_performers");
            entity.HasKey(e => e.PerformerId);
            entity.Property(e => e.Meta).HasColumnType("jsonb");
        });
        modelBuilder.Entity<SponsorView>(entity =>
        {
            entity.ToView("vw_sponsors");
            entity.HasKey(e => e.SponsorId);
            entity.Property(e => e.Meta).HasColumnType("jsonb");
        });
        modelBuilder.Entity<EventSummaryView>(entity =>
        {
            entity.ToView("vw_event_summary");
            entity.HasKey(e => e.EventId);
        });
        modelBuilder.Entity<TableView>(entity =>
        {
            entity.ToView("vw_tables");
            entity.HasKey(e => e.TableId);
        });
        modelBuilder.Entity<PurchaseView>(entity =>
        {
            entity.ToView("vw_purchases");
            entity.HasKey(e => e.PurchaseId);
        });
        modelBuilder.Entity<PurchaseTicketView>(entity =>
        {
            entity.ToView("vw_purchase_tickets");
            entity.HasKey(e => e.PurchaseTicketId);
        });
        modelBuilder.Entity<VenueView>(entity =>
        {
            entity.ToView("vw_venues");
            entity.HasKey(e => e.VenueId);
        });
        modelBuilder.Entity<TenantView>(entity =>
        {
            entity.ToView("vw_tenants");
            entity.HasKey(e => e.TenantId);
        });
        modelBuilder.Entity<StripeTransactionView>(entity =>
        {
            entity.ToView("vw_stripe_transactions");
            entity.HasKey(e => e.TransactionId);
            entity.Property(e => e.Status).HasConversion<string>();
            entity.Property(e => e.PurchaseStatus).HasConversion<string>();
        });
        modelBuilder.Entity<UserProfileView>(entity =>
        {
            entity.ToView("vw_user_profile");
            entity.HasKey(e => e.UserId);
        });
        modelBuilder.Entity<EventTablesSummaryView>(entity =>
        {
            entity.ToView("vw_event_tables_summary");
            entity.HasKey(e => e.EventTableId);
        });
        modelBuilder.Entity<EventTicketTypeSummaryView>(entity =>
        {
            entity.ToView("vw_event_ticket_types_summary");
            entity.HasKey(e => e.EventTicketTypeId);
        });
        modelBuilder.Entity<UserView>(entity =>
        {
            entity.ToView("vw_users");
            entity.HasKey(e => e.UserId);
        });
        modelBuilder.Entity<UserEventView>(entity =>
        {
            entity.ToView("vw_user_events");
            entity.HasKey(e => e.UserEventId);
        });
        modelBuilder.Entity<DeviceSessionView>(entity =>
        {
            entity.ToView("vw_device_sessions");
            entity.HasKey(e => e.DeviceSessionId);
        });
        modelBuilder.Entity<InvitationView>(entity =>
        {
            entity.ToView("vw_invitations");
            entity.HasKey(e => e.InvitationId);
            entity.Property(e => e.Status).HasConversion<string>();
        });
        modelBuilder.Entity<FeedbackView>(entity =>
        {
            entity.ToView("vw_feedbacks");
            entity.HasKey(e => e.FeedbackId);
        });
        modelBuilder.Entity<EventImageView>(entity =>
        {
            entity.ToView("vw_event_images");
            entity.HasKey(e => e.EventImageId);
        });
        modelBuilder.Entity<VenueImageView>(entity =>
        {
            entity.ToView("vw_venue_images");
            entity.HasKey(e => e.VenueImageId);
        });
        modelBuilder.Entity<PlatformImageView>(entity =>
        {
            entity.ToView("vw_platform_images");
            entity.HasKey(e => e.PlatformImageId);
        });
        modelBuilder.Entity<BusinessLogView>(entity =>
        {
            entity.ToView("vw_business_logs");
            entity.HasKey(e => e.Id);
        });
        modelBuilder.Entity<SystemLogView>(entity =>
        {
            entity.ToView("vw_system_logs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Category).HasMaxLength(30);
        });
        modelBuilder.Entity<DeveloperLogView>(entity =>
        {
            entity.ToView("vw_developer_logs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Severity).HasMaxLength(20);
        });
        modelBuilder.Entity<SiteVisitView>(entity =>
        {
            entity.ToView("vw_site_visits");
            entity.HasKey(e => e.Id);
        });
        modelBuilder.Entity<AdminDashboardStatsView>(entity =>
        {
            entity.ToView("vw_admin_dashboard_stats");
            entity.HasNoKey();
        });
        modelBuilder.Entity<TopEventRevenueView>(entity =>
        {
            entity.ToView("vw_top_events_revenue");
            entity.HasKey(e => e.EventId);
        });
        modelBuilder.Entity<PurchasesByStatusView>(entity =>
        {
            entity.ToView("vw_purchases_by_status");
            entity.HasKey(e => e.Status);
        });
        modelBuilder.Entity<EventsByCategoryView>(entity =>
        {
            entity.ToView("vw_events_by_category");
            entity.HasKey(e => e.Category);
        });
        modelBuilder.Entity<EventTableStatsView>(entity =>
        {
            entity.ToView("vw_event_table_stats");
            entity.HasKey(e => e.EventId);
        });
        modelBuilder.Entity<EventFacetsView>(entity =>
        {
            entity.ToView("vw_event_facets");
            entity.HasNoKey();
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
