using System.Collections.Generic;
using DG.Tweening;
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

    [Header("Hero anim")]
    public float          m_HeroAppearDuration = 0.35f;
    public float          m_HeroExitDuration   = 0.20f;
    public float          m_HeroSwapPunch      = 0.15f;
    public float          m_HeroSwapDuration   = 0.35f;

    [Header("Cell anim")]
    public float          m_CellAppearDuration = 0.3f;
    public float          m_CellStaggerDelay   = 0.03f;

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
        if (GameService.currentPhase != GamePhase.SKIN_SELECTION)
            return;

        // Commit selection BEFORE the phase change. GameService.ChangePhase
        // runs the MAIN_MENU case synchronously (SetColor reads m_PlayerSkinID)
        // before firing onGamePhaseChanged, so writes deferred to Close() land
        // one round trip late.
        if (m_SelectedSkin >= 0)
        {
            m_StatsService.FavoriteSkin = m_SelectedSkin;
            GameService.m_PlayerSkinID  = m_SelectedSkin;
        }

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
        AnimateHeroIn();
        AnimateCellsIn();

        Select(Mathf.Clamp(m_StatsService.FavoriteSkin, 0, m_Cells.Count - 1));
        Transition(true);
    }

    private void Close()
    {
        m_Atlas.SetActive(false);
        AnimateHeroOut();

        Transition(false);

        // Reset selection memory so the next Open treats it as a fresh entry
        // — Select() sees m_SelectedSkin < 0, skips the bump branch on the
        // favorite cell, and all 12 cells stagger in cleanly.
        m_SelectedSkin = -1;
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

    private void AnimateCellsIn()
    {
        for (int i = 0; i < m_Cells.Count; i++)
        {
            Transform t = m_Cells[i].transform;
            t.DOKill();
            t.localScale = Vector3.zero;
            t.DOScale(Vector3.one, m_CellAppearDuration)
                .SetDelay(i * m_CellStaggerDelay)
                .SetEase(Ease.OutBack);
        }
    }

    private void Select(int _Index)
    {
        if (_Index < 0 || _Index >= m_Cells.Count)
            return;

        bool initial = m_SelectedSkin < 0;

        if (m_SelectedSkin >= 0 && m_SelectedSkin < m_Cells.Count && m_SelectedSkin != _Index)
            m_Cells[m_SelectedSkin].SetSelected(false, !initial);

        m_SelectedSkin = _Index;
        m_Cells[_Index].SetSelected(true, !initial);

        if (m_HeroBrush != null)
        {
            BrushData brush = GameService.m_Skins[_Index].Brush;
            m_HeroBrush.Set(GameService.m_Skins[_Index]);

            if (m_HeroBrush.m_Current != null)
            {
                // BrushMainMenu.Set parents at localPosition = zero, but the
                // brush prefab itself has its mesh offset from its root. Apply
                // the baked m_MenuCenter so the bounds center lands on
                // m_BrushParent's origin — matches what SkinAtlas does for
                // cells. Without this, moving the hero rig down still leaves
                // the brush biased up by the prefab-internal offset.
                m_HeroBrush.m_Current.localPosition = -brush.m_MenuCenter;

                if (!initial)
                {
                    m_HeroBrush.m_Current.DOKill();
                    m_HeroBrush.m_Current.DOPunchScale(
                        Vector3.one * m_HeroSwapPunch,
                        m_HeroSwapDuration, 0, 0).SetEase(Ease.OutSine);
                }
            }
        }
    }

    private void AnimateHeroIn()
    {
        ScaleIn(m_HeroVisuals);
        ScaleIn(m_HeroBrush != null ? m_HeroBrush.gameObject : null);
    }

    private void AnimateHeroOut()
    {
        ScaleOut(m_HeroVisuals);
        ScaleOut(m_HeroBrush != null ? m_HeroBrush.gameObject : null);
    }

    private void ScaleIn(GameObject _Go)
    {
        if (_Go == null) return;
        _Go.SetActive(true);
        Transform t = _Go.transform;
        t.DOKill();
        t.localScale = Vector3.zero;
        t.DOScale(Vector3.one, m_HeroAppearDuration).SetEase(Ease.OutBack);
    }

    private void ScaleOut(GameObject _Go)
    {
        if (_Go == null) return;
        Transform t = _Go.transform;
        t.DOKill();
        t.DOScale(Vector3.zero, m_HeroExitDuration).SetEase(Ease.InBack)
            .OnComplete(() => { if (_Go != null) _Go.SetActive(false); });
    }
}
