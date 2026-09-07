using ModelContextProtocol.AspNetCore;
using SchoolRouting.McpServer.Data;
using SchoolRouting.McpServer.Semantic;
using SchoolRouting.McpServer.Tools;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ISchoolRepository, SqliteSchoolRepository>();
builder.Services.AddSingleton<ISemanticRoutingService, DomainSemanticRoutingService>();
builder.Services
    .AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true)
    .WithTools<SchoolRoutingTools>();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    service = "School Routing MCP Server",
    status = "ready",
    mcpEndpoint = "/mcp"
}));
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapMcp("/mcp");

app.Run();

public partial class Program;
