using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class MessageView : SingletonMB<MessageView>
{
	private const float 		c_TransitionDuration = 1.0f;

	public CanvasGroup 			m_Container;
	public Text 				m_Message;
	public RectTransform 		m_Start;
	public RectTransform 		m_End;
	public AnimationCurve		m_AlphaCurve;
	public AnimationCurve		m_MovementCurve;

	private bool 				m_IsShowing;
	private Queue<string> 		m_MessageQueue;

	// Cache
	private IGameService			m_GameService;
	private RectTransform		m_MessageTr;

	[Inject]
	public void Construct(IGameService gameService)
	{
		m_GameService = gameService;
	}

	void Awake()
	{
		m_IsShowing = false;
		m_MessageQueue = new Queue<string> ();

		// Cache
		m_MessageTr = m_Message.GetComponent<RectTransform> ();

		m_GameService.onGamePhaseChanged += OnGamePhaseChanged;
	}

	protected override void OnDestroySpecific()
	{
		base.OnDestroySpecific();
		m_GameService.onGamePhaseChanged -= OnGamePhaseChanged;
	}

	void OnGamePhaseChanged (GamePhase _GamePhase)
	{
		switch (_GamePhase)
		{
		case GamePhase.END:
			StopAllCoroutines ();
			m_Container.alpha = 0.0f;
			m_MessageQueue.Clear ();
			break;
		}
	}

	public void QueueMessage(string _Message)
	{
		if (m_IsShowing)
			m_MessageQueue.Enqueue (_Message);
		else
			ShowMessage (_Message);
	}

	private void ShowMessage(string _Message)
	{
		m_IsShowing = true;
		StartCoroutine(ShowMessageCoroutine(_Message));
	}

	private IEnumerator ShowMessageCoroutine(string _Message)
	{
		float time = 0.0f;

		m_Container.alpha = 0.0f;
		m_Message.text = _Message;

		while (time < 1.0f)
		{
			time += Time.deltaTime / c_TransitionDuration;
			float percent = m_AlphaCurve.Evaluate (time);
			m_Container.alpha = percent;
			m_MessageTr.localScale = Vector3.Lerp (Vector3.one, Vector3.one * 1.1f, percent);
			yield return null;
		}

		m_Container.alpha = 0.0f;

		if (m_MessageQueue.Count == 0)
			m_IsShowing = false;
		else
			StartCoroutine(ShowMessageCoroutine(m_MessageQueue.Dequeue()));
	}
}
