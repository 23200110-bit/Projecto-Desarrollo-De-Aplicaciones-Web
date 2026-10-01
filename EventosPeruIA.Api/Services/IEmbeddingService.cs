namespace EventosPeruIA.Api.Services
{
    public interface IEmbeddingService
    {
        Task<float[]> GenerateEmbeddingAsync(string text);

        float CosineSimilarity(float[] vectorA, float[] vectorB);
    }
}