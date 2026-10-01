using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EventosPeruIA.Api.Data;
using EventosPeruIA.Api.DTOs;
using EventosPeruIA.Api.Models;

namespace EventosPeruIA.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TicketsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public TicketsController(ApplicationDbContext db)
        {
            _db = db;
        }

        [HttpPost("purchase")]
        public async Task<IActionResult> Purchase([FromBody] PurchaseTicketRequest request)
        {
            if (request.Cantidad <= 0)
                return BadRequest(new { message = "Cantidad debe ser mayor que cero." });

            // Use serializable transaction to avoid sobreventa en escenarios concurrentes
            using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            var evento = await _db.Eventos
                .Where(e => e.Id == request.EventId)
                .FirstOrDefaultAsync();

            if (evento == null)
            {
                await transaction.RollbackAsync();
                return NotFound(new { message = "Evento no encontrado." });
            }

            if (evento.Aforo < request.Cantidad)
            {
                await transaction.RollbackAsync();
                return BadRequest(new { message = "No hay suficientes cupos disponibles." });
            }

            evento.Aforo -= request.Cantidad;

            var ticket = new Ticket
            {
                Id = Guid.NewGuid(),
                EventoId = request.EventId,
                UsuarioId = request.UserId,
                Cantidad = request.Cantidad,
                Estado = EstadoTicket.Valida,
                FechaCompra = DateTime.UtcNow,
                QRPayload = "" // set after Id generated
            };

            ticket.QRPayload = ticket.Id.ToString();

            _db.Tickets.Add(ticket);

            // Añadido try/catch para capturar información diagnóstica sin alterar la lógica previa
            try
            {
                // Información útil para depuración en tiempo de ejecución
                var conn = _db.Database.GetDbConnection();
                var ticketEntity = _db.Model.FindEntityType(typeof(EventosPeruIA.Api.Models.Ticket));
                var tableName = ticketEntity?.GetTableName();
                var schema = ticketEntity?.GetSchema() ?? "dbo";

                System.Diagnostics.Debug.WriteLine($"[TicketsController] Guardando ticket. Database='{conn?.Database}', DataSource='{conn?.DataSource}', Tabla='{schema}.{tableName}'");

                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Registrar contexto para ayudar a identificar causas como 'Invalid object name'
                var conn = _db.Database.GetDbConnection();
                var ticketEntity = _db.Model.FindEntityType(typeof(EventosPeruIA.Api.Models.Ticket));
                var tableName = ticketEntity?.GetTableName();
                var schema = ticketEntity?.GetSchema() ?? "dbo";

                System.Diagnostics.Debug.WriteLine($"[TicketsController] Error guardando ticket. Conn='{conn?.ConnectionString}', Database='{conn?.Database}', Tabla='{schema}.{tableName}', Ex='{ex}'");

                // Re-lanzar para mantener el comportamiento actual (y que se vea la excepción en VS)
                throw;
            }
            // Bloque con _logger eliminado para evitar duplicación.
            // Se mantiene el bloque diagnóstico previo que usa System.Diagnostics.Debug.WriteLine.
            await transaction.CommitAsync();

            var response = new PurchaseTicketResponse
            {
                TicketId = ticket.Id,
                QRPayload = ticket.QRPayload,
                Cantidad = ticket.Cantidad,
                EventoId = ticket.EventoId
            };

            return CreatedAtAction(nameof(GetById), new { id = ticket.Id }, response);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById([FromRoute] Guid id)
        {
            var ticket = await _db.Tickets
                .Include(t => t.Evento)
                .Include(t => t.Usuario)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (ticket == null) return NotFound();

            return Ok(ticket);
        }
    }
}
