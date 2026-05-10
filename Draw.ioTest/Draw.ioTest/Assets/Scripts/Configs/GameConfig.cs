using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "GameConfig", menuName = "Config/GameConfig")]
public class GameConfig : ScriptableObject
{
    public int m_DebugLevel = 1;

    [Header("Prefabs")]
    public PowerUp m_BrushPowerUpPrefab;
    public GameObject m_HumanPlayer;
    public GameObject m_IAPlayer;
    public Transform m_HumanSpotlight;

    [Header("Mode")]
    public List<int> m_XPByRank;
    public float m_MinPowerUpRate = 1f;
    public float m_MaxPowerUpRate = 2.5f;
    public float m_BrushSpawnRate = 16f;
    [Range(1, 8)]
    public int m_PlayerCount = 8;
}
