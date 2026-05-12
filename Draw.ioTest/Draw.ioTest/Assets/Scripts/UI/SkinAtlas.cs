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

    // Per-brush auto-fit. Builds one world-space AABB from every visible mesh
    // (both static MeshRenderer/MeshFilter and SkinnedMeshRenderer - the latter
    // is what Brush_2 / Brush_4 use, and missing it underestimated the size and
    // made those brushes overflow their cell). Then scales + recentres the
    // brush so its largest dimension is cellWorldSize * brushFit.
    private void FitBrushToCell(BrushMainMenu _Slot)
    {
        if (_Slot.m_Current == null)
            return;

        Bounds world = default;
        bool   hasAny = false;

        MeshFilter[] filters = _Slot.m_Current.GetComponentsInChildren<MeshFilter>();
        for (int f = 0; f < filters.Length; f++)
        {
            MeshFilter mf = filters[f];
            if (mf.sharedMesh == null) continue;
            MeshRenderer mr = mf.GetComponent<MeshRenderer>();
            if (mr == null || !mr.enabled || !mr.gameObject.activeInHierarchy) continue;

            EncapsulateMeshCorners(mf.sharedMesh.bounds, mf.transform, ref world, ref hasAny);
        }

        SkinnedMeshRenderer[] skinned = _Slot.m_Current.GetComponentsInChildren<SkinnedMeshRenderer>();
        for (int s = 0; s < skinned.Length; s++)
        {
            SkinnedMeshRenderer smr = skinned[s];
            if (smr.sharedMesh == null) continue;
            if (!smr.enabled || !smr.gameObject.activeInHierarchy) continue;

            EncapsulateMeshCorners(smr.sharedMesh.bounds, smr.transform, ref world, ref hasAny);
        }

        if (!hasAny)
            return;

        float maxDim = Mathf.Max(world.size.x, world.size.y, world.size.z);
        if (maxDim < 0.0001f)
            return;

        float scale = (m_CellWorldSize * m_BrushFit) / maxDim;
        Vector3 offsetFromPivot = world.center - _Slot.m_Current.position;

        _Slot.m_Current.localPosition = -offsetFromPivot * scale;
        _Slot.m_Current.localScale    = Vector3.one * scale;
    }

    private static void EncapsulateMeshCorners(Bounds _Local, Transform _T, ref Bounds _World, ref bool _HasAny)
    {
        Vector3 mn = _Local.min;
        Vector3 mx = _Local.max;

        for (int cx = 0; cx < 2; cx++)
        for (int cy = 0; cy < 2; cy++)
        for (int cz = 0; cz < 2; cz++)
        {
            Vector3 c = _T.TransformPoint(new Vector3(
                cx == 0 ? mn.x : mx.x,
                cy == 0 ? mn.y : mx.y,
                cz == 0 ? mn.z : mx.z));

            if (!_HasAny) { _World = new Bounds(c, Vector3.zero); _HasAny = true; }
            else          _World.Encapsulate(c);
        }
    }
}
