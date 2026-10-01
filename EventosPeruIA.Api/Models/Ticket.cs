using System;

namespace EventosPeruIA.Api.Models
{
    public enum EstadoTicket
    {
        Valida,
        Utilizada,
        Anulada
    }

    public class Ticket
    {
        public Guid Id { get; set; }

        public int EventoId { get; set; }
        public Evento? Evento { get; set; }

        public int UsuarioId { get; set; }
        public Usuario? Usuario { get; set; }

        public int Cantidad { get; set; }

        public EstadoTicket Estado { get; set; } = EstadoTicket.Valida;

        public DateTime FechaCompra { get; set; } = DateTime.UtcNow;

        // Payload usable para renderizar un QR (por ejemplo el Guid)
        public string QRPayload { get; set; } = string.Empty;
    }
}
