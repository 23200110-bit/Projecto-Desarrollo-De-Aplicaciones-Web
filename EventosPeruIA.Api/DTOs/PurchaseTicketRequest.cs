namespace EventosPeruIA.Api.DTOs
{
    public class PurchaseTicketRequest
    {
        public int EventId { get; set; }
        public int UserId { get; set; }
        public int Cantidad { get; set; }
    }
}
