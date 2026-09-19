namespace TournamentServices.Domain;
// Nota, Cambiar Nombre de Round Robin a Mundial

public sealed record TournamentFormat
{
    public const string RoundRobin = "ROUND_ROBIN";
    public const int MinGroups = 8;
    public const int MaxGroupsLimit = 8;
    public const int MinTeamsPerGroup = 4;
    public const int MaxTeamsPerGroupLimit = 4;

    public string Type { get; }
    public int MaxGroups { get; }
    public int MaxTeamsPerGroup { get; }

    public TournamentFormat(string type, int maxGroups, int maxTeamsPerGroup)
    {
        if (type != RoundRobin)
            throw new ArgumentException("The supported tournament format is ROUND_ROBIN.", nameof(type));

        if (maxGroups < MinGroups || maxGroups > MaxGroupsLimit)
            throw new ArgumentOutOfRangeException(nameof(maxGroups), "Groups must be of 8.");

        if (maxTeamsPerGroup < MinTeamsPerGroup || maxTeamsPerGroup > MaxTeamsPerGroupLimit)
            throw new ArgumentOutOfRangeException(nameof(maxTeamsPerGroup), "Teams per group must be of 4.");

        Type = type;
        MaxGroups = maxGroups;
        MaxTeamsPerGroup = maxTeamsPerGroup;
    }
}
