using System.Net;

namespace Infrastructure.AIChat;

// Contains only classified metadata. Never retain provider bodies, keys or user text.
public sealed class ChatProviderException : Exception
{
    public string Reason { get; }
    public int ApiStatus { get; }
    public bool Retryable { get; }
    public int? ProviderStatusCode { get; }
    public string? ProviderStatus { get; }
    public string? ProviderReason { get; }

    public ChatProviderException(string reason, int apiStatus = 502, bool retryable = true,
        int? providerStatusCode = null, string? providerStatus = null, string? providerReason = null)
        : base("Chat provider request failed.")
    {
        Reason = reason;
        ApiStatus = apiStatus;
        Retryable = retryable;
        ProviderStatusCode = providerStatusCode;
        ProviderStatus = providerStatus;
        ProviderReason = providerReason;
    }

    public static ChatProviderException FromHttp(HttpStatusCode status, string? googleStatus = null, string? googleReason = null)
    {
        var safeStatus = googleStatus is "INVALID_ARGUMENT" or "FAILED_PRECONDITION" or "UNAUTHENTICATED" or
            "PERMISSION_DENIED" or "RESOURCE_EXHAUSTED" or "NOT_FOUND" or "UNAVAILABLE" or "INTERNAL" or
            "DEADLINE_EXCEEDED" ? googleStatus : null;
        var safeReason = googleReason is "API_KEY_INVALID" or "API_KEY_EXPIRED" or "API_KEY_SERVICE_BLOCKED" or
            "BILLING_DISABLED" or "SERVICE_DISABLED" or "CONSUMER_INVALID" or "ACCESS_TOKEN_SCOPE_INSUFFICIENT" or
            "RATE_LIMIT_EXCEEDED" or "QUOTA_EXCEEDED" ? googleReason : null;
        var failure = (int)status switch
        {
            _ when safeReason is "API_KEY_INVALID" or "API_KEY_EXPIRED" || safeStatus == "UNAUTHENTICATED" => ("invalid_key", 503, false),
            _ when safeReason == "BILLING_DISABLED" => ("billing_disabled", 503, false),
            _ when safeReason is "API_KEY_SERVICE_BLOCKED" or "SERVICE_DISABLED" or "ACCESS_TOKEN_SCOPE_INSUFFICIENT" || safeStatus == "PERMISSION_DENIED" => ("permission_denied", 503, false),
            _ when safeStatus == "FAILED_PRECONDITION" => ("project_not_ready", 503, false),
            _ when safeStatus == "RESOURCE_EXHAUSTED" => ("quota_exceeded", 429, true),
            _ when safeStatus == "NOT_FOUND" => ("model_unavailable", 503, false),
            401 => ("invalid_key", 503, false),
            402 => ("billing_required", 503, false),
            403 => ("permission_denied", 503, false),
            404 or 410 => ("model_unavailable", 503, false),
            400 or 422 => ("request_rejected", 502, false),
            429 => ("quota_exceeded", 429, true),
            408 or 504 => ("provider_timeout", 504, true),
            _ => ("provider_unavailable", 502, true)
        };
        return new(failure.Item1, failure.Item2, failure.Item3, (int)status, safeStatus, safeReason);
    }
}
