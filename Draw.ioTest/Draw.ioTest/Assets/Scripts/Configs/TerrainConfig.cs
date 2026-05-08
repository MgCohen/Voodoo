using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "TerrainConfig", menuName = "Config/TerrainConfig")]
public class TerrainConfig : ScriptableObject
{
    public Color32 					m_DefaultColor;
    public AnimationCurve           m_DefaultFillCurve;
}
