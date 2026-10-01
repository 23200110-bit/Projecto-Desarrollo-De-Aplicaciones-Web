using EventosPeruIA.Api.Models;

namespace EventosPeruIA.Api.Services
{
    public interface IQdrantService
    {
        Task EnsureCollectionAsync(int vectorSize);

        Task IndexEventAsync(Evento evento, float[] embedding);

        Task<List<int>> SearchEventIdsAsync(
            float[] queryEmbedding,
            int limit = 5
        );

        Task DeleteEventAsync(int eventoId);
    }
}