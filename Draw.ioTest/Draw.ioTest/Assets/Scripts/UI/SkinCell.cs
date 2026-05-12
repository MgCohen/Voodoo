using System;
using UnityEngine;
using UnityEngine.UI;

public class SkinCell : MonoBehaviour
{
    [SerializeField] private RawImage m_Preview;
    [SerializeField] private Image    m_Disc;
    [SerializeField] private Button   m_Button;
    [SerializeField] private Sprite   m_DiscIdle;
    [SerializeField] private Sprite   m_DiscSelected;

    private int m_Index;
    private Action<int> m_OnClick;

    public void Setup(int _Index, Texture _Atlas, Rect _UV, Action<int> _OnClick)
    {
        m_Index   = _Index;
        m_OnClick = _OnClick;

        m_Preview.texture = _Atlas;
        m_Preview.uvRect  = _UV;

        SetSelected(false, Color.white);

        m_Button.onClick.RemoveAllListeners();
        m_Button.onClick.AddListener(OnClick);
    }

    public void SetSelected(bool _Selected, Color _Tint)
    {
        if (m_Disc == null)
            return;

        m_Disc.sprite = _Selected ? m_DiscSelected : m_DiscIdle;
        m_Disc.color  = _Selected ? _Tint : Color.white;
    }

    private void OnClick()
    {
        if (m_OnClick != null)
            m_OnClick(m_Index);
    }
}
