namespace TournamentServices.Domain;

public class Group
{
    private readonly List<string> _teamIds = [];

    public const string IdPattern = Team.IdPattern;

    public Group(string id, string name, string tournamentId)
    {
        Id = id;
        Name = name;
        TournamentId = tournamentId;
    }

    public string Id { get; }
    public string Name { get; private set; }
    public string TournamentId { get; }
    public IReadOnlyList<string> TeamIds => _teamIds;

    public void Rename(string name) => Name = name;

    public void AssignTeams(IEnumerable<string> teamIds)
    {
        _teamIds.Clear();
        _teamIds.AddRange(teamIds);
    }
}