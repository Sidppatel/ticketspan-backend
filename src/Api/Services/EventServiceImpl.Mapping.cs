using Grpc.Core;
using Npgsql;
using TicketSpan.Protos.Event;

namespace TicketSpan.Api.Services;

public sealed partial class EventServiceImpl
{
    private static EventImage MapEventImage(NpgsqlDataReader r) => new()
    {
        ImagesId = r.GetGuid(0).ToString(),
        StorageKey = r.GetString(1),
        Type = r.GetString(2),
        IsPrimary = r.GetBoolean(3),
        SortOrder = r.GetInt32(4)
    };

    private static ScheduleItem MapScheduleItem(NpgsqlDataReader r) => new()
    {
        ScheduleItemsId = r.GetGuid(0).ToString(),
        EventsId = r.GetGuid(1).ToString(),
        Title = r.GetString(2),
        TypeCategory = r.GetString(3),
        StartTime = new DateTimeOffset(r.GetDateTime(4), TimeSpan.Zero).ToUnixTimeSeconds(),
        EndTime = new DateTimeOffset(r.GetDateTime(5), TimeSpan.Zero).ToUnixTimeSeconds()
    };

    private static RpcException MapPostgres(PostgresException ex) => ex.SqlState switch
    {
        "P0001" => new RpcException(new Status(StatusCode.FailedPrecondition, ex.MessageText)),
        "P0002" => new RpcException(new Status(StatusCode.NotFound, ex.MessageText)),
        "22023" => new RpcException(new Status(StatusCode.FailedPrecondition, ex.MessageText)),
        "23P01" => new RpcException(new Status(StatusCode.FailedPrecondition, ex.MessageText)),
        "23514" => new RpcException(new Status(StatusCode.FailedPrecondition, ex.MessageText)),
        _ => new RpcException(new Status(StatusCode.Internal, ex.MessageText))
    };

    private const string EventSelect =
        "SELECT events_id, title, slug, description, status, category, start_date, end_date, image_path, "
        + "is_featured, layout_mode, total_capacity, venues_id, performers::text, sponsors::text, fees_included, event_type, primary_image_id, extra_info::text, ach_enabled, "
        + "venue_state_tax_rate, venue_county_tax_rate, venue_city_tax_rate, venue_local_tax_rate, venue_combined_tax_rate, "
        + "short_description, story_description, hero_backdrop_image_id, poster_image_id, is_verified_organizer, urgency_badge_text, tax_exempt "
        + "FROM vw_events";

    private static Event MapEvent(NpgsqlDataReader r) => new()
    {
        EventsId = r.GetGuid(0).ToString(),
        Title = r.GetString(1),
        Slug = r.GetString(2),
        Description = r.IsDBNull(3) ? string.Empty : r.GetString(3),
        Status = r.IsDBNull(4) ? string.Empty : r.GetString(4),
        Category = r.IsDBNull(5) ? string.Empty : r.GetString(5),
        StartDate = new DateTimeOffset(r.GetDateTime(6), TimeSpan.Zero).ToUnixTimeSeconds(),
        EndDate = new DateTimeOffset(r.GetDateTime(7), TimeSpan.Zero).ToUnixTimeSeconds(),
        ImagePath = r.IsDBNull(8) ? string.Empty : r.GetString(8),
        IsFeatured = !r.IsDBNull(9) && r.GetBoolean(9),
        LayoutMode = r.IsDBNull(10) ? string.Empty : r.GetString(10),
        TotalCapacity = r.IsDBNull(11) ? 0 : r.GetInt32(11),
        VenuesId = r.IsDBNull(12) ? string.Empty : r.GetGuid(12).ToString(),
        PerformersJson = r.IsDBNull(13) ? "[]" : r.GetString(13),
        SponsorsJson = r.IsDBNull(14) ? "[]" : r.GetString(14),
        FeesIncluded = !r.IsDBNull(15) && r.GetBoolean(15),
        EventType = r.IsDBNull(16) ? string.Empty : r.GetString(16),
        PrimaryImageId = r.IsDBNull(17) ? string.Empty : r.GetGuid(17).ToString(),
        ExtraInfoJson = r.IsDBNull(18) ? "[]" : r.GetString(18),
        AchEnabled = !r.IsDBNull(19) && r.GetBoolean(19),
        VenueStateTaxRate = r.IsDBNull(20) ? 0.0 : r.GetDouble(20),
        VenueCountyTaxRate = r.IsDBNull(21) ? 0.0 : r.GetDouble(21),
        VenueCityTaxRate = r.IsDBNull(22) ? 0.0 : r.GetDouble(22),
        VenueLocalTaxRate = r.IsDBNull(23) ? 0.0 : r.GetDouble(23),
        VenueCombinedTaxRate = r.IsDBNull(24) ? 0.0 : r.GetDouble(24),
        ShortDescription = r.FieldCount > 25 && !r.IsDBNull(25) ? r.GetString(25) : string.Empty,
        StoryDescription = r.FieldCount > 26 && !r.IsDBNull(26) ? r.GetString(26) : string.Empty,
        HeroBackdropImageId = r.FieldCount > 27 && !r.IsDBNull(27) ? r.GetGuid(27).ToString() : string.Empty,
        PosterImageId = r.FieldCount > 28 && !r.IsDBNull(28) ? r.GetGuid(28).ToString() : string.Empty,
        IsVerifiedOrganizer = r.FieldCount <= 29 || r.IsDBNull(29) || r.GetBoolean(29),
        UrgencyBadgeText = r.FieldCount > 30 && !r.IsDBNull(30) ? r.GetString(30) : string.Empty,
        TaxExempt = r.FieldCount > 31 && !r.IsDBNull(31) && r.GetBoolean(31)
    };

    private static string? NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;

    private static string? GetNormalizedCategory(string? category)
    {
        var trimmed = category?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }
        var allowed = new[] { "Music", "Business", "Social", "Dining", "Tech", "Arts", "Family", "Sports" };
        var matched = allowed.FirstOrDefault(c => string.Equals(c, trimmed, StringComparison.OrdinalIgnoreCase));
        return matched ?? trimmed;
    }
}
