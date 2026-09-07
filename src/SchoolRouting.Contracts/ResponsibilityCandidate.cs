namespace SchoolRouting.Contracts;

public sealed record ResponsibilityCandidate(
    string ResponsibilityId,
    string Description,
    string Keywords,
    int Priority,
    string DepartmentId,
    string DepartmentName,
    string? PersonId,
    string? PersonName,
    string? PersonTitle,
    IReadOnlyList<string> MatchedTerms,
    int MatchScore,
    int SemanticScore = 0,
    int KeywordScore = 0,
    string Confidence = "規則比對",
    string MatchReason = "");
