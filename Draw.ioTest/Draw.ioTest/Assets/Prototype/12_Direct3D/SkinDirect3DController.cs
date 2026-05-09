using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Prototype.Shared;

namespace Prototype.Direct3D
{
    /// 12 actual 3D brush meshes drawn in world space, made to follow their corresponding
    /// UI cell positions. NO RenderTexture. Cells are invisible-content UI rects acting
    /// as positional anchors and click targets. Custom clip shader masks each brush
    /// fragment to the ScrollRect viewport.
    public class SkinDirect3DController : MonoBehaviour, IBenchmarkSelectable, IBenchmarkRecolorable
    {
        [Header("Brush prefabs (drop 2 menu prefabs)")]
        [SerializeField] private GameObject[] m_BrushPrefabs;

        [Header("Refs")]
        [SerializeField] private Camera m_UICamera;
        [SerializeField] private Transform m_BrushRoot;
        [SerializeField] private RectTransform m_GridParent;
        [SerializeField] private GameObject m_CellPrefab;
        [SerializeField] private RectTransform m_SelectionHighlight;
        [SerializeField] private Shader m_ClippedShader;

        [Header("Layout")]
        [SerializeField] private float m_ContainerScale = 14f; // world units; matches BrushMainMenu's m_BrushParent.localScale pattern (~140 there, ~14 here due to a different camera setup)
        [SerializeField] private float m_ZOffsetTowardCamera = 1f;

        private Direct3DCell[] m_Cells;
        private GameObject[] m_BrushInstances;
        private Transform[] m_BrushContainers;
        private List<Renderer>[] m_BrushRenderers;
        private MaterialPropertyBlock m_Mpb;

        private int m_SelectedIndex;
        private int m_ColorPropertyId;

        public int Count => PrototypeSkinSet.Count;

        private void Awake()
        {
            m_Mpb = new MaterialPropertyBlock();
            m_ColorPropertyId = Shader.PropertyToID("_Color");
            BuildUI();
            BuildBrushes();
        }

        private void BuildUI()
        {
            if (m_CellPrefab == null || m_GridParent == null)
            {
                Debug.LogError("[SkinDirect3DController] Missing UI cell prefab or grid parent.");
                return;
            }

            m_Cells = new Direct3DCell[PrototypeSkinSet.Count];
            for (int i = 0; i < PrototypeSkinSet.Count; i++)
            {
                GameObject cellGO = Instantiate(m_CellPrefab, m_GridParent);
                cellGO.name = $"Direct3DCell_{i:00}";
                var cell = cellGO.GetComponent<Direct3DCell>();
                cell.Setup(i, OnCellTapped);
                m_Cells[i] = cell;
            }
        }

        private void BuildBrushes()
        {
            int prefabCount = m_BrushPrefabs != null ? m_BrushPrefabs.Length : 0;
            if (prefabCount == 0)
            {
                Debug.LogError("[SkinDirect3DController] No brush prefabs assigned.");
                return;
            }

            m_BrushInstances  = new GameObject[PrototypeSkinSet.Count];
            m_BrushContainers = new Transform[PrototypeSkinSet.Count];
            m_BrushRenderers  = new List<Renderer>[PrototypeSkinSet.Count];

            for (int i = 0; i < PrototypeSkinSet.Count; i++)
            {
                // Per-cell container, scaled like BrushMainMenu's m_BrushParent.
                // Brush is parented to it at localScale=1, mirroring BrushMainMenu.Set().
                var container = new GameObject($"BrushContainer_{i:00}").transform;
                container.SetParent(m_BrushRoot, false);
                container.localScale = Vector3.one * m_ContainerScale;
                m_BrushContainers[i] = container;

                GameObject prefab = m_BrushPrefabs[PrototypeSkinSet.PrefabIndex(i, prefabCount)];
                GameObject brush = Instantiate(prefab, container);
                brush.name = $"Brush_{i:00}";
                brush.transform.localPosition = Vector3.zero;
                brush.transform.localRotation = Quaternion.identity;
                brush.transform.localScale = Vector3.one;

                PrototypeBrushUtil.StripGameplayComponents(brush);

                if (brush.GetComponent<BrushRotation>() == null &&
                    brush.GetComponent<BrushRotationStepped>() == null)
                {
                    brush.AddComponent<BrushRotation>();
                }

                var renderers = PrototypeBrushUtil.CollectRenderers(brush);
                ReplaceShader(renderers);
                ApplyColor(renderers, PrototypeSkinSet.ColorFor(i));

                // BrushScreenSync follows the cell with the CONTAINER, not the brush itself
                // — keeps localScale=1 invariant on the brush.
                var sync = container.gameObject.AddComponent<BrushScreenSync>();
                sync.Init((RectTransform)m_Cells[i].transform, m_ZOffsetTowardCamera);

                m_BrushInstances[i] = brush;
                m_BrushRenderers[i] = renderers;
            }
        }

        private void ReplaceShader(List<Renderer> _Renderers)
        {
            if (m_ClippedShader == null)
            {
                Debug.LogWarning("[SkinDirect3DController] No clip shader assigned; brushes will not be viewport-clipped.");
                return;
            }

            for (int i = 0; i < _Renderers.Count; i++)
            {
                var r = _Renderers[i];
                if (r == null) continue;

                var mats = r.sharedMaterials;
                for (int j = 0; j < mats.Length; j++)
                {
                    var newMat = new Material(m_ClippedShader);
                    if (mats[j] != null)
                    {
                        if (mats[j].HasProperty("_Color"))
                            newMat.SetColor("_Color", mats[j].GetColor("_Color"));
                        if (mats[j].HasProperty("_MainTex"))
                            newMat.SetTexture("_MainTex", mats[j].GetTexture("_MainTex"));
                    }
                    newMat.enableInstancing = true;
                    mats[j] = newMat;
                }
                r.materials = mats;
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
                ApplyColor(m_BrushRenderers[i], c);
            }
        }

        private void ApplyColor(List<Renderer> _Renderers, Color _Color)
        {
            for (int i = 0; i < _Renderers.Count; i++)
            {
                var r = _Renderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(m_Mpb);
                m_Mpb.SetColor(m_ColorPropertyId, _Color);
                r.SetPropertyBlock(m_Mpb);
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
