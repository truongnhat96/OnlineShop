using Infrastructure.AIChat.Model;

namespace Infrastructure.AIChat
{
    public interface IAIChat
    {
        public Task<AIResponse> AskAsync(string prompt, CancellationToken cancellationToken = default);
    }
}
