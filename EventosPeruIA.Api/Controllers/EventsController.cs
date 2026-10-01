using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EventosPeruIA.Api.Data;
using EventosPeruIA.Api.DTOs;
using EventosPeruIA.Api.Models;

namespace EventosPeruIA.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class EventsController : ControllerBase
    {
        private static readonly string[] MotivosValidosConVentas =
        {
            "FuerzaMayor", "OrdenAutoridad", "ProblemaRecinto",
            "AusenciaArtista", "ClimaAdverso", "Seguridad", "Otro"
        };

        private readonly ApplicationDbContext _context;

        public EventsController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int UsuarioActualId =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

		// GET: /api/events?categoriaId=1&ubicacion=Lima&fecha=2026-10-15 FILTROS
		[HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Listar(
		    [FromQuery] int? categoriaId,
			[FromQuery] string? ubicacion,
			[FromQuery] DateTime? fecha)
        {
			var query = _context.Eventos
				.Include(e => e.Categoria)
				.Where(e => e.Estado == "Publicado")
				.AsQueryable();
			if (categoriaId.HasValue)
			{
				query = query.Where(e => e.CategoriaId == categoriaId.Value);
			}

			if (!string.IsNullOrWhiteSpace(ubicacion))
			{
				query = query.Where(e => e.Ubicacion.Contains(ubicacion));
			}

			if (fecha.HasValue)
			{
				var fechaFiltro = fecha.Value.Date;
				query = query.Where(e => e.FechaInicio.Date == fechaFiltro);
			}
			var eventos = await query.ToListAsync();
			return Ok(eventos);
		}
		// GET: /api/events/5 AFORO
        [HttpGet("{id}")]
		[AllowAnonymous]
		public async Task<IActionResult> ObtenerDetalle(int id)
        {
			var evento = await _context.Eventos
				.Include(e => e.Categoria)
				.FirstOrDefaultAsync(e => e.Id == id);

			if (evento == null)
			{
				return NotFound(new { message = $"No existe el evento con ID {id} ." });
			}

            // US-006: Cálculo de disponibilidad (Aforo y las Entradas vendidas)
            int ticketsVendidos = await _context.Tickets
                .CountAsync(t => t.EventoId == id && t.Estado != EstadoTicket.Anulada);
            int cuposDisponibles = Math.Max(0, evento.Aforo - ticketsVendidos);


			var detalle = new
			{
				evento.Id,
				evento.Nombre,
				evento.Descripcion,
				evento.FechaInicio,
				evento.Ubicacion,
				evento.Precio,
				evento.Estado,
				evento.Aforo,
				TicketsVendidos = ticketsVendidos,
				CuposDisponibles = cuposDisponibles,
				Categoria = evento.Categoria != null ? evento.Categoria.Nombre : null
			};

			return Ok(detalle);
		}


		[HttpPost]
        public async Task<IActionResult> Crear(CreateEventoRequest request)
        {
            if (request.Aforo <= 0)
                return BadRequest("El aforo debe ser un número entero mayor que cero.");

            if (request.Precio < 0)
                return BadRequest("El precio no puede ser negativo.");

            var evento = new Evento
            {
                OrganizadorId = UsuarioActualId,
                CategoriaId = request.CategoriaId,
                Nombre = request.Nombre,
                Descripcion = request.Descripcion,
                FechaInicio = request.FechaInicio,
                Ubicacion = request.Ubicacion,
                Aforo = request.Aforo,
                Precio = request.Precio,
                Estado = request.Publicar ? "Publicado" : "Borrador"
            };

            _context.Eventos.Add(evento);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(Listar), new { id = evento.Id }, evento);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Editar(int id, UpdateEventoRequest request)
        {
            var evento = await _context.Eventos.FindAsync(id);
            if (evento == null) return NotFound("El evento no existe.");

            if (evento.OrganizadorId != UsuarioActualId)
                return Forbid();

            if (evento.Estado == "Cancelado" || evento.Estado == "Finalizado")
                return BadRequest("Un evento cancelado o finalizado no puede editarse.");

            if (request.Aforo <= 0)
                return BadRequest("El aforo debe ser un número entero mayor que cero.");

            if (request.Precio < 0)
                return BadRequest("El precio no puede ser negativo.");

            evento.CategoriaId = request.CategoriaId;
            evento.Nombre = request.Nombre;
            evento.Descripcion = request.Descripcion;
            evento.FechaInicio = request.FechaInicio;
            evento.Ubicacion = request.Ubicacion;
            evento.Aforo = request.Aforo;
            evento.Precio = request.Precio;

            await _context.SaveChangesAsync();
            return Ok(evento);
        }

        [HttpPatch("{id}/cancel")]
        public async Task<IActionResult> Cancelar(int id, CancelEventoRequest request)
        {
            var evento = await _context.Eventos.FindAsync(id);
            if (evento == null) return NotFound("El evento no existe.");

            if (evento.OrganizadorId != UsuarioActualId)
                return Forbid();

            if (evento.Estado != "Borrador" && evento.Estado != "Publicado")
                return BadRequest("Solo se pueden cancelar eventos en Borrador o Publicado.");

            if (evento.FechaInicio <= DateTime.UtcNow)
                return BadRequest("El evento ya inició; no puede cancelarse.");

            if (string.IsNullOrWhiteSpace(request.Motivo) ||
                !MotivosValidosConVentas.Contains(request.Motivo))
                return BadRequest("Debes indicar un motivo de cancelación válido.");

            if (string.IsNullOrWhiteSpace(request.Descripcion))
                return BadRequest("La descripción del motivo es obligatoria.");

            evento.Estado = "Cancelado";
            evento.MotivoCancelacion = request.Motivo;
            evento.DescripcionCancelacion = request.Descripcion;
            evento.FechaCancelacion = DateTime.UtcNow;
            evento.CanceladoPorId = UsuarioActualId;

            await _context.SaveChangesAsync();
            return Ok(evento);
        }
    }
}