using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class SkinSelectionScreen : View<SkinSelectionScreen>
{
    public SkinAtlas      m_Atlas;
    public RectTransform  m_CellParent;
    public SkinCell       m_CellPrefab;

    [Header("Hero (direct 3D in scene)")]
    public BrushMainMenu  m_HeroBrush;
    public GameObject     m_HeroVisuals;

    private IStatsService m_StatsService;

    private readonly List<SkinCell> m_Cells = new List<SkinCell>();
    private int  m_SelectedSkin = -1;
    private bool m_Built;

    [Inject]
    public void Construct(IStatsService _StatsService)
    {
        m_StatsService = _StatsService;
    }

    public void Hide()
    {
        if (GameService.currentPhase == GamePhase.SKIN_SELECTION)
            GameService.ChangePhase(GamePhase.MAIN_MENU);
    }

    protected override void OnGamePhaseChanged(GamePhase _GamePhase)
    {
        base.OnGamePhaseChanged(_GamePhase);

        switch (_GamePhase)
        {
            case GamePhase.SKIN_SELECTION:
                Open();
                break;

            default:
                if (m_Visible)
                    Close();
                break;
        }
    }

    private void Open()
    {
        if (!m_Built)
            Build();

        m_Atlas.SetActive(true);
        if (m_HeroVisuals != null)
            m_HeroVisuals.SetActive(true);
        if (m_HeroBrush != null)
            m_HeroBrush.gameObject.SetActive(true);

        Select(Mathf.Clamp(m_StatsService.FavoriteSkin, 0, m_Cells.Count - 1));
        Transition(true);
    }

    private void Close()
    {
        if (m_SelectedSkin >= 0)
        {
            m_StatsService.FavoriteSkin = m_SelectedSkin;
            GameService.m_PlayerSkinID  = m_SelectedSkin;
        }

        m_Atlas.SetActive(false);
        if (m_HeroVisuals != null)
            m_HeroVisuals.SetActive(false);
        if (m_HeroBrush != null)
            m_HeroBrush.gameObject.SetActive(false);

        Transition(false);
    }

    private void Build()
    {
        List<SkinData> skins = GameService.m_Skins;
        m_Atlas.Build(skins);

        for (int i = 0; i < skins.Count; i++)
        {
            SkinCell cell = Instantiate(m_CellPrefab, m_CellParent);
            cell.Setup(i, m_Atlas.Output, m_Atlas.GetUV(i), Select);
            m_Cells.Add(cell);
        }

        m_Built = true;
    }

    private void Select(int _Index)
    {
        if (_Index < 0 || _Index >= m_Cells.Count)
            return;

        bool initial = m_SelectedSkin < 0;

        if (m_SelectedSkin >= 0 && m_SelectedSkin < m_Cells.Count && m_SelectedSkin != _Index)
            m_Cells[m_SelectedSkin].SetSelected(false, false);

        m_SelectedSkin = _Index;
        m_Cells[_Index].SetSelected(true, !initial);

        if (m_HeroBrush != null)
            m_HeroBrush.Set(GameService.m_Skins[_Index]);
    }
}
