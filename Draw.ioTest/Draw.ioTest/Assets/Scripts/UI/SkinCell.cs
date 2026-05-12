using System;
using UnityEngine;
using UnityEngine.UI;

public class SkinCell : MonoBehaviour
{
    [SerializeField] private RawImage m_Preview;
    [SerializeField] private Button   m_Button;
    [SerializeField] private Image    m_Background;

    private int m_Index;
    private Action<int> m_OnClick;

    public void Setup(int _Index, Texture _Atlas, Rect _UV, Action<int> _OnClick)
    {
        m_Index   = _Index;
        m_OnClick = _OnClick;

        m_Preview.texture = _Atlas;
        m_Preview.uvRect  = _UV;

        m_Button.onClick.RemoveAllListeners();
        m_Button.onClick.AddListener(OnClick);
    }

    public void SetBackgroundColor(Color _Color)
    {
        if (m_Background != null)
            m_Background.color = _Color;
    }

    private void OnClick()
    {
        if (m_OnClick != null)
            m_OnClick(m_Index);
    }
}
