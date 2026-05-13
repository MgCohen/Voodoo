using System;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class SkinCell : MonoBehaviour
{
    public RawImage m_Preview;
    public Button   m_Button;
    public Image    m_Background;

    [Header("Selection")]
    public Color    m_BackgroundIdle     = new Color(0.18f, 0.22f, 0.45f, 1f);
    public Color    m_BackgroundSelected = new Color(0.40f, 0.30f, 0.70f, 1f);
    public float    m_ColorDuration      = 0.2f;

    [Header("Bump")]
    public float    m_BumpScale    = 0.15f;
    public float    m_BumpDuration = 0.3f;
    public Ease     m_BumpEase     = Ease.OutSine;

    private int           m_Index;
    private Action<int>   m_OnClick;

    public void Setup(int _Index, Texture _Atlas, Rect _UV, Action<int> _OnClick)
    {
        m_Index   = _Index;
        m_OnClick = _OnClick;

        m_Preview.texture = _Atlas;
        m_Preview.uvRect  = _UV;

        m_Button.onClick.RemoveAllListeners();
        m_Button.onClick.AddListener(OnClick);

        SetSelected(false, false);
    }

    public void SetSelected(bool _Selected, bool _Animate)
    {
        Color target = _Selected ? m_BackgroundSelected : m_BackgroundIdle;
        if (m_Background != null)
        {
            m_Background.DOKill();
            if (_Animate)
                m_Background.DOColor(target, m_ColorDuration);
            else
                m_Background.color = target;
        }

        if (_Selected && _Animate)
        {
            transform.DOKill();
            transform.localScale = Vector3.one;
            transform.DOPunchScale(Vector3.one * m_BumpScale, m_BumpDuration, 0, 0).SetEase(m_BumpEase);
        }
    }

    private void OnClick()
    {
        if (m_OnClick != null)
            m_OnClick(m_Index);
    }
}
