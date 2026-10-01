namespace EventosPeruIA.Api.DTOs
{
    public class CancelEventoRequest
    {
        // FuerzaMayor, OrdenAutoridad, ProblemaRecinto, AusenciaArtista, ClimaAdverso, Seguridad, Otro
        public string Motivo { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
    }
}
