using System.ComponentModel.DataAnnotations;

namespace EventosPeruIA.Api.DTOs
{
    public class AIChatRequest
    {
        [Required]
        public string Message { get; set; } = string.Empty;
    }
}
