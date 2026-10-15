using System.ComponentModel.DataAnnotations;

namespace EventosPeruIA.Api.DTOs
{
    public class AIChatRequest
    {
        [Required]
        [StringLength(2000, MinimumLength = 1)]
        public string Message { get; set; } = string.Empty;
    }
}
