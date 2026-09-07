using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System.Text.Json;

try
{
    var endpoint = args.Length > 0 ? args[0] : "http://localhost:5200/mcp";
    var transport = new HttpClientTransport(new HttpClientTransportOptions
    {
        Endpoint = new Uri(endpoint)
    });

    await using var client = await McpClient.CreateAsync(transport);
    var tools = await client.ListToolsAsync();
    var toolNames = tools.Select(tool => tool.Name).OrderBy(name => name).ToArray();

    Console.WriteLine($"Connected to {endpoint}");
    Console.WriteLine($"Tools ({toolNames.Length}): {string.Join(", ", toolNames)}");

    var expectedTools = new[]
    {
        "get_department",
        "get_person_responsibilities",
        "list_departments",
        "list_people_by_department",
        "search_responsibilities",
        "semantic_search_responsibilities"
    };

    if (!expectedTools.All(toolNames.Contains))
    {
        return Fail("MCP Server 未提供完整的預期工具。");
    }

    var result = await client.CallToolAsync(
        "semantic_search_responsibilities",
        new Dictionary<string, object?>
        {
            ["documentText"] = "校內主機遭勒索軟體加密，疑似發生個資外洩，請立即調查。",
            ["limit"] = 10
        });

    if (result.IsError is true)
    {
        return Fail("semantic_search_responsibilities 回傳錯誤。");
    }

    var content = result.StructuredContent?.GetRawText()
        ?? string.Join(' ', result.Content.OfType<TextContentBlock>().Select(block => block.Text));
    using var document = JsonDocument.Parse(content);
    var hasExpectedCandidate = document.RootElement.ValueKind == JsonValueKind.Array &&
        document.RootElement.EnumerateArray().Any(candidate =>
            candidate.TryGetProperty("departmentName", out var departmentName) &&
            candidate.TryGetProperty("personName", out var personName) &&
            departmentName.GetString() == "圖書資訊服務處" &&
            personName.GetString() == "彭詩涵");

    if (!hasExpectedCandidate)
    {
        return Fail("搜尋結果沒有包含預期的資訊安全承辦人。");
    }

    Console.WriteLine("semantic_search_responsibilities returned the expected candidate.");
    Console.WriteLine("MCP smoke test passed.");
    return 0;
}
catch (Exception exception)
{
    return Fail($"Unexpected error: {exception.Message}");
}

static int Fail(string message)
{
    Console.Error.WriteLine($"FAILED: {message}");
    return 1;
}
