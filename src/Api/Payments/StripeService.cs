using Stripe;

namespace TicketSpan.Api.Payments;

public sealed class StripeService
{
    private readonly StripeClient client;

    public string PublishableKey { get; }
    public bool Configured { get; }

    public StripeService(IConfiguration configuration)
    {
        var secret = configuration["STRIPE_SECRET_KEY"];
        PublishableKey = configuration["STRIPE_PUBLISHABLE_KEY"] ?? string.Empty;
        Configured = !string.IsNullOrWhiteSpace(secret);
        client = new StripeClient(secret ?? "sk_test_unconfigured");
    }


    public async Task<string> CreateExpressAccountAsync(string? email, CancellationToken ct)
        => await CreateExpressAccountAsync(new StripeAccountPrefill { Email = email }, ct);

    public async Task<string> CreateExpressAccountAsync(StripeAccountPrefill prefill, CancellationToken ct)
    {
        var options = new AccountCreateOptions
        {
            Type = "express",
            Country = string.IsNullOrWhiteSpace(prefill.Country) ? "US" : prefill.Country,
            Email = NullIfBlank(prefill.Email),
            Capabilities = new AccountCapabilitiesOptions
            {
                CardPayments = new AccountCapabilitiesCardPaymentsOptions { Requested = true },
                Transfers = new AccountCapabilitiesTransfersOptions { Requested = true }
            }
        };

        if (!string.IsNullOrWhiteSpace(prefill.BusinessType))
        {
            options.BusinessType = prefill.BusinessType;
        }

        var profile = new AccountBusinessProfileOptions
        {
            Name = NullIfBlank(prefill.BusinessName),
            Url = NullIfBlank(prefill.Url),
            ProductDescription = NullIfBlank(prefill.ProductDescription),
            SupportEmail = NullIfBlank(prefill.SupportEmail),
            Mcc = prefill.Mcc is { Length: 4 } mcc && mcc.All(char.IsDigit) ? mcc : null
        };
        if (profile.Name is not null || profile.Url is not null || profile.ProductDescription is not null
            || profile.SupportEmail is not null || profile.Mcc is not null)
        {
            options.BusinessProfile = profile;
        }

        if (prefill.BusinessType == "individual"
            && (!string.IsNullOrWhiteSpace(prefill.IndividualFirstName) || !string.IsNullOrWhiteSpace(prefill.IndividualLastName)))
        {
            options.Individual = new AccountIndividualOptions
            {
                FirstName = NullIfBlank(prefill.IndividualFirstName),
                LastName = NullIfBlank(prefill.IndividualLastName),
                Email = NullIfBlank(prefill.Email)
            };
        }
        else if (prefill.BusinessType == "company" && !string.IsNullOrWhiteSpace(prefill.BusinessName))
        {
            options.Company = new AccountCompanyOptions { Name = prefill.BusinessName };
        }

        var account = await new AccountService(client).CreateAsync(options, cancellationToken: ct);
        return account.Id;
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    public async Task<string> CreateAccountLinkAsync(string accountId, string returnUrl, string refreshUrl, CancellationToken ct)
    {
        var service = new AccountLinkService(client);
        var link = await service.CreateAsync(new AccountLinkCreateOptions
        {
            Account = accountId,
            ReturnUrl = returnUrl,
            RefreshUrl = refreshUrl,
            Type = "account_onboarding"
        }, cancellationToken: ct);
        return link.Url;
    }

    public async Task<Account> GetAccountAsync(string accountId, CancellationToken ct)
        => await new AccountService(client).GetAsync(
            accountId,
            new AccountGetOptions { Expand = new List<string> { "external_accounts" } },
            cancellationToken: ct);

    public async Task UpdateAccountAsync(string accountId, StripeAccountPrefill prefill, CancellationToken ct)
    {
        var options = new AccountUpdateOptions();
        if (!string.IsNullOrWhiteSpace(prefill.BusinessType))
        {
            options.BusinessType = prefill.BusinessType;
        }

        var profile = new AccountBusinessProfileOptions
        {
            Name = NullIfBlank(prefill.BusinessName),
            Url = NullIfBlank(prefill.Url),
            ProductDescription = NullIfBlank(prefill.ProductDescription),
            SupportEmail = NullIfBlank(prefill.SupportEmail),
            Mcc = prefill.Mcc is { Length: 4 } mcc && mcc.All(char.IsDigit) ? mcc : null
        };
        if (profile.Name is not null || profile.Url is not null || profile.ProductDescription is not null
            || profile.SupportEmail is not null || profile.Mcc is not null)
        {
            options.BusinessProfile = profile;
        }

        if (prefill.BusinessType == "company" && !string.IsNullOrWhiteSpace(prefill.BusinessName))
        {
            options.Company = new AccountCompanyOptions { Name = prefill.BusinessName };
        }
        else if (prefill.BusinessType == "individual"
            && (!string.IsNullOrWhiteSpace(prefill.IndividualFirstName) || !string.IsNullOrWhiteSpace(prefill.IndividualLastName)))
        {
            options.Individual = new AccountIndividualOptions
            {
                FirstName = NullIfBlank(prefill.IndividualFirstName),
                LastName = NullIfBlank(prefill.IndividualLastName)
            };
        }

        await new AccountService(client).UpdateAsync(accountId, options, cancellationToken: ct);
    }


    public async Task<PaymentIntent> CreateDestinationPaymentIntentAsync(
        long amountCents, long applicationFeeCents, string currency,
        string destinationAccountId, Guid bookingId, bool achAllowed, bool bankOnly, CancellationToken ct,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        var service = new PaymentIntentService(client);
        var methods = bankOnly
            ? new List<string> { "us_bank_account" }
            : new List<string> { "card", "cashapp" };
        var meta = new Dictionary<string, string> { ["bookings_id"] = bookingId.ToString() };
        if (metadata is not null)
        {
            foreach (var (key, value) in metadata)
            {
                meta[key] = value;
            }
        }
        var options = new PaymentIntentCreateOptions
        {
            Amount = amountCents,
            Currency = currency,
            PaymentMethodTypes = methods,
            ApplicationFeeAmount = applicationFeeCents,
            TransferData = new PaymentIntentTransferDataOptions { Destination = destinationAccountId },
            Metadata = meta
        };
        var requestOptions = new RequestOptions
        {
            IdempotencyKey = $"pi_create_{bookingId}_{(bankOnly ? "ach" : "card")}"
        };
        return await service.CreateAsync(options, requestOptions, ct);
    }

    public async Task<PaymentIntent> GetPaymentIntentAsync(string intentId, CancellationToken ct)
        => await new PaymentIntentService(client).GetAsync(intentId, cancellationToken: ct);

    public async Task<PaymentIntent> UpdatePaymentIntentAmountAsync(
        string intentId, long amountCents, long applicationFeeCents, CancellationToken ct)
        => await new PaymentIntentService(client).UpdateAsync(intentId, new PaymentIntentUpdateOptions
        {
            Amount = amountCents,
            ApplicationFeeAmount = applicationFeeCents
        }, cancellationToken: ct);

    public async Task CancelPaymentIntentAsync(string intentId, CancellationToken ct)
    {
        try
        {
            await new PaymentIntentService(client).CancelAsync(intentId, cancellationToken: ct);
        }
        catch (StripeException)
        {
        }
    }

    public async Task<Refund> RefundPaymentIntentAsync(string intentId, CancellationToken ct)
        => await new RefundService(client).CreateAsync(new RefundCreateOptions
        {
            PaymentIntent = intentId,
            RefundApplicationFee = true,
            ReverseTransfer = true
        }, cancellationToken: ct);

    public static bool IsPayable(string status) => status is
        "requires_payment_method" or "requires_confirmation" or "requires_action" or "processing";
}

public sealed record StripeAccountPrefill
{
    public string? Country { get; init; }
    public string? Email { get; init; }
    public string? BusinessType { get; init; }
    public string? BusinessName { get; init; }
    public string? Url { get; init; }
    public string? ProductDescription { get; init; }
    public string? Mcc { get; init; }
    public string? SupportEmail { get; init; }
    public string? IndividualFirstName { get; init; }
    public string? IndividualLastName { get; init; }
}
