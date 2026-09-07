namespace SchoolRouting.Contracts;

public sealed record PersonResponsibilities(
    Person Person,
    IReadOnlyList<Responsibility> Responsibilities);
