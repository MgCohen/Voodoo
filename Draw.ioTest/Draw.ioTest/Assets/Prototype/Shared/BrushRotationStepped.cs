using UnityEngine;

namespace Prototype.Shared
{
    public class BrushRotationStepped : MonoBehaviour
    {
        [SerializeField] private float m_DegreesPerSecond = 90f;
        [SerializeField] private bool m_AutoStep = true;

        private Transform m_Transform;

        private void Awake()
        {
            m_Transform = transform;
        }

        private void Update()
        {
            if (m_AutoStep)
            {
                Step(m_DegreesPerSecond * Time.deltaTime);
            }
        }

        public void Step(float _Degrees)
        {
            if (m_Transform == null) m_Transform = transform;
            m_Transform.RotateAround(m_Transform.position, m_Transform.up, _Degrees);
        }

        public void SetAuto(bool _Auto)
        {
            m_AutoStep = _Auto;
        }
    }
}
