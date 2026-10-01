using Microsoft.Extensions.AI;

namespace EventosPeruIA.Api.Services
{
    public class EmbeddingService : IEmbeddingService
    {
        private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;

        public EmbeddingService(
            IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
        {
            _embeddingGenerator = embeddingGenerator;
        }

        public async Task<float[]> GenerateEmbeddingAsync(string text)
        {
            var result = await _embeddingGenerator.GenerateAsync(text);

            return result.Vector.ToArray();
        }

        public float CosineSimilarity(float[] vectorA, float[] vectorB)
        {
            if (vectorA.Length != vectorB.Length)
                throw new ArgumentException("Los vectores deben tener la misma dimensión.");

            double dotProduct = 0;
            double magnitudeA = 0;
            double magnitudeB = 0;

            for (int i = 0; i < vectorA.Length; i++)
            {
                dotProduct += vectorA[i] * vectorB[i];
                magnitudeA += vectorA[i] * vectorA[i];
                magnitudeB += vectorB[i] * vectorB[i];
            }

            if (magnitudeA == 0 || magnitudeB == 0)
                return 0;

            return (float)(
                dotProduct /
                (Math.Sqrt(magnitudeA) * Math.Sqrt(magnitudeB))
            );
        }
    }
}