using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using SchoolRouting.Contracts;

namespace SchoolRouting.McpServer.Data;

public sealed partial class SqliteSchoolRepository : ISchoolRepository
{
    private readonly string _connectionString;

    public SqliteSchoolRepository(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var configuredPath = configuration["SchoolDatabase:Path"]
            ?? throw new InvalidOperationException("尚未設定 SchoolDatabase:Path。");
        var databasePath = Path.GetFullPath(configuredPath, environment.ContentRootPath);

        if (!File.Exists(databasePath))
        {
            throw new FileNotFoundException("找不到學校測試資料庫。", databasePath);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    public async Task<IReadOnlyList<Department>> ListDepartmentsAsync(
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id, name, description, parent_department_id
            FROM departments
            WHERE active = 1
            ORDER BY id;
            """;

        var results = new List<Department>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new Department(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3)));
        }

        return results;
    }

    public async Task<Department?> GetDepartmentAsync(
        string departmentId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id, name, description, parent_department_id
            FROM departments
            WHERE id = $departmentId AND active = 1;
            """;

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$departmentId", departmentId.Trim());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new Department(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3));
    }

    public async Task<IReadOnlyList<Person>> ListPeopleByDepartmentAsync(
        string departmentId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT p.id, p.name, p.title, p.email, p.department_id, d.name
            FROM people AS p
            JOIN departments AS d ON d.id = p.department_id
            WHERE p.department_id = $departmentId AND p.active = 1
            ORDER BY p.id;
            """;

        var results = new List<Person>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$departmentId", departmentId.Trim());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(ReadPerson(reader));
        }

        return results;
    }

    public async Task<PersonResponsibilities?> GetPersonResponsibilitiesAsync(
        string personId,
        CancellationToken cancellationToken)
    {
        const string personSql = """
            SELECT p.id, p.name, p.title, p.email, p.department_id, d.name
            FROM people AS p
            JOIN departments AS d ON d.id = p.department_id
            WHERE p.id = $personId AND p.active = 1;
            """;
        const string responsibilitySql = """
            SELECT id, description, keywords, priority
            FROM responsibilities
            WHERE person_id = $personId AND active = 1
            ORDER BY priority DESC, id;
            """;

        await using var connection = await OpenConnectionAsync(cancellationToken);
        Person? person;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = personSql;
            command.Parameters.AddWithValue("$personId", personId.Trim());
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            person = await reader.ReadAsync(cancellationToken) ? ReadPerson(reader) : null;
        }

        if (person is null)
        {
            return null;
        }

        var responsibilities = new List<Responsibility>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = responsibilitySql;
            command.Parameters.AddWithValue("$personId", personId.Trim());
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                responsibilities.Add(new Responsibility(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetInt32(3)));
            }
        }

        return new PersonResponsibilities(person, responsibilities);
    }

    public async Task<IReadOnlyList<ResponsibilityCandidate>> SearchResponsibilitiesAsync(
        string query,
        int limit,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT responsibility_id, description, keywords, priority,
                   department_id, department_name, person_id, person_name, person_title
            FROM responsibility_search_view;
            """;

        var terms = SearchSeparatorRegex()
            .Split(query.Trim())
            .Where(term => !string.IsNullOrWhiteSpace(term))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (terms.Length == 0)
        {
            return [];
        }

        var candidates = new List<ResponsibilityCandidate>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var searchableText = string.Join(' ',
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(5),
                reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                reader.IsDBNull(8) ? string.Empty : reader.GetString(8));
            var matchedTerms = terms
                .Where(term => searchableText.Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (matchedTerms.Length == 0)
            {
                continue;
            }

            var priority = reader.GetInt32(3);
            candidates.Add(new ResponsibilityCandidate(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                priority,
                reader.GetString(4),
                reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                matchedTerms,
                (matchedTerms.Length * 10) + priority));
        }

        return candidates
            .OrderByDescending(candidate => candidate.MatchScore)
            .ThenByDescending(candidate => candidate.Priority)
            .Take(Math.Clamp(limit, 1, 30))
            .ToArray();
    }

    public async Task<IReadOnlyList<ResponsibilityCandidate>> ListResponsibilityCandidatesAsync(
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT responsibility_id, description, keywords, priority,
                   department_id, department_name, person_id, person_name, person_title
            FROM responsibility_search_view
            ORDER BY responsibility_id;
            """;

        var candidates = new List<ResponsibilityCandidate>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            candidates.Add(new ResponsibilityCandidate(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                [],
                0));
        }

        return candidates;
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static Person ReadPerson(SqliteDataReader reader) => new(
        reader.GetString(0),
        reader.GetString(1),
        reader.GetString(2),
        reader.GetString(3),
        reader.GetString(4),
        reader.GetString(5));

    [GeneratedRegex(@"[\s,，、；;]+")]
    private static partial Regex SearchSeparatorRegex();
}
