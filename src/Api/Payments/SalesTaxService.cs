using System.Text.Json.Serialization;
using Npgsql;
using Svyne.Api.Data;
using Svyne.Api.ErrorHandling;

namespace Svyne.Api.Payments;

public sealed class SalesTaxService
{
    private readonly ErrorLogger errors;
    private readonly ILogger<SalesTaxService> logger;
    private readonly HttpClient http;
    private readonly string baseUrl;
    private readonly TimeSpan ttl;

    public bool MockMode { get; }

    public SalesTaxService(IConfiguration configuration, IHttpClientFactory httpClientFactory,
        ErrorLogger errors, ILogger<SalesTaxService> logger, IHostEnvironment environment)
    {
        this.errors = errors;
        this.logger = logger;
        var configUrl = configuration["SALESTAXZIP_BASE_URL"];
        MockMode = string.IsNullOrWhiteSpace(configUrl) && environment.IsDevelopment();
        baseUrl = (string.IsNullOrWhiteSpace(configUrl) ? "https://salestaxzip.com" : configUrl).TrimEnd('/');
        var hours = int.TryParse(configuration["SALESTAXZIP_CACHE_TTL_HOURS"], out var h) ? h : 24;
        ttl = TimeSpan.FromHours(hours);
        http = httpClientFactory.CreateClient("salestaxzip");
        http.Timeout = TimeSpan.FromSeconds(10);
    }

    public async Task EnsureRateForEventAsync(NpgsqlConnection connection, Guid eventId, CancellationToken ct)
    {
        string? zip;
        await using (var cmd = new NpgsqlCommand("SELECT app.event_zip(@e)", connection))
        {
            cmd.Parameters.AddWithValue("e", eventId);
            zip = await cmd.ExecuteScalarAsync(ct) as string;
        }
        await EnsureRateForZipAsync(connection, zip, ct);
    }

    public async Task EnsureRateForZipAsync(NpgsqlConnection connection, string? zip, CancellationToken ct, bool force = false)
    {
        if (string.IsNullOrWhiteSpace(zip))
        {
            return;
        }

        if (!force)
        {
            DateTime? fetchedAt = null;
            await using (var cmd = new NpgsqlCommand(
                "SELECT fetched_at FROM vw_tax_rate_cache WHERE zip_code = @z", connection))
            {
                cmd.Parameters.AddWithValue("z", zip);
                var result = await cmd.ExecuteScalarAsync(ct);
                if (result is DateTime dt)
                {
                    fetchedAt = dt;
                }
            }

            if (fetchedAt is { } f && DateTime.UtcNow - f < ttl)
            {
                return;
            }
        }

        if (MockMode)
        {
            decimal mockRate = 0.05m;
            if (zip == "36611") mockRate = 0.10m;
            else if (zip == "90210") mockRate = 0.0825m;
            else if (zip == "11111") mockRate = 0.07m;

            var mockData = new SalesTaxZipData
            {
                ZipCode = zip,
                City = "Mock City",
                State = "AL",
                County = "Mock County",
                Rates = new SalesTaxZipRates
                {
                    Combined = mockRate,
                    State = mockRate * 0.4m,
                    County = mockRate * 0.6m,
                    City = 0m,
                    Local = 0m
                },
                LastUpdated = DateTime.UtcNow.ToString("o")
            };
            await UpsertAsync(connection, zip, mockData, ct);
            return;
        }

        SalesTaxZipEnvelope? envelope;
        try
        {
            using var response = await http.GetAsync($"{baseUrl}/api/v1/rate/{zip}", ct);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                await UpsertAsync(connection, zip, null, ct);
                await errors.LogWarningAsync("tax_zip_not_found",
                    $"SalesTaxZip has no rate for zip {zip}; defaulting to 0% tax", ct: ct);
                return;
            }

            response.EnsureSuccessStatusCode();
            envelope = await response.Content.ReadFromJsonAsync<SalesTaxZipEnvelope>(ct);
        }
        catch (Exception ex)
        {
            await errors.LogWarningAsync("tax_api_unavailable",
                $"SalesTaxZip lookup failed for zip {zip}; using cached rate if available", ct: ct);
            logger.LogWarning(ex, "Tax rate lookup failed for zip {Zip}", zip);
            return;
        }

        if (envelope is null)
        {
            return;
        }

        if (!envelope.Success || envelope.Data?.Rates is null)
        {
            await UpsertAsync(connection, zip, null, ct);
            await errors.LogWarningAsync("tax_zip_not_found",
                $"SalesTaxZip has no rate for zip {zip}; defaulting to 0% tax", ct: ct);
            return;
        }

        await UpsertAsync(connection, zip, envelope.Data, ct);
        await errors.LogInfoAsync("tax_rate_refreshed",
            $"Refreshed tax rate for zip {zip}: {envelope.Data.Rates.Combined:P2}", ct: ct);

        if (envelope.Data.Rates.Combined == 0m)
        {
            await errors.LogWarningAsync("tax_rate_zero",
                $"SalesTaxZip returned 0% combined rate for zip {zip}", ct: ct);
        }
        else if (envelope.Data.Rates.Combined > 0.20m)
        {
            await errors.LogWarningAsync("tax_rate_suspicious",
                $"SalesTaxZip returned unusually high combined rate {envelope.Data.Rates.Combined:P2} for zip {zip}; review", ct: ct);
        }
    }

    private static async Task UpsertAsync(NpgsqlConnection connection, string zip, SalesTaxZipData? data, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(
            "SELECT sp_upsert_tax_rate(@zip, @state, @county, @city, @sr, @cor, @cir, @lr, @combined, @api)", connection);
        cmd.Parameters.AddWithValue("zip", zip);
        cmd.Parameters.AddWithValue("state", (object?)data?.State ?? DBNull.Value);
        cmd.Parameters.AddWithValue("county", (object?)data?.County ?? DBNull.Value);
        cmd.Parameters.AddWithValue("city", (object?)data?.City ?? DBNull.Value);
        cmd.Parameters.AddWithValue("sr", data?.Rates?.State ?? 0m);
        cmd.Parameters.AddWithValue("cor", data?.Rates?.County ?? 0m);
        cmd.Parameters.AddWithValue("cir", data?.Rates?.City ?? 0m);
        cmd.Parameters.AddWithValue("lr", data?.Rates?.Local ?? 0m);
        cmd.Parameters.AddWithValue("combined", data?.Rates?.Combined ?? 0m);
        cmd.Parameters.AddWithValue("api", (object?)data?.LastUpdated ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}

public sealed record SalesTaxZipEnvelope
{
    [JsonPropertyName("success")] public bool Success { get; init; }
    [JsonPropertyName("data")] public SalesTaxZipData? Data { get; init; }
}

public sealed record SalesTaxZipData
{
    [JsonPropertyName("zip_code")] public string? ZipCode { get; init; }
    [JsonPropertyName("city")] public string? City { get; init; }
    [JsonPropertyName("state")] public string? State { get; init; }
    [JsonPropertyName("county")] public string? County { get; init; }
    [JsonPropertyName("rates")] public SalesTaxZipRates? Rates { get; init; }
    [JsonPropertyName("last_updated")] public string? LastUpdated { get; init; }
}

public sealed record SalesTaxZipRates
{
    [JsonPropertyName("combined")] public decimal Combined { get; init; }
    [JsonPropertyName("state")] public decimal State { get; init; }
    [JsonPropertyName("county")] public decimal County { get; init; }
    [JsonPropertyName("city")] public decimal City { get; init; }
    [JsonPropertyName("local")] public decimal Local { get; init; }
}
