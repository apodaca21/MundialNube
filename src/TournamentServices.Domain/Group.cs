namespace TournamentServices.Domain;

public class Group
{
    public const string IdPattern = Team.IdPattern;

    private Group()
    {
    }

    public Group(string id, string name, string tournamentId)
    {
        Id = id;
        Name = name;
        TournamentId = tournamentId;
    }

    public string Id { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string TournamentId { get; private set; } = string.Empty;
    public List<string> TeamIds { get; private set; } = [];

    public void Rename(string name) => Name = name;

    public void AssignTeams(IEnumerable<string> teamIds)
    {
        var next = teamIds.ToList();
        TeamIds.Clear();
        TeamIds.AddRange(next);
    }
}
