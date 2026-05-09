using System.Collections.Generic;
using UnityEngine;
using Prototype.Shared;

namespace Prototype.Flipbook
{
    /// Capture rotation as a finite flipbook ONCE at startup, then disable the rendering
    /// camera permanently. Cells play back via UV-stepping over a strip RenderTexture.
    public class SkinFlipbookCapturer : MonoBehaviour, IBenchmarkSelectable, IBenchmarkRecolorable
    {
        public enum CapturePacing { Amortized, Burst }

        [Header("Brush prefabs (drop 2 menu prefabs)")]
        [SerializeField] private GameObject[] m_BrushPrefabs;

        [Header("Atlas Capture")]
        [SerializeField] private Camera m_AtlasCamera;
        [SerializeField] private Transform m_GridRoot;
        [SerializeField] private int m_CellPx = 128;
        [SerializeField] private int m_FrameCount = 30;
        [SerializeField] private float m_PlaybackFps = 30f;
        [SerializeField] private float m_WorldCellSize = 5f;
        [SerializeField] private int m_SkinPreviewLayer = 31;
        [SerializeField] private CapturePacing m_Pacing = CapturePacing.Amortized;
        [SerializeField] private bool m_RecaptureOnRecolor = true;

        [Header("UI")]
        [SerializeField] private RectTransform m_GridParent;
        [SerializeField] private GameObject m_CellPrefab;
        [SerializeField] private RectTransform m_SelectionHighlight;

        private RenderTexture m_FrameRT;
        private RenderTexture m_FlipbookStripRT;

        private GameObject[] m_BrushInstances;
        private List<Renderer>[] m_BrushRenderers;
        private BrushRotationStepped[] m_BrushRotations;
        private FlipbookCell[] m_Cells;

        private enum Phase { Initializing, Capturing, Ready }
        private Phase m_Phase = Phase.Initializing;
        private int m_FrameIdx;

        private int m_SelectedIndex;

        public int Count => PrototypeSkinSet.Count;

        private void Awake()
        {
            BuildRTs();
            BuildWorldGrid();
            BuildUI();
            FrameAtlasCamera();
            BeginCapture();

            if (m_Pacing == CapturePacing.Burst)
            {
                CaptureAllNow();
            }
        }

        private void OnDestroy()
        {
            ReleaseRT(ref m_FrameRT);
            ReleaseRT(ref m_FlipbookStripRT);
        }

        private static void ReleaseRT(ref RenderTexture _Rt)
        {
            if (_Rt == null) return;
            _Rt.Release();
            Destroy(_Rt);
            _Rt = null;
        }

        private void BuildRTs()
        {
            int cols = PrototypeSkinSet.Columns;
            int rows = PrototypeSkinSet.Rows;

            m_FrameRT = new RenderTexture(cols * m_CellPx, rows * m_CellPx, 16, RenderTextureFormat.ARGB32)
            {
                name = "FlipbookFrameRT",
                useMipMap = false,
                antiAliasing = 1
            };
            m_FrameRT.Create();

            m_FlipbookStripRT = new RenderTexture(m_FrameCount * m_CellPx, PrototypeSkinSet.Count * m_CellPx, 0, RenderTextureFormat.ARGB32)
            {
                name = "FlipbookStripRT",
                useMipMap = false,
                antiAliasing = 1
            };
            m_FlipbookStripRT.Create();

            m_AtlasCamera.targetTexture = m_FrameRT;
            m_AtlasCamera.cullingMask = 1 << m_SkinPreviewLayer;
            m_AtlasCamera.clearFlags = CameraClearFlags.SolidColor;
            m_AtlasCamera.backgroundColor = new Color(0, 0, 0, 0);
            m_AtlasCamera.orthographic = true;
            m_AtlasCamera.enabled = false; // we drive renders manually
        }

        private void BuildWorldGrid()
        {
            int cols = PrototypeSkinSet.Columns;
            int rows = PrototypeSkinSet.Rows;
            int prefabCount = m_BrushPrefabs != null ? m_BrushPrefabs.Length : 0;

            m_BrushInstances = new GameObject[PrototypeSkinSet.Count];
            m_BrushRenderers = new List<Renderer>[PrototypeSkinSet.Count];
            m_BrushRotations = new BrushRotationStepped[PrototypeSkinSet.Count];

            if (prefabCount == 0)
            {
                Debug.LogError("[SkinFlipbookCapturer] No brush prefabs assigned.");
                return;
            }

            for (int i = 0; i < PrototypeSkinSet.Count; i++)
            {
                int col = i % cols;
                int row = i / cols;
                Vector3 localPos = new Vector3(
                    (col - (cols - 1) * 0.5f) * m_WorldCellSize,
                    -(row - (rows - 1) * 0.5f) * m_WorldCellSize,
                    0f);

                GameObject prefab = m_BrushPrefabs[PrototypeSkinSet.PrefabIndex(i, prefabCount)];
                GameObject brush = Instantiate(prefab, m_GridRoot);
                brush.name = $"Brush_{i:00}";
                brush.transform.localPosition = localPos;
                brush.transform.localRotation = Quaternion.identity;
                brush.transform.localScale = Vector3.one;

                PrototypeBrushUtil.SetLayerRecursive(brush, m_SkinPreviewLayer);
                PrototypeBrushUtil.StripGameplayComponents(brush);

                // Replace any auto-rotation with our deterministic stepper.
                var auto = brush.GetComponent<BrushRotation>();
                if (auto != null) Destroy(auto);
                var stepped = brush.GetComponent<BrushRotationStepped>();
                if (stepped == null) stepped = brush.AddComponent<BrushRotationStepped>();
                stepped.SetAuto(false);
                m_BrushRotations[i] = stepped;

                var renderers = PrototypeBrushUtil.CollectRenderers(brush);
                PrototypeBrushUtil.ApplyColorToRenderers(renderers, PrototypeSkinSet.ColorFor(i));

                m_BrushInstances[i] = brush;
                m_BrushRenderers[i] = renderers;
            }
        }

