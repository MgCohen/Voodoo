using System.Collections.Generic;
using UnityEngine;

public class SkinAtlas : MonoBehaviour
{
    [SerializeField] private Camera         m_Camera;
    [SerializeField] private Transform      m_Root;
    [SerializeField] private BrushMainMenu  m_SlotPrefab;
    [SerializeField] private int            m_Columns       = 3;
    [SerializeField] private int            m_Layer         = 31;
    [SerializeField] private int            m_RTWidth       = 512;
    [SerializeField] private float          m_CellWorldSize = 3f;
    [SerializeField, Range(0.1f, 1f)] private float m_BrushFit = 0.9f;

    public Texture Output => m_RT;
    public int     Cols   => m_Columns;
    public int     Rows   { get; private set; }

    private RenderTexture m_RT;
    private readonly List<BrushMainMenu> m_Slots = new List<BrushMainMenu>();

    public void Build(IList<SkinData> _Skins)
    {
        Teardown();

        int count = _Skins.Count;
        Rows = Mathf.CeilToInt(count / (float)m_Columns);

        int h = Mathf.RoundToInt(m_RTWidth * ((float)Rows / m_Columns));
        m_RT = new RenderTexture(m_RTWidth, h, 16, RenderTextureFormat.ARGB32);
        m_RT.useMipMap   = false;
        m_RT.antiAliasing = 1;
        m_RT.Create();

        m_Camera.targetTexture    = m_RT;
        m_Camera.cullingMask      = 1 << m_Layer;
        m_Camera.clearFlags       = CameraClearFlags.SolidColor;
        m_Camera.backgroundColor  = new Color(0f, 0f, 0f, 0f);
        m_Camera.orthographic     = true;
        m_Camera.orthographicSize = Rows * m_CellWorldSize * 0.5f;
        m_Camera.aspect           = (float)m_Columns / Rows;

        for (int i = 0; i < count; i++)
        {
            int col = i % m_Columns;
            int row = i / m_Columns;

            BrushMainMenu slot = Instantiate(m_SlotPrefab, m_Root);
            slot.transform.localPosition = new Vector3(
                (col - (m_Columns - 1) * 0.5f) * m_CellWorldSize,
               -(row - (Rows      - 1) * 0.5f) * m_CellWorldSize,
                0f);

            slot.Set(_Skins[i]);
            FitBrushToCell(slot);
            SetLayerRecursive(slot.gameObject, m_Layer);
            m_Slots.Add(slot);
        }
    }

    public Rect GetUV(int _Index)
    {
        float uvW = 1f / m_Columns;
        float uvH = 1f / Rows;
        int col = _Index % m_Columns;
        int row = _Index / m_Columns;
        return new Rect(col * uvW, 1f - (row + 1) * uvH, uvW, uvH);
    }

    public void SetActive(bool _On)
    {
        if (m_Camera != null)
            m_Camera.enabled = _On;
    }

    public void Teardown()
    {
        for (int i = 0; i < m_Slots.Count; i++)
            if (m_Slots[i] != null)
                Destroy(m_Slots[i].gameObject);
        m_Slots.Clear();

        if (m_RT != null)
        {
            if (m_Camera != null)
                m_Camera.targetTexture = null;
            m_RT.Release();
            Destroy(m_RT);
            m_RT = null;
        }
    }

    private void OnDestroy()
    {
        Teardown();
    }

    private static void SetLayerRecursive(GameObject _GO, int _Layer)
    {
        _GO.layer = _Layer;
        foreach (Transform child in _GO.transform)
            SetLayerRecursive(child.gameObject, _Layer);
    }

    // Per-brush auto-fit. Walks every visible Renderer on the spawned brush,
    // builds the world-space AABB from each one's .bounds, then scales +
    // recentres the brush so its largest dimension fills cellWorldSize *
    // brushFit. Using the Renderer base class covers MeshRenderer (Brush_1,
    // Brush_3) and SkinnedMeshRenderer (Brush_2, Brush_4) plus any future
    // type, without us having to remember to add a separate pass. Particle
    // renderers are excluded because their bounds cover simulation space
    // rather than the visible particles.
    private void FitBrushToCell(BrushMainMenu _Slot)
    {
        if (_Slot.m_Current == null)
            return;

        Bounds bounds = default;
        bool   hasAny = false;

        Renderer[] renderers = _Slot.m_Current.GetComponentsInChildren<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
            if (r is ParticleSystemRenderer) continue;

            if (!hasAny) { bounds = r.bounds; hasAny = true; }
            else         bounds.Encapsulate(r.bounds);
        }

        if (!hasAny)
            return;

        float maxDim = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (maxDim < 0.0001f)
            return;

        float   scale  = (m_CellWorldSize * m_BrushFit) / maxDim;
        Vector3 offset = bounds.center - _Slot.m_Current.position;

        _Slot.m_Current.localPosition = -offset * scale;
        _Slot.m_Current.localScale    = Vector3.one * scale;
    }
}
