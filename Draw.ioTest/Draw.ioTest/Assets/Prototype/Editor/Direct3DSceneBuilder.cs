using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Prototype.Direct3D;
using Prototype.Shared;

namespace Prototype.EditorTools
{
    public static class Direct3DSceneBuilder
    {
        private const string c_ScenePath = "Assets/Prototype/12_Direct3D/Prototype_12_Direct3D.unity";
        private const string c_ShaderPath = "Assets/Prototype/12_Direct3D/BrushClipped.shader";

        [MenuItem("Tools/Prototype/Build #12 Direct3D Scene")]
        public static void Build()
        {
            var scene = PrototypeBuilderUtil.NewEmptyScene();

            // --- Benchmark
            BenchmarkOverlay overlay;
            BenchmarkScenario scenario;
            PrototypeBuilderUtil.CreateBenchmarkRoot("12_direct3d", out overlay, out scenario);

            // --- EventSystem
            PrototypeBuilderUtil.CreateEventSystem();

            // --- UI camera (this prototype REQUIRES Screen Space - Camera)
            var (uiCamGO, uiCam) = PrototypeBuilderUtil.CreatePerspectiveCamera("UICamera", 0);
            uiCamGO.tag = "MainCamera";
            // Renders everything (UI + brushes) in this prototype.

            // --- Canvas + ScrollRect (Screen Space - Camera mode)
            var (canvas, scaler, raycaster) = PrototypeBuilderUtil.CreateCanvas(
                "Canvas", RenderMode.ScreenSpaceCamera, uiCam);

            var (scroll, viewport, content) = PrototypeBuilderUtil.CreateScrollView(
                canvas.transform,
                _Size: new Vector2(900, 1000),
                _Cols: 3,
                _CellSize: new Vector2(300, 300),
                _Spacing: new Vector2(12, 12));

            // --- Selection highlight
            var highlight = PrototypeBuilderUtil.CreateSelectionHighlight(canvas.transform);
            highlight.transform.SetAsLastSibling();

            // --- ClipRectFeeder (writes _SkinClipRect global each frame)
            var feederGO = new GameObject("ClipRectFeeder");
            var feeder = feederGO.AddComponent<ClipRectFeeder>();
            PrototypeBuilderUtil.SetSerializedField(feeder, "m_Viewport", viewport);

            // --- Cell template (invisible image so it raycasts but draws nothing)
            var templates = PrototypeBuilderUtil.CreateInactiveTemplatesRoot();
            var cellTemplate = PrototypeBuilderUtil.CreateInvisibleCellTemplate(
                "Direct3DCellTemplate", templates.transform);
            var direct3DCell = cellTemplate.AddComponent<Direct3DCell>();
            PrototypeBuilderUtil.SetSerializedField(direct3DCell, "m_Button", cellTemplate.GetComponent<Button>());

            // --- Brush root (in world; controller positions individual brushes per-cell)
            var brushRoot = new GameObject("BrushRoot");
            brushRoot.transform.position = Vector3.zero;

            // --- Controller
            var controllerGO = new GameObject("SkinDirect3DController");
            var controller = controllerGO.AddComponent<SkinDirect3DController>();

            var prefabs = PrototypeBuilderUtil.LoadBrushPrefabs();
            var clippedShader = AssetDatabase.LoadAssetAtPath<Shader>(c_ShaderPath);
            if (clippedShader == null)
            {
                Debug.LogError($"[Direct3DSceneBuilder] Could not load shader at {c_ShaderPath}");
            }

            PrototypeBuilderUtil.SetSerializedObjectArray(controller, "m_BrushPrefabs", prefabs);
            PrototypeBuilderUtil.SetSerializedField(controller, "m_UICamera", uiCam);
            PrototypeBuilderUtil.SetSerializedField(controller, "m_BrushRoot", brushRoot.transform);
            PrototypeBuilderUtil.SetSerializedField(controller, "m_GridParent", content);
            PrototypeBuilderUtil.SetSerializedField(controller, "m_CellPrefab", cellTemplate);
            PrototypeBuilderUtil.SetSerializedField(controller, "m_SelectionHighlight", highlight.GetComponent<RectTransform>());
            PrototypeBuilderUtil.SetSerializedField(controller, "m_ClippedShader", clippedShader);
            PrototypeBuilderUtil.SetSerializedField(controller, "m_BrushScale", 0.5f);
            PrototypeBuilderUtil.SetSerializedField(controller, "m_ZOffsetTowardCamera", 1f);

            // --- Wire benchmark scenario
            PrototypeBuilderUtil.SetSerializedField(scenario, "m_ScrollRect", scroll);
            PrototypeBuilderUtil.SetSerializedField(scenario, "m_SelectableHost", controller);
            PrototypeBuilderUtil.SetSerializedField(scenario, "m_RecolorableHost", controller);

            PrototypeBuilderUtil.SaveSceneTo(scene, c_ScenePath);
        }
    }
}
