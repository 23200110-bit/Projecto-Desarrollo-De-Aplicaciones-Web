namespace EventosPeruIA.Api.DTOs
{
	public class ValidateQrRequest
	{
		public int EventoId { get; set; }
		// Identificador del ticket según la implementación de Cristofer (Guid)
		public Guid TicketId { get; set; }
	}
}
