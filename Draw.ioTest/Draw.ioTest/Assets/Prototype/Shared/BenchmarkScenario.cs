using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Prototype.Shared
{
    public interface IBenchmarkSelectable
    {
        int Count { get; }
        void Select(int _Index);
    }

    public interface IBenchmarkRecolorable
    {
        void RecolorAll();
    }

    public class BenchmarkScenario : MonoBehaviour
    {
        [SerializeField] private string m_PrototypeName = "unknown";
        [SerializeField] private BenchmarkOverlay m_Overlay;
        [SerializeField] private ScrollRect m_ScrollRect;
        [SerializeField] private MonoBehaviour m_SelectableHost;
        [SerializeField] private MonoBehaviour m_RecolorableHost;

        [SerializeField] private float m_IdleSeconds = 5f;
        [SerializeField] private float m_ScrollSeconds = 20f;
        [SerializeField] private float m_SelectionSeconds = 10f;
        [SerializeField] private float m_RecolorSeconds = 10f;
        [SerializeField] private float m_RecoverySeconds = 10f;

        [SerializeField] private bool m_AutoStart = true;
        [SerializeField] private int m_TargetFrameRate = 60;

        private IBenchmarkSelectable m_Selectable;
        private IBenchmarkRecolorable m_Recolorable;

        private void Awake()
        {
            Application.targetFrameRate = m_TargetFrameRate;
            QualitySettings.vSyncCount = 0;

            m_Selectable = m_SelectableHost as IBenchmarkSelectable;
            m_Recolorable = m_RecolorableHost as IBenchmarkRecolorable;
        }

        private void Start()
        {
            if (m_AutoStart) StartCoroutine(Run());
        }

        public IEnumerator Run()
        {
            yield return new WaitForSeconds(0.5f);

            yield return RunPhase("idle_baseline", m_IdleSeconds, null);
            yield return RunPhase("scroll", m_ScrollSeconds, ScrollDriver());
            yield return RunPhase("selection_storm", m_SelectionSeconds, SelectionDriver());
            yield return RunPhase("recolor_storm", m_RecolorSeconds, RecolorDriver());
            yield return RunPhase("idle_recovery", m_RecoverySeconds, null);

            if (m_Overlay != null) m_Overlay.DumpCsv(m_PrototypeName);
            Debug.Log("[Benchmark] complete");
        }

        private IEnumerator RunPhase(string _Name, float _Duration, IEnumerator _Driver)
        {
            if (m_Overlay != null) m_Overlay.StartPhase(_Name);

            float end = Time.unscaledTime + _Duration;
            while (Time.unscaledTime < end)
            {
                if (_Driver != null && !_Driver.MoveNext()) _Driver = null;
                yield return null;
            }

            if (m_Overlay != null) m_Overlay.EndPhase();
        }

        private IEnumerator ScrollDriver()
        {
            if (m_ScrollRect == null) yield break;
            float t = 0f;
            while (true)
            {
                t += Time.deltaTime * 0.25f * Mathf.PI * 2f;
                m_ScrollRect.verticalNormalizedPosition = 0.5f + 0.5f * Mathf.Sin(t);
                yield return null;
            }
        }

        private IEnumerator SelectionDriver()
        {
            if (m_Selectable == null || m_Selectable.Count == 0) yield break;
            int count = m_Selectable.Count;
            int idx = 0;
            while (true)
            {
                m_Selectable.Select(idx % count);
                idx++;
                yield return new WaitForSeconds(0.5f);
            }
        }

        private IEnumerator RecolorDriver()
        {
            if (m_Recolorable == null) yield break;
            while (true)
            {
                m_Recolorable.RecolorAll();
                yield return new WaitForSeconds(0.5f);
            }
        }
    }
}
