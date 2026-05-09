using UnityEngine;

namespace Prototype.Direct3D
{
    /// Per-frame world-space follow of a UI cell. Runs in LateUpdate so the Canvas has
    /// already laid out for the current frame.
    public class BrushScreenSync : MonoBehaviour
    {
        private RectTransform m_CellRect;
        private Transform m_Transform;
        private float m_ZOffsetTowardCamera;
        private Vector3[] m_Corners = new Vector3[4];
        private Camera m_Camera;
        private Canvas m_Canvas;

        public void Init(RectTransform _CellRect, float _ZOffsetTowardCamera)
        {
            m_CellRect = _CellRect;
            m_Transform = transform;
            m_ZOffsetTowardCamera = _ZOffsetTowardCamera;

            m_Canvas = _CellRect.GetComponentInParent<Canvas>();
            if (m_Canvas != null && m_Canvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                m_Camera = m_Canvas.worldCamera;
            }
        }

        private void LateUpdate()
        {
            if (m_CellRect == null) return;

            // Cell corners are in world space (Screen Space - Camera canvas places UI in world).
            m_CellRect.GetWorldCorners(m_Corners);
            Vector3 center = (m_Corners[0] + m_Corners[2]) * 0.5f;

            // Pull the brush slightly toward the camera so it sorts in front of the UI plane.
            if (m_Camera != null)
            {
                Vector3 toCam = (m_Camera.transform.position - center).normalized;
                m_Transform.position = center + toCam * m_ZOffsetTowardCamera;
            }
            else
            {
                m_Transform.position = center;
            }
        }
    }
}
