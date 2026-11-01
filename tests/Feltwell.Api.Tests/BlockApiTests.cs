using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Feltwell.Api.Tests;

public sealed class HealthTests : IClassFixture<FeltwellApiFactory>
{
    private readonly HttpClient _client;

    public HealthTests(FeltwellApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ok", body.GetProperty("status").GetString());
        Assert.Equal("Feltwell", body.GetProperty("service").GetString());
    }
}

public sealed class BlockApiTests : IClassFixture<FeltwellApiFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly FeltwellApiFactory _factory;
    private readonly HttpClient _client;

    public BlockApiTests(FeltwellApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync()
    {
        _factory.Store.Clear();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreateAndGetBlock_Works()
    {
        var created = await _client.PostAsJsonAsync("/api/v1/blocks", new
        {
            code = "HB-01",
            name = "Maple Cloche Block",
            room = "Front Room",
            headSizeCm = 56,
            maxActiveHats = 2,
            notes = "Primary felt blocking."
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var block = await created.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var id = block.GetProperty("id").GetGuid();
        Assert.Equal("HB-01", block.GetProperty("code").GetString());
        Assert.Equal("Open", block.GetProperty("status").GetString());

        var fetched = await _client.GetAsync($"/api/v1/blocks/{id}");
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
    }

    [Fact]
    public async Task HatJob_RespectsCapacity_AndOfflineBlock()
    {
        var blockResponse = await _client.PostAsJsonAsync("/api/v1/blocks", new
        {
            code = "HB-02",
            name = "Straw Brim Block",
            room = "Window Bay",
            headSizeCm = 58,
            maxActiveHats = 1
        });
        var blockId = (await blockResponse.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("id").GetGuid();

        var first = await _client.PostAsJsonAsync($"/api/v1/blocks/{blockId}/hats", new
        {
            label = "Harbor Cloche",
            client = "Nora Ellis",
            material = "Felt",
            style = "Cloche"
        });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var overflow = await _client.PostAsJsonAsync($"/api/v1/blocks/{blockId}/hats", new
        {
            label = "Garden Fedora",
            client = "Sam Ortega",
            material = "Felt",
            style = "Fedora"
        });
        Assert.Equal(HttpStatusCode.Conflict, overflow.StatusCode);

        var offline = await _client.PatchAsJsonAsync($"/api/v1/blocks/{blockId}", new
        {
            status = "Offline"
        });
        Assert.Equal(HttpStatusCode.OK, offline.StatusCode);

        var jobId = (await first.Content.ReadFromJsonAsync<JsonElement>(JsonOptions))
            .GetProperty("id").GetGuid();
        await _client.PatchAsJsonAsync($"/api/v1/blocks/{blockId}/hats/{jobId}", new
        {
            status = "Ready"
        });

        var blocked = await _client.PostAsJsonAsync($"/api/v1/blocks/{blockId}/hats", new
        {
            label = "Extra",
            client = "Jordan Lee",
            material = "Straw",
            style = "WideBrim"
        });
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
    }

    [Fact]
    public async Task List_FiltersByRoomAndStatus()
    {
        await _client.PostAsJsonAsync("/api/v1/blocks", new
        {
            code = "HB-A",
            name = "Block A",
            room = "Front Room",
            headSizeCm = 56,
            maxActiveHats = 2
        });
        await _client.PostAsJsonAsync("/api/v1/blocks", new
        {
            code = "HB-B",
            name = "Block B",
            room = "Back Room",
            headSizeCm = 54,
            maxActiveHats = 1
        });

        var response = await _client.GetAsync("/api/v1/blocks?room=Front&status=Open");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Equal(1, body.GetProperty("total").GetInt32());
        Assert.Equal("HB-A", body.GetProperty("items")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task GetMissingBlock_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/v1/blocks/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
