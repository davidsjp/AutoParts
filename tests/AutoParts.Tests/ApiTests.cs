using System.Net;
using System.Net.Http.Json;
using AutoParts.Api.Models;

namespace AutoParts.Tests;

public sealed class ApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Part_CanBeCreatedAndFoundByOem()
    {
        var request = new { oemPartNumber = "ABC-123", description = "Test brake pad", category = "Brakes", supersededByPartNumber = (string?)null, source = "Test" };
        var created = await _client.PostAsJsonAsync("/api/parts", request);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var found = await _client.GetFromJsonAsync<Part>("/api/parts/oem/abc-123");
        Assert.NotNull(found);
        Assert.Equal("ABC-123", found.OemPartNumber);
    }

    [Fact]
    public async Task InvalidPart_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/parts", new { oemPartNumber = "", description = "", category = "", source = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
