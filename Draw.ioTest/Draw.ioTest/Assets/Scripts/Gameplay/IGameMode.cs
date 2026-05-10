using System.Collections.Generic;

public interface IGameMode
{
    string StatsKeyPrefix { get; }

    List<PowerUpData> PowerUps { get; }
    float MinPowerUpRate { get; }
    float MaxPowerUpRate { get; }
    float BrushSpawnRate { get; }
    int PlayerCount { get; }
    float AIDifficultyMin { get; }
    float AIDifficultyMax { get; }

    void OnPreEndGame(int _PlayerRank, int _PlayerScore);
    void OnPostEndGame();
}
