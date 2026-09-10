using AutoParts.Api.Clients;
using AutoParts.Api.Data;
using AutoParts.Api.Infrastructure;
using AutoParts.Api.Models;
using AutoParts.Api.Services;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Composition root: keep external integrations behind interfaces so controllers
// remain focused on HTTP concerns and are easy to exercise in tests.
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
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
builder.Services.AddSingleton<IPartMetadataService, PartMetadataService>();
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
    // Integration tests use an isolated provider and create the schema on boot.
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
    // One-shot maintenance commands operate on the local catalog and exit before
    // the HTTP server starts. Back up production-like SQLite files first.
    using var scope = app.Services.CreateScope();
    var database = scope.ServiceProvider.GetRequiredService<AutoPartsDbContext>();
    var changed = await database.Database.ExecuteSqlRawAsync(
        "UPDATE Parts SET Category = 'Sem categoria (revisar)' WHERE Category = 'BMV.parts';");
    Console.WriteLine($"Categorias legadas normalizadas: {changed}.");
    return;
}

if (args.Contains("--migrate-database", StringComparer.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var database = scope.ServiceProvider.GetRequiredService<AutoPartsDbContext>();
    await database.Database.MigrateAsync();
    Console.WriteLine("Database migrations applied.");
    return;
}

if (args.Contains("--repair-structured-metadata-columns", StringComparer.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var database = scope.ServiceProvider.GetRequiredService<AutoPartsDbContext>();
    await EnsureStructuredMetadataColumnsAsync(database);
    Console.WriteLine("Colunas e indices de metadados estruturados conferidos.");
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
        SET Category = 'Ignorar - item sem catalogo comercial'
        WHERE Source = 'BMV.parts'
          AND Category NOT LIKE 'Ignorar%'
          AND (
            lower(Description) LIKE '%parafuso%' OR lower(Description) LIKE '%porca%'
            OR lower(Description) LIKE '%arruela%' OR lower(Description) LIKE '%mangueira%'
            OR lower(Description) LIKE '%screw%' OR lower(Description) LIKE '%bolt%'
            OR lower(Description) LIKE '%nut%' OR lower(Description) LIKE '%washer%'
            OR lower(Description) LIKE '%hose%'
            OR lower(Description) LIKE '%template%' OR lower(Description) LIKE '%manual%'
            OR lower(Description) LIKE '%owner%' OR lower(Description) LIKE '%instruction%'
            OR lower(Description) LIKE '%handbook%' OR lower(Description) LIKE '%booklet%'
            OR lower(Description) LIKE '%literature%' OR lower(Description) LIKE '%documentation%'
            OR lower(Description) LIKE '%certificate%' OR lower(Description) LIKE '%label%'
            OR lower(Description) LIKE '%etiqueta%' OR lower(Description) LIKE '%tag%'
            OR lower(Description) LIKE '%form%'
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

if (args.Contains("--recategorize-bmv-parts", StringComparer.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var database = scope.ServiceProvider.GetRequiredService<AutoPartsDbContext>();
    var normalizer = scope.ServiceProvider.GetRequiredService<MarketPartNameNormalizer>();
    var partMetadata = scope.ServiceProvider.GetRequiredService<IPartMetadataService>();
    var dryRun = args.Contains("--dry-run", StringComparer.OrdinalIgnoreCase);
    var result = await RecategorizeBmvPartsAsync(database, normalizer, partMetadata, dryRun);

    Console.WriteLine($"Pecas BMV avaliadas: {result.PartsSeen}");
    Console.WriteLine($"Nomes revisados: {result.NamesChanged}");
    Console.WriteLine($"Categorias revisadas: {result.CategoriesChanged}");
    Console.WriteLine($"Metadados lado/posicao revisados: {result.MetadataChanged}");
    Console.WriteLine($"Itens marcados para ignorar: {result.IgnoredParts}");
    if (dryRun) Console.WriteLine("Dry run: nenhuma alteracao foi salva.");
    return;
}

if (args.Contains("--curate-imported-parts-by-vehicle", StringComparer.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var database = scope.ServiceProvider.GetRequiredService<AutoPartsDbContext>();
    var normalizer = scope.ServiceProvider.GetRequiredService<MarketPartNameNormalizer>();
    var partMetadata = scope.ServiceProvider.GetRequiredService<IPartMetadataService>();
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var dryRun = args.Contains("--dry-run", StringComparer.OrdinalIgnoreCase);
    var batchSize = GetIntOption(args, "--translation-batch-size", 50);
    var result = await CurateImportedPartsByVehicleAsync(database, normalizer, partMetadata, configuration, batchSize, dryRun);

    Console.WriteLine($"Veiculos processados: {result.VehicleCount}");
    Console.WriteLine($"Pecas avaliadas: {result.PartsSeen}");
    Console.WriteLine($"Pecas marcadas para ignorar: {result.IgnoredParts}");
    Console.WriteLine($"Pecas traduzidas: {result.TranslatedParts}");
    Console.WriteLine($"Pecas reutilizadas em outros veiculos: {result.ReusedPartsSkipped}");
    if (dryRun) Console.WriteLine("Dry run: nenhuma alteracao foi salva.");
    return;
}

if (args.Contains("--enrich-part-metadata", StringComparer.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var database = scope.ServiceProvider.GetRequiredService<AutoPartsDbContext>();
    var partMetadata = scope.ServiceProvider.GetRequiredService<IPartMetadataService>();
    var dryRun = args.Contains("--dry-run", StringComparer.OrdinalIgnoreCase);
    var changed = await EnrichPartMetadataAsync(database, partMetadata, dryRun);
    Console.WriteLine($"Pecas com metadados atualizados: {changed}");
    if (dryRun) Console.WriteLine("Dry run: nenhuma alteracao foi salva.");
    return;
}

if (args.Contains("--delete-ignored-catalog-items", StringComparer.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var database = scope.ServiceProvider.GetRequiredService<AutoPartsDbContext>();
    var confirmed = args.Contains("--confirm-delete", StringComparer.OrdinalIgnoreCase);
    var includeUncategorized = args.Contains("--include-uncategorized", StringComparer.OrdinalIgnoreCase);
    var result = await DeleteIgnoredCatalogItemsAsync(database, !confirmed, includeUncategorized);

    Console.WriteLine($"Pecas ignoradas encontradas: {result.PartCount}");
    Console.WriteLine($"Compatibilidades relacionadas: {result.CompatibilityCount}");
    if (confirmed)
        Console.WriteLine("Exclusao confirmada: registros removidos do banco.");
    else
        Console.WriteLine("Preview apenas: adicione --confirm-delete para remover do banco.");
    return;
}

app.Run();

static int GetIntOption(string[] args, string optionName, int fallback)
{
    var prefix = optionName + "=";
    var value = args.FirstOrDefault(x => x.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    return value is not null && int.TryParse(value[prefix.Length..], out var parsed) && parsed > 0
        ? parsed
        : fallback;
}

static async Task EnsureStructuredMetadataColumnsAsync(AutoPartsDbContext database)
{
    if (!await ColumnExistsAsync(database, "Parts", "Position"))
        await database.Database.ExecuteSqlRawAsync("""ALTER TABLE Parts ADD COLUMN Position TEXT;""");

    if (!await ColumnExistsAsync(database, "Parts", "Side"))
        await database.Database.ExecuteSqlRawAsync("""ALTER TABLE Parts ADD COLUMN Side TEXT;""");

    await database.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS IX_Parts_Position ON Parts (Position);""");
    await database.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS IX_Parts_Side ON Parts (Side);""");
    await database.Database.ExecuteSqlRawAsync("""
        CREATE INDEX IF NOT EXISTS IX_Vehicles_Manufacturer_Model_Chassis_Engine_TypeCode
        ON Vehicles (Manufacturer, Model, Chassis, Engine, TypeCode);
        """);
}

static async Task<bool> ColumnExistsAsync(AutoPartsDbContext database, string tableName, string columnName)
{
    await database.Database.OpenConnectionAsync();
    await using var command = database.Database.GetDbConnection().CreateCommand();
    command.CommandText = $"PRAGMA table_info({tableName});";
    await using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
            return true;
    }
    return false;
}

static async Task<CurationResult> CurateImportedPartsByVehicleAsync(
    AutoPartsDbContext database,
    MarketPartNameNormalizer normalizer,
    IPartMetadataService partMetadata,
    IConfiguration configuration,
    int batchSize,
    bool dryRun)
{
    batchSize = Math.Clamp(batchSize, 1, 100);
    var apiKey = configuration["OpenAI:ApiKey"] ?? configuration["OPENAI_API_KEY"];
    if (!dryRun && string.IsNullOrWhiteSpace(apiKey))
        throw new ApiException(503, "OpenAI API key is not configured.");

    using var httpClient = new HttpClient
    {
        BaseAddress = new Uri("https://api.openai.com/"),
        Timeout = TimeSpan.FromMinutes(3)
    };
    if (!string.IsNullOrWhiteSpace(apiKey))
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

    var vehicles = await database.Vehicles.AsNoTracking()
        .Where(vehicle => vehicle.Compatibilities.Any(compatibility => compatibility.Part.Source == "BMV.parts"))
        .OrderBy(vehicle => vehicle.Manufacturer)
        .ThenBy(vehicle => vehicle.Model)
        .ThenBy(vehicle => vehicle.Chassis)
        .ThenBy(vehicle => vehicle.Engine)
        .Select(vehicle => new VehicleWorkItem(
            vehicle.Id,
            vehicle.Manufacturer,
            vehicle.Model,
            vehicle.Chassis,
            vehicle.Engine,
            vehicle.TypeCode))
        .ToListAsync();

    var processedPartIds = new HashSet<int>();
    var vehicleCount = 0;
    var partsSeen = 0;
    var ignoredParts = 0;
    var translatedParts = 0;
    var reusedPartsSkipped = 0;

    foreach (var vehicle in vehicles)
    {
        vehicleCount++;
        Console.WriteLine($"[{vehicleCount}/{vehicles.Count}] {vehicle.DisplayName}");

        var partIds = await database.PartCompatibilities.AsNoTracking()
            .Where(compatibility => compatibility.VehicleId == vehicle.Id && compatibility.Part.Source == "BMV.parts")
            .Select(compatibility => compatibility.PartId)
            .Distinct()
            .ToListAsync();

        var parts = await database.Parts
            .Where(part => partIds.Contains(part.Id))
            .OrderBy(part => part.Description)
            .ThenBy(part => part.OemPartNumber)
            .ToListAsync();

        var translationBatch = new List<Part>();
        foreach (var part in parts)
        {
            partsSeen++;
            if (!processedPartIds.Add(part.Id))
            {
                reusedPartsSkipped++;
                continue;
            }

            var normalizedName = normalizer.Normalize(part.Description);
            if (normalizer.ShouldIgnoreForCommercialCatalog(normalizedName))
            {
                ignoredParts++;
                Console.WriteLine($"  IGNORAR {part.OemPartNumber} - {normalizedName}");
                if (!dryRun)
                {
                    var metadata = partMetadata.Extract(normalizedName, "Ignorar - item sem catalogo comercial");
                    part.Description = normalizedName;
                    part.Category = "Ignorar - item sem catalogo comercial";
                    part.Side = metadata.Side;
                    part.Position = metadata.Position;
                    part.KeywordGroup = $"{normalizedName} {part.OemPartNumber}".ToLowerInvariant();
                    part.UpdatedAt = DateTimeOffset.UtcNow;
                }
                continue;
            }

            if (!string.Equals(normalizedName, part.Description, StringComparison.Ordinal))
            {
                var metadata = partMetadata.Extract(normalizedName, part.Category);
                part.Description = normalizedName;
                part.Side = metadata.Side;
                part.Position = metadata.Position;
                part.KeywordGroup = $"{normalizedName} {part.OemPartNumber}".ToLowerInvariant();
                part.UpdatedAt = DateTimeOffset.UtcNow;
            }

            translationBatch.Add(part);
            if (translationBatch.Count >= batchSize)
            {
                translatedParts += await TranslateAndApplyBatchAsync(httpClient, configuration, partMetadata, translationBatch, dryRun);
                translationBatch.Clear();
                if (!dryRun) await database.SaveChangesAsync();
            }
        }

        if (translationBatch.Count > 0)
        {
            translatedParts += await TranslateAndApplyBatchAsync(httpClient, configuration, partMetadata, translationBatch, dryRun);
            if (!dryRun) await database.SaveChangesAsync();
        }
        else if (!dryRun)
        {
            await database.SaveChangesAsync();
        }

        database.ChangeTracker.Clear();
    }

    return new CurationResult(vehicleCount, partsSeen, ignoredParts, translatedParts, reusedPartsSkipped);
}

static async Task<RecategorizeResult> RecategorizeBmvPartsAsync(
    AutoPartsDbContext database,
    MarketPartNameNormalizer normalizer,
    IPartMetadataService partMetadata,
    bool dryRun)
{
    const int batchSize = 1_000;
    var lastId = 0;
    var partsSeen = 0;
    var namesChanged = 0;
    var categoriesChanged = 0;
    var metadataChanged = 0;
    var ignoredParts = 0;

    while (true)
    {
        var parts = await database.Parts
            .Where(part => part.Id > lastId && part.Source == "BMV.parts")
            .OrderBy(part => part.Id)
            .Take(batchSize)
            .ToListAsync();
        if (parts.Count == 0) break;

        foreach (var part in parts)
        {
            partsSeen++;
            var normalizedName = normalizer.Normalize(part.Description);
            var normalizedCategory = normalizer.NormalizeCategory(part.Category, normalizedName);
            var metadata = partMetadata.Extract(normalizedName, normalizedCategory);

            var changed = false;
            if (!string.Equals(part.Description, normalizedName, StringComparison.Ordinal))
            {
                namesChanged++;
                changed = true;
                if (!dryRun) part.Description = normalizedName;
            }

            if (!string.Equals(part.Category, normalizedCategory, StringComparison.Ordinal))
            {
                categoriesChanged++;
                changed = true;
                if (!dryRun) part.Category = normalizedCategory;
            }

            if (part.Side != metadata.Side || part.Position != metadata.Position)
            {
                metadataChanged++;
                changed = true;
                if (!dryRun)
                {
                    part.Side = metadata.Side;
                    part.Position = metadata.Position;
                }
            }

            if (normalizedCategory == MarketPartNameNormalizer.IgnoredCategory)
                ignoredParts++;

            if (changed && !dryRun)
            {
                part.KeywordGroup = $"{normalizedName} {part.OemPartNumber}".ToLowerInvariant();
                part.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        lastId = parts[^1].Id;
        if (!dryRun) await database.SaveChangesAsync();
        database.ChangeTracker.Clear();
    }

    return new RecategorizeResult(partsSeen, namesChanged, categoriesChanged, metadataChanged, ignoredParts);
}

static async Task<int> TranslateAndApplyBatchAsync(
    HttpClient httpClient,
    IConfiguration configuration,
    IPartMetadataService partMetadata,
    IReadOnlyList<Part> parts,
    bool dryRun)
{
    if (parts.Count == 0) return 0;
    if (dryRun)
    {
        foreach (var part in parts)
            Console.WriteLine($"  TRADUZIR {part.OemPartNumber} - {part.Description}");
        return parts.Count;
    }

    var items = parts.Select(part => new
    {
        id = part.Id,
        oem = part.OemPartNumber,
        title = part.Description,
        category = part.Category
    });
    var schema = new Dictionary<string, object>
    {
        ["type"] = "object",
        ["additionalProperties"] = false,
        ["properties"] = new Dictionary<string, object>
        {
            ["items"] = new Dictionary<string, object>
            {
                ["type"] = "array",
                ["items"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["id"] = new { type = "integer" },
                        ["title"] = new { type = "string" },
                        ["category"] = new { type = "string" },
                        ["keywordGroup"] = new { type = "string" }
                    },
                    ["required"] = new[] { "id", "title", "category", "keywordGroup" }
                }
            }
        },
        ["required"] = new[] { "items" }
    };
    const string instructions = """
Traduza nomes de pecas automotivas BMW para portugues brasileiro comercial e tecnico.
Preserve exatamente numeros OEM, codigos BMW, chassis, motor, lado, posicao e anos se aparecerem.
Nao invente compatibilidade, modelo, acabamento, lado, material ou aplicacao.
Remova ingles residual quando houver traducao segura.
Retorne titulo curto, categoria curta e keywords em minusculas.
""";
    var requestBody = new
    {
        model = configuration["OpenAI:Model"] ?? "gpt-5-mini",
        instructions,
        input = JsonSerializer.Serialize(new { items }),
        text = new
        {
            format = new
            {
                type = "json_schema",
                name = "translated_part_titles",
                strict = true,
                schema
            }
        }
    };

    using var request = new HttpRequestMessage(HttpMethod.Post, "v1/responses")
    {
        Content = JsonContent.Create(requestBody)
    };
    using var response = await httpClient.SendAsync(request);
    if (!response.IsSuccessStatusCode)
        throw new ApiException(502, $"OpenAI batch title translation failed with HTTP {(int)response.StatusCode}.");

    using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());
    var outputText = document.RootElement.GetProperty("output").EnumerateArray()
        .Where(x => x.TryGetProperty("type", out var type) && type.GetString() == "message")
        .SelectMany(x => x.GetProperty("content").EnumerateArray())
        .Where(x => x.TryGetProperty("type", out var type) && type.GetString() == "output_text")
        .Select(x => x.GetProperty("text").GetString())
        .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
        ?? throw new ApiException(502, "OpenAI returned no translated part titles.");

    var payload = JsonSerializer.Deserialize<TranslatedPartTitleBatch>(outputText,
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
        ?? throw new ApiException(502, "OpenAI returned invalid translated part titles.");

    var partById = parts.ToDictionary(part => part.Id);
    var updated = 0;
    foreach (var translated in payload.Items)
    {
        if (!partById.TryGetValue(translated.Id, out var part)) continue;
        var title = translated.Title.Trim();
        var category = translated.Category.Trim();
        var metadata = partMetadata.Extract(title, category);
        part.Description = title;
        part.Category = category;
        part.Side = metadata.Side;
        part.Position = metadata.Position;
        part.KeywordGroup = translated.KeywordGroup.Trim();
        part.UpdatedAt = DateTimeOffset.UtcNow;
        updated++;
        Console.WriteLine($"  OK {part.OemPartNumber} - {part.Description}");
    }
    return updated;
}

static async Task<DeleteIgnoredResult> DeleteIgnoredCatalogItemsAsync(
    AutoPartsDbContext database,
    bool dryRun,
    bool includeUncategorized)
{
    var ignoredParts = database.Parts
        .Where(part => part.Source == "BMV.parts")
        .Where(part =>
            EF.Functions.Like(part.Category, "Ignorar%") ||
            (includeUncategorized && part.Category == "Sem categoria (revisar)") ||
            EF.Functions.Like(part.Description, "Template") ||
            EF.Functions.Like(part.Description, "Read comments%") ||
            EF.Functions.Like(part.Description, "Consulte as observações%") ||
            EF.Functions.Like(part.Description, "Operating instructions%") ||
            EF.Functions.Like(part.Description, "Manual de instruções%") ||
            EF.Functions.Like(part.Description, "Installation instructions%") ||
            EF.Functions.Like(part.Description, "Mounting information%") ||
            EF.Functions.Like(part.Description, "Installation information%") ||
            EF.Functions.Like(part.Description, "Form, airbag%") ||
            EF.Functions.Like(part.Description, "Etiqueta%") ||
            EF.Functions.Like(part.Description, "Labels%") ||
            EF.Functions.Like(part.Description, "Adhesive Etiqueta%") ||
            EF.Functions.Like(part.Description, "Tag") ||
            EF.Functions.Like(part.Description, "%instructions%") ||
            EF.Functions.Like(part.Description, "%handbook%") ||
            EF.Functions.Like(part.Description, "%literature%") ||
            EF.Functions.Like(part.Description, "%Access request%") ||
            EF.Functions.Like(part.Description, "%Claim Form%") ||
            EF.Functions.Like(part.Description, "%radio pass%") ||
            EF.Functions.Like(part.Description, "%booklet%") ||
            EF.Functions.Like(part.Description, "%Care tips%") ||
            EF.Functions.Like(part.Description, "%Logbook%") ||
            EF.Functions.Like(part.Description, "%owner% manual%") ||
            EF.Functions.Like(part.Description, "%owner% handbook%") ||
            EF.Functions.Like(part.Description, "%Supplem.% manual%") ||
            EF.Functions.Like(part.Description, "%tag%") ||
            EF.Functions.Like(part.Description, "%labels%") ||
            EF.Functions.Like(part.Description, "%Insertion sheet%") ||
            EF.Functions.Like(part.Description, "%Documentation%") ||
            EF.Functions.Like(part.Description, "%certificate%"));

    var partCount = await ignoredParts.CountAsync();
    if (partCount == 0) return new DeleteIgnoredResult(0, 0);

    var examples = await ignoredParts.AsNoTracking()
        .OrderBy(part => part.OemPartNumber)
        .Take(20)
        .Select(part => new { part.OemPartNumber, part.Description, part.Category })
        .ToListAsync();
    foreach (var example in examples)
        Console.WriteLine($"  {example.OemPartNumber} - {example.Description} ({example.Category})");

    var ignoredPartIds = ignoredParts.Select(part => part.Id);
    var compatibilityCount = await database.PartCompatibilities.AsNoTracking()
        .CountAsync(compatibility => ignoredPartIds.Contains(compatibility.PartId));
    if (dryRun) return new DeleteIgnoredResult(partCount, compatibilityCount);

    await using var transaction = await database.Database.BeginTransactionAsync();
    await database.PartCompatibilities
        .Where(compatibility => ignoredPartIds.Contains(compatibility.PartId))
        .ExecuteDeleteAsync();
    await ignoredParts.ExecuteDeleteAsync();
    await transaction.CommitAsync();

    return new DeleteIgnoredResult(partCount, compatibilityCount);
}

static async Task<int> EnrichPartMetadataAsync(
    AutoPartsDbContext database,
    IPartMetadataService partMetadata,
    bool dryRun)
{
    const int batchSize = 1_000;
    var lastId = 0;
    var changed = 0;

    while (true)
    {
        var parts = await database.Parts
            .Where(part => part.Id > lastId)
            .OrderBy(part => part.Id)
            .Take(batchSize)
            .ToListAsync();
        if (parts.Count == 0) break;

        foreach (var part in parts)
        {
            var metadata = partMetadata.Extract(part.Description, part.Category);
            if (part.Side == metadata.Side && part.Position == metadata.Position) continue;

            changed++;
            Console.WriteLine($"  {part.OemPartNumber} - lado: {metadata.Side ?? "-"}, posicao: {metadata.Position ?? "-"}");
            if (!dryRun)
            {
                part.Side = metadata.Side;
                part.Position = metadata.Position;
                part.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        lastId = parts[^1].Id;
        if (!dryRun) await database.SaveChangesAsync();
        database.ChangeTracker.Clear();
    }

    return changed;
}

public partial class Program;

public sealed record VehicleWorkItem(
    int Id,
    string Manufacturer,
    string Model,
    string? Chassis,
    string? Engine,
    string? TypeCode)
{
    public string DisplayName => string.Join(" ", new[] { Manufacturer, Model, Chassis, Engine, TypeCode }
        .Where(value => !string.IsNullOrWhiteSpace(value)));
}

public sealed record CurationResult(
    int VehicleCount,
    int PartsSeen,
    int IgnoredParts,
    int TranslatedParts,
    int ReusedPartsSkipped);

public sealed record RecategorizeResult(
    int PartsSeen,
    int NamesChanged,
    int CategoriesChanged,
    int MetadataChanged,
    int IgnoredParts);

public sealed record TranslatedPartTitleBatch(IReadOnlyList<TranslatedPartTitle> Items);

public sealed record TranslatedPartTitle(int Id, string Title, string Category, string KeywordGroup);

public sealed record DeleteIgnoredResult(int PartCount, int CompatibilityCount);
