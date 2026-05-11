using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BoosterMode", menuName = "GameMode/Booster")]
public class BoosterMode : GameMode
{
    public List<BoosterLevelData> m_AuthoredLevels;
    public BoosterLevelData m_FallbackLevel;

    public override string StatsKeyPrefix => "Booster_";

    public override MatchSettings GetCurrentMatch(IStatsService _Stats)
        => GetLevelData(_Stats.GetPlayerLevel() - 1).m_Match;

    public override int GetXPForLevel(int _LevelIndex)
        => GetLevelData(_LevelIndex).m_XPToNextLevel;

    private BoosterLevelData GetLevelData(int _Index)
    {
        if (_Index >= 0 && _Index < m_AuthoredLevels.Count)
            return m_AuthoredLevels[_Index];
        return m_FallbackLevel;
    }
}
