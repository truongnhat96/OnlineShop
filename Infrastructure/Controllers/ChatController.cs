using System.Diagnostics;
using System.Text.Json;
using Infrastructure.AIChat;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Infrastructure.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ChatController(IAIChat aiChat, ILogger<ChatController> logger, IOptions<GeminiOptions> settings) : ControllerBase
{
    [HttpPost("ask")]
    public async Task<IActionResult> Ask([FromBody] string prompt, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(prompt) || prompt.Length > 1000)
            return BadRequest(new { code = "invalid_prompt", message = "Vui lòng nhập câu hỏi từ 1 đến 1.000 ký tự." });

        var watch = Stopwatch.StartNew();
        try
        {
            var response = await aiChat.AskAsync(prompt, cancellationToken);
            if (string.IsNullOrWhiteSpace(response?.Choices?.FirstOrDefault()?.Message?.Content))
                return Failure(new ChatProviderException("empty_response"), watch);
            return Ok(response);
        }
        catch (ChatUnavailableException) { return Failure(new("missing_key", 503, false), watch); }
        catch (ChatProviderException error) { return Failure(error, watch); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Failure(new("request_cancelled", 499, false), watch);
        }
        catch (OperationCanceledException error) { return Failure(new("timeout", 504), watch, error.GetType().Name); }
        catch (HttpRequestException error)
        {
            return Failure(error.StatusCode.HasValue ? ChatProviderException.FromHttp(error.StatusCode.Value) : new("network_" + error.HttpRequestError), watch, error.GetType().Name);
        }
        catch (JsonException) { return Failure(new("invalid_json"), watch); }
        catch (Exception error)
        {
            // No exception message/stack: they may include user text, credentials or request bodies.
            return Failure(new("internal_error", 500), watch, error.GetType().Name);
        }
    }

    private ObjectResult Failure(ChatProviderException error, Stopwatch watch, string? exceptionType = null)
    {
        var model = GeminiChat.IsValidModel(settings.Value.Model) ? settings.Value.Model : "[invalid]";
        logger.LogWarning("Chat failed. Provider={Provider} Model={Model} Reason={Reason} ProviderHttpStatus={ProviderHttpStatus} " +
            "ProviderStatus={ProviderStatus} ProviderReason={ProviderReason} ExceptionType={ExceptionType} ElapsedMs={ElapsedMs} TraceId={TraceId}",
            "Gemini", model, error.Reason, error.ProviderStatusCode, error.ProviderStatus, error.ProviderReason,
            exceptionType ?? error.GetType().Name, watch.ElapsedMilliseconds, HttpContext?.TraceIdentifier);
        return StatusCode(error.ApiStatus, new { code = "chat_unavailable", message = ChatMessages.Unavailable, retryable = error.Retryable });
    }
}
