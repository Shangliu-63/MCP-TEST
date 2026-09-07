namespace SchoolRouting.Contracts;

public sealed record Department(
    string Id,
    string Name,
    string Description,
    string? ParentDepartmentId);
