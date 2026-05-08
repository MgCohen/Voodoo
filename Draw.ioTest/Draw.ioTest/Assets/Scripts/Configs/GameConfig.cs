using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "GameConfig", menuName = "Config/GameConfig")]
public class GameConfig : ScriptableObject
{
    public int m_DebugLevel = 1;

    public PowerUp m_BrushPowerUpPrefab;
    public GameObject m_HumanPlayer;
    public GameObject m_IAPlayer;
    public Transform m_HumanSpotlight;

    public List<int> m_XPByRank;
}
