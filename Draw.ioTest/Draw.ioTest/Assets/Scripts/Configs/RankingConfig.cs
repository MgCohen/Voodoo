using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "RankingConfig", menuName = "Config/RankingConfig")]
public class RankingConfig : ScriptableObject
{
    public List<int> m_XPByRank;
    public int m_MaxRanks = 10;
    public int m_PointsPerRank = 3000;
}
