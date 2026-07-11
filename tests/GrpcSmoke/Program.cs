using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Configuration;
using Svyne.Api.Security;
using Svyne.Protos.Common;
using Svyne.Protos.Tenant;

AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
{
    ["JWT_SIGNING_KEY"] = "local-development-jwt-signing-key-change-me-32+chars",
    ["JWT_ISSUER"] = "svyne",
    ["JWT_AUDIENCE"] = "svyne-clients"
}).Build();

var jwt = new JwtTokenService(config);
var devUserId = Guid.Parse("20000000-0000-0000-0000-000000000099");
var (devToken, _) = jwt.Issue(devUserId, "developer@svyne.test", null, 99, "");

var channel = GrpcChannel.ForAddress("http://localhost:5262");
var headers = new Metadata { { "Authorization", $"Bearer {devToken}" } };
var client = new TenantService.TenantServiceClient(channel);

var slug = "grpc-" + Guid.NewGuid().ToString("N").Substring(0, 8);
var created = await client.CreateTenantAsync(new CreateTenantRequest
{
    Slug = slug,
    Name = "Grpc Test Co",
    AdminEmail = $"admin@{slug}.test",
    AdminFirstName = "Grpc",
    AdminLastName = "Admin"
}, headers);
Console.WriteLine($"CreateTenant -> tenants_id={created.TenantsId} admin={created.AdminUsersId} setup={created.SetupUrl}");

var list = await client.ListTenantsAsync(new PageRequest { Limit = 100 }, headers);
var found = list.Tenants.FirstOrDefault(t => t.Slug == slug);
Console.WriteLine($"ListTenants -> total={list.Tenants.Count} found_new={(found != null)} member_count={found?.MemberCount}");

var members = await client.ListTenantMembersAsync(new UuidValue { Value = created.TenantsId }, headers);
Console.WriteLine($"ListTenantMembers -> count={members.Members.Count} first_role={members.Members.FirstOrDefault()?.Role}");

var (adminToken, _) = jwt.Issue(Guid.Parse(created.AdminUsersId), $"admin@{slug}.test", Guid.Parse(created.TenantsId), 1, slug);
var adminHeaders = new Metadata { { "Authorization", $"Bearer {adminToken}" } };
var venueClient = new Svyne.Protos.Catalog.VenueService.VenueServiceClient(channel);
var venue = await venueClient.CreateVenueAsync(new Svyne.Protos.Catalog.CreateVenueRequest
{
    Name = "Main Hall", City = "Town", State = "TS", Zip = "11111"
}, adminHeaders);
Console.WriteLine($"CreateVenue -> venues_id={venue.Value}");
var venues = await venueClient.ListVenuesAsync(new PageRequest { Limit = 50 }, adminHeaders);
Console.WriteLine($"ListVenues -> count={venues.Venues.Count}");

var perfClient = new Svyne.Protos.Catalog.PerformerService.PerformerServiceClient(channel);
var perf = await perfClient.CreatePerformerAsync(new Svyne.Protos.Catalog.CreatePerformerRequest
{
    Name = "Headliner", Slug = "headliner-" + slug, MetaJson = "[]"
}, adminHeaders);
Console.WriteLine($"CreatePerformer -> performers_id={perf.Value}");

var eventClient = new Svyne.Protos.Event.EventService.EventServiceClient(channel);
var ev = await eventClient.CreateEventAsync(new Svyne.Protos.Event.CreateEventRequest
{
    Title = "Gala Night", Slug = "gala-" + slug, Status = "Draft", LayoutMode = "Open",
    VenuesId = venue.Value,
    StartDate = DateTimeOffset.UtcNow.AddDays(10).ToUnixTimeSeconds(),
    EndDate = DateTimeOffset.UtcNow.AddDays(11).ToUnixTimeSeconds()
}, adminHeaders);
Console.WriteLine($"CreateEvent -> events_id={ev.EventsId}");
var gotEvent = await eventClient.GetEventAsync(new UuidValue { Value = ev.EventsId }, adminHeaders);
Console.WriteLine($"GetEvent -> title={gotEvent.Title} status={gotEvent.Status}");

