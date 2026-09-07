using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using SchoolRouting.McpServer.Data;
using SchoolRouting.McpServer.Semantic;

namespace SchoolRouting.McpServer.Tests;

public sealed class SqliteSchoolRepositoryTests
{
    private static SqliteSchoolRepository CreateRepository()
    {
        var projectRoot = FindProjectRoot();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SchoolDatabase:Path"] = Path.Combine(projectRoot, "data", "school.db")
            })
            .Build();
        var environment = new TestWebHostEnvironment(projectRoot);
        return new SqliteSchoolRepository(configuration, environment);
    }

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "data", "school.db")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("找不到包含 data/school.db 的專案根目錄。");
    }

    [Fact]
    public async Task ListDepartments_ReturnsTenActiveDepartments()
    {
        var repository = CreateRepository();
        var departments = await repository.ListDepartmentsAsync(CancellationToken.None);
        Assert.Equal(10, departments.Count);
    }

    [Fact]
    public async Task SearchResponsibilities_FindsInformationSecurityOwner()
    {
        var repository = CreateRepository();
        var candidates = await repository.SearchResponsibilitiesAsync(
            "資訊安全 教育訓練", 10, CancellationToken.None);
        Assert.NotEmpty(candidates);
        Assert.Contains(candidates, candidate =>
            candidate.DepartmentName == "圖書資訊服務處" &&
            candidate.PersonName == "彭詩涵");
    }

    [Fact]
    public async Task GetPersonResponsibilities_ReturnsExpectedAssignments()
    {
        var repository = CreateRepository();
        var result = await repository.GetPersonResponsibilitiesAsync("P019", CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal("資訊安全管理員", result.Person.Title);
        Assert.Equal(2, result.Responsibilities.Count);
    }

    [Fact]
    public async Task SemanticSearch_FindsSecurityOwnerWithoutConfiguredKeywords()
    {
        var service = new DomainSemanticRoutingService(CreateRepository());

        var candidates = await service.SearchAsync(
            "校內主機遭勒索軟體加密，疑似發生個資外洩，請立即調查。",
            5,
            CancellationToken.None);

        Assert.NotEmpty(candidates);
        Assert.Equal("P019", candidates[0].PersonId);
        Assert.Equal("圖書資訊服務處", candidates[0].DepartmentName);
        Assert.Contains("資訊安全", candidates[0].MatchedTerms);
        Assert.True(candidates[0].SemanticScore >= 80);
    }

    [Fact]
    public async Task SemanticSearch_MarksUnknownTopicForManualReview()
    {
        var service = new DomainSemanticRoutingService(CreateRepository());

        var candidates = await service.SearchAsync(
            "請評估校園無人機飛航與起降場域申請程序。",
            3,
            CancellationToken.None);

        Assert.Equal(3, candidates.Count);
        Assert.All(candidates, candidate =>
            Assert.Equal("低－需人工判斷", candidate.Confidence));
    }

    private sealed class TestWebHostEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "SchoolRouting.McpServer.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = contentRootPath;
        public string EnvironmentName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
