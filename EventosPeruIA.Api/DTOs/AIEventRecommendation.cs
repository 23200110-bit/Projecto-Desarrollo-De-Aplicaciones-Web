namespace EventosPeruIA.Api.DTOs
{
    public class AIEventRecommendation
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
        public string Ubicacion { get; set; } = string.Empty;
        public decimal Precio { get; set; }
        public int? EntradasDisponibles { get; set; }
        public string Estado { get; set; } = string.Empty;
    }
}