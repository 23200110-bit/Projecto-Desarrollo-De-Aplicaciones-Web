using EventosPeruIA.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace EventosPeruIA.Api.Services
{
    public class RagIndexService : IRagIndexService
    {
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
            var eventos = await _context.Eventos
                .Include(e => e.Categoria)
                .Where(e => e.Estado == "Publicado")
                .Where(e => e.FechaInicio > DateTime.UtcNow)
                .ToListAsync();

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
    }
}