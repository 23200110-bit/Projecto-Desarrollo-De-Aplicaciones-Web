using EventosPeruIA.Api.DTOs;
using EventosPeruIA.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

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

            if (string.IsNullOrWhiteSpace(request.Message))
                return BadRequest(new { message = "Escribe una consulta sobre eventos." });
            try
            {
                return Ok(await _aiService.ChatAsync(request.Message));
            }
            catch (Exception ex) when (ex is HttpRequestException or Grpc.Core.RpcException or TimeoutException or TaskCanceledException)
            {
                return StatusCode(503, new { message = "El asistente no está disponible temporalmente. Inténtalo más tarde." });
            }
        }


        [HttpPost("reindex")]
        [Authorize(Policy = "AIReindex")]
        public async Task<IActionResult> Reindex()
        {
            int cantidad;
            try { cantidad = await _ragIndexService.ReindexEventsAsync(); }
            catch (Exception ex) when (ex is HttpRequestException or Grpc.Core.RpcException or TimeoutException or TaskCanceledException)
            {
                return StatusCode(503, new { message = "No se pudo completar la reindexación. Inténtalo más tarde." });
            }

            return Ok(new
            {
                mensaje = "Reindexación completada",
                eventosIndexados = cantidad
            });
        }
    }
}
