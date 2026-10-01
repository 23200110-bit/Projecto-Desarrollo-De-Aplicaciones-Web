namespace EventosPeruIA.Api.DTOs
{
	public class ValidateQrRequest
	{
		public int EventoId { get; set; }
		public string QrCode { get; set; } = string.Empty;
	}
}
