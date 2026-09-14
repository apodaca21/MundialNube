namespace TournamentServices.Domain;

public sealed record TournamentFormat
{
    public const string RoundRobin = "ROUND_ROBIN";
    public const int MinGroups = 1;
    public const int MaxGroupsLimit = 16;
    public const int MinTeamsPerGroup = 2;
    public const int MaxTeamsPerGroupLimit = 8;

    public string Type { get; }
    public int MaxGroups { get; }
    public int MaxTeamsPerGroup { get; }

    public TournamentFormat(string type, int maxGroups, int maxTeamsPerGroup)
    {
        if (type != RoundRobin)
            throw new ArgumentException("The supported tournament format is ROUND_ROBIN.", nameof(type));

        if (maxGroups < MinGroups || maxGroups > MaxGroupsLimit)
            throw new ArgumentOutOfRangeException(nameof(maxGroups), "Groups must be between 1 and 16.");

        if (maxTeamsPerGroup < MinTeamsPerGroup || maxTeamsPerGroup > MaxTeamsPerGroupLimit)
            throw new ArgumentOutOfRangeException(nameof(maxTeamsPerGroup), "Teams per group must be between 2 and 8.");

        Type = type;
        MaxGroups = maxGroups;
        MaxTeamsPerGroup = maxTeamsPerGroup;
    }
}
