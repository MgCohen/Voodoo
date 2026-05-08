using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public interface ITerrainService
{
    float WorldHalfWidth { get; }
    float WorldHalfHeight { get; }
    float ScoreMultiplier { get; }
    void ClampPosition(ref Vector3 posBuffer, float spawnBorderOffset);
    void SetTerrain();
    public void FillCircle(Player _Player, Vector3 _Pos, float _Radius, float _Duration, UnityAction _EndCallback = null);
    void ReplaceColor(Vector3 center, float startWidth, float endWidth, int oldColorHash, int playerColorHash, ref Color playerColor);
    Vector3 GetLowestColoredPosition(int colorHash);
    bool NearEdge(Vector3 transformPosition, float warningEdgeDistance);
    float GetColorPercent(int colorHash);
    void AddCircle(Vector3 pos, float getBrushSizeMultiplier, int colorHash, ref Color color);
}
