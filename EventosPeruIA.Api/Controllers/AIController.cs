using EventosPeruIA.Api.DTOs;
using EventosPeruIA.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace EventosPeruIA.Api.Controllers
{
    [ApiController]
    [Route("api/ai")]
    public class AIController : ControllerBase
    {
        private readonly IAIService _aiService;

        public AIController(IAIService aiService)
        {
            _aiService = aiService;
        }

        [HttpPost("chat")]
        public async Task<ActionResult<AIChatResponse>> Chat(AIChatRequest request)
        {
            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var response = await _aiService.ChatAsync(request.Message);

            return Ok(response);
        }
    }
}
