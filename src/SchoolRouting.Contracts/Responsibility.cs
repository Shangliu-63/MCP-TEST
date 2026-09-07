namespace SchoolRouting.Contracts;

public sealed record Responsibility(
    string Id,
    string Description,
    string Keywords,
    int Priority);
