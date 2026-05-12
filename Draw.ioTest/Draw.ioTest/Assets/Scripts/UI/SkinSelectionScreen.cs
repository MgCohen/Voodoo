using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class SkinSelectionScreen : View<SkinSelectionScreen>
{
    [SerializeField] private SkinAtlas      m_Atlas;
    [SerializeField] private RectTransform  m_CellParent;
    [SerializeField] private SkinCell       m_CellPrefab;
    [SerializeField] private RectTransform  m_SelectionHighlight;
    [SerializeField] private SkinAtlas      m_HeroAtlas;
    [SerializeField] private RawImage       m_HeroPreview;

    private IStatsService m_StatsService;

    private readonly List<SkinCell> m_Cells = new List<SkinCell>();
    private int  m_SelectedSkin = -1;
    private bool m_Built;

    [Inject]
    public void Construct(IStatsService _StatsService)
    {
        m_StatsService = _StatsService;
    }

    public void Show()
    {
        if (!m_Built)
            Build();

        m_Atlas.SetActive(true);
        if (m_HeroAtlas != null)
            m_HeroAtlas.SetActive(true);

        Select(Mathf.Clamp(m_StatsService.FavoriteSkin, 0, m_Cells.Count - 1));
        Transition(true);
    }

    public void Hide()
    {
        if (m_SelectedSkin >= 0)
        {
            m_StatsService.FavoriteSkin   = m_SelectedSkin;
            GameService.m_PlayerSkinID    = m_SelectedSkin;
        }

        m_Atlas.SetActive(false);
        if (m_HeroAtlas != null)
            m_HeroAtlas.SetActive(false);

        Transition(false);
    }

    protected override void OnGamePhaseChanged(GamePhase _GamePhase)
    {
        base.OnGamePhaseChanged(_GamePhase);

        if (_GamePhase != GamePhase.MAIN_MENU && m_Visible)
            Hide();
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

        if (m_SelectedSkin >= 0 && m_SelectedSkin < m_Cells.Count)
            m_Cells[m_SelectedSkin].SetSelected(false, Color.white);

        m_SelectedSkin = _Index;

        SkinData skin = GameService.m_Skins[_Index];
        m_Cells[_Index].SetSelected(true, skin.Color.m_Colors[0]);

        if (m_SelectionHighlight != null)
        {
            m_SelectionHighlight.SetParent(m_Cells[_Index].transform, false);
            m_SelectionHighlight.anchorMin = Vector2.zero;
            m_SelectionHighlight.anchorMax = Vector2.one;
            m_SelectionHighlight.offsetMin = Vector2.zero;
            m_SelectionHighlight.offsetMax = Vector2.zero;
        }

        if (m_HeroAtlas != null)
        {
            m_HeroAtlas.Build(new List<SkinData> { skin });
            if (m_HeroPreview != null)
            {
                m_HeroPreview.texture = m_HeroAtlas.Output;
                m_HeroPreview.uvRect  = new Rect(0f, 0f, 1f, 1f);
            }
        }
    }
}
