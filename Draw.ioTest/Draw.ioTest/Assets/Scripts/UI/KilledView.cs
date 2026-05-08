using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

public class KilledView : View<KilledView> 
{
    private Player m_HumanPlayer;
    private IBattleRoyaleService m_BattleRoyaleService;

    [Inject]
    public void Construct(IBattleRoyaleService battleRoyaleService)
    {
        m_BattleRoyaleService = battleRoyaleService;
    }

    protected override void Awake()
    {
        base.Awake();
        GameService.onGamePhaseChanged += OnGamePhaseChanged;
    }

    protected override void OnDestroySpecific()
    {
        base.OnDestroySpecific();
        GameService.onGamePhaseChanged -= OnGamePhaseChanged;
    }

    protected override void OnGamePhaseChanged(GamePhase _GamePhase)
    {
        base.OnGamePhaseChanged(_GamePhase);
        switch (_GamePhase)
        {
            case GamePhase.GAME:
                m_HumanPlayer = m_BattleRoyaleService.GetHumanPlayer();
                m_HumanPlayer.onKilled += OnKilled;
                m_HumanPlayer.onRevive += OnRevive;
                break;
        }
    }

    void OnKilled()
    {
        Transition(true);
    }

    void OnRevive()
    {
        Transition(false);
    }
}
