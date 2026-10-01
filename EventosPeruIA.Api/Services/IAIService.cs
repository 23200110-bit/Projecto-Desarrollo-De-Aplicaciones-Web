using EventosPeruIA.Api.DTOs;

namespace EventosPeruIA.Api.Services
{
    public interface IAIService
    {
        Task<AIChatResponse> ChatAsync(string message);
    }
}
