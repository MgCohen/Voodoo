using System;
using UnityEngine;
using UnityEngine.UI;

namespace Prototype.Atlas
{
    public class AtlasCell : MonoBehaviour
    {
        [SerializeField] private RawImage m_RawImage;
        [SerializeField] private Button m_Button;

        private int m_Index;
        private Action<int> m_OnTapped;

        public int Index => m_Index;

        public void Setup(int _Index, Texture _Atlas, Rect _UvRect, Action<int> _OnTapped)
        {
            m_Index = _Index;
            m_OnTapped = _OnTapped;

            if (m_RawImage != null)
            {
                m_RawImage.texture = _Atlas;
                m_RawImage.uvRect = _UvRect;
            }

            if (m_Button != null)
            {
                m_Button.onClick.RemoveAllListeners();
                m_Button.onClick.AddListener(() => m_OnTapped?.Invoke(m_Index));
            }
        }
    }
}
