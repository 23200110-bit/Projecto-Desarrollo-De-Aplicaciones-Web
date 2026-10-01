namespace EventosPeruIA.Api.Services
{
    public interface IRagIndexService
    {
        Task<int> ReindexEventsAsync();
    }
}