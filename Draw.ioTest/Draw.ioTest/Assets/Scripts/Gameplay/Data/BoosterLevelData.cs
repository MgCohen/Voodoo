using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BoosterLevelData", menuName = "Data/BoosterLevelData")]
public class BoosterLevelData : ScriptableObject
{
    [Header("Power-ups")]
    public List<PowerUpData> m_AvailablePowerUps;
    public float m_MinPowerUpRate = 1f;
    public float m_MaxPowerUpRate = 2.5f;
    public float m_BrushSpawnRate = 16f;

    [Header("Match")]
    [Range(1, 8)]
    public int m_PlayerCount = 8;

    [Header("AI")]
    public float m_AIDifficultyMin = 0f;
    public float m_AIDifficultyMax = 1f;
}
