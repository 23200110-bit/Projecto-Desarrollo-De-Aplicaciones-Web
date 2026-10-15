using EventosPeruIA.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace EventosPeruIA.Api.Services
{
    public class RagIndexService : IRagIndexService
    {
        private static readonly SemaphoreSlim Gate = new(1, 1);
        private readonly ApplicationDbContext _context;
        private readonly IEmbeddingService _embeddingService;
        private readonly IQdrantService _qdrantService;

        public RagIndexService(
            ApplicationDbContext context,
            IEmbeddingService embeddingService,
            IQdrantService qdrantService)
        {
            _context = context;
            _embeddingService = embeddingService;
            _qdrantService = qdrantService;
        }

        public async Task<int> ReindexEventsAsync()
        {
            await Gate.WaitAsync();
            try
            {
                var eventos = await _context.Eventos.AsNoTracking()
                    .Include(e => e.Categoria)
                    .Where(e => e.Estado == "Publicado")
                    .Where(e => e.FechaInicio > DateTime.UtcNow)
                    .ToListAsync();

                var available = await EventAvailability.GetAsync(_context, eventos);
                eventos = eventos.Where(e => available[e.Id] > 0).ToList();
                await _qdrantService.RemoveObsoleteEventsAsync(eventos.Select(e => e.Id).ToArray());
                foreach (var evento in eventos)
                {
                    var textoEvento = $"""
                    Nombre: {evento.Nombre}
                    Descripción: {evento.Descripcion}
                    Categoría: {evento.Categoria?.Nombre ?? "Sin categoría"}
                    Ubicación: {evento.Ubicacion}
                    Fecha: {evento.FechaInicio:dd/MM/yyyy HH:mm}
                    Precio: S/{evento.Precio}
                    """;

                    var embedding =
                        await _embeddingService.GenerateEmbeddingAsync(textoEvento);

                    await _qdrantService.IndexEventAsync(
                        evento,
                        embedding
                    );
                }

                return eventos.Count;
            }
            finally { Gate.Release(); }
        }
    }
}