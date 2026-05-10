using System.Collections.Generic;
using UnityEngine;

public class ClassicGameMode : IGameMode
{
    private readonly IStatsService m_StatsService;
    private readonly List<int> m_XPByRank;
    private readonly List<PowerUpData> m_PowerUps;

    public ClassicGameMode(IStatsService _StatsService, List<int> _XPByRank)
    {
        m_StatsService = _StatsService;
        m_XPByRank = _XPByRank;
        m_PowerUps = new List<PowerUpData>(Resources.LoadAll<PowerUpData>("PowerUps"));
    }

    public string StatsKeyPrefix => "";

    public List<PowerUpData> PowerUps => m_PowerUps;
    public float MinPowerUpRate => 1f;
    public float MaxPowerUpRate => 2.5f;
    public float BrushSpawnRate => 16f;
    public int PlayerCount => 8;
    public float AIDifficultyMin => Mathf.Clamp01(m_StatsService.GetLevel() / 2f);
    public float AIDifficultyMax => 1f;

    public void OnPreEndGame(int _PlayerRank, int _PlayerScore)
    {
        m_StatsService.TryToSetBestScore(_PlayerScore);

        int rankingScore = -1;
        if (_PlayerRank == 0)        rankingScore = 1;
        else if (_PlayerRank >= 2)   rankingScore = 0;

        m_StatsService.AddGameResult(rankingScore);
        m_StatsService.SetLastXP(m_XPByRank[_PlayerRank]);
    }

    public void OnPostEndGame()
    {
        m_StatsService.GainXP();
    }
}
