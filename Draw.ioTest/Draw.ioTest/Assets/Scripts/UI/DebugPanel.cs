using UnityEngine;
using UnityEngine.UI;

public class DebugPanel : MonoBehaviour
{
    public Animator  m_PanelAnim;
    public Image     m_BoosterButton;
    public Image     m_SkinSelectionButton;
    public Sprite    m_ToggleOnSprite;
    public Sprite    m_ToggleOffSprite;

    // Main-menu button that opens this panel. Disabled together with the
    // panel in release builds so the debug menu has no entry point at all.
    public GameObject m_DebugEntryButton;

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
        // Debug.isDebugBuild is true in the Editor and in Development Build
        // APKs, false in release builds. Reviewers receive a Development
        // Build so they can toggle features; shipped releases hide the menu
        // entirely.
        if (!Debug.isDebugBuild)
        {
            if (m_DebugEntryButton != null) m_DebugEntryButton.SetActive(false);
            gameObject.SetActive(false);
            return;
        }

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
