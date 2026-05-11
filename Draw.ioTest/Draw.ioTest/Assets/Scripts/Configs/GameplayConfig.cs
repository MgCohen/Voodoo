using UnityEngine;

[CreateAssetMenu(fileName = "GameplayConfig", menuName = "Config/GameplayConfig")]
public class GameplayConfig : ScriptableObject
{
    [Header("Prefabs")]
    public PowerUp m_BrushPowerUpPrefab;
    public GameObject m_HumanPlayer;
    public GameObject m_IAPlayer;
    public Transform m_HumanSpotlight;
}
