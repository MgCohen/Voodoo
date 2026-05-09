using System;
using UnityEngine;
using UnityEngine.UI;

namespace Prototype.Direct3D
{
    /// A cell that has no visual of its own; just acts as a click target and a positional
    /// anchor for the world-space brush that will be drawn over it.
    public class Direct3DCell : MonoBehaviour
    {
        [SerializeField] private Button m_Button;

        private int m_Index;
        private Action<int> m_OnTapped;

        public int Index => m_Index;

        public void Setup(int _Index, Action<int> _OnTapped)
        {
            m_Index = _Index;
            m_OnTapped = _OnTapped;

            if (m_Button != null)
            {
                m_Button.onClick.RemoveAllListeners();
                m_Button.onClick.AddListener(() => m_OnTapped?.Invoke(m_Index));
            }
        }
    }
}
