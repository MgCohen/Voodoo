using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

public interface IBattleRoyaleService
{
    event Action<Player> onElimination;
    public Player GetHumanPlayer();
    void SetPlayers(List<Player> players);
    void SetHumanPlayer(Player player);
    int GetAlivePlayersCount();
    void KillHumanPlayer();
    bool m_IsPlaying { get; set; }
    List<Player> m_Players { get; set; }
    void Order();
    Player GetBestPlayer();
    Player GetPlayer(int ranking);
    float GetTimeBeforeNextElimination();
    void ApplySaveMechanic(Player humanPlayer);
    Player GetWorstPlayer();
}
