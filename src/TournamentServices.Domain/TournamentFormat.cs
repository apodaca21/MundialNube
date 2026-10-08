namespace TournamentServices.Domain;

public sealed record TournamentFormat
{
    public const string RoundRobin = "ROUND_ROBIN";
    public const int RequiredGroups = 8;
    public const int RequiredTeamsPerGroup = 4;

    public string Type { get; }
    public int MaxGroups { get; }
    public int MaxTeamsPerGroup { get; }

    public TournamentFormat(string type, int maxGroups, int maxTeamsPerGroup)
    {
        if (type != RoundRobin)
            throw new ArgumentException("The supported tournament format is ROUND_ROBIN.", nameof(type));

        if (maxGroups != RequiredGroups)
            throw new ArgumentOutOfRangeException(nameof(maxGroups), $"Groups must be of {RequiredGroups}.");

        if (maxTeamsPerGroup != RequiredTeamsPerGroup)
            throw new ArgumentOutOfRangeException(nameof(maxTeamsPerGroup), $"Teams per group must be of {RequiredTeamsPerGroup}.");

        Type = type;
        MaxGroups = maxGroups;
        MaxTeamsPerGroup = maxTeamsPerGroup;
    }
}