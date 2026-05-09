using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Prototype.Atlas;
using Prototype.Shared;

namespace Prototype.EditorTools
{
    public static class AtlasSceneBuilder
    {
        private const string c_ScenePath = "Assets/Prototype/06_Atlas/Prototype_06_Atlas.unity";

        [MenuItem("Tools/Prototype/Build #06 Atlas Scene")]
        public static void Build()
        {
            PrototypeBuilderUtil.EnsureLayer(
                PrototypeBuilderUtil.c_SkinPreviewLayerName,
                PrototypeBuilderUtil.c_SkinPreviewLayer);

            var scene = PrototypeBuilderUtil.NewEmptyScene();

            // --- Benchmark
            BenchmarkOverlay overlay;
            BenchmarkScenario scenario;
            PrototypeBuilderUtil.CreateBenchmarkRoot("06_atlas", out overlay, out scenario);

            // --- EventSystem
            PrototypeBuilderUtil.CreateEventSystem();

            // --- UI camera (Overlay canvas doesn't strictly need it, but Unity requires
            //     one camera tagged MainCamera in the scene to avoid editor warnings).
            var (uiCamGO, uiCam) = PrototypeBuilderUtil.CreatePerspectiveCamera("UICamera", 0);
            uiCamGO.tag = "MainCamera";
            // Exclude SkinPreview layer so the UI camera doesn't show the world brushes.
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

            // --- Selection highlight (parented to canvas; controller reparents at runtime)
            var highlight = PrototypeBuilderUtil.CreateSelectionHighlight(canvas.transform);
            highlight.transform.SetAsLastSibling();

            // --- Cell template
            var templates = PrototypeBuilderUtil.CreateInactiveTemplatesRoot();
            var cellTemplate = PrototypeBuilderUtil.CreateRawImageCellTemplate(
                "AtlasCellTemplate", templates.transform);
            cellTemplate.AddComponent<AtlasCell>();
            // wire serialized references on the AtlasCell template
            var atlasCell = cellTemplate.GetComponent<AtlasCell>();
            PrototypeBuilderUtil.SetSerializedField(atlasCell, "m_RawImage", cellTemplate.GetComponent<RawImage>());
            PrototypeBuilderUtil.SetSerializedField(atlasCell, "m_Button", cellTemplate.GetComponent<Button>());

            // --- World root + atlas camera (children of SkinAtlas, on SkinPreview layer)
            var skinAtlas = new GameObject("SkinAtlas");
            skinAtlas.transform.position = new Vector3(0, -100, 0);
            skinAtlas.layer = PrototypeBuilderUtil.c_SkinPreviewLayer;

            var gridRoot = new GameObject("GridRoot");
            gridRoot.transform.SetParent(skinAtlas.transform, false);
            gridRoot.layer = PrototypeBuilderUtil.c_SkinPreviewLayer;

            var (atlasCamGO, atlasCam) = PrototypeBuilderUtil.CreateOrthoCamera(
                "AtlasCamera", PrototypeBuilderUtil.c_SkinPreviewLayer, _Depth: -10);
            atlasCamGO.transform.SetParent(skinAtlas.transform, false);

            // --- Renderer controller
            var rendererGO = new GameObject("SkinAtlasRenderer");
            var renderer = rendererGO.AddComponent<SkinAtlasRenderer>();

            var prefabs = PrototypeBuilderUtil.LoadBrushPrefabs();
            PrototypeBuilderUtil.SetSerializedObjectArray(renderer, "m_BrushPrefabs", prefabs);
            PrototypeBuilderUtil.SetSerializedField(renderer, "m_AtlasCamera", atlasCam);
            PrototypeBuilderUtil.SetSerializedField(renderer, "m_GridRoot", gridRoot.transform);
            PrototypeBuilderUtil.SetSerializedField(renderer, "m_AtlasWidth", 512);
            PrototypeBuilderUtil.SetSerializedField(renderer, "m_AtlasHeight", 768);
            PrototypeBuilderUtil.SetSerializedField(renderer, "m_WorldCellSize", 5f);
            PrototypeBuilderUtil.SetSerializedField(renderer, "m_SkinPreviewLayer", PrototypeBuilderUtil.c_SkinPreviewLayer);
            PrototypeBuilderUtil.SetSerializedField(renderer, "m_GridParent", content);
            PrototypeBuilderUtil.SetSerializedField(renderer, "m_CellPrefab", cellTemplate);
            PrototypeBuilderUtil.SetSerializedField(renderer, "m_SelectionHighlight", highlight.GetComponent<RectTransform>());

            // --- Wire benchmark scenario
            PrototypeBuilderUtil.SetSerializedField(scenario, "m_ScrollRect", scroll);
            PrototypeBuilderUtil.SetSerializedField(scenario, "m_SelectableHost", renderer);
            PrototypeBuilderUtil.SetSerializedField(scenario, "m_RecolorableHost", renderer);

            PrototypeBuilderUtil.SaveSceneTo(scene, c_ScenePath);
        }
    }
}
