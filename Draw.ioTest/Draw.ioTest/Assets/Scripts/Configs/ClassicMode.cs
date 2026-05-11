using UnityEngine;

[CreateAssetMenu(fileName = "ClassicMode", menuName = "GameMode/Classic")]
public class ClassicMode : GameMode
{
    public MatchSettings m_Match;

    public override string StatsKeyPrefix => "";
    public override MatchSettings GetCurrentMatch(IStatsService _Stats) => m_Match;
}
