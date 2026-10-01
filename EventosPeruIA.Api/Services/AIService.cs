using EventosPeruIA.Api.Data;
using EventosPeruIA.Api.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace EventosPeruIA.Api.Services
{
    public class AIService : IAIService
    {
        private readonly IChatClient _chatClient;
        private readonly ApplicationDbContext _context;
        private readonly IEmbeddingService _embeddingService;
        private readonly IQdrantService _qdrantService;

        public AIService(
            IChatClient chatClient,
            ApplicationDbContext context,
            IEmbeddingService embeddingService,
            IQdrantService qdrantService)
        {
            _chatClient = chatClient;
            _context = context;
            _embeddingService = embeddingService;
            _qdrantService = qdrantService;
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

            // 1. Generar embedding de la pregunta
            var queryEmbedding =
                await _embeddingService.GenerateEmbeddingAsync(message);

            // 2. Buscar en Qdrant los eventos semánticamente más cercanos
            var eventIds =
                await _qdrantService.SearchEventIdsAsync(
                    queryEmbedding,
                    10
                );

            if (eventIds.Count == 0)
            {
                return new AIChatResponse
                {
                    Answer = "No encontré eventos relacionados con tu consulta."
                };
            }

            // 3. Recuperar desde SQL Server los datos actuales
            var eventos = await _context.Eventos
                .Include(e => e.Categoria)
                .Where(e => eventIds.Contains(e.Id))
                .Where(e => e.Estado == "Publicado")
                .Where(e => e.FechaInicio > DateTime.UtcNow)
                .ToListAsync();

            // Mantener el orden de relevancia entregado por Qdrant
            var ordenQdrant = eventIds
                .Select((id, index) => new { id, index })
                .ToDictionary(x => x.id, x => x.index);

            eventos = eventos
                .OrderBy(e =>
                    ordenQdrant.TryGetValue(e.Id, out var index)
                        ? index
                        : int.MaxValue)
                .ToList();

            // 4. Aplicar filtros exactos cuando el usuario los indique
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
                    Answer = "No encontré eventos disponibles que coincidan con tu consulta."
                };
            }

            // Evitar enviar demasiado contexto al LLM
            var eventosRelevantes = eventos
                .Take(5)
                .ToList();

            // 5. Construir contexto RAG con información actual de SQL Server
            var contexto = string.Join(
                "\n",
                eventosRelevantes.Select(e =>
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

            // 6. Enviar únicamente el contexto recuperado al LLM
            var prompt = $"""
Eres el asistente oficial de EventosPeruIA.

Responde siempre en español de forma clara y breve.

Tu respuesta debe basarse únicamente en la información proporcionada
en la sección "Eventos recuperados".

Reglas:
- No inventes eventos.
- No inventes nombres.
- No inventes precios.
- No inventes fechas.
- No inventes ubicaciones.
- No inventes disponibilidad.
- Solo recomienda eventos incluidos en el contexto.
- Si la información solicitada no está disponible, indícalo claramente.
- Si el usuario solicita una comparación, compara únicamente los eventos recuperados.
- El aforo representa la capacidad total del evento y no necesariamente las entradas disponibles.
- No menciones eventos que no estén incluidos en "Eventos recuperados".
- No expliques el funcionamiento interno del sistema, los embeddings ni Qdrant.

Pregunta del usuario:
{message}

Eventos recuperados:
{contexto}

Responde la consulta del usuario.
""";

            var aiResponse =
                await _chatClient.GetResponseAsync(prompt);

            // 7. Devolver también los eventos estructurados para el frontend
            var recommendations = eventosRelevantes
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
                text.Contains("inteligencia artificial") ||
                text.Contains("software") ||
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

            if (text.Contains("arte") ||
                text.Contains("exposicion"))
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
                "santiago de surco",
                "san borja",
                "la molina",
                "jesus maria",
                "lince",
                "magdalena",
                "magdalena del mar",
                "pueblo libre",
                "centro de lima",
                "lima",
                "callao"
            ];

            return locations
                .OrderByDescending(x => x.Length)
                .FirstOrDefault(text.Contains);
        }

        private static decimal? DetectMaxPrice(string text)
        {
            var patterns = new[]
            {
                @"menos\s+de\s+(?:s\/\.?\s*)?(\d+(?:[.,]\d+)?)",
                @"menor\s+de\s+(?:s\/\.?\s*)?(\d+(?:[.,]\d+)?)",
                @"maximo\s+(?:de\s+)?(?:s\/\.?\s*)?(\d+(?:[.,]\d+)?)",
                @"hasta\s+(?:s\/\.?\s*)?(\d+(?:[.,]\d+)?)",
                @"no\s+mas\s+de\s+(?:s\/\.?\s*)?(\d+(?:[.,]\d+)?)"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(text, pattern);

                if (!match.Success)
                {
                    continue;
                }

                var value =
                    match.Groups[1].Value.Replace(',', '.');

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
                if (CharUnicodeInfo.GetUnicodeCategory(character) !=
                    UnicodeCategory.NonSpacingMark)
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