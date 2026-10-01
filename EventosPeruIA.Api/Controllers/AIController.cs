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
        private readonly IRagIndexService _ragIndexService;

        public AIController(
            IAIService aiService,
            IRagIndexService ragIndexService)
        {
            _aiService = aiService;
            _ragIndexService = ragIndexService;
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


        [HttpPost("reindex")]
        public async Task<IActionResult> Reindex()
        {
            var cantidad =
                await _ragIndexService.ReindexEventsAsync();

            return Ok(new
            {
                mensaje = "Reindexación completada",
                eventosIndexados = cantidad
            });
        }
    }
}
