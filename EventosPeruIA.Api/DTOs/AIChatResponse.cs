namespace EventosPeruIA.Api.DTOs
{
    public class AIChatResponse
    {
        public string Answer { get; set; } = string.Empty;
        public List<AIEventRecommendation> Events { get; set; } = new();
    }
}
