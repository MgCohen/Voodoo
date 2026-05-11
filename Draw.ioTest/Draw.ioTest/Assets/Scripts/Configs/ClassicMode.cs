using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ClassicMode", menuName = "GameMode/Classic")]
public class ClassicMode : GameMode
{
    public MatchSettings m_Match;
    public List<int> m_XPThresholdPerLevel;

    public override string StatsKeyPrefix => "";
    public override MatchSettings GetCurrentMatch(IStatsService _Stats) => m_Match;

    public override int GetXPForLevel(int _LevelIndex)
    {
        int idx = Mathf.Min(_LevelIndex, m_XPThresholdPerLevel.Count - 1);
        return m_XPThresholdPerLevel[idx];
    }

    public override float GetAIDifficultyMin(IStatsService _Stats)
        => Mathf.Clamp01(_Stats.GetLevel() / 2f);
}
