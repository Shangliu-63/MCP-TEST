using System.ComponentModel;
using ModelContextProtocol.Server;
using SchoolRouting.Contracts;
using SchoolRouting.McpServer.Data;
using SchoolRouting.McpServer.Semantic;

namespace SchoolRouting.McpServer.Tools;

[McpServerToolType]
public sealed class SchoolRoutingTools(
    ISchoolRepository repository,
    ISemanticRoutingService semanticRoutingService)
{
    [McpServerTool(Name = "list_departments", Title = "列出學校處室", ReadOnly = true,
        OpenWorld = false, UseStructuredContent = true)]
    [Description("列出所有啟用中的學校處室。適合在不知道處室代碼時使用。")]
    public Task<IReadOnlyList<Department>> ListDepartmentsAsync(
        CancellationToken cancellationToken) =>
        repository.ListDepartmentsAsync(cancellationToken);

    [McpServerTool(Name = "get_department", Title = "取得處室資料", ReadOnly = true,
        OpenWorld = false, UseStructuredContent = true)]
    [Description("依處室代碼取得處室名稱、說明與上級單位。")]
    public Task<Department?> GetDepartmentAsync(
        [Description("處室代碼，例如 D007")] string departmentId,
        CancellationToken cancellationToken) =>
        repository.GetDepartmentAsync(departmentId, cancellationToken);

    [McpServerTool(Name = "list_people_by_department", Title = "列出處室人員", ReadOnly = true,
        OpenWorld = false, UseStructuredContent = true)]
    [Description("列出指定處室內所有啟用中的人員。")]
    public Task<IReadOnlyList<Person>> ListPeopleByDepartmentAsync(
        [Description("處室代碼，例如 D005")] string departmentId,
        CancellationToken cancellationToken) =>
        repository.ListPeopleByDepartmentAsync(departmentId, cancellationToken);

    [McpServerTool(Name = "get_person_responsibilities", Title = "取得人員工作職掌",
        ReadOnly = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("取得指定人員的基本資料與完整工作職掌。")]
    public Task<PersonResponsibilities?> GetPersonResponsibilitiesAsync(
        [Description("人員代碼，例如 P019")] string personId,
        CancellationToken cancellationToken) =>
        repository.GetPersonResponsibilitiesAsync(personId, cancellationToken);

    [McpServerTool(Name = "search_responsibilities", Title = "搜尋工作職掌", ReadOnly = true,
        OpenWorld = false, UseStructuredContent = true)]
    [Description("依公文主題或任務關鍵字搜尋可能負責的處室、人員與工作職掌。")]
    public Task<IReadOnlyList<ResponsibilityCandidate>> SearchResponsibilitiesAsync(
        [Description("以空白分隔的公文主題或關鍵字，例如：資訊安全 教育訓練")] string query,
        [Description("最多回傳筆數，範圍為 1 到 30")] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult<IReadOnlyList<ResponsibilityCandidate>>([]);
        }

        return repository.SearchResponsibilitiesAsync(query, limit, cancellationToken);
    }

    [McpServerTool(Name = "semantic_search_responsibilities", Title = "語意搜尋工作職掌",
        ReadOnly = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("依完整公文內容進行領域概念、同義詞與文字相似度的混合搜尋，適合公文未包含既有關鍵字時使用。")]
    public Task<IReadOnlyList<ResponsibilityCandidate>> SemanticSearchResponsibilitiesAsync(
        [Description("完整公文文字，建議包含主旨與內容")] string documentText,
        [Description("最多回傳筆數，範圍為 1 到 30")] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentText))
        {
            return Task.FromResult<IReadOnlyList<ResponsibilityCandidate>>([]);
        }

        return semanticRoutingService.SearchAsync(documentText, limit, cancellationToken);
    }
}
