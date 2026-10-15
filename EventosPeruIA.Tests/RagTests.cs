using EventosPeruIA.Api.Data;
using EventosPeruIA.Api.Models;
using EventosPeruIA.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Xunit;

namespace EventosPeruIA.Tests;

public sealed class RagTests : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly ApplicationDbContext db;
    private readonly FakeVectors vectors = new();
    private readonly FakeEmbeddings embeddings = new();
    private readonly FakeChat chat = new();

    public RagTests()
    {
        connection.Open();
        db = new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated();
        // References use real foreign keys, rather than an in-memory EF provider.
        db.Usuarios.Add(new Usuario { Id = 1, Nombre = "Operador" });
        db.Categorias.Add(new Categoria { Id = 1, Nombre = "Tecnología" });
        db.SaveChanges();
    }

    private Evento Event(int id = 1, decimal price = 50, int capacity = 10)
    {
        var e = new Evento { Id = id, Nombre = $"Evento {id}", Precio = price, Aforo = capacity,
            Estado = "Publicado", CategoriaId = 1, OrganizadorId = 1,
            FechaInicio = DateTime.UtcNow.AddDays(7), Ubicacion = "Lima" };
        db.Eventos.Add(e); db.SaveChanges(); return e;
    }
    private void Ticket(int quantity, EstadoTicket state, int eventId = 1)
    {
        db.Tickets.Add(new Ticket { Id = Guid.NewGuid(), EventoId = eventId, UsuarioId = 1,
            Cantidad = quantity, Estado = state }); db.SaveChanges();
    }
    private AIService AI() => new(chat, db, embeddings, vectors);

    [Fact]
    public async Task AvailabilityCountsQuantitiesIncludingUsedButNotVoided()
    {
        var e = Event(); Ticket(3, EstadoTicket.Valida); Ticket(2, EstadoTicket.Utilizada);
        Ticket(9, EstadoTicket.Anulada);
        Assert.Equal(5, (await EventAvailability.GetAsync(db, [e]))[e.Id]);
        Ticket(8, EstadoTicket.Valida);
        Assert.Equal(0, (await EventAvailability.GetAsync(db, [e]))[e.Id]);
    }

    [Fact]
    public async Task ReindexUpdatesChangedEventsAndRemovesCancelledPastDraftDeletedAndSoldOut()
    {
        var first = Event(); var cancelled = Event(2); var past = Event(3);
        var draft = Event(4); Event(5); Event(6); Event(7);
        var index = new RagIndexService(db, embeddings, vectors);
        Assert.Equal(7, await index.ReindexEventsAsync());
        first.Nombre = "Nombre actualizado"; cancelled.Estado = "Cancelado";
        past.FechaInicio = DateTime.UtcNow.AddDays(-1); draft.Estado = "Borrador";
        db.Eventos.Remove(db.Eventos.Single(e => e.Id == 5));
        Ticket(10, EstadoTicket.Valida, 6); db.SaveChanges();
        Assert.Equal(2, await index.ReindexEventsAsync());
        Assert.Equal(new[] { 1, 7 }, vectors.Points.Keys.Order().ToArray());
        Assert.Equal("Nombre actualizado", vectors.Points[1]);
        db.Eventos.RemoveRange(db.Eventos); db.SaveChanges();
        Assert.Equal(0, await index.ReindexEventsAsync()); Assert.Empty(vectors.Points);
    }

    [Theory]
    [InlineData("hasta S/50", true)]
    [InlineData("máximo de 50", true)]
    [InlineData("no más de 50", true)]
    [InlineData("menos de 50", false)]
    [InlineData("menor de 50", false)]
    [InlineData("entre 40 y 50", true)]
    [InlineData("entre 50 y 60", true)]
    [InlineData("desde 51", false)]
    [InlineData("hasta 49,99", false)]
    [InlineData("hasta S/. 50,00", true)]
    [InlineData("entre 50,00 y 50,00", true)]
    [InlineData("entre 60 y 40", false)]
    public async Task PriceBoundariesMatchSpanishMeaning(string query, bool expected)
    {
        Event(); vectors.Result = [1, 1];
        var response = await AI().ChatAsync(query);
        Assert.Equal(expected ? 1 : 0, response.Events.Count);
        if (expected) Assert.Equal(10, response.Events[0].EntradasDisponibles);
    }

    [Fact]
    public async Task SoldOutAndStaleIdsCannotReachTheModel()
    {
        Event(); Event(2); Ticket(10, EstadoTicket.Valida);
        vectors.Result = [1, 999, 2, 2];
        var response = await AI().ChatAsync("eventos en Lima");
        Assert.Equal(2, Assert.Single(response.Events).Id);
        Assert.Equal(new[] { 2 }, vectors.LastEligible);
        Assert.DoesNotContain("Evento 1", chat.LastMessages.Last().Text);
        Assert.Equal(ChatRole.System, chat.LastMessages.First().Role);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EmptyQueriesDoNotCallProviders(string query)
    {
        await AI().ChatAsync(query); Assert.Empty(embeddings.Inputs); Assert.Empty(chat.LastMessages);
    }
    [Fact]
    public async Task OversizedQueriesDoNotCallProviders()
    {
        await AI().ChatAsync(new string('a', 2001)); Assert.Empty(embeddings.Inputs);
    }
    [Fact]
    public async Task CategoryDetectionDoesNotMistakeParteForArte()
    {
        Event(); vectors.Result = [1];
        Assert.Single((await AI().ChatAsync("qué eventos hay para compartir")).Events);
    }
    [Fact]
    public async Task FailedEmbeddingIsRetriedAndObsoleteVectorsAreStillRemoved()
    {
        var e = Event(); var index = new RagIndexService(db, embeddings, vectors);
        await index.ReindexEventsAsync(); e.Estado = "Cancelado"; Event(2); db.SaveChanges();
        embeddings.Fail = true;
        await Assert.ThrowsAsync<HttpRequestException>(() => index.ReindexEventsAsync());
        Assert.Empty(vectors.Points);
        embeddings.Fail = false;
        Assert.Equal(1, await index.ReindexEventsAsync()); Assert.True(vectors.Points.ContainsKey(2));
    }
    [Fact]
    public async Task PurchasePreservesCapacityAndRejectsOverbooking()
    {
        var e = Event();
        var controller = new EventosPeruIA.Api.Controllers.TicketsController(db);
        var response = await controller.Purchase(new EventosPeruIA.Api.DTOs.PurchaseTicketRequest
            { EventId = e.Id, UserId = 1, Cantidad = 7 });
        Assert.IsType<Microsoft.AspNetCore.Mvc.CreatedAtActionResult>(response);
        Assert.Equal(10, e.Aforo);
        Assert.Equal(3, (await EventAvailability.GetAsync(db, [e]))[e.Id]);
        var rejected = await controller.Purchase(new EventosPeruIA.Api.DTOs.PurchaseTicketRequest
            { EventId = e.Id, UserId = 1, Cantidad = 4 });
        Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(rejected);
        Assert.Single(await db.Tickets.ToListAsync());
        vectors.Result = [1];
        Assert.Equal(3, Assert.Single((await AI().ChatAsync("eventos")).Events).EntradasDisponibles);
    }
    [Theory]
    [InlineData("Cancelado")]
    [InlineData("Borrador")]
    [InlineData("Finalizado")]
    public async Task PurchaseRejectsUnavailableState(string state)
    {
        var e = Event(); e.Estado = state; db.SaveChanges();
        var response = await new EventosPeruIA.Api.Controllers.TicketsController(db).Purchase(
            new EventosPeruIA.Api.DTOs.PurchaseTicketRequest { EventId = 1, UserId = 1, Cantidad = 1 });
        Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(response);
        Assert.Empty(await db.Tickets.ToListAsync());
    }
    [Fact]
    public async Task DetailAndChatAgreeOnQuantities()
    {
        Event(); Ticket(4, EstadoTicket.Valida); Ticket(3, EstadoTicket.Anulada);
        var result = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(
            await new EventosPeruIA.Api.Controllers.EventsController(db).ObtenerDetalle(1));
        var json = System.Text.Json.JsonSerializer.SerializeToElement(result.Value);
        Assert.Equal(4, json.GetProperty("TicketsVendidos").GetInt32());
        Assert.Equal(6, json.GetProperty("CuposDisponibles").GetInt32());
    }
    [Fact]
    public async Task EmptyCatalogDoesNotCallAnyAIProvider()
    {
        var result = await AI().ChatAsync("eventos");
        Assert.Empty(result.Events); Assert.Empty(embeddings.Inputs); Assert.Empty(chat.LastMessages);
    }
    [Fact]
    public async Task QueryIsTrimmedAndIaIsRecognizedAsWholeWord()
    {
        Event(); vectors.Result = [1];
        Assert.Single((await AI().ChatAsync("  IA  ")).Events);
        Assert.Equal("IA", embeddings.Inputs.Single());
    }
    [Theory]
    [InlineData("cancel")]
    [InlineData("soldout")]
    [InlineData("price")]
    public async Task ExternalLatencyCannotBypassFreshSqlChecks(string change)
    {
        var e = Event(); vectors.Result = [1];
        embeddings.OnGenerate = () =>
        {
            if (change == "cancel") e.Estado = "Cancelado";
            if (change == "soldout") Ticket(10, EstadoTicket.Valida);
            if (change == "price") e.Precio = 100;
            db.SaveChanges();
        };
        Assert.Empty((await AI().ChatAsync("hasta 50")).Events);
        Assert.Empty(chat.LastMessages);
    }
    public void Dispose() { db.Dispose(); connection.Dispose(); }
}

