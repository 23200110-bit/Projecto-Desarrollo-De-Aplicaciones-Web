using EventosPeruIA.Api.Models;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace EventosPeruIA.Api.Services
{
    public class QdrantService : IQdrantService
    {
        private readonly QdrantClient _client;
        private readonly string _collection;

        public QdrantService(
            QdrantClient client,
            IConfiguration configuration)
        {
            _client = client;
            _collection =
                configuration["Qdrant:Collection"]
                ?? "eventos";
        }

        public async Task EnsureCollectionAsync(int vectorSize)
        {
            var collections = await _client.ListCollectionsAsync();

            var exists = collections
                .Any(c => c == _collection);

            if (exists)
                return;

            await _client.CreateCollectionAsync(
                collectionName: _collection,
                vectorsConfig: new VectorParams
                {
                    Size = (ulong)vectorSize,
                    Distance = Distance.Cosine
                }
            );
        }

        public async Task IndexEventAsync(
            Evento evento,
            float[] embedding)
        {
            await EnsureCollectionAsync(embedding.Length);

            var point = new PointStruct
            {
                Id = (ulong)evento.Id,
                Vectors = embedding
            };

            point.Payload.Add(
                "eventoId",
                evento.Id
            );

            point.Payload.Add(
                "nombre",
                evento.Nombre
            );

            point.Payload.Add(
                "estado",
                evento.Estado
            );

            await _client.UpsertAsync(
                collectionName: _collection,
                points: new[]
                {
                    point
                }
            );
        }

        public async Task<List<int>> SearchEventIdsAsync(
            float[] queryEmbedding,
            int limit = 5)
        {
            var results = await _client.SearchAsync(
                collectionName: _collection,
                vector: queryEmbedding,
                limit: (ulong)limit
            );

            return results
                .Select(r => (int)r.Id.Num)
                .ToList();
        }

        public async Task DeleteEventAsync(int eventoId)
        {
            await _client.DeleteAsync(
                collectionName: _collection,
                ids: new[]
                {
                    new PointId
                    {
                        Num = (ulong)eventoId
                    }
                }
            );
        }
    }
}
