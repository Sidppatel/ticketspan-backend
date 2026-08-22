using Asp.Versioning;
using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace TicketSpan.Api.Endpoints;

public static class ApiVersioningExtensions
{
    public static IServiceCollection AddTicketSpanApiVersioning(this IServiceCollection services)
    {
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
            options.ApiVersionReader = ApiVersionReader.Combine(
                new UrlSegmentApiVersionReader(),
                new HeaderApiVersionReader("X-Api-Version"),
                new QueryStringApiVersionReader("api-version")
            );
        }).AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        });

        services.AddOpenApi("v1");

        return services;
    }

    public static RouteGroupBuilder CreateVersionedApiGroup(this IEndpointRouteBuilder app, int majorVersion = 1, int minorVersion = 0)
    {
        var version = new ApiVersion(majorVersion, minorVersion);
        var versionSet = app.NewApiVersionSet()
            .HasApiVersion(version)
            .ReportApiVersions()
            .Build();

        return app.MapGroup("/api/v{version:apiVersion}")
            .WithApiVersionSet(versionSet)
            .MapToApiVersion(version);
    }
}
