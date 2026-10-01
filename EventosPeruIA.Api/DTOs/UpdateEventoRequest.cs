namespace EventosPeruIA.Api.DTOs
{
    public class UpdateEventoRequest
    {
        public int CategoriaId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public DateTime FechaInicio { get; set; }
        public string Ubicacion { get; set; } = string.Empty;
        public int Aforo { get; set; }
        public decimal Precio { get; set; }
    }
}
