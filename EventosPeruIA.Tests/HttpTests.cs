using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EventosPeruIA.Api.DTOs;
using EventosPeruIA.Api.Models;
using EventosPeruIA.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace EventosPeruIA.Tests;

public sealed class HttpTests
{
    [Theory]
    [InlineData(null, null, 401)]
    [InlineData(2, "Usuario", 403)]
    [InlineData(2, "Administrador", 403)]
    [InlineData(1, "Usuario", 200)]
    public async Task ReindexRequiresTrustedOperator(int? id, string? role, int expected)
    {
        using var factory = new ApiFactory(); using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        if (id.HasValue) Authenticate(factory, client, id.Value, role!);
        var result = await client.PostAsync("/api/ai/reindex", null);
        Assert.Equal(expected, (int)result.StatusCode);
    }
    [Fact]
    public async Task EmptyOperatorConfigurationDeniesEvenAuthenticatedUser()
    {
        using var factory = new ApiFactory(false); using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        Authenticate(factory, client, 1, "Administrador");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/ai/reindex", null)).StatusCode);
    }
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task InvalidQueriesReturn400(string query)
    {
        using var factory = new ApiFactory(); using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/api/ai/chat", new { message = query })).StatusCode);
    }
    [Fact]
    public async Task OversizedQueryReturns400()
    {
        using var factory = new ApiFactory(); using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/api/ai/chat", new { message = new string('a', 2001) })).StatusCode);
    }
    [Fact]
    public async Task ProviderFailureReturns503WithoutInternalError()
    {
        using var factory = new ApiFactory(); using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        factory.Service.Fail = true;
        var response = await client.PostAsJsonAsync("/api/ai/chat", new { message = "eventos" });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.DoesNotContain("secret", await response.Content.ReadAsStringAsync());
        Authenticate(factory, client, 1, "Usuario");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.PostAsync("/api/ai/reindex", null)).StatusCode);
    }
    private static void Authenticate(ApiFactory factory, HttpClient client, int id, string role)
    {
        var configuration = factory.Services.GetRequiredService<IConfiguration>();
        var token = new JwtService(configuration).GenerarToken(new Usuario { Id = id, Rol = role });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
}
public sealed class ApiFactory(bool allowOperator = true) : WebApplicationFactory<Program>
{
    public FakeAIService Service { get; } = new();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AI:ReindexOperatorIds:0"] = allowOperator ? "1" : null,
            ["Jwt:Key"] = "TestOnlySigningKey_012345678901234567890123456789",
            ["Jwt:Issuer"] = "Tests", ["Jwt:Audience"] = "Tests"
        }));
        builder.ConfigureServices(services =>
        {
            foreach (var service in services.Where(s => s.ServiceType == typeof(IHostedService)
                && s.ImplementationType == typeof(RagIndexWorker)).ToArray()) services.Remove(service);
            services.RemoveAll<IAIService>(); services.RemoveAll<IRagIndexService>();
            services.AddSingleton<IAIService>(Service); services.AddSingleton<IRagIndexService>(Service);
        });
    }
}
public sealed class FakeAIService : IAIService, IRagIndexService
{
    public bool Fail { get; set; }
    public Task<AIChatResponse> ChatAsync(string message) => Fail
        ? throw new HttpRequestException("secret provider error") : Task.FromResult(new AIChatResponse { Answer = "OK" });
    public Task<int> ReindexEventsAsync() => Fail
        ? throw new HttpRequestException("secret provider error") : Task.FromResult(0);
}
