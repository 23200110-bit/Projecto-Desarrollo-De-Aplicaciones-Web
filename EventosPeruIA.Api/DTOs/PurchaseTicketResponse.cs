using System;

namespace EventosPeruIA.Api.DTOs
{
    public class PurchaseTicketResponse
    {
        public Guid TicketId { get; set; }
        public string QRPayload { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public int EventoId { get; set; }
    }
}
