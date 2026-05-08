using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

public class View<T> : SingletonMB<T> where T : MonoBehaviour
{
	public float			m_FadeInDuration = 0.3f;
	public float 			m_FadeOutDuration = 0.3f;
	public CanvasGroup      m_Group;

	protected bool 			m_Visible;

	// Cache
	protected IGameService	GameService;

	// Buffers
	private float 			m_StartTime;
	private float 			m_Duration;
	private bool			m_InTransition;
	private bool 			m_InOrOut;

	
	[Inject]
	public void ConstructView(IGameService gameService)
	{
		GameService = gameService;
	}

	protected virtual void Awake()
	{
		// Init
		m_Visible = false;
		m_Group.alpha = 0.0f;
		m_Group.interactable = false;
		m_Group.blocksRaycasts = false;

		GameService.onGamePhaseChanged += OnGamePhaseChanged;
	}

	protected override void OnDestroySpecific()
	{
		base.OnDestroySpecific();
		GameService.onGamePhaseChanged -= OnGamePhaseChanged;
	}

	protected virtual void OnGamePhaseChanged (GamePhase _GamePhase) {}

	public void Transition(bool _InOrOut)
	{
		m_Visible = _InOrOut;

		m_StartTime = Time.time;
		m_InTransition = true;
		m_InOrOut = _InOrOut;
		m_Duration = _InOrOut ? m_FadeInDuration : m_FadeOutDuration;
		m_Group.interactable = false;
		m_Group.blocksRaycasts = false;
	}

	protected virtual void Update()
	{
		if (m_InTransition)
		{
			float time = Time.time - m_StartTime;
			float percent = time / m_Duration;

			if (percent < 1.0f)
			{
				m_Group.alpha = m_InOrOut ? percent : (1.0f - percent);
			}
			else
			{
				m_InTransition = false;
				m_Group.alpha = m_InOrOut ? 1.0f : 0.0f;
				m_Group.interactable = m_InOrOut;
				m_Group.blocksRaycasts = m_InOrOut;
			}
		}
	}
}
