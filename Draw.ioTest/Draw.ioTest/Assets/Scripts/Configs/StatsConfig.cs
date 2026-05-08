using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "StatsConfig", menuName = "Config/StatsConfig")]
public class StatsConfig : ScriptableObject
{
    public List<int>    m_XPForLevel;
}
