namespace EventosPeruIA.Api.DTOs
{
    public class ResetPasswordRequest
    {
        public string Token { get; set; } = string.Empty;
        public string NuevaPassword { get; set; } = string.Empty;
    }
}