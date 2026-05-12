using System.Collections.Generic;
using UnityEngine;
using Zenject;

public class SkinSelectionScreen : MonoBehaviour
{
    [SerializeField] private Animator       m_Animator;
    [SerializeField] private SkinAtlas      m_Atlas;
    [SerializeField] private RectTransform  m_CellParent;
    [SerializeField] private SkinCell       m_CellPrefab;
    [SerializeField] private RectTransform  m_SelectionHighlight;
    [SerializeField] private BrushMainMenu  m_Hero;

    private IGameService  m_GameService;
    private IStatsService m_StatsService;

    private readonly List<SkinCell> m_Cells = new List<SkinCell>();
    private int  m_SelectedSkin = -1;
    private bool m_Built;

    [Inject]
    public void Construct(IGameService _GameService, IStatsService _StatsService)
    {
        m_GameService  = _GameService;
        m_StatsService = _StatsService;
    }

    public void Show()
    {
        if (!m_Built)
            Build();

        m_Atlas.SetActive(true);
        Select(Mathf.Clamp(m_StatsService.FavoriteSkin, 0, m_Cells.Count - 1));
        m_Animator.SetBool("Visible", true);
    }

    public void Hide()
    {
        if (m_SelectedSkin >= 0)
        {
            m_StatsService.FavoriteSkin    = m_SelectedSkin;
            m_GameService.m_PlayerSkinID   = m_SelectedSkin;
            if (m_Hero != null)
                m_Hero.Set(m_GameService.m_Skins[m_SelectedSkin]);
        }

        m_Atlas.SetActive(false);
        m_Animator.SetBool("Visible", false);
    }

    private void Build()
    {
        List<SkinData> skins = m_GameService.m_Skins;
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

        Color tint = m_GameService.m_Skins[_Index].Color.m_Colors[0];
        m_Cells[_Index].SetSelected(true, tint);

        if (m_SelectionHighlight != null)
        {
            m_SelectionHighlight.SetParent(m_Cells[_Index].transform, false);
            m_SelectionHighlight.anchorMin = Vector2.zero;
            m_SelectionHighlight.anchorMax = Vector2.one;
            m_SelectionHighlight.offsetMin = Vector2.zero;
            m_SelectionHighlight.offsetMax = Vector2.zero;
        }

        if (m_Hero != null)
            m_Hero.Set(m_GameService.m_Skins[_Index]);
    }
}
