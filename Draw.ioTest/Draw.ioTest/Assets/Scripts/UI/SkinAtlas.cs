using System.Collections.Generic;
using UnityEngine;

public class SkinAtlas : MonoBehaviour
{
    public Camera         m_Camera;
    public Transform      m_Root;
    public BrushMainMenu  m_SlotPrefab;
    public int            m_Columns       = 3;
    public LayerMask      m_Layer         = 1 << 10;
    public int            m_RTWidth       = 512;
    public float          m_CellWorldSize = 3f;
    [Range(0.1f, 1f)] public float m_BrushFit = 0.9f;

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
        m_Camera.cullingMask      = m_Layer;
        m_Camera.clearFlags       = CameraClearFlags.SolidColor;
        m_Camera.backgroundColor  = new Color(0f, 0f, 0f, 0f);
        m_Camera.orthographic     = true;
        m_Camera.orthographicSize = Rows * m_CellWorldSize * 0.5f;
        m_Camera.aspect           = (float)m_Columns / Rows;

        // (uint) cast handles legacy int values stored before the field type
        // change to LayerMask, plus the sign-bit-31 edge case where
        // m_Layer.value reads negative.
        int layer = (int)Mathf.Log((uint)m_Layer.value, 2);

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
            ApplyMenuScale(slot, _Skins[i].Brush);
            foreach (Renderer r in slot.GetComponentsInChildren<Renderer>(true))
                r.gameObject.layer = layer;
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

    // Reads the baked m_MenuScale + m_MenuCenter on BrushData (populated by
    // BrushData.OnValidate at edit time) and applies them so each brush fits
    // its cell at the same target size regardless of authored prefab scale.
    private void ApplyMenuScale(BrushMainMenu _Slot, BrushData _Brush)
    {
        if (_Slot.m_Current == null || _Brush == null)
            return;

        float scale = _Brush.m_MenuScale * m_CellWorldSize * m_BrushFit;
        _Slot.m_Current.localScale    = Vector3.one * scale;
        _Slot.m_Current.localPosition = -_Brush.m_MenuCenter * scale;
    }
}
