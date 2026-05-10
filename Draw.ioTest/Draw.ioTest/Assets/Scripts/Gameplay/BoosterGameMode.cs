using System.Collections.Generic;
using UnityEngine;

public class BoosterGameMode : IGameMode
{
    private readonly IStatsService m_StatsService;
    private readonly BoosterModeConfig m_Config;

    public BoosterGameMode(IStatsService _StatsService, BoosterModeConfig _Config)
    {
        m_StatsService = _StatsService;
        m_Config = _Config;
    }

    private BoosterLevelData CurrentLevel =>
        m_Config.GetLevel(m_StatsService.GetPlayerLevel());

    public string StatsKeyPrefix => "Booster_";

    public List<PowerUpData> PowerUps => CurrentLevel.m_AvailablePowerUps;
    public List<int> XPPerLevel => m_Config.m_XPPerLevel;
    public float MinPowerUpRate => CurrentLevel.m_MinPowerUpRate;
    public float MaxPowerUpRate => CurrentLevel.m_MaxPowerUpRate;
    public float BrushSpawnRate => CurrentLevel.m_BrushSpawnRate;
    public int PlayerCount => CurrentLevel.m_PlayerCount;
    public float AIDifficultyMin => CurrentLevel.m_AIDifficultyMin;
    public float AIDifficultyMax => CurrentLevel.m_AIDifficultyMax;

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
