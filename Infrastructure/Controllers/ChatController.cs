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
        public ChatController(IAIChat aiChat)
        {
            _aiChat = aiChat;
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
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }
    }
}
