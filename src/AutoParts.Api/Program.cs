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

app.Run();

public partial class Program;
