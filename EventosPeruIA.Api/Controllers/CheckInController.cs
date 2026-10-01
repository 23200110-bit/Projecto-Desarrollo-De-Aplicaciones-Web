using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EventosPeruIA.Api.Data;
using EventosPeruIA.Api.DTOs;
using EventosPeruIA.Api.Models;

namespace EventosPeruIA.Api.Controllers
{
	[ApiController]
	[Route("api/tickets")]
	//[Authorize]
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
			if (request.TicketId == Guid.Empty)
			{
				return BadRequest(new { message = "El identificador del ticket es obligatorio." });
			}

			// Buscar el ticket por Id (la implementación de Cristofer usa Guid en Ticket.Id y QRPayload contiene el Guid en string)
			var ticket = await _context.Tickets
				.FirstOrDefaultAsync(t => t.Id == request.TicketId);

			if (ticket == null || ticket.EventoId != request.EventoId)
			{
				return NotFound(new { message = "El ticket no existe o no corresponde al evento" });
			}

			if (ticket.Estado == EstadoTicket.Utilizada)
			{
				return BadRequest(new { message = "El ticket ya ha sido utilizado previamente" });
			}

			if (ticket.Estado == EstadoTicket.Anulada)
			{
				return BadRequest(new { message = "El ticket se encuentra anulado" });
			}

			// Estado válido -> marcar como utilizada
			ticket.Estado = EstadoTicket.Utilizada;
			await _context.SaveChangesAsync();

			return Ok(new
			{
				success = true,
				message = "Check-in exitoso. Entrada marcada como Utilizada.",
				estado = ticket.Estado.ToString(),
				fechaValidacion = DateTime.UtcNow
			});
		}
	}
}