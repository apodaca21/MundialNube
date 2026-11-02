namespace TournamentServices.Domain;

public static class MatchStages
{
    public const string Group = "GROUP";
    public const string RoundOf16 = "ROUND_OF_16";
    public const string Quarterfinal = "QUARTERFINAL";
    public const string Semifinal = "SEMIFINAL";
    public const string ThirdPlace = "THIRD_PLACE";
    public const string Final = "FINAL";

    public static bool IsKnockout(string stage) => stage != Group;

    public static int OrderOf(string stage) => stage switch
    {
        Group => 0,
        RoundOf16 => 1,
        Quarterfinal => 2,
        Semifinal => 3,
        ThirdPlace => 4,
        Final => 5,
        _ => int.MaxValue
    };

    public static IReadOnlyList<string> NextStages(string stage) => stage switch
    {
        Group => [RoundOf16],
        RoundOf16 => [Quarterfinal],
        Quarterfinal => [Semifinal],
        Semifinal => [ThirdPlace, Final],
        _ => []
    };
}
