namespace Infrastructure.AIChat;

public sealed class ChatUnavailableException : Exception
{
    public ChatUnavailableException() : base("Chat provider is not configured.") { }
}
