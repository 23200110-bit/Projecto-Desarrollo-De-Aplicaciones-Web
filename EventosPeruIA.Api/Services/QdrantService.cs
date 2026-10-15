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
            int limit = 5,
            IReadOnlyCollection<int>? eligibleEventIds = null)
        {
            if (eligibleEventIds?.Count == 0) return [];
            if (!(await _client.ListCollectionsAsync()).Contains(_collection)) return [];
            var results = await _client.SearchAsync(
                collectionName: _collection,
                vector: queryEmbedding,
                filter: eligibleEventIds == null ? null : new Filter
                {
                    Must = { new Condition { HasId = new HasIdCondition
                        { HasId = { eligibleEventIds.Select(id => new PointId { Num = (ulong)id }) } } } }
                },
                limit: (ulong)limit
            );

            return results
                .Select(r => (int)r.Id.Num)
                .ToList();
        }

        public async Task RemoveObsoleteEventsAsync(IReadOnlyCollection<int> activeIds)
        {
            if (!(await _client.ListCollectionsAsync()).Contains(_collection)) return;
            var keep = activeIds.Select(id => (ulong)id).ToHashSet();
            PointId? offset = null;
            do
            {
                var page = await _client.ScrollAsync(_collection, limit: 256, offset: offset,
                    payloadSelector: false, vectorsSelector: false);
                var stale = page.Result.Where(p => p.Id.PointIdOptionsCase != PointId.PointIdOptionsOneofCase.Num
                    || !keep.Contains(p.Id.Num)).Select(p => p.Id).ToArray();
                if (stale.Length > 0) await _client.DeleteAsync(_collection, stale, wait: true);
                offset = page.NextPageOffset;
            } while (offset != null);
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
