using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class MainMenuView : View<MainMenuView>
{
    private const string m_BestScorePrefix = "BEST SCORE ";

    public Text m_BestScoreText;
    public Image m_BestScoreBar;
    public GameObject m_BestScoreObject;
    public InputField m_InputField;
    public List<Image> m_ColoredImages;
    public List<Text> m_ColoredTexts;

    public GameObject m_PointsPerRank;
    public RankingView m_RankingView;

    [Header("Booster")]
    public TMP_Text m_BoosterLevelText;
    public GameMode m_BoosterMode;


    [Header("Ranks")]
    public string[] m_Ratings;

    private IStatsService m_StatsService;

    [Inject]
    public void Construct(IStatsService statsService)
    {
        m_StatsService = statsService;
    }

    public void OnPlayButton()
    {
        if (GameService.currentPhase == GamePhase.MAIN_MENU)
            GameService.ChangePhase(GamePhase.LOADING);
    }

    public void OnBoosterButton()
    {
        if (GameService.currentPhase == GamePhase.MAIN_MENU)
            GameService.StartBoosterMode();
    }

    private void RefreshBoosterLevelLabel()
    {
        if (m_BoosterLevelText == null || m_BoosterMode == null)
            return;
        m_BoosterLevelText.text = "Lvl " + m_StatsService.GetPlayerLevel(m_BoosterMode).ToString("D2");
    }

    protected override void OnGamePhaseChanged(GamePhase _GamePhase)
    {
        base.OnGamePhaseChanged(_GamePhase);

        switch (_GamePhase)
        {
            case GamePhase.MAIN_MENU:
                RefreshBoosterLevelLabel();
                Transition(true);
                break;

            case GamePhase.LOADING:
            case GamePhase.SKIN_SELECTION:
                if (m_Visible)
                    Transition(false);
                break;
        }
    }

    public void SetTitleColor(Color _Color)
    {
        string playerName = m_StatsService.GetNickname();

        if (playerName != null)
            m_InputField.text = playerName;

        for (int i = 0; i < m_ColoredImages.Count; ++i)
            m_ColoredImages[i].color = _Color;

        for (int i = 0; i < m_ColoredTexts.Count; i++)
            m_ColoredTexts[i].color = _Color;
            
        m_RankingView.gameObject.SetActive(true);
        m_RankingView.RefreshNormal();
    }

    public void OnSetPlayerName(string _Name)
    {
        m_StatsService.SetNickname(_Name);
    }

    public string GetRanking(int _Rank)
    {
        return m_Ratings[_Rank];
    }

    public int GetRankingCount()
    {
        return m_Ratings.Length;
    }

    public void OpenSkinScreen()
    {
        if (GameService.currentPhase == GamePhase.MAIN_MENU)
            GameService.ChangePhase(GamePhase.SKIN_SELECTION);
    }
}
