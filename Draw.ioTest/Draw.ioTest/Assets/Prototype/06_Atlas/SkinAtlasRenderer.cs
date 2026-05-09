using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Prototype.Shared;

namespace Prototype.Atlas
{
    /// One secondary camera renders all 12 brushes (live, rotating, colored) into one
    /// RenderTexture every frame. Each UI cell is a RawImage that samples a fixed sub-rect
    /// of that RT.
    public class SkinAtlasRenderer : MonoBehaviour, IBenchmarkSelectable, IBenchmarkRecolorable
    {
        [Header("Brush prefabs (drop 2 menu prefabs)")]
        [SerializeField] private GameObject[] m_BrushPrefabs;

        [Header("Atlas")]
        [SerializeField] private Camera m_AtlasCamera;
        [SerializeField] private Transform m_GridRoot;
        [SerializeField] private int m_AtlasWidth = 512;
        [SerializeField] private int m_AtlasHeight = 768;
        [SerializeField] private float m_WorldCellSize = 5f;
        [SerializeField] private int m_SkinPreviewLayer = 31;

        [Header("UI")]
        [SerializeField] private RectTransform m_GridParent;
        [SerializeField] private GameObject m_CellPrefab;
        [SerializeField] private RectTransform m_SelectionHighlight;

        private RenderTexture m_AtlasRT;
        private GameObject[] m_BrushInstances;
        private List<Renderer>[] m_BrushRenderers;
        private AtlasCell[] m_Cells;
        private int m_SelectedIndex;

        public int Count => PrototypeSkinSet.Count;

        private void Awake()
        {
            BuildAtlasRT();
            BuildWorldGrid();
            BuildUI();
            FrameAtlasCamera();
        }

        private void OnDestroy()
        {
            if (m_AtlasRT != null)
            {
                if (m_AtlasCamera != null) m_AtlasCamera.targetTexture = null;
                m_AtlasRT.Release();
                Destroy(m_AtlasRT);
            }
        }

        private void OnEnable()
        {
            if (m_AtlasCamera != null) m_AtlasCamera.enabled = true;
        }

        private void OnDisable()
        {
            if (m_AtlasCamera != null) m_AtlasCamera.enabled = false;
        }

        private void BuildAtlasRT()
        {
            m_AtlasRT = new RenderTexture(m_AtlasWidth, m_AtlasHeight, 16, RenderTextureFormat.ARGB32)
            {
                name = "SkinAtlasRT",
                useMipMap = false,
                antiAliasing = 1
            };
            m_AtlasRT.Create();

            m_AtlasCamera.targetTexture = m_AtlasRT;
            m_AtlasCamera.cullingMask = 1 << m_SkinPreviewLayer;
            m_AtlasCamera.clearFlags = CameraClearFlags.SolidColor;
            m_AtlasCamera.backgroundColor = new Color(0, 0, 0, 0);
            m_AtlasCamera.orthographic = true;
        }

        private void BuildWorldGrid()
        {
            int cols = PrototypeSkinSet.Columns;
            int rows = PrototypeSkinSet.Rows;
            int prefabCount = m_BrushPrefabs != null ? m_BrushPrefabs.Length : 0;

            m_BrushInstances = new GameObject[PrototypeSkinSet.Count];
            m_BrushRenderers = new List<Renderer>[PrototypeSkinSet.Count];

            if (prefabCount == 0)
            {
                Debug.LogError("[SkinAtlasRenderer] No brush prefabs assigned.");
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

                if (brush.GetComponent<BrushRotation>() == null &&
                    brush.GetComponent<BrushRotationStepped>() == null)
                {
                    brush.AddComponent<BrushRotation>();
                }

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
                Debug.LogError("[SkinAtlasRenderer] Missing UI cell prefab or grid parent.");
                return;
            }

            int cols = PrototypeSkinSet.Columns;
            int rows = PrototypeSkinSet.Rows;
            float uvW = 1f / cols;
            float uvH = 1f / rows;

            m_Cells = new AtlasCell[PrototypeSkinSet.Count];
            for (int i = 0; i < PrototypeSkinSet.Count; i++)
            {
                int col = i % cols;
                int row = i / cols;

                GameObject cellGO = Instantiate(m_CellPrefab, m_GridParent);
                cellGO.name = $"AtlasCell_{i:00}";
                var cell = cellGO.GetComponent<AtlasCell>();

                Rect uv = new Rect(col * uvW, 1f - (row + 1) * uvH, uvW, uvH);
                cell.Setup(i, m_AtlasRT, uv, OnCellTapped);
                m_Cells[i] = cell;
            }

            UpdateSelectionHighlight();
        }

        private void FrameAtlasCamera()
        {
            int cols = PrototypeSkinSet.Columns;
            int rows = PrototypeSkinSet.Rows;
            float gridW = cols * m_WorldCellSize;
            float gridH = rows * m_WorldCellSize;

            m_AtlasCamera.aspect = gridW / gridH;
            m_AtlasCamera.orthographicSize = gridH * 0.5f;

            var camTr = m_AtlasCamera.transform;
            camTr.SetParent(m_GridRoot, false);
            camTr.localPosition = new Vector3(0f, 0f, -10f);
            camTr.localRotation = Quaternion.identity;
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
