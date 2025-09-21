using Infrastructure.AIChat;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Infrastructure.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChatController : ControllerBase
    {
        private readonly IAIChat _aiChat;
        private readonly ILogger<ChatController> _logger;
        public ChatController(IAIChat aiChat, ILogger<ChatController> logger)
        {
            _aiChat = aiChat;
            _logger = logger;
        }
        [HttpPost("ask")]
        public async Task<IActionResult> Ask([FromBody] string prompt, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                return BadRequest("Prompt cannot be empty.");
            }
            try
            {
                var response = await _aiChat.AskAsync(prompt, cancellationToken);
                return Ok(response);
            }
            catch(Exception ex)
            {
                // Log the exception (not shown here for brevity)
                _logger.LogError(ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }
    }
}
