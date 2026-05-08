using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class DeathView : View<DeathView> {

	public Text m_DeadNameText;
	private IBattleRoyaleService m_BattleRoyaleService;

	[Inject]
	public void Construct(IBattleRoyaleService battleRoyaleService)
	{
		m_BattleRoyaleService = battleRoyaleService;
	}

	protected override void Awake()
	{
        base.Awake();
        m_BattleRoyaleService.onElimination += OnElimination;
	}

	protected override void OnDestroySpecific()
	{
		base.OnDestroySpecific();
		m_BattleRoyaleService.onElimination -= OnElimination;
	}

	void OnElimination(Player _EliminatedPlayer)
	{
		if (GameService.currentPhase != GamePhase.GAME)
			return;
		
		m_DeadNameText.text = _EliminatedPlayer.Name;
		if (_EliminatedPlayer is IAPlayer)
		    StartCoroutine(ShowAndHide());
	}

    IEnumerator ShowAndHide()
	{
		Transition(true);
		yield return new WaitForSeconds(1f);
		Transition(false);
	}
}
