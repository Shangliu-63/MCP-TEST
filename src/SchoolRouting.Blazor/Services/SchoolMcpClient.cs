using System.Text.Encodings.Web;
using System.Text.Json;
using ModelContextProtocol.Client;
using SchoolRouting.Contracts;

namespace SchoolRouting.Blazor.Services;

public sealed class SchoolMcpClient(IConfiguration configuration) : ISchoolMcpClient, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private McpClient? _client;

    public async Task<IReadOnlyList<string>> ListToolNamesAsync(
        CancellationToken cancellationToken = default)
    {
        var client = await GetClientAsync(cancellationToken);
        var tools = await client.ListToolsAsync(cancellationToken: cancellationToken);
        return tools.Select(tool => tool.Name).OrderBy(name => name).ToArray();
    }

    public async Task<ToolCallDisplayResult> SearchResponsibilitiesAsync(
        string query,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = await GetClientAsync(cancellationToken);
            var result = await client.CallToolAsync(
                "semantic_search_responsibilities",
                new Dictionary<string, object?>
                {
                    ["documentText"] = query,
                    ["limit"] = limit
                },
                cancellationToken: cancellationToken);

            IReadOnlyList<ResponsibilityCandidate> candidates = [];
            string json;

            if (result.StructuredContent is { } structuredContent)
            {
                candidates = JsonSerializer.Deserialize<List<ResponsibilityCandidate>>(
                    structuredContent.GetRawText(), JsonOptions) ?? [];
                json = JsonSerializer.Serialize(candidates, JsonOptions);
            }
            else
            {
                json = JsonSerializer.Serialize(result.Content, JsonOptions);
            }

            return new ToolCallDisplayResult(
                result.IsError is not true,
                "semantic_search_responsibilities",
                candidates,
                json,
                result.IsError is true ? "MCP 工具回傳錯誤。" : null);
        }
        catch (Exception exception)
        {
            return new ToolCallDisplayResult(
                false,
                "semantic_search_responsibilities",
                [],
                "{}",
                exception.Message);
        }
    }

    private async Task<McpClient> GetClientAsync(CancellationToken cancellationToken)
    {
        if (_client is not null)
        {
            return _client;
        }

        await _connectLock.WaitAsync(cancellationToken);
        try
        {
            if (_client is not null)
            {
                return _client;
            }

            var endpoint = configuration["Mcp:Endpoint"]
                ?? throw new InvalidOperationException("尚未設定 Mcp:Endpoint。");
            var transport = new HttpClientTransport(new HttpClientTransportOptions
            {
                Endpoint = new Uri(endpoint)
            });
            _client = await McpClient.CreateAsync(transport, cancellationToken: cancellationToken);
            return _client;
        }
        finally
        {
            _connectLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
        {
            await _client.DisposeAsync();
        }

        _connectLock.Dispose();
    }
}
