using UnityEngine;
using UnityEngine.UI;

public class DebugPanel : MonoBehaviour
{
    public Animator m_PanelAnim;
    public Image    m_BoosterButton;
    public Image    m_SkinSelectionButton;
    public Sprite   m_ToggleOnSprite;
    public Sprite   m_ToggleOffSprite;

    private bool m_PanelVisible;

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
        m_PanelVisible = false;
        m_PanelAnim.SetBool("Visible", m_PanelVisible);
        RefreshButtonsVisual();
    }

    public void ClickToggleDebugPanel()
    {
        m_PanelVisible = !m_PanelVisible;
        m_PanelAnim.SetBool("Visible", m_PanelVisible);
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
