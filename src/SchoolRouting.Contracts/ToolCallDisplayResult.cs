namespace SchoolRouting.Contracts;

public sealed record ToolCallDisplayResult(
    bool Success,
    string ToolName,
    IReadOnlyList<ResponsibilityCandidate> Candidates,
    string Json,
    string? Error = null);
