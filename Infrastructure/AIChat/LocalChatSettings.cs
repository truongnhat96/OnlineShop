using Infrastructure.Configuration;

namespace Infrastructure.AIChat;

/// <summary>Reads only GEMINI_API_KEY; other .env values are never loaded or exported.</summary>
public static class LocalChatSettings
{
    public static string? ReadToken(string contentRoot)
    {
        var values = LocalEnvironmentFile.Read(contentRoot, "GEMINI_API_KEY");
        return values.TryGetValue("GEMINI_API_KEY", out var token) ? token : null;
    }
}
