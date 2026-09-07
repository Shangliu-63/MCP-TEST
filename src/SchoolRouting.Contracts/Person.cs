namespace SchoolRouting.Contracts;

public sealed record Person(
    string Id,
    string Name,
    string Title,
    string Email,
    string DepartmentId,
    string DepartmentName);
