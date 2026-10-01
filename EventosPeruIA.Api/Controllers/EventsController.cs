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

        
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Listar()
        {
            var eventos = await _context.Eventos
                .Where(e => e.Estado == "Publicado")
                .ToListAsync();

            return Ok(eventos);
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