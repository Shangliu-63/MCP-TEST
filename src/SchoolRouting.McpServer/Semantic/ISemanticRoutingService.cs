using SchoolRouting.Contracts;

namespace SchoolRouting.McpServer.Semantic;

public interface ISemanticRoutingService
{
    Task<IReadOnlyList<ResponsibilityCandidate>> SearchAsync(
        string documentText,
        int limit,
        CancellationToken cancellationToken);
}
