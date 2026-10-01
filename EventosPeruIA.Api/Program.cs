using EventosPeruIA.Api.Data;
using EventosPeruIA.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using OllamaSharp;
using Qdrant.Client;
using System.Text;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);



builder.Services.AddScoped<JwtService>();

builder.Services.AddSingleton<IChatClient>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();

    var endpoint = configuration["AI:Endpoint"]
        ?? "http://localhost:11434";

    var model = configuration["AI:Model"]
        ?? "llama3.2";

    return new OllamaApiClient(
        new Uri(endpoint),
        model
    );
});

builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();

    var endpoint = configuration["AI:Endpoint"]
        ?? "http://localhost:11434";

    var model = configuration["AI:EmbeddingModel"]
        ?? "nomic-embed-text";

    return new OllamaApiClient(
        new Uri(endpoint),
        model
    );
});



builder.Services.AddSingleton<QdrantClient>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();

    var host = configuration["Qdrant:Host"] ?? "localhost";
    var port = int.Parse(configuration["Qdrant:Port"] ?? "6334");

    return new QdrantClient(host, port);
});

builder.Services.AddScoped<IAIService, AIService>();
builder.Services.AddScoped<IEmbeddingService, EmbeddingService>();
builder.Services.AddScoped<IQdrantService, QdrantService>();
builder.Services.AddScoped<IRagIndexService, RagIndexService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],

            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)
            )
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Ingrese el token JWT."
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("bearer", document)] = []
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();