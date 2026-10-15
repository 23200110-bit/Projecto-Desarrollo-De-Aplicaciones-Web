using EventosPeruIA.Api.Data;
using EventosPeruIA.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace EventosPeruIA.Api.Services;

public static class EventAvailability
{
    public static async Task<Dictionary<int, int>> GetAsync(ApplicationDbContext db, IEnumerable<Evento> events)
    {
        var items = events.ToList();
        var ids = items.Select(e => e.Id).ToList();
        var occupied = await db.Tickets.AsNoTracking()
            .Where(t => ids.Contains(t.EventoId) && t.Estado != EstadoTicket.Anulada)
            .GroupBy(t => t.EventoId)
            .Select(g => new { Id = g.Key, Quantity = g.Sum(t => t.Cantidad) })
            .ToDictionaryAsync(t => t.Id, t => t.Quantity);
        return items.ToDictionary(e => e.Id, e => Math.Max(0, e.Aforo - occupied.GetValueOrDefault(e.Id)));
    }
}
