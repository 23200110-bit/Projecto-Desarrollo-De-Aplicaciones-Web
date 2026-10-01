namespace EventosPeruIA.Api.Models
{
    public class Usuario
    {
        public int Id { get; set; }

        public string Nombre { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        public string Rol { get; set; } = "Usuario";
        public string? ResetToken { get; set; }
        public DateTime? ResetTokenExpira { get; set; }
    }
}