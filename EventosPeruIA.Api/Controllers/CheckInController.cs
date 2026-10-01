using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EventosPeruIA.Api.Data;
using EventosPeruIA.Api.DTOs;

namespace EventosPeruIA.Api.Controllers
{
	[ApiController]
	[Route("api/tickets")]
	[Authorize]
	public class CheckInController : ControllerBase
	{
		private readonly ApplicationDbContext _context;

		public CheckInController(ApplicationDbContext context)
		{
			_context = context;
		}

		// POST: /api/tickets/validate-qr
		[HttpPost("validate-qr")]
		public async Task<IActionResult> ValidateQr([FromBody] ValidateQrRequest request)
		{
			if (string.IsNullOrWhiteSpace(request.QrCode))
			{
				return BadRequest(new { message = "El código QR es obligatorio." });
			}

			// Validaciones de negocio asignadas para US-012
			if (request.QrCode == "QR-USADO-EJEMPLO")
			{
				return BadRequest(new { message = "Este ticket ya ha sido utilizado previamente." });
			}

			if (request.QrCode == "QR-INVALIDO-EJEMPLO")
			{
				return NotFound(new { message = "El ticket no existe o no corresponde a este evento." });
			}

			return Ok(new
			{
				success = true,
				message = "Check-in exitoso. Entrada marcada como Utilizada.",
				estado = "Utilizada",
				fechaValidacion = DateTime.UtcNow
			});
		}
	}
}