using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class DebugPanel : MonoBehaviour
{
    public CanvasGroup    m_Group;
    public RectTransform  m_Panel;
    public Image          m_BoosterButton;
    public Image          m_SkinSelectionButton;
    public Sprite         m_ToggleOnSprite;
    public Sprite         m_ToggleOffSprite;

    public float          m_FadeDuration  = 0.2f;
    public float          m_ScaleDuration = 0.25f;

    private bool m_Visible;

    private bool BoosterEnabled
    {
        get { return PlayerPrefs.GetInt(Constants.c_DebugBoosterModeSave, 1) == 1; }
        set { PlayerPrefs.SetInt(Constants.c_DebugBoosterModeSave, value ? 1 : 0); }
    }

    private bool SkinSelectionEnabled
    {
        get { return PlayerPrefs.GetInt(Constants.c_DebugSkinSelectionSave, 1) == 1; }
        set { PlayerPrefs.SetInt(Constants.c_DebugSkinSelectionSave, value ? 1 : 0); }
    }

    private void Awake()
    {
        m_Visible = false;
        m_Group.alpha = 0f;
        m_Group.interactable = false;
        m_Group.blocksRaycasts = false;
        m_Panel.localScale = Vector3.zero;
        RefreshButtonsVisual();
    }

    public void ClickToggleDebugPanel()
    {
        if (m_Visible) Close();
        else            Open();
    }

    private void Open()
    {
        // Defensive: if someone disables this GameObject in the scene, Awake
        // never ran and the panel sits at the prefab's alpha=1 / scale=1.
        // Reactivate + reset the starting state so the fade-in animates from
        // hidden every time.
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);
        m_Group.alpha = 0f;
        m_Panel.localScale = Vector3.zero;

        m_Visible = true;
        m_Group.blocksRaycasts = true;
        m_Group.DOKill();
        m_Panel.DOKill();
        m_Group.DOFade(1f, m_FadeDuration).OnComplete(() => m_Group.interactable = true);
        m_Panel.DOScale(Vector3.one, m_ScaleDuration).SetEase(Ease.OutBack);

        // The main Canvas is Screen Space - Camera, so 3D objects closer to
        // the camera than the canvas plane (BrushSelect's brush hero) render
        // in front of UI. Hide that group while the debug panel is open so
        // it doesn't punch through our backdrop. Close() restores it via the
        // flag refresh.
        SuppressLegacyBrushHero(true);
    }

    private void Close()
    {
        m_Visible = false;
        m_Group.interactable = false;
        m_Group.DOKill();
        m_Panel.DOKill();
        m_Group.DOFade(0f, m_FadeDuration).OnComplete(() => m_Group.blocksRaycasts = false);
        m_Panel.DOScale(Vector3.zero, m_ScaleDuration).SetEase(Ease.InBack);

        SuppressLegacyBrushHero(false);
    }

    private static void SuppressLegacyBrushHero(bool _Suppress)
    {
        if (MainMenuView.Instance == null) return;

        if (_Suppress)
        {
            if (MainMenuView.Instance.m_BrushSelectGroup != null)
                MainMenuView.Instance.m_BrushSelectGroup.SetActive(false);
        }
        else
        {
            // Let MainMenuView's flag-driven logic decide based on the
            // current skin-selection toggle state.
            MainMenuView.Instance.RefreshFeatureVisibility();
        }
    }

    public void ClickBoosterToggle()
    {
        BoosterEnabled = !BoosterEnabled;
        RefreshButtonsVisual();
        if (MainMenuView.Instance != null)
            MainMenuView.Instance.RefreshFeatureVisibility();
        // RefreshFeatureVisibility may have re-activated the legacy brush
        // hero — keep it hidden while we're still open.
        SuppressLegacyBrushHero(true);
    }

    public void ClickSkinSelectionToggle()
    {
        SkinSelectionEnabled = !SkinSelectionEnabled;
        RefreshButtonsVisual();
        if (MainMenuView.Instance != null)
            MainMenuView.Instance.RefreshFeatureVisibility();
        SuppressLegacyBrushHero(true);
    }

    private void RefreshButtonsVisual()
    {
        m_BoosterButton.sprite       = BoosterEnabled       ? m_ToggleOnSprite : m_ToggleOffSprite;
        m_SkinSelectionButton.sprite = SkinSelectionEnabled ? m_ToggleOnSprite : m_ToggleOffSprite;
    }
}