var taxVenue = await venueClient.CreateVenueAsync(new Svyne.Protos.Catalog.CreateVenueRequest
{
    Name = "Taxless Venue"
}, adminHeaders);
Console.WriteLine($"CreateVenue(no zip) -> venues_id={taxVenue.Value}");
var taxEvent = await eventClient.CreateEventAsync(new Svyne.Protos.Event.CreateEventRequest
{
    Title = "Tax Recalc Event", Slug = "tax-" + slug, Status = "Draft", LayoutMode = "Open",
    VenuesId = taxVenue.Value,
    StartDate = DateTimeOffset.UtcNow.AddDays(20).ToUnixTimeSeconds(),
    EndDate = DateTimeOffset.UtcNow.AddDays(21).ToUnixTimeSeconds()
}, adminHeaders);
var venueBefore = await venueClient.GetVenueAsync(new UuidValue { Value = taxVenue.Value }, adminHeaders);
Console.WriteLine($"GetVenue(before zip) -> zip='{venueBefore.Zip}' combined_rate={venueBefore.CombinedTaxRate}");
await venueClient.UpdateVenueAsync(new Svyne.Protos.Catalog.UpdateVenueRequest
{
    VenuesId = taxVenue.Value, Name = "Taxless Venue", IsActive = true,
    Line1 = "1 Test St", City = "Mobile", State = "AL", Zip = "36611"
}, adminHeaders);
var venueAfter = await venueClient.GetVenueAsync(new UuidValue { Value = taxVenue.Value }, adminHeaders);
Console.WriteLine($"GetVenue(after zip) -> zip='{venueAfter.Zip}' combined_rate={venueAfter.CombinedTaxRate}");
decimal eventTaxRate;
await using (var dbConn = new Npgsql.NpgsqlConnection("Host=127.0.0.1;Port=5432;Database=event_platform;Username=ep_dev;Password=ep_dev_password"))
{
    await dbConn.OpenAsync();
    await using var taxCmd = new Npgsql.NpgsqlCommand("SELECT app.event_tax_rate(@e)", dbConn);
    taxCmd.Parameters.AddWithValue("e", Guid.Parse(taxEvent.EventsId));
    eventTaxRate = (decimal)(await taxCmd.ExecuteScalarAsync())!;
}
Console.WriteLine($"event_tax_rate(after zip) -> {eventTaxRate}");
var taxRecalcOk = venueBefore.CombinedTaxRate == 0 && venueAfter.Zip == "36611"
    && venueAfter.CombinedTaxRate > 0 && eventTaxRate > 0;
Console.WriteLine($"TaxRecalc -> {(taxRecalcOk ? "PASS" : "FAIL")}");
var ghostRejected = false;
try
{
    await venueClient.UpdateVenueAsync(new Svyne.Protos.Catalog.UpdateVenueRequest
    {
        VenuesId = Guid.NewGuid().ToString(), Name = "Ghost", IsActive = true, Zip = "36611"
    }, adminHeaders);
}
catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
{
    ghostRejected = true;
}
Console.WriteLine($"UpdateVenue(missing id) -> {(ghostRejected ? "NotFound as expected" : "FAIL: silent success")}");

var dashClient = new Svyne.Protos.Admin.DashboardService.DashboardServiceClient(channel);
var devDash = await dashClient.GetDeveloperDashboardAsync(new Empty(), headers);
Console.WriteLine($"DeveloperDashboard -> tenants={devDash.TotalTenants} users={devDash.TotalUsers}");

var healthClient = new Svyne.Protos.Admin.HealthService.HealthServiceClient(channel);
var health = await healthClient.CheckAsync(new Empty());
Console.WriteLine($"Health -> status={health.Status} db={health.Database}");

var ok = found != null && members.Members.Count == 1 && !string.IsNullOrEmpty(perf.Value)
    && !string.IsNullOrEmpty(ev.EventsId) && gotEvent.Title == "Gala Night"
    && devDash.TotalTenants > 0 && health.Database && taxRecalcOk && ghostRejected;
Console.WriteLine(ok ? "SMOKE PASS" : "SMOKE FAIL");
