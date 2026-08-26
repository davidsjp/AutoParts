using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
        Assert.Equal("ABC123", found.OemPartNumber);
    }

    [Fact]
    public async Task InvalidPart_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/parts", new { oemPartNumber = "", description = "", category = "", source = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PartCompatibilityLookup_ReturnsStructuredVehicleAndPartMetadata()
    {
        var vehicleResponse = await _client.PostAsJsonAsync("/api/vehicles", new
        {
            manufacturer = "BMW",
            model = "118i",
            chassis = "F20",
            engine = "N13",
            modelYear = 2014,
            productionDate = "2014-01-01",
            vin = "WBA1A11000ABC1234"
        });
        vehicleResponse.EnsureSuccessStatusCode();
        var vehicle = await vehicleResponse.Content.ReadFromJsonAsync<Vehicle>();
        Assert.NotNull(vehicle);

        var partResponse = await _client.PostAsJsonAsync("/api/parts", new
        {
            oemPartNumber = "5133-7294-828",
            description = "Maquina de vidro dianteira esquerda",
            category = "Vidros",
            side = (string?)null,
            position = (string?)null,
            supersededByPartNumber = (string?)null,
            source = "Test",
            applications = "BMW F20 118i 2014 a 2016"
        });
        partResponse.EnsureSuccessStatusCode();
        var part = await partResponse.Content.ReadFromJsonAsync<Part>();
        Assert.NotNull(part);

        var compatibilityResponse = await _client.PostAsJsonAsync($"/api/parts/{part.Id}/compatibilities", new
        {
            vehicleId = vehicle.Id,
            productionStart = "2014-01-01",
            productionEnd = "2016-12-31",
            notes = "teste"
        });
        compatibilityResponse.EnsureSuccessStatusCode();

        using var lookup = await _client.GetFromJsonAsync<JsonDocument>("/api/parts/oem/51337294828/compatibility");
        Assert.NotNull(lookup);
        var root = lookup.RootElement;
        Assert.Equal("51337294828", root.GetProperty("oemPartNumber").GetString());
        Assert.Equal("Esquerda", root.GetProperty("side").GetString());
        Assert.Equal("Dianteira", root.GetProperty("position").GetString());

        var vehicleItem = root.GetProperty("vehicles")[0];
        Assert.Equal("BMW", vehicleItem.GetProperty("manufacturer").GetString());
        Assert.Equal("118i", vehicleItem.GetProperty("model").GetString());
        Assert.Equal("F20", vehicleItem.GetProperty("chassis").GetString());
        Assert.Equal("N13", vehicleItem.GetProperty("engine").GetString());
        Assert.Equal(2014, vehicleItem.GetProperty("yearStart").GetInt32());
        Assert.Equal(2016, vehicleItem.GetProperty("yearEnd").GetInt32());
    }

    [Fact]
    public async Task PartCompatibilityLookup_ConsolidatesModelVersionYearRange()
    {
        var firstVehicleResponse = await _client.PostAsJsonAsync("/api/vehicles", new
        {
            manufacturer = "BMW",
            model = "320i",
            chassis = "F30",
            engine = "N20",
            modelYear = 2013,
            productionDate = "2013-01-01",
            vin = "WBA3A11000ABC1234"
        });
        firstVehicleResponse.EnsureSuccessStatusCode();
        var firstVehicle = await firstVehicleResponse.Content.ReadFromJsonAsync<Vehicle>();
        Assert.NotNull(firstVehicle);

        var secondVehicleResponse = await _client.PostAsJsonAsync("/api/vehicles", new
        {
            manufacturer = "BMW",
            model = "320i",
            chassis = "F30",
            engine = "N20",
            modelYear = 2015,
            productionDate = "2015-01-01",
            vin = "WBA3A11000ABC5678"
        });
        secondVehicleResponse.EnsureSuccessStatusCode();
        var secondVehicle = await secondVehicleResponse.Content.ReadFromJsonAsync<Vehicle>();
        Assert.NotNull(secondVehicle);

        var partResponse = await _client.PostAsJsonAsync("/api/parts", new
        {
            oemPartNumber = "51 11 7 300 001",
            description = "Moldura do para-choque",
            category = "Molduras",
            side = "Direita",
            position = "Dianteira",
            supersededByPartNumber = (string?)null,
            source = "Test"
        });
        partResponse.EnsureSuccessStatusCode();
        var part = await partResponse.Content.ReadFromJsonAsync<Part>();
        Assert.NotNull(part);
        Assert.Equal("Direita", part.Side);
        Assert.Equal("Dianteira", part.Position);

        (await _client.PostAsJsonAsync($"/api/parts/{part.Id}/compatibilities", new
        {
            vehicleId = firstVehicle.Id,
            productionStart = "2012-01-01",
            productionEnd = "2014-12-31"
        })).EnsureSuccessStatusCode();

        (await _client.PostAsJsonAsync($"/api/parts/{part.Id}/compatibilities", new
        {
            vehicleId = secondVehicle.Id,
            productionStart = "2015-01-01",
            productionEnd = "2018-12-31"
        })).EnsureSuccessStatusCode();

        using var lookup = await _client.GetFromJsonAsync<JsonDocument>("/api/parts/oem/51117300001/compatibility");
        Assert.NotNull(lookup);
        var version = lookup.RootElement.GetProperty("brands")[0].GetProperty("models")[0].GetProperty("versions")[0];
        Assert.Equal(2012, version.GetProperty("yearStart").GetInt32());
        Assert.Equal(2018, version.GetProperty("yearEnd").GetInt32());
        Assert.Equal(2, version.GetProperty("vehicleCount").GetInt32());
    }
}
