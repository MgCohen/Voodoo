using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BoosterMode", menuName = "GameMode/Booster")]
public class BoosterMode : GameMode
{
    public List<BoosterLevelData> m_AuthoredLevels;
    public BoosterLevelData m_FallbackLevel;

    public override string StatsKeyPrefix => "Booster_";

    public override MatchSettings GetCurrentMatch(IStatsService _Stats)
    {
        int idx = _Stats.GetPlayerLevel() - 1;
        if (idx >= 0 && idx < m_AuthoredLevels.Count)
            return m_AuthoredLevels[idx].m_Match;
        return m_FallbackLevel.m_Match;
    }
}
