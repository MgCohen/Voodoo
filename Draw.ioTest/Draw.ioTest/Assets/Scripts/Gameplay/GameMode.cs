using System.Collections.Generic;
using UnityEngine;

public abstract class GameMode : ScriptableObject
{
    public List<int> m_XPRewardByFinishRank;

    public abstract string StatsKeyPrefix { get; }
    public abstract MatchSettings GetCurrentMatch(IStatsService _Stats);

    public void OnPreEndGame(IStatsService _Stats, int _FinishRank, int _Score)
    {
        _Stats.TryToSetBestScore(_Score);

        int ranking = -1;
        if (_FinishRank == 0)        ranking = 1;
        else if (_FinishRank >= 2)   ranking = 0;

        _Stats.AddGameResult(ranking);

        int idx = Mathf.Min(_FinishRank, m_XPRewardByFinishRank.Count - 1);
        _Stats.SetLastXP(m_XPRewardByFinishRank[idx]);
    }

    public void OnPostEndGame(IStatsService _Stats)
    {
        _Stats.GainXP();
    }
}
