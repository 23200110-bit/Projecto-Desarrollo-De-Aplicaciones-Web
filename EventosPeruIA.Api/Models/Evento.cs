namespace EventosPeruIA.Api.Models
{
    public class Evento
    {
        public int Id { get; set; }

        public int OrganizadorId { get; set; }
        public Usuario? Organizador { get; set; }

        public int CategoriaId { get; set; }
        public Categoria? Categoria { get; set; }

        public string Nombre { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;

        public DateTime FechaInicio { get; set; }
        public string Ubicacion { get; set; } = string.Empty;

        public int Aforo { get; set; }
        public decimal Precio { get; set; }

        // Borrador, Publicado, Finalizado, Cancelado
        public string Estado { get; set; } = "Borrador";

        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

        // Datos de cancelación (US-009)
        public string? MotivoCancelacion { get; set; }
        public string? DescripcionCancelacion { get; set; }
        public DateTime? FechaCancelacion { get; set; }
        public int? CanceladoPorId { get; set; }
    }
}
