using UnityEngine;

namespace Prototype.Direct3D
{
    /// Reads the ScrollRect viewport's screen-space rect each frame and pushes it as
    /// a global shader vector that the BrushClipped shader uses to discard fragments
    /// outside the viewport.
    public class ClipRectFeeder : MonoBehaviour
    {
        [SerializeField] private RectTransform m_Viewport;

        private static readonly int s_ClipRectId = Shader.PropertyToID("_SkinClipRect");
        private Vector3[] m_Corners = new Vector3[4];
        private Camera m_Camera;
        private bool m_HasCamera;

        private void Awake()
        {
            ResolveCamera();
        }

        private void ResolveCamera()
        {
            if (m_Viewport == null) return;
            var canvas = m_Viewport.GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                m_Camera = canvas.worldCamera;
                m_HasCamera = m_Camera != null;
            }
            else
            {
                m_HasCamera = false;
            }
        }

        private void LateUpdate()
        {
            if (m_Viewport == null) return;
            if (!m_HasCamera) ResolveCamera();

            m_Viewport.GetWorldCorners(m_Corners);

            Vector2 a = WorldToScreen(m_Corners[0]);
            Vector2 b = WorldToScreen(m_Corners[2]);

            float xMin = Mathf.Min(a.x, b.x);
            float yMin = Mathf.Min(a.y, b.y);
            float xMax = Mathf.Max(a.x, b.x);
            float yMax = Mathf.Max(a.y, b.y);

            Shader.SetGlobalVector(s_ClipRectId, new Vector4(xMin, yMin, xMax, yMax));
        }

        private Vector2 WorldToScreen(Vector3 _World)
        {
            if (m_Camera == null) return new Vector2(_World.x, _World.y);
            return m_Camera.WorldToScreenPoint(_World);
        }
    }
}
