using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BoosterModeConfig", menuName = "Config/BoosterModeConfig")]
public class BoosterModeConfig : ScriptableObject
{
    public List<BoosterLevelData> m_AuthoredLevels;
    public BoosterLevelData m_FallbackTemplate;
    public List<int> m_XPByRank;

    public BoosterLevelData GetLevel(int _OneIndexedLevel)
    {
        int index = _OneIndexedLevel - 1;
        if (index >= 0 && index < m_AuthoredLevels.Count)
            return m_AuthoredLevels[index];
        return m_FallbackTemplate;
    }
}
