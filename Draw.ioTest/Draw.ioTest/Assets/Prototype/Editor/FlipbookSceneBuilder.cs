using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Prototype.Flipbook;
using Prototype.Shared;

namespace Prototype.EditorTools
{
    public static class FlipbookSceneBuilder
    {
        private const string c_ScenePath = "Assets/Prototype/13_Flipbook/Prototype_13_Flipbook.unity";

        [MenuItem("Tools/Prototype/Build #13 Flipbook Scene")]
        public static void Build()
        {
            PrototypeBuilderUtil.EnsureLayer(
                PrototypeBuilderUtil.c_SkinPreviewLayerName,
                PrototypeBuilderUtil.c_SkinPreviewLayer);

            var scene = PrototypeBuilderUtil.NewEmptyScene();

            // --- Benchmark
            BenchmarkOverlay overlay;
            BenchmarkScenario scenario;
            PrototypeBuilderUtil.CreateBenchmarkRoot("13_flipbook", out overlay, out scenario);

            // --- EventSystem
            PrototypeBuilderUtil.CreateEventSystem();

            // --- UI camera
            var (uiCamGO, uiCam) = PrototypeBuilderUtil.CreatePerspectiveCamera("UICamera", 0);
            uiCamGO.tag = "MainCamera";
            uiCam.cullingMask = ~(1 << PrototypeBuilderUtil.c_SkinPreviewLayer);

            // --- Canvas + ScrollRect
            var (canvas, scaler, raycaster) = PrototypeBuilderUtil.CreateCanvas(
                "Canvas", RenderMode.ScreenSpaceOverlay);

            var (scroll, viewport, content) = PrototypeBuilderUtil.CreateScrollView(
                canvas.transform,
                _Size: new Vector2(900, 1000),
                _Cols: 3,
                _CellSize: new Vector2(300, 300),
                _Spacing: new Vector2(12, 12));

            // --- Selection highlight
            var highlight = PrototypeBuilderUtil.CreateSelectionHighlight(canvas.transform);
            highlight.transform.SetAsLastSibling();

            // --- Cell template
            var templates = PrototypeBuilderUtil.CreateInactiveTemplatesRoot();
            var cellTemplate = PrototypeBuilderUtil.CreateRawImageCellTemplate(
                "FlipbookCellTemplate", templates.transform);
            var flipbookCell = cellTemplate.AddComponent<FlipbookCell>();
            PrototypeBuilderUtil.SetSerializedField(flipbookCell, "m_RawImage", cellTemplate.GetComponent<RawImage>());
            PrototypeBuilderUtil.SetSerializedField(flipbookCell, "m_Button", cellTemplate.GetComponent<Button>());

            // --- World root + capture camera
            var skinFlipbook = new GameObject("SkinFlipbook");
            skinFlipbook.transform.position = new Vector3(0, -100, 0);
            skinFlipbook.layer = PrototypeBuilderUtil.c_SkinPreviewLayer;

            var gridRoot = new GameObject("GridRoot");
            gridRoot.transform.SetParent(skinFlipbook.transform, false);
            gridRoot.layer = PrototypeBuilderUtil.c_SkinPreviewLayer;

            var (capCamGO, capCam) = PrototypeBuilderUtil.CreateOrthoCamera(
                "CaptureCamera", PrototypeBuilderUtil.c_SkinPreviewLayer, _Depth: -10);
            capCamGO.transform.SetParent(skinFlipbook.transform, false);
            capCam.enabled = false; // capturer drives Render() manually

            // --- Capturer controller
            var captureGO = new GameObject("SkinFlipbookCapturer");
            var capturer = captureGO.AddComponent<SkinFlipbookCapturer>();

            var prefabs = PrototypeBuilderUtil.LoadBrushPrefabs();
            PrototypeBuilderUtil.SetSerializedObjectArray(capturer, "m_BrushPrefabs", prefabs);
            PrototypeBuilderUtil.SetSerializedField(capturer, "m_AtlasCamera", capCam);
            PrototypeBuilderUtil.SetSerializedField(capturer, "m_GridRoot", gridRoot.transform);
            PrototypeBuilderUtil.SetSerializedField(capturer, "m_CellPx", 128);
            PrototypeBuilderUtil.SetSerializedField(capturer, "m_FrameCount", 30);
            PrototypeBuilderUtil.SetSerializedField(capturer, "m_PlaybackFps", 30f);
            PrototypeBuilderUtil.SetSerializedField(capturer, "m_WorldCellSize", 5f);
            PrototypeBuilderUtil.SetSerializedField(capturer, "m_SkinPreviewLayer", PrototypeBuilderUtil.c_SkinPreviewLayer);
            // m_Pacing is enum: 0=Amortized, 1=Burst
            PrototypeBuilderUtil.SetSerializedField(capturer, "m_Pacing", 0);
            PrototypeBuilderUtil.SetSerializedField(capturer, "m_RecaptureOnRecolor", true);
            PrototypeBuilderUtil.SetSerializedField(capturer, "m_GridParent", content);
            PrototypeBuilderUtil.SetSerializedField(capturer, "m_CellPrefab", cellTemplate);
            PrototypeBuilderUtil.SetSerializedField(capturer, "m_SelectionHighlight", highlight.GetComponent<RectTransform>());

            // --- Wire benchmark scenario
            PrototypeBuilderUtil.SetSerializedField(scenario, "m_ScrollRect", scroll);
            PrototypeBuilderUtil.SetSerializedField(scenario, "m_SelectableHost", capturer);
            PrototypeBuilderUtil.SetSerializedField(scenario, "m_RecolorableHost", capturer);

            PrototypeBuilderUtil.SaveSceneTo(scene, c_ScenePath);
        }
    }
}
