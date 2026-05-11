using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ClassicModeConfig", menuName = "Config/ClassicModeConfig")]
public class ClassicModeConfig : ScriptableObject
{
    public int m_DebugLevel = 1;

    public List<int> m_XPByRank;
    public float m_MinPowerUpRate = 1f;
    public float m_MaxPowerUpRate = 2.5f;
    public float m_BrushSpawnRate = 16f;
    [Range(1, 8)]
    public int m_PlayerCount = 8;
}
