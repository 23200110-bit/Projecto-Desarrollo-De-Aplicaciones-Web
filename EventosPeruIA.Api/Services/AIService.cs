using Microsoft.Extensions.AI;
using EventosPeruIA.Api.Data;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using EventosPeruIA.Api.DTOs;

namespace EventosPeruIA.Api.Services
{
    public class AIService : IAIService
    {

        private readonly IChatClient _chatClient;
        private readonly ApplicationDbContext _context;

        public AIService(
            IChatClient chatClient,
            ApplicationDbContext context)
        {
            _chatClient = chatClient;
            _context = context;
        }


        public async Task<AIChatResponse> ChatAsync(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return new AIChatResponse
                {
                    Answer = "Escribe una consulta sobre los eventos disponibles."
                };
            }

            var eventos = await _context.Eventos
                .Include(e => e.Categoria)
                .Where(e => e.Estado == "Publicado")
                .Where(e => e.FechaInicio > DateTime.UtcNow)
                .OrderBy(e => e.FechaInicio)
                .Take(20)
                .ToListAsync();

            var normalized = Normalize(message);

            var category = DetectCategory(normalized);

            if (category is not null)
            {
                eventos = eventos
                    .Where(e =>
                        Normalize(e.Categoria?.Nombre ?? "") == category)
                    .ToList();
            }

            var location = DetectLocation(normalized);

            if (location is not null)
            {
                eventos = eventos
                    .Where(e =>
                        Normalize(e.Ubicacion).Contains(location))
                    .ToList();
            }

            var maxPrice = DetectMaxPrice(normalized);

            if (maxPrice.HasValue)
            {
                eventos = eventos
                    .Where(e => e.Precio < maxPrice.Value)
                    .ToList();
            }

            if (eventos.Count == 0)
            {
                return new AIChatResponse
                {
                    Answer = "Actualmente no hay eventos publicados disponibles."
                };
            }

            var contexto = string.Join(
                "\n",
                eventos.Select(e =>
                    $"- ID: {e.Id}" +
                    $" | Nombre: {e.Nombre}" +
                    $" | Descripción: {e.Descripcion}" +
                    $" | Categoría: {e.Categoria?.Nombre ?? "Sin categoría"}" +
                    $" | Fecha: {e.FechaInicio:dd/MM/yyyy HH:mm}" +
                    $" | Ubicación: {e.Ubicacion}" +
                    $" | Precio: S/{e.Precio}" +
                    $" | Aforo: {e.Aforo}" +
                    $" | Estado: {e.Estado}")
            );

            var prompt = $"""
Eres el asistente oficial de EventosPeruIA.

Responde siempre en español de forma clara y breve.

Debes responder únicamente utilizando los eventos proporcionados en el contexto.

Reglas:
- No inventes eventos.
- No inventes precios.
- No inventes fechas.
- No inventes ubicaciones.
- Solo recomienda eventos publicados.
- Si ningún evento coincide con lo solicitado, indica que no encontraste resultados.
- Si el usuario pide comparar eventos, realiza la comparación únicamente con los datos proporcionados.
- El aforo representa la capacidad total del evento, no necesariamente las entradas disponibles.

Pregunta del usuario:
{message}

Eventos disponibles:
{contexto}

Responde la consulta del usuario.
""";

            var aiResponse = await _chatClient.GetResponseAsync(prompt);

            var recommendations = eventos
                .Select(e => new AIEventRecommendation
                {
                    Id = e.Id,
                    Nombre = e.Nombre,
                    Categoria = e.Categoria?.Nombre ?? "Sin categoría",
                    Fecha = e.FechaInicio,
                    Ubicacion = e.Ubicacion,
                    Precio = e.Precio,
                    Estado = e.Estado
                })
                .ToList();

            return new AIChatResponse
            {
                Answer = aiResponse.Text,
                Events = recommendations
            };
        }

        private static string? DetectCategory(string text)
        {
            if (text.Contains("tecnologia") ||
                text.Contains("programacion") ||
                text.Contains("desarrollo web") ||
                text.Contains(" ia ") ||
                text.StartsWith("ia ") ||
                text.EndsWith(" ia"))
            {
                return "tecnologia";
            }

            if (text.Contains("musica") ||
                text.Contains("concierto") ||
                text.Contains("festival musical") ||
                text.Contains("jazz"))
            {
                return "musica";
            }

            if (text.Contains("gastronomia") ||
                text.Contains("comida") ||
                text.Contains("gastronomico"))
            {
                return "gastronomia";
            }

            if (text.Contains("arte") || text.Contains("exposicion"))
            {
                return "arte";
            }

            return null;
        }

        private static string? DetectLocation(string text)
        {
            string[] locations =
            [
                "miraflores",
                "san isidro",
                "barranco",
                "surco",
                "san borja",
                "la molina",
                "jesus maria",
                "lince",
                "magdalena",
                "pueblo libre",
                "centro de lima",
                "callao"
            ];

            return locations.FirstOrDefault(text.Contains);
        }

        private static decimal? DetectMaxPrice(string text)
        {
            var patterns = new[]
            {
                @"menos\s+de\s+(?:s\/\.?\s*)?(\d+(?:[.,]\d+)?)",
                @"menor\s+de\s+(?:s\/\.?\s*)?(\d+(?:[.,]\d+)?)",
                @"maximo\s+(?:s\/\.?\s*)?(\d+(?:[.,]\d+)?)",
                @"hasta\s+(?:s\/\.?\s*)?(\d+(?:[.,]\d+)?)"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(text, pattern);
                if (!match.Success)
                {
                    continue;
                }

                var value = match.Groups[1].Value.Replace(',', '.');

                if (decimal.TryParse(
                    value,
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out var price))
                {
                    return price;
                }
            }

            return null;
        }

        private static string Normalize(string value)
        {
            var normalized = value
                .ToLowerInvariant()
                .Normalize(NormalizationForm.FormD);

            var builder = new StringBuilder();

            foreach (var character in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(character);
                }
            }

            return builder
                .ToString()
                .Normalize(NormalizationForm.FormC);
        }

        
    }
}
