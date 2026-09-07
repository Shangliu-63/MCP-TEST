using SchoolRouting.Contracts;

namespace SchoolRouting.Blazor.Services;

public interface ISchoolMcpClient
{
    Task<IReadOnlyList<string>> ListToolNamesAsync(CancellationToken cancellationToken = default);

    Task<ToolCallDisplayResult> SearchResponsibilitiesAsync(
        string query,
        int limit = 10,
        CancellationToken cancellationToken = default);
}
