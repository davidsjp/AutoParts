using AutoParts.Api.Clients;
using AutoParts.Api.Data;
using AutoParts.Api.Infrastructure;
using AutoParts.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<AutoPartsDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("AutoParts")));
builder.Services.AddScoped<IVehicleService, VehicleService>();
builder.Services.AddScoped<IPartService, PartService>();
builder.Services.AddHttpClient<IPartsCatalogClient, PartsCatalogClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["PartsCatalog:BaseUrl"] ?? "https://bmv.parts/");
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("AutoParts/1.0");
});
builder.Services.AddScoped<IExternalCatalogService, ExternalCatalogService>();
builder.Services.AddSingleton<MarketPartNameNormalizer>();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<IVehicleImageService, WikimediaVehicleImageService>(client =>
{
    client.BaseAddress = new Uri("https://commons.wikimedia.org/");
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("AutoParts/1.0 (vehicle catalogue reference)");
});
builder.Services.AddScoped<IListingDescriptionService, TemplateListingDescriptionService>();
builder.Services.AddHttpClient<IPartTranslationService, OpenAiPartTranslationService>(client =>
{
    client.BaseAddress = new Uri("https://api.openai.com/");
    client.Timeout = TimeSpan.FromSeconds(60);
});
builder.Services.AddHttpClient<IListingGenerationService, OpenAiListingGenerationService>(client =>
{
    client.BaseAddress = new Uri("https://api.openai.com/");
    client.Timeout = TimeSpan.FromSeconds(60);
});
builder.Services.AddHttpClient<IListingImageService, OpenAiListingImageService>(client =>
{
    client.BaseAddress = new Uri("https://api.openai.com/");
    client.Timeout = TimeSpan.FromMinutes(3);
});

var app = builder.Build();

if (app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<AutoPartsDbContext>().Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
app.UseExceptionHandler();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }))
    .WithName("Health")
    .WithTags("Health");

if (args.Contains("--normalize-legacy-categories", StringComparer.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var database = scope.ServiceProvider.GetRequiredService<AutoPartsDbContext>();
    var changed = await database.Database.ExecuteSqlRawAsync(
        "UPDATE Parts SET Category = 'Sem categoria (revisar)' WHERE Category = 'BMV.parts';");
    Console.WriteLine($"Categorias legadas normalizadas: {changed}.");
    return;
}

if (args.Contains("--apply-market-name-fixes", StringComparer.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var database = scope.ServiceProvider.GetRequiredService<AutoPartsDbContext>();
    var changed = await database.Database.ExecuteSqlRawAsync("""
        UPDATE Parts
        SET Description = CASE Description
            WHEN 'Stick-on Etiqueta, CD Changer' THEN 'Etiqueta adesiva do trocador de CD'
            WHEN 'Adesivo Etiqueta, Trocador de CD' THEN 'Etiqueta adesiva do trocador de CD'
            WHEN 'Stick-on Etiqueta' THEN 'Etiqueta adesiva'
            WHEN 'Adesivo Etiqueta' THEN 'Etiqueta adesiva'
            WHEN 'Hex head Parafuso' THEN 'Parafuso sextavado'
            WHEN 'Hex Parafuso with Arruela' THEN 'Parafuso sextavado com arruela'
            WHEN 'Hex Parafuso' THEN 'Parafuso sextavado'
            WHEN 'Hex Porca' THEN 'Porca sextavada'
            WHEN 'Parafuso, self tapping' THEN 'Parafuso autoatarraxante'
            WHEN 'Operating instructions' THEN 'Manual de instruções'
            WHEN 'Read comments and instruct, carefully!' THEN 'Consulte as observações e instruções com atenção'
            ELSE Description
        END
        WHERE Description IN (
            'Stick-on Etiqueta, CD Changer', 'Adesivo Etiqueta, Trocador de CD',
            'Stick-on Etiqueta', 'Adesivo Etiqueta', 'Hex head Parafuso',
            'Hex Parafuso with Arruela', 'Hex Parafuso', 'Hex Porca',
            'Parafuso, self tapping', 'Operating instructions',
            'Read comments and instruct, carefully!');
        """);
    Console.WriteLine($"Correções diretas de nomes aplicadas: {changed}.");
    return;
}

if (args.Contains("--mark-ignored-catalog-items", StringComparer.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var database = scope.ServiceProvider.GetRequiredService<AutoPartsDbContext>();
    var changed = await database.Database.ExecuteSqlRawAsync("""
        UPDATE Parts
        SET Category = 'Ignorar - fixadores e mangueiras'
        WHERE Source = 'BMV.parts'
          AND Category NOT LIKE 'Ignorar%'
          AND (
            lower(Description) LIKE '%parafuso%' OR lower(Description) LIKE '%porca%'
            OR lower(Description) LIKE '%arruela%' OR lower(Description) LIKE '%mangueira%'
            OR lower(Description) LIKE '%screw%' OR lower(Description) LIKE '%bolt%'
            OR lower(Description) LIKE '%nut%' OR lower(Description) LIKE '%washer%'
            OR lower(Description) LIKE '%hose%'
          );
        """);
    Console.WriteLine($"Itens ignorados na vitrine comercial: {changed}.");
    return;
}

if (args.Contains("--normalize-market-part-names", StringComparer.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var database = scope.ServiceProvider.GetRequiredService<AutoPartsDbContext>();
    var normalizer = scope.ServiceProvider.GetRequiredService<MarketPartNameNormalizer>();
    const int batchSize = 1_000;
    var lastId = 0;
    var changed = 0;

    while (true)
    {
        var parts = await database.Parts
            .Where(part => part.Id > lastId && part.Source == "BMV.parts")
            .OrderBy(part => part.Id).Take(batchSize).ToListAsync();
        if (parts.Count == 0) break;

        foreach (var part in parts)
        {
            var normalized = normalizer.Normalize(part.Description);
            if (!string.Equals(normalized, part.Description, StringComparison.Ordinal))
            {
                part.Description = normalized;
                part.KeywordGroup = $"{normalized} {part.OemPartNumber}".ToLowerInvariant();
                part.UpdatedAt = DateTimeOffset.UtcNow;
                changed++;
            }
        }
        lastId = parts[^1].Id;
        await database.SaveChangesAsync();
        database.ChangeTracker.Clear();
    }

    Console.WriteLine($"Nomes de mercado normalizados: {changed}.");
    return;
}

app.Run();

public partial class Program;
