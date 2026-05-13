using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Brush", menuName = "Data/Brush", order = 1)]
public class BrushData : ScriptableObject
{
	public GameObject 	m_Prefab;

	// Baked at edit time by OnValidate. Local scale at which this brush's
	// largest dimension equals 1 world unit. SkinAtlas multiplies by its cell
	// framing on top, so this stays decoupled from cell layout.
	public float        m_MenuScale  = 1f;

	// Baked AABB center of the brush meshes in prefab-root-local space.
	// SkinAtlas applies -m_MenuCenter * finalScale as localPosition so the
	// mesh AABB lands centered on the cell origin even if the prefab pivot
	// is off-center.
	public Vector3      m_MenuCenter = Vector3.zero;

#if UNITY_EDITOR
	private void OnValidate()
	{
		if (m_Prefab == null)
		{
			m_MenuScale = 1f;
			return;
		}

		Transform   root        = m_Prefab.transform;
		Matrix4x4   rootInverse = root.worldToLocalMatrix;
		Bounds      combined    = default;
		bool        any         = false;

		foreach (MeshFilter mf in m_Prefab.GetComponentsInChildren<MeshFilter>(true))
		{
			Mesh mesh = mf.sharedMesh;
			if (mesh == null) continue;
			Matrix4x4 meshToRoot = rootInverse * mf.transform.localToWorldMatrix;
			Bounds b = TransformBounds(meshToRoot, mesh.bounds);
			if (!any) { combined = b; any = true; } else combined.Encapsulate(b);
		}
		foreach (SkinnedMeshRenderer smr in m_Prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
		{
			Mesh mesh = smr.sharedMesh;
			if (mesh == null) continue;
			Matrix4x4 meshToRoot = rootInverse * smr.transform.localToWorldMatrix;
			Bounds b = TransformBounds(meshToRoot, mesh.bounds);
			if (!any) { combined = b; any = true; } else combined.Encapsulate(b);
		}

		if (!any)
		{
			m_MenuScale  = 1f;
			m_MenuCenter = Vector3.zero;
			return;
		}

		float maxDim = Mathf.Max(combined.size.x, combined.size.y, combined.size.z);
		m_MenuScale  = maxDim > 0.0001f ? 1f / maxDim : 1f;
		m_MenuCenter = combined.center;
	}

	private static Bounds TransformBounds(Matrix4x4 _Matrix, Bounds _LocalBounds)
	{
		Vector3 c = _LocalBounds.center;
		Vector3 e = _LocalBounds.extents;
		Vector3 p0 = _Matrix.MultiplyPoint3x4(c + new Vector3(-e.x, -e.y, -e.z));
		Bounds  r  = new Bounds(p0, Vector3.zero);
		r.Encapsulate(_Matrix.MultiplyPoint3x4(c + new Vector3( e.x, -e.y, -e.z)));
		r.Encapsulate(_Matrix.MultiplyPoint3x4(c + new Vector3(-e.x,  e.y, -e.z)));
		r.Encapsulate(_Matrix.MultiplyPoint3x4(c + new Vector3( e.x,  e.y, -e.z)));
		r.Encapsulate(_Matrix.MultiplyPoint3x4(c + new Vector3(-e.x, -e.y,  e.z)));
		r.Encapsulate(_Matrix.MultiplyPoint3x4(c + new Vector3( e.x, -e.y,  e.z)));
		r.Encapsulate(_Matrix.MultiplyPoint3x4(c + new Vector3(-e.x,  e.y,  e.z)));
		r.Encapsulate(_Matrix.MultiplyPoint3x4(c + new Vector3( e.x,  e.y,  e.z)));
		return r;
	}
#endif
}