        private void BuildUI()
        {
            if (m_CellPrefab == null || m_GridParent == null)
            {
                Debug.LogError("[SkinFlipbookCapturer] Missing UI cell prefab or grid parent.");
                return;
            }

            m_Cells = new FlipbookCell[PrototypeSkinSet.Count];
            for (int i = 0; i < PrototypeSkinSet.Count; i++)
            {
                GameObject cellGO = Instantiate(m_CellPrefab, m_GridParent);
                cellGO.name = $"FlipbookCell_{i:00}";
                var cell = cellGO.GetComponent<FlipbookCell>();
                cell.Setup(i, m_FlipbookStripRT, m_FrameCount, PrototypeSkinSet.Count, m_PlaybackFps, OnCellTapped);
                m_Cells[i] = cell;
            }
        }

        private void FrameAtlasCamera()
        {
            int cols = PrototypeSkinSet.Columns;
            int rows = PrototypeSkinSet.Rows;

            m_AtlasCamera.aspect = (float)cols / rows;
            m_AtlasCamera.orthographicSize = rows * m_WorldCellSize * 0.5f;

            var camTr = m_AtlasCamera.transform;
            camTr.SetParent(m_GridRoot, false);
            camTr.localPosition = new Vector3(0f, 0f, -10f);
            camTr.localRotation = Quaternion.identity;
        }

        private void BeginCapture()
        {
            m_Phase = Phase.Capturing;
            m_FrameIdx = 0;
        }

        private void CaptureAllNow()
        {
            while (m_FrameIdx < m_FrameCount)
            {
                CaptureOneFrame();
            }
            FinishCapture();
        }

        private void LateUpdate()
        {
            if (m_Phase != Phase.Capturing) return;
            if (m_Pacing == CapturePacing.Burst) return;

            CaptureOneFrame();

            if (m_FrameIdx >= m_FrameCount)
            {
                FinishCapture();
            }
        }

        private void CaptureOneFrame()
        {
            float deg = 360f / m_FrameCount;
            for (int i = 0; i < m_BrushRotations.Length; i++)
            {
                if (m_BrushRotations[i] != null)
                    m_BrushRotations[i].Step(deg);
            }

            m_AtlasCamera.Render();

            int cols = PrototypeSkinSet.Columns;
            int rows = PrototypeSkinSet.Rows;
            for (int i = 0; i < PrototypeSkinSet.Count; i++)
            {
                int col = i % cols;
                int row = i / cols;
                int srcX = col * m_CellPx;
                int srcY = (rows - 1 - row) * m_CellPx;
                int dstX = m_FrameIdx * m_CellPx;
                int dstY = (PrototypeSkinSet.Count - 1 - i) * m_CellPx;

                Graphics.CopyTexture(
                    m_FrameRT, 0, 0, srcX, srcY, m_CellPx, m_CellPx,
                    m_FlipbookStripRT, 0, 0, dstX, dstY);
            }

            m_FrameIdx++;
        }

        private void FinishCapture()
        {
            m_AtlasCamera.enabled = false;
            m_Phase = Phase.Ready;

            if (m_FrameRT != null)
            {
                m_AtlasCamera.targetTexture = null;
                ReleaseRT(ref m_FrameRT);
            }

            for (int i = 0; i < m_Cells.Length; i++)
            {
                if (m_Cells[i] != null) m_Cells[i].StartPlayback();
            }
        }

        private void OnCellTapped(int _Index)
        {
            Select(_Index);
        }

        public void Select(int _Index)
        {
            m_SelectedIndex = Mathf.Clamp(_Index, 0, PrototypeSkinSet.Count - 1);
            UpdateSelectionHighlight();
        }

        public void RecolorAll()
        {
            if (m_BrushRenderers == null) return;
            int colorCount = PrototypeSkinSet.Colors.Length;
            int offset = Time.frameCount;
            for (int i = 0; i < m_BrushRenderers.Length; i++)
            {
                if (m_BrushRenderers[i] == null) continue;
                Color c = PrototypeSkinSet.Colors[(i + offset) % colorCount];
                PrototypeBrushUtil.ApplyColorToRenderers(m_BrushRenderers[i], c);
            }

            if (m_RecaptureOnRecolor)
            {
                if (m_FrameRT == null)
                {
                    int cols = PrototypeSkinSet.Columns;
                    int rows = PrototypeSkinSet.Rows;
                    m_FrameRT = new RenderTexture(cols * m_CellPx, rows * m_CellPx, 16, RenderTextureFormat.ARGB32)
                    {
                        useMipMap = false,
                        antiAliasing = 1
                    };
                    m_FrameRT.Create();
                    m_AtlasCamera.targetTexture = m_FrameRT;
                }

                m_AtlasCamera.enabled = false;
                m_FrameIdx = 0;
                m_Phase = Phase.Capturing;
            }
        }

        private void UpdateSelectionHighlight()
        {
            if (m_SelectionHighlight == null || m_Cells == null) return;
            if (m_SelectedIndex < 0 || m_SelectedIndex >= m_Cells.Length) return;

            var target = (RectTransform)m_Cells[m_SelectedIndex].transform;
            m_SelectionHighlight.SetParent(target, false);
            m_SelectionHighlight.anchorMin = Vector2.zero;
            m_SelectionHighlight.anchorMax = Vector2.one;
            m_SelectionHighlight.offsetMin = Vector2.zero;
            m_SelectionHighlight.offsetMax = Vector2.zero;
        }
    }
}
