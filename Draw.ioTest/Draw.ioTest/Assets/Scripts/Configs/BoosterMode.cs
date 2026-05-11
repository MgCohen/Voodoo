using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BoosterMode", menuName = "GameMode/Booster")]
public class BoosterMode : GameMode
{
    public List<BoosterLevelData> m_AuthoredLevels;
    public BoosterLevelData m_FallbackLevel;

    [Header("XP curve")]
    public int m_BaseXPThreshold = 100;
    public int m_XPIncrementPerLevel = 50;

    public override string StatsKeyPrefix => "Booster_";

    public override MatchSettings GetCurrentMatch(IStatsService _Stats)
        => GetLevelData(_Stats.GetPlayerLevel() - 1).m_Match;

    public override int GetXPForLevel(int _LevelIndex)
        => m_BaseXPThreshold + Mathf.Max(0, _LevelIndex) * m_XPIncrementPerLevel;

    public override float GetAIDifficultyMin(IStatsService _Stats)
        => GetLevelData(_Stats.GetPlayerLevel() - 1).m_Match.m_AIDifficultyMin;

    public override float GetAIDifficultyMax(IStatsService _Stats)
        => GetLevelData(_Stats.GetPlayerLevel() - 1).m_Match.m_AIDifficultyMax;

    private BoosterLevelData GetLevelData(int _Index)
    {
        if (_Index >= 0 && _Index < m_AuthoredLevels.Count)
            return m_AuthoredLevels[_Index];
        return m_FallbackLevel;
    }
}
