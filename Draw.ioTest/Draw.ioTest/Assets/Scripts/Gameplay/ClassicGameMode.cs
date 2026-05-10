using System.Collections.Generic;
using UnityEngine;

public class ClassicGameMode : IGameMode
{
    private readonly IStatsService m_StatsService;
    private readonly GameConfig m_Config;
    private readonly List<PowerUpData> m_PowerUps;

    public ClassicGameMode(IStatsService _StatsService, GameConfig _Config)
    {
        m_StatsService = _StatsService;
        m_Config = _Config;
        m_PowerUps = new List<PowerUpData>(Resources.LoadAll<PowerUpData>("PowerUps"));
    }

    public string StatsKeyPrefix => "";

    public List<PowerUpData> PowerUps => m_PowerUps;
    public List<int> XPPerLevel => m_Config.m_XPPerLevel;
    public float MinPowerUpRate => m_Config.m_MinPowerUpRate;
    public float MaxPowerUpRate => m_Config.m_MaxPowerUpRate;
    public float BrushSpawnRate => m_Config.m_BrushSpawnRate;
    public int PlayerCount => m_Config.m_PlayerCount;
    public float AIDifficultyMin => Mathf.Clamp01(m_StatsService.GetLevel() / 2f);
    public float AIDifficultyMax => 1f;

    public void OnPreEndGame(int _PlayerRank, int _PlayerScore)
    {
        m_StatsService.TryToSetBestScore(_PlayerScore);

        int rankingScore = -1;
        if (_PlayerRank == 0)        rankingScore = 1;
        else if (_PlayerRank >= 2)   rankingScore = 0;

        m_StatsService.AddGameResult(rankingScore);
        int xpIndex = Mathf.Min(_PlayerRank, m_Config.m_XPByRank.Count - 1);
        m_StatsService.SetLastXP(m_Config.m_XPByRank[xpIndex]);
    }

    public void OnPostEndGame()
    {
        m_StatsService.GainXP();
    }
}
