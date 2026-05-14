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

    [Header("Legacy skin cycle (used when Skin Selection feature is OFF)")]
    public GameObject m_BrushesPrefab;
    public int m_IdSkin = 0;

    [Header("Booster")]
    public TMP_Text m_BoosterLevelText;
    public GameMode m_BoosterMode;

    [Header("Feature flag targets")]
    public GameObject m_BoosterButton;
    public GameObject m_SkinScreenButton;
    public GameObject m_BrushSelectGroup;

    [Header("Ranks")]
    public string[] m_Ratings;

    private IStatsService m_StatsService;

    [Inject]
    public void Construct(IStatsService statsService)
    {
        m_StatsService = statsService;
    }

    protected override void Awake()
    {
        base.Awake();
        m_IdSkin = m_StatsService.FavoriteSkin;
        RefreshFeatureVisibility();
    }

    public void RefreshFeatureVisibility()
    {
        bool boosterOn    = PlayerPrefs.GetInt(Constants.c_DebugBoosterModeSave, 1) == 1;
        bool skinScreenOn = PlayerPrefs.GetInt(Constants.c_DebugSkinSelectionSave, 1) == 1;

        if (m_BoosterButton != null)    m_BoosterButton.SetActive(boosterOn);
        if (m_SkinScreenButton != null) m_SkinScreenButton.SetActive(skinScreenOn);
        if (m_BrushSelectGroup != null)
        {
            m_BrushSelectGroup.SetActive(!skinScreenOn);
            // When re-activating the legacy hero, sync its mesh/color to
            // whatever the latest FavoriteSkin is — otherwise it shows
            // whatever was last Set() on it (often the initial-load skin).
            if (!skinScreenOn && m_BrushesPrefab != null && m_StatsService != null && GameService != null && GameService.m_Skins != null && GameService.m_Skins.Count > 0)
            {
                int favoriteSkin = Mathf.Clamp(m_StatsService.FavoriteSkin, 0, GameService.m_Skins.Count - 1);
                m_IdSkin = favoriteSkin;
                m_BrushesPrefab.GetComponent<BrushMainMenu>().Set(GameService.m_Skins[favoriteSkin]);
            }
        }
    }

    public void LeftButtonBrush()  { ChangeBrush(m_IdSkin - 1); }
    public void RightButtonBrush() { ChangeBrush(m_IdSkin + 1); }

    public void ChangeBrush(int _NewBrush)
    {
        int count = GameService.m_Skins.Count;
        if (count == 0) return;
        m_IdSkin = ((_NewBrush % count) + count) % count;
        GameService.m_PlayerSkinID = m_IdSkin;
        m_StatsService.FavoriteSkin = m_IdSkin;
        if (m_BrushesPrefab != null && m_BrushesPrefab.activeInHierarchy)
            m_BrushesPrefab.GetComponent<BrushMainMenu>().Set(GameService.m_Skins[m_IdSkin]);
        GameService.SetColor(GameService.ComputeCurrentPlayerColor(true, 0));
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

        if (m_BrushesPrefab != null && m_BrushesPrefab.activeInHierarchy)
        {
            int favoriteSkin = Mathf.Min(m_StatsService.FavoriteSkin, GameService.m_Skins.Count - 1);
            m_BrushesPrefab.GetComponent<BrushMainMenu>().Set(GameService.m_Skins[favoriteSkin]);
        }

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
