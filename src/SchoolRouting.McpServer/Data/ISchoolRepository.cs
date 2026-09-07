using SchoolRouting.Contracts;

namespace SchoolRouting.McpServer.Data;

public interface ISchoolRepository
{
    Task<IReadOnlyList<Department>> ListDepartmentsAsync(CancellationToken cancellationToken);

    Task<Department?> GetDepartmentAsync(string departmentId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Person>> ListPeopleByDepartmentAsync(
        string departmentId,
        CancellationToken cancellationToken);

    Task<PersonResponsibilities?> GetPersonResponsibilitiesAsync(
        string personId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ResponsibilityCandidate>> SearchResponsibilitiesAsync(
        string query,
        int limit,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ResponsibilityCandidate>> ListResponsibilityCandidatesAsync(
        CancellationToken cancellationToken);
}
