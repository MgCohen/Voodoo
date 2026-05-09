using System;
using UnityEngine;
using UnityEngine.UI;

namespace Prototype.Flipbook
{
    public class FlipbookCell : MonoBehaviour
    {
        [SerializeField] private RawImage m_RawImage;
        [SerializeField] private Button m_Button;

        private int m_Index;
        private int m_FrameCount;
        private int m_RowCount;
        private float m_PlaybackFps;
        private bool m_Playing;
        private Action<int> m_OnTapped;

        public int Index => m_Index;

        public void Setup(int _Index, Texture _Strip, int _FrameCount, int _RowCount, float _PlaybackFps, Action<int> _OnTapped)
        {
            m_Index = _Index;
            m_FrameCount = Mathf.Max(1, _FrameCount);
            m_RowCount = Mathf.Max(1, _RowCount);
            m_PlaybackFps = _PlaybackFps;
            m_OnTapped = _OnTapped;
            m_Playing = false;

            if (m_RawImage != null)
            {
                m_RawImage.texture = _Strip;
                m_RawImage.uvRect = ComputeUv(0);
            }

            if (m_Button != null)
            {
                m_Button.onClick.RemoveAllListeners();
                m_Button.onClick.AddListener(() => m_OnTapped?.Invoke(m_Index));
            }
        }

        public void StartPlayback()
        {
            m_Playing = true;
        }

        public void StopPlayback()
        {
            m_Playing = false;
        }

        private void Update()
        {
            if (!m_Playing || m_RawImage == null) return;

            int frame = ((int)(Time.time * m_PlaybackFps)) % m_FrameCount;
            m_RawImage.uvRect = ComputeUv(frame);
        }

        private Rect ComputeUv(int _Frame)
        {
            float u = _Frame / (float)m_FrameCount;
            // Strip rows are stacked from bottom-to-top in the RT (row 0 is at the bottom).
            // We placed cell i at strip-row (Count-1-i), so v lookup is (Count-1-i)/Count.
            float v = (m_RowCount - 1 - m_Index) / (float)m_RowCount;
            return new Rect(u, v, 1f / m_FrameCount, 1f / m_RowCount);
        }
    }
}
