using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BrushRotation : MonoBehaviour
{
	// Cache
	private Transform m_Transform;

	void Awake()
	{
		// Cache
		m_Transform = transform;
	}

	void Update ()
	{
		// Rotate(axis, angle) only touches rotation. RotateAround(pos, axis, angle)
		// here was a no-op pivot — but it still accumulated floating-point error
		// in position every frame, which is visible after a long-running match.
		m_Transform.Rotate(Vector3.up, Time.deltaTime * 90f, Space.Self);
	}
}
