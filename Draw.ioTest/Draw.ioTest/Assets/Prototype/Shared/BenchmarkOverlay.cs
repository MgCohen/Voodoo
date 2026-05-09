using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

namespace Prototype.Shared
{
    public class BenchmarkOverlay : MonoBehaviour
    {
        [SerializeField] private int m_FontSize = 18;
        [SerializeField] private bool m_ShowOnScreen = true;
        [SerializeField] private int m_RollingWindowFrames = 120;

        private struct PhaseStats
        {
            public string Name;
            public float StartTime;
            public float EndTime;
            public float AvgFrameMs;
            public float P99FrameMs;
            public float PeakFrameMs;
            public float FpsAvg;
            public long ManagedMemDeltaBytes;
            public long GfxMemDeltaBytes;
        }

        private readonly List<float> m_FrameTimesCurrentPhase = new List<float>(4096);
        private long m_ManagedMemAtPhaseStart;
        private long m_GfxMemAtPhaseStart;
        private readonly List<PhaseStats> m_CompletedPhases = new List<PhaseStats>();
        private PhaseStats m_CurrentPhase;
        private bool m_PhaseActive;

        private readonly Queue<float> m_RollingFrameMs = new Queue<float>(120);
        private float m_RollingSum;

        public void StartPhase(string _Name)
        {
            if (m_PhaseActive) EndPhase();

            m_CurrentPhase = new PhaseStats
            {
                Name = _Name,
                StartTime = Time.unscaledTime
            };
            m_FrameTimesCurrentPhase.Clear();
            m_ManagedMemAtPhaseStart = Profiler.GetTotalAllocatedMemoryLong();
            m_GfxMemAtPhaseStart = Profiler.GetAllocatedMemoryForGraphicsDriver();
            m_PhaseActive = true;
        }

        public void EndPhase()
        {
            if (!m_PhaseActive) return;

            m_CurrentPhase.EndTime = Time.unscaledTime;

            if (m_FrameTimesCurrentPhase.Count > 0)
            {
                float sum = 0f, peak = 0f;
                for (int i = 0; i < m_FrameTimesCurrentPhase.Count; i++)
                {
                    float v = m_FrameTimesCurrentPhase[i];
                    sum += v;
                    if (v > peak) peak = v;
                }
                m_CurrentPhase.AvgFrameMs = sum / m_FrameTimesCurrentPhase.Count;
                m_CurrentPhase.PeakFrameMs = peak;
                m_CurrentPhase.P99FrameMs = ComputePercentile(m_FrameTimesCurrentPhase, 0.99f);
                m_CurrentPhase.FpsAvg = 1000f / Mathf.Max(0.001f, m_CurrentPhase.AvgFrameMs);
            }

            m_CurrentPhase.ManagedMemDeltaBytes = Profiler.GetTotalAllocatedMemoryLong() - m_ManagedMemAtPhaseStart;
            m_CurrentPhase.GfxMemDeltaBytes = Profiler.GetAllocatedMemoryForGraphicsDriver() - m_GfxMemAtPhaseStart;

            m_CompletedPhases.Add(m_CurrentPhase);
            m_PhaseActive = false;
        }

        private void Update()
        {
            float dtMs = Time.unscaledDeltaTime * 1000f;

            m_RollingFrameMs.Enqueue(dtMs);
            m_RollingSum += dtMs;
            while (m_RollingFrameMs.Count > m_RollingWindowFrames)
            {
                m_RollingSum -= m_RollingFrameMs.Dequeue();
            }

            if (m_PhaseActive)
            {
                m_FrameTimesCurrentPhase.Add(dtMs);
            }
        }

        private static float ComputePercentile(List<float> _Data, float _P)
        {
            if (_Data.Count == 0) return 0f;
            var sorted = new List<float>(_Data);
            sorted.Sort();
            int idx = Mathf.Clamp(Mathf.FloorToInt(_P * (sorted.Count - 1)), 0, sorted.Count - 1);
            return sorted[idx];
        }

        private void OnGUI()
        {
            if (!m_ShowOnScreen) return;

            GUI.skin.label.fontSize = m_FontSize;
            float avgMs = m_RollingFrameMs.Count > 0 ? m_RollingSum / m_RollingFrameMs.Count : 0f;

            float y = 10f;
            GUI.Label(new Rect(10, y, 800, 30),
                $"Frame: {avgMs:F2} ms ({1000f / Mathf.Max(0.001f, avgMs):F0} fps)");
            y += 24f;
            GUI.Label(new Rect(10, y, 800, 30),
                $"Managed: {Profiler.GetTotalAllocatedMemoryLong() / (1024f * 1024f):F1} MB   Gfx: {Profiler.GetAllocatedMemoryForGraphicsDriver() / (1024f * 1024f):F1} MB");
            y += 24f;

            if (m_PhaseActive)
            {
                GUI.Label(new Rect(10, y, 800, 30),
                    $"Phase: {m_CurrentPhase.Name}  ({Time.unscaledTime - m_CurrentPhase.StartTime:F1}s)");
                y += 24f;
            }

            for (int i = 0; i < m_CompletedPhases.Count; i++)
            {
                var p = m_CompletedPhases[i];
                GUI.Label(new Rect(10, y, 1000, 30),
                    $"{p.Name}: avg={p.AvgFrameMs:F2}ms  99p={p.P99FrameMs:F2}ms  peak={p.PeakFrameMs:F2}ms  mem+={p.ManagedMemDeltaBytes / (1024f * 1024f):+0.0;-0.0;0.0}MB");
                y += 24f;
            }
        }

        public void DumpCsv(string _PrototypeName)
        {
            string deviceTag = SystemInfo.deviceModel.Replace(" ", "_").Replace("/", "_");
            string path = System.IO.Path.Combine(Application.persistentDataPath,
                $"prototype_{_PrototypeName}_{deviceTag}.csv");

            using (var w = new System.IO.StreamWriter(path, false))
            {
                w.WriteLine("phase,duration_s,frame_avg_ms,frame_99p_ms,frame_peak_ms,fps_avg,managed_delta_mb,gfx_delta_mb");
                for (int i = 0; i < m_CompletedPhases.Count; i++)
                {
                    var p = m_CompletedPhases[i];
                    w.WriteLine(
                        $"{p.Name},{p.EndTime - p.StartTime:F2}," +
                        $"{p.AvgFrameMs:F3},{p.P99FrameMs:F3},{p.PeakFrameMs:F3}," +
                        $"{p.FpsAvg:F1}," +
                        $"{p.ManagedMemDeltaBytes / (1024f * 1024f):F2}," +
                        $"{p.GfxMemDeltaBytes / (1024f * 1024f):F2}");
                }
            }

            Debug.Log($"[Benchmark] CSV written: {path}");
        }
    }
}