public sealed class FakeEmbeddings : IEmbeddingService
{
    public List<string> Inputs { get; } = [];
    public bool Fail { get; set; }
    public Action? OnGenerate { get; set; }
    public Task<float[]> GenerateEmbeddingAsync(string text)
    {
        if (Fail) throw new HttpRequestException("provider failed");
        OnGenerate?.Invoke();
        Inputs.Add(text); return Task.FromResult(new float[] { 1, 0 });
    }
    public float CosineSimilarity(float[] a, float[] b) => 1;
}
public sealed class FakeVectors : IQdrantService
{
    public Dictionary<int, string> Points { get; } = [];
    public List<int> Result { get; set; } = [];
    public int[] LastEligible { get; private set; } = [];
    public Task EnsureCollectionAsync(int size) => Task.CompletedTask;
    public Task IndexEventAsync(Evento e, float[] embedding) { Points[e.Id] = e.Nombre; return Task.CompletedTask; }
    public Task DeleteEventAsync(int id) { Points.Remove(id); return Task.CompletedTask; }
    public Task RemoveObsoleteEventsAsync(IReadOnlyCollection<int> ids)
    {
        foreach (var id in Points.Keys.Except(ids).ToArray()) Points.Remove(id);
        return Task.CompletedTask;
    }
    public Task<List<int>> SearchEventIdsAsync(float[] embedding, int limit = 5, IReadOnlyCollection<int>? eligibleEventIds = null)
    {
        LastEligible = eligibleEventIds?.ToArray() ?? []; return Task.FromResult(Result);
    }
}
public sealed class FakeChat : IChatClient
{
    public List<ChatMessage> LastMessages { get; private set; } = [];
    public ChatClientMetadata Metadata { get; } = new("test");
    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        LastMessages = messages.ToList();
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Respuesta de prueba")));
    }
    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public object? GetService(Type serviceType, object? serviceKey = null) => null;
    public void Dispose() { }
}
