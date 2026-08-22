namespace TicketSpan.Api.Endpoints.Common;

public sealed record ApiEnvelope<T>(bool Success, T? Data, string? Message = null);

public sealed record PagedEnvelope<T>(bool Success, IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);

public sealed record AckEnvelope(bool Success, string Message, int Code);
