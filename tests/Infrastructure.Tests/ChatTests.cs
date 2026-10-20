using System.Net;
using System.Text;
using System.Text.Json;
using Entities;
using Infrastructure.AIChat;
using Infrastructure.AIChat.CacheSupport;
using Infrastructure.AIChat.Model;
using Infrastructure.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using UseCase.Business_Logic;
using Xunit;

namespace Infrastructure.Tests;

public sealed class ChatTests
{
    private const string Secret = "PRIVATE_TEST_KEY";
    private const string Success = """{"responseId":"real-provider-id","modelVersion":"gemini-3.5-flash-lite","candidates":[{"content":{"parts":[{"text":"PRIVATE_THOUGHT","thought":true},{"text":"Chào "},{"text":"bạn"}]},"finishReason":"STOP"}],"usageMetadata":{"promptTokenCount":15,"candidatesTokenCount":4,"totalTokenCount":25}}""";
    private static GeminiOptions Settings() => new() { ApiKey = Secret };
    private static ChatController Controller(IAIChat service, TestLogger? logger = null) => new(service, logger ?? new TestLogger(), Options.Create(Settings()))
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { TraceIdentifier = "TEST_TRACE_ID" } }
    };
    private static GeminiChat Chat(IHttpClientFactory factory, Mock<IProductManage>? products = null, Mock<IAHPRecommendationService>? ahp = null, GeminiOptions? settings = null)
        => new(products?.Object ?? Products().Object, factory, Options.Create(settings ?? Settings()),
            new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())), new CacheProductOption(), ahp?.Object ?? new Mock<IAHPRecommendationService>().Object);
    private static Mock<IProductManage> Products()
    {
        var result = new Mock<IProductManage>();
        result.Setup(p => p.GetProductsAsync()).ReturnsAsync([new Product { Id = 7, Name = "ORIGINAL_CATALOG_NAME", Brand = "Original brand", Description = "NOT_SENT_DESCRIPTION", Price = 123000, Quantity = 4 }]);
        return result;
    }
    private static IHttpClientFactory Factory(HttpMessageHandler handler)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("Gemini")).Returns(() => new HttpClient(handler, false));
        return factory.Object;
    }
    private static void AssertCommonError(ObjectResult result)
    {
        using var body = JsonDocument.Parse(JsonSerializer.Serialize(result.Value));
        Assert.Equal("chat_unavailable", body.RootElement.GetProperty("code").GetString());
        Assert.Equal(ChatMessages.Unavailable, body.RootElement.GetProperty("message").GetString());
        Assert.DoesNotContain(Secret, body.RootElement.ToString());
        Assert.DoesNotContain("PRIVATE_PROVIDER", body.RootElement.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("too-long")]
    public async Task InvalidPromptsNeverReachTheService(string prompt)
    {
        var service = new Mock<IAIChat>(MockBehavior.Strict);
        Assert.IsType<BadRequestObjectResult>(await Controller(service.Object).Ask(prompt == "too-long" ? new string('x', 1001) : prompt, default));
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RequestKeepsActualCatalogAndMapsGeminiToTheExistingContract()
    {
        using var handler = new Stub(HttpStatusCode.OK, Success);
        var products = Products();
        var chat = Chat(Factory(handler), products);
        var result = Assert.IsType<OkObjectResult>(await Controller(chat).Ask("Cửa hàng có sản phẩm nào?", default));
        var answer = Assert.IsType<AIResponse>(result.Value);
        Assert.Equal("Chào bạn", answer.Choices[0].Message.Content);
        Assert.Equal("assistant", answer.Choices[0].Message.Role);
        Assert.Equal("real-provider-id", answer.Id);
        Assert.Equal(15, answer.Usage.PromptTokens);
        Assert.Equal(25, answer.Usage.TotalTokens);
        Assert.Equal(Secret, handler.Key);
        Assert.Null(handler.Authorization);
        Assert.Equal("https://generativelanguage.googleapis.com/v1beta/models/gemini-3.5-flash-lite:generateContent", handler.Url);
        Assert.DoesNotContain(Secret, handler.Url!);
        Assert.DoesNotContain(Secret, handler.Body);
        Assert.DoesNotContain("NOT_SENT_DESCRIPTION", handler.Body);
        using var payload = JsonDocument.Parse(handler.Body);
        var catalog = payload.RootElement.GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("text").GetString()!;
        Assert.Contains("ORIGINAL_CATALOG_NAME", catalog);
        Assert.Contains("123000", catalog);
        Assert.True(payload.RootElement.TryGetProperty("systemInstruction", out _));
        var generation = payload.RootElement.GetProperty("generationConfig");
        Assert.Equal(2048, generation.GetProperty("maxOutputTokens").GetInt32());
        Assert.Equal("minimal", generation.GetProperty("thinkingConfig").GetProperty("thinkingLevel").GetString());
        Assert.False(generation.GetProperty("thinkingConfig").GetProperty("includeThoughts").GetBoolean());
        await chat.AskAsync("hello");
        products.Verify(p => p.GetProductsAsync(), Times.Once);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task MissingKeyDoesNotReadCatalogOrCallProvider()
    {
        var factory = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        var products = new Mock<IProductManage>(MockBehavior.Strict);
        var result = Assert.IsType<ObjectResult>(await Controller(Chat(factory.Object, products, settings: new GeminiOptions())).Ask("hello", default));
        Assert.Equal(503, result.StatusCode);
        AssertCommonError(result);
        factory.VerifyNoOtherCalls(); products.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("Gợi ý sản phẩm")]
    [InlineData("Tư vấn sản phẩm")]
    [InlineData("So sánh sản phẩm")]
    [InlineData("Sản phẩm giá rẻ")]
    public async Task AhpStillRunsWithoutAnyGeminiKeyOrProvider(string prompt)
    {
        var ahp = new Mock<IAHPRecommendationService>();
        ahp.Setup(a => a.GenerateRecommendationResponseAsync(prompt, It.IsAny<string>())).ReturnsAsync("ACTUAL_AHP_RESULT");
        var factory = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        var result = await Chat(factory.Object, new Mock<IProductManage>(MockBehavior.Strict), ahp, new GeminiOptions()).AskAsync(prompt);
        Assert.Equal("ACTUAL_AHP_RESULT", result.Choices[0].Message.Content);
        factory.VerifyNoOtherCalls(); ahp.VerifyAll();
    }

    [Theory]
    [InlineData(400, "INVALID_ARGUMENT", "API_KEY_INVALID", "invalid_key", 503, false)]
    [InlineData(400, "FAILED_PRECONDITION", "BILLING_DISABLED", "billing_disabled", 503, false)]
    [InlineData(400, "FAILED_PRECONDITION", "", "project_not_ready", 503, false)]
    [InlineData(400, "INVALID_ARGUMENT", "", "request_rejected", 502, false)]
    [InlineData(401, "UNAUTHENTICATED", "", "invalid_key", 503, false)]
    [InlineData(403, "PERMISSION_DENIED", "SERVICE_DISABLED", "permission_denied", 503, false)]
    [InlineData(404, "NOT_FOUND", "", "model_unavailable", 503, false)]
    [InlineData(429, "RESOURCE_EXHAUSTED", "QUOTA_EXCEEDED", "quota_exceeded", 429, true)]
    [InlineData(503, "UNAVAILABLE", "", "provider_unavailable", 502, true)]
    [InlineData(504, "DEADLINE_EXCEEDED", "", "provider_timeout", 504, true)]
    [InlineData(500, "PRIVATE_PROVIDER_SECRET", "PRIVATE_PROVIDER_SECRET", "provider_unavailable", 502, true)]
    public async Task AllHttpErrorsHaveOnePublicMessageButDistinctSafeLogs(int status, string googleStatus, string reason, string classification, int expected, bool retryable)
    {
        var payload = JsonSerializer.Serialize(new { error = new { status = googleStatus, message = "PRIVATE_PROVIDER_ACCOUNT_DETAIL " + Secret, details = new[] { new { reason, metadata = new { key = Secret } } } } });
        using var handler = new Stub((HttpStatusCode)status, payload);
        var logger = new TestLogger();
        var result = Assert.IsType<ObjectResult>(await Controller(Chat(Factory(handler)), logger).Ask("PRIVATE_USER_PROMPT", default));
        Assert.Equal(expected, result.StatusCode);
        AssertCommonError(result);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result.Value));
        Assert.Equal(retryable, json.RootElement.GetProperty("retryable").GetBoolean());
        var logged = string.Join('\n', logger.Messages);
        Assert.Contains("Reason=" + classification, logged);
        Assert.Contains("ProviderHttpStatus=" + status, logged);
        Assert.Contains("TEST_TRACE_ID", logged);
        Assert.Contains("ElapsedMs=", logged);
        Assert.DoesNotContain("PRIVATE_", logged);
        Assert.Equal(1, handler.Calls); // no automatic retries or provider fallback
    }

    [Theory]
    [InlineData("{}", "empty_response", true)]
    [InlineData("not-json", "invalid_json", true)]
    [InlineData("{\"promptFeedback\":{\"blockReason\":\"SAFETY\"}}", "prompt_blocked", false)]
    [InlineData("{\"candidates\":[{\"finishReason\":\"SAFETY\"}]}", "response_blocked", false)]
    [InlineData("{\"candidates\":[{\"finishReason\":\"MAX_TOKENS\",\"content\":{\"parts\":[{\"text\":\"partial answer\"}]}}]}", "output_truncated", false)]
    [InlineData("{\"candidates\":[{\"finishReason\":\"STOP\",\"content\":{\"parts\":[{\"text\":\"private thought\",\"thought\":true}]}}]}", "invalid_text", true)]
    [InlineData("{\"candidates\":[{\"finishReason\":\"UNKNOWN\"}]}", "invalid_finish_reason", true)]
    public async Task InvalidSuccessfulResponsesAreNotShownAsAnswers(string payload, string reason, bool retryable)
    {
        using var handler = new Stub(HttpStatusCode.OK, payload);
        var logger = new TestLogger();
        var result = Assert.IsType<ObjectResult>(await Controller(Chat(Factory(handler)), logger).Ask("hello", default));
        AssertCommonError(result);
        Assert.Contains("Reason=" + reason, string.Join('\n', logger.Messages));
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result.Value));
        Assert.Equal(retryable, json.RootElement.GetProperty("retryable").GetBoolean());
    }

    [Theory]
    [InlineData("network", 502)]
    [InlineData("timeout", 504)]
    [InlineData("internal", 500)]
    public async Task NonHttpFailuresAlsoUseTheSameMessageAndNeverLogRawExceptionText(string kind, int status)
    {
        var service = new Mock<IAIChat>();
        Exception error = kind switch { "network" => new HttpRequestException(Secret), "timeout" => new TaskCanceledException(Secret), _ => new Exception(Secret) };
        service.Setup(s => s.AskAsync("hello", It.IsAny<CancellationToken>())).ThrowsAsync(error);
        var logger = new TestLogger();
        var result = Assert.IsType<ObjectResult>(await Controller(service.Object, logger).Ask("hello", default));
        Assert.Equal(status, result.StatusCode); AssertCommonError(result);
        Assert.DoesNotContain(Secret, string.Join('\n', logger.Messages));
    }

    [Fact]
    public async Task CancellationReachesTheProviderAndStopsWithoutRetry()
    {
        using var cancel = new CancellationTokenSource();
        using var handler = new WaitingStub();
        var task = Chat(Factory(handler)).AskAsync("hello", cancel.Token);
        await handler.Started.Task;
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.True(handler.Token.IsCancellationRequested);
    }

    [Fact]
    public async Task ProviderTimeoutStopsTheHttpCallAndReturnsTheCommonError()
    {
        using var handler = new WaitingStub();
        var options = Settings(); options.TimeoutSeconds = 1;
        var result = Assert.IsType<ObjectResult>(await Controller(Chat(Factory(handler), settings: options)).Ask("hello", default));
        Assert.Equal(504, result.StatusCode); AssertCommonError(result);
        Assert.True(handler.Token.IsCancellationRequested);
    }

    [Theory]
    [InlineData("https://evil.invalid", 45, 2048)]
    [InlineData("gemini-good", 46, 2048)]
    [InlineData("gemini-good", 45, 0)]
    public async Task InvalidConfigurationNeverSendsTheKeyToANetworkTarget(string model, int timeout, int outputTokens)
    {
        var factory = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        var options = Settings(); options.Model = model; options.TimeoutSeconds = timeout; options.MaxOutputTokens = outputTokens;
        var result = Assert.IsType<ObjectResult>(await Controller(Chat(factory.Object, new Mock<IProductManage>(MockBehavior.Strict), settings: options)).Ask("hello", default));
        Assert.Equal(503, result.StatusCode); AssertCommonError(result);
        factory.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("GEMINI_API_KEY=test-only", "test-only")]
    [InlineData("GEMINI_API_KEY=\"test-only\" # comment", "test-only")]
    [InlineData("export GEMINI_API_KEY='test-only'", "test-only")]
    [InlineData("GEMINI_API_KEY=test-only # comment", "test-only")]
    [InlineData("HF_TOKEN=ignored\nOTHER_SECRET=ignored\nGEMINI_API_KEY=", null)]
    [InlineData("GEMINI_API_KEY=\"unterminated", null)]
    public void EnvParserOnlyReadsTheNewKey(string content, string? expected)
    {
        WithEnv(content, (_, contentRoot) => Assert.Equal(expected, LocalChatSettings.ReadToken(contentRoot)));
    }

    [Fact]
    public void ConfigurationPrecedenceAndExistingEnvLocationArePreserved()
    {
        WithEnv("GEMINI_API_KEY=env-only", (directory, contentRoot) => {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GEMINI_API_KEY"] = "explicit-only", ["Gemini:ApiKey"] = "section-only" }).Build();
            using var explicitServices = new ServiceCollection().AddChatbot(config, contentRoot).BuildServiceProvider();
            Assert.Equal("explicit-only", explicitServices.GetRequiredService<IOptions<GeminiOptions>>().Value.ApiKey);
            using var localServices = new ServiceCollection().AddChatbot(new ConfigurationBuilder().Build(), contentRoot).BuildServiceProvider();
            Assert.Equal("env-only", localServices.GetRequiredService<IOptions<GeminiOptions>>().Value.ApiKey);
            File.WriteAllText(Path.Combine(contentRoot, ".env"), "GEMINI_API_KEY=project-only");
            Assert.Equal("project-only", LocalChatSettings.ReadToken(contentRoot));
            File.Delete(Path.Combine(contentRoot, ".env")); File.Delete(Path.Combine(directory, ".env"));
            var fallbackConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Gemini:ApiKey"] = "section-only" }).Build();
            using var fallback = new ServiceCollection().AddChatbot(fallbackConfig, contentRoot).BuildServiceProvider();
            Assert.Equal("section-only", fallback.GetRequiredService<IOptions<GeminiOptions>>().Value.ApiKey);
        });
    }

    private static void WithEnv(string content, Action<string, string> test)
    {
        var directory = Path.Combine(Path.GetTempPath(), "motoshop-gemini-test-" + Guid.NewGuid());
        var contentRoot = Path.Combine(directory, "Infrastructure");
        Directory.CreateDirectory(contentRoot);
        try
        {
            File.WriteAllText(Path.Combine(directory, "OnlineShop.sln"), "test marker");
            File.WriteAllText(Path.Combine(directory, ".env"), content);
            test(directory, contentRoot);
        }
        finally { Directory.Delete(directory, true); }
    }

    private sealed class TestLogger : ILogger<ChatController>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }
    private sealed class Stub(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string? Key { get; private set; }
        public string? Authorization { get; private set; }
        public string? Url { get; private set; }
        public string Body { get; private set; } = "";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++; Key = request.Headers.GetValues("x-goog-api-key").Single(); Authorization = request.Headers.Authorization?.ToString();
            Url = request.RequestUri!.ToString(); Body = await request.Content!.ReadAsStringAsync(token);
            return new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
    private sealed class WaitingStub : HttpMessageHandler
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CancellationToken Token { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        { Token = token; Started.SetResult(); await Task.Delay(Timeout.Infinite, token); throw new InvalidOperationException(); }
    }
}
