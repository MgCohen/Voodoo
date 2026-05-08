using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IGameService
{
    event Action<GamePhase> onGamePhaseChanged;
    event Action onEndGame;
    event Action onScoresCalculated;
    bool m_IsPlaying { get; set; }
    GamePhase currentPhase { get; }
    int m_PlayerSkinID { get; set; }
    List<SkinData> m_Skins { get; set; }
    List<Player> m_Players { get; set; }
    void OnGameFinished();
    void AddMapObject(GameObject powerUpGameObject);
    void TriggerSceneReload();
    int ComputeCurrentPlayerColor(bool b, int i);
    void SetColor(int computeCurrentPlayerColor);
    void ChangePhase(GamePhase loading);
    int ComputeCurrentPlayeBrushIndex(bool b, int i);
    void SkipRV();
    void TryRevive();
    Player GetBestPlayer();
}
