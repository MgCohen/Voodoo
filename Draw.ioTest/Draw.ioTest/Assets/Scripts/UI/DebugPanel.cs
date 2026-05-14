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
        Debug.Log("[DebugPanel] ClickToggleDebugPanel. m_Visible=" + m_Visible + " m_Group=" + (m_Group != null) + " m_Panel=" + (m_Panel != null));
        if (m_Visible) Close();
        else            Open();
    }

    private void Open()
    {
        m_Visible = true;
        m_Group.blocksRaycasts = true;
        m_Group.DOKill();
        m_Panel.DOKill();
        m_Group.DOFade(1f, m_FadeDuration).OnComplete(() => m_Group.interactable = true);
        m_Panel.DOScale(Vector3.one, m_ScaleDuration).SetEase(Ease.OutBack);

        Debug.Log("[DebugPanel] Open. self.active=" + gameObject.activeInHierarchy
            + " self.parent=" + (transform.parent != null ? transform.parent.name : "NONE")
            + " group.alpha=" + m_Group.alpha
            + " panel.scale=" + m_Panel.localScale
            + " panel.parent=" + (m_Panel.parent != null ? m_Panel.parent.name : "NONE"));
    }

    private void Close()
    {
        m_Visible = false;
        m_Group.interactable = false;
        m_Group.DOKill();
        m_Panel.DOKill();
        m_Group.DOFade(0f, m_FadeDuration).OnComplete(() => m_Group.blocksRaycasts = false);
        m_Panel.DOScale(Vector3.zero, m_ScaleDuration).SetEase(Ease.InBack);
    }

    public void ClickBoosterToggle()
    {
        BoosterEnabled = !BoosterEnabled;
        RefreshButtonsVisual();
        if (MainMenuView.Instance != null)
            MainMenuView.Instance.RefreshFeatureVisibility();
    }

    public void ClickSkinSelectionToggle()
    {
        SkinSelectionEnabled = !SkinSelectionEnabled;
        RefreshButtonsVisual();
        if (MainMenuView.Instance != null)
            MainMenuView.Instance.RefreshFeatureVisibility();
    }

    private void RefreshButtonsVisual()
    {
        m_BoosterButton.sprite       = BoosterEnabled       ? m_ToggleOnSprite : m_ToggleOffSprite;
        m_SkinSelectionButton.sprite = SkinSelectionEnabled ? m_ToggleOnSprite : m_ToggleOffSprite;
    }
}
