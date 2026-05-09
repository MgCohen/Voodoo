using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Prototype.EditorTools
{
    /// Shared helpers used by the three scene builders. Self-contained — only depends on
    /// Unity Editor APIs and the runtime classes under Assets/Prototype/.
    public static class PrototypeBuilderUtil
    {
        public const string c_BrushMenuPath1   = "Assets/Prefabs/Brushs/MainMenuBrushes/Brush_1-Menu.prefab";
        public const string c_BrushMenuPath2   = "Assets/Prefabs/Brushs/MainMenuBrushes/Brush_2-Menu.prefab";
        public const string c_BrushGameplayPath1 = "Assets/Prefabs/Brushs/Brush_1.prefab";
        public const string c_BrushGameplayPath2 = "Assets/Prefabs/Brushs/Brush_2.prefab";

        public const int c_SkinPreviewLayer = 31;
        public const string c_SkinPreviewLayerName = "SkinPreview";

        public static void EnsureLayer(string _Name, int _Idx)
        {
            var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (asset == null || asset.Length == 0)
            {
                Debug.LogWarning("[PrototypeBuilder] Could not load TagManager.asset; layer not added.");
                return;
            }

            var so = new SerializedObject(asset[0]);
            var layers = so.FindProperty("layers");
            if (layers == null || _Idx < 0 || _Idx >= layers.arraySize)
            {
                Debug.LogWarning($"[PrototypeBuilder] Invalid layer index {_Idx}.");
                return;
            }

            var prop = layers.GetArrayElementAtIndex(_Idx);
            if (string.IsNullOrEmpty(prop.stringValue))
            {
                prop.stringValue = _Name;
                so.ApplyModifiedProperties();
                Debug.Log($"[PrototypeBuilder] Added layer '{_Name}' at slot {_Idx}.");
            }
            else if (prop.stringValue != _Name)
            {
                Debug.LogWarning($"[PrototypeBuilder] Layer slot {_Idx} already used by '{prop.stringValue}'; prototypes will use that slot anyway.");
            }
        }

        public static GameObject[] LoadBrushPrefabs()
        {
            var menu1     = AssetDatabase.LoadAssetAtPath<GameObject>(c_BrushMenuPath1);
            var menu2     = AssetDatabase.LoadAssetAtPath<GameObject>(c_BrushMenuPath2);
            var gameplay1 = AssetDatabase.LoadAssetAtPath<GameObject>(c_BrushGameplayPath1);
            var gameplay2 = AssetDatabase.LoadAssetAtPath<GameObject>(c_BrushGameplayPath2);

            var p1 = menu1 != null ? menu1 : gameplay1;
            var p2 = menu2 != null ? menu2 : gameplay2;

            if (p1 == null || p2 == null)
            {
                Debug.LogError("[PrototypeBuilder] Could not locate brush prefabs. " +
                               "Expected at Prefabs/Brushs/MainMenuBrushes/ or Prefabs/Brushs/.");
                return new GameObject[0];
            }

            return new[] { p1, p2 };
        }

        public static Scene NewEmptyScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            return scene;
        }

        public static void SaveSceneTo(Scene _Scene, string _Path)
        {
            var dir = System.IO.Path.GetDirectoryName(_Path);
            if (!System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);

            EditorSceneManager.SaveScene(_Scene, _Path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[PrototypeBuilder] Saved scene: {_Path}");
        }

        public static GameObject CreateEventSystem()
        {
            var go = new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));
            return go;
        }

        public static (Canvas canvas, CanvasScaler scaler, GraphicRaycaster raycaster)
            CreateCanvas(string _Name, RenderMode _Mode, Camera _Camera = null)
        {
            var go = new GameObject(_Name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = _Mode;
            if (_Mode == RenderMode.ScreenSpaceCamera && _Camera != null)
            {
                canvas.worldCamera = _Camera;
                canvas.planeDistance = 100f;
            }

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            return (canvas, scaler, go.GetComponent<GraphicRaycaster>());
        }

        public static (ScrollRect scroll, RectTransform viewport, RectTransform content)
            CreateScrollView(Transform _Parent, Vector2 _Size, int _Cols, Vector2 _CellSize, Vector2 _Spacing)
        {
            var scrollGO = new GameObject("ScrollView", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollGO.transform.SetParent(_Parent, false);
            var scrollRT = (RectTransform)scrollGO.transform;
            scrollRT.anchorMin = new Vector2(0.5f, 0.5f);
            scrollRT.anchorMax = new Vector2(0.5f, 0.5f);
            scrollRT.pivot = new Vector2(0.5f, 0.5f);
            scrollRT.sizeDelta = _Size;
            scrollRT.anchoredPosition = Vector2.zero;

            var bg = scrollGO.GetComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.1f);

            var scroll = scrollGO.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;

            var viewportGO = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewportGO.transform.SetParent(scrollGO.transform, false);
            var viewportRT = (RectTransform)viewportGO.transform;
            viewportRT.anchorMin = Vector2.zero;
            viewportRT.anchorMax = Vector2.one;
            viewportRT.offsetMin = Vector2.zero;
            viewportRT.offsetMax = Vector2.zero;
            viewportGO.GetComponent<Image>().color = new Color(1, 1, 1, 0.01f);
            viewportGO.GetComponent<Mask>().showMaskGraphic = false;

            var contentGO = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            contentGO.transform.SetParent(viewportGO.transform, false);
            var contentRT = (RectTransform)contentGO.transform;
            contentRT.anchorMin = new Vector2(0, 1);
            contentRT.anchorMax = new Vector2(1, 1);
            contentRT.pivot = new Vector2(0.5f, 1);
            contentRT.anchoredPosition = Vector2.zero;
            contentRT.sizeDelta = new Vector2(0, 0);

            var grid = contentGO.GetComponent<GridLayoutGroup>();
            grid.cellSize = _CellSize;
            grid.spacing = _Spacing;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = _Cols;
            grid.padding = new RectOffset(8, 8, 8, 8);

            var fitter = contentGO.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewportRT;
            scroll.content = contentRT;

            return (scroll, viewportRT, contentRT);
        }

        public static GameObject CreateSelectionHighlight(Transform _Parent)
        {
            var go = new GameObject("SelectionHighlight", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_Parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = go.GetComponent<Image>();
            img.color = new Color(1f, 0.85f, 0.2f, 0.5f);
            img.raycastTarget = false;

            return go;
        }

        public static GameObject CreateRawImageCellTemplate(string _Name, Transform _ParentInactive)
        {
            var go = new GameObject(_Name, typeof(RectTransform), typeof(RawImage), typeof(Button));
            go.transform.SetParent(_ParentInactive, false);

            var raw = go.GetComponent<RawImage>();
            raw.color = Color.white;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = raw;

            return go;
        }

        public static GameObject CreateInvisibleCellTemplate(string _Name, Transform _ParentInactive)
        {
            var go = new GameObject(_Name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(_ParentInactive, false);

            var img = go.GetComponent<Image>();
            img.color = new Color(1, 1, 1, 0f); // invisible but raycast target on
            img.raycastTarget = true;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;

            return go;
        }

        public static GameObject CreateInactiveTemplatesRoot()
        {
            var go = new GameObject("__Templates");
            go.SetActive(false);
            return go;
        }

        public static (GameObject go, Camera cam) CreateOrthoCamera(string _Name, int _CullingLayer, int _Depth)
        {
            var go = new GameObject(_Name, typeof(Camera));
            var cam = go.GetComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0, 0);
            cam.cullingMask = 1 << _CullingLayer;
            cam.depth = _Depth;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            return (go, cam);
        }

        public static (GameObject go, Camera cam) CreatePerspectiveCamera(string _Name, int _Depth)
        {
            var go = new GameObject(_Name, typeof(Camera));
            var cam = go.GetComponent<Camera>();
            cam.orthographic = false;
            cam.fieldOfView = 60f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.12f, 0.14f, 1f);
            cam.depth = _Depth;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1000f;
            return (go, cam);
        }

        public static GameObject CreateBenchmarkRoot(string _PrototypeName,
            out Prototype.Shared.BenchmarkOverlay _Overlay,
            out Prototype.Shared.BenchmarkScenario _Scenario)
        {
            var go = new GameObject("__Benchmark");
            _Overlay = go.AddComponent<Prototype.Shared.BenchmarkOverlay>();
            _Scenario = go.AddComponent<Prototype.Shared.BenchmarkScenario>();
            SetSerializedField(_Scenario, "m_PrototypeName", _PrototypeName);
            SetSerializedField(_Scenario, "m_Overlay", _Overlay);
            return go;
        }

        public static void SetSerializedField(UnityEngine.Object _Target, string _Field, object _Value)
        {
            var so = new SerializedObject(_Target);
            var prop = so.FindProperty(_Field);
            if (prop == null)
            {
                Debug.LogWarning($"[PrototypeBuilder] Field '{_Field}' not found on {_Target.GetType().Name}.");
                return;
            }

            switch (prop.propertyType)
            {
                case SerializedPropertyType.ObjectReference:
                    prop.objectReferenceValue = _Value as UnityEngine.Object;
                    break;
                case SerializedPropertyType.Integer:
                    prop.intValue = System.Convert.ToInt32(_Value);
                    break;
                case SerializedPropertyType.Float:
                    prop.floatValue = System.Convert.ToSingle(_Value);
                    break;
                case SerializedPropertyType.Boolean:
                    prop.boolValue = System.Convert.ToBoolean(_Value);
                    break;
                case SerializedPropertyType.String:
                    prop.stringValue = (string)_Value;
                    break;
                case SerializedPropertyType.Enum:
                    prop.enumValueIndex = System.Convert.ToInt32(_Value);
                    break;
                default:
                    Debug.LogWarning($"[PrototypeBuilder] Unhandled prop type {prop.propertyType} for {_Field}.");
                    break;
            }
            so.ApplyModifiedProperties();
        }

        public static void SetSerializedObjectArray(UnityEngine.Object _Target, string _Field, UnityEngine.Object[] _Values)
        {
            var so = new SerializedObject(_Target);
            var prop = so.FindProperty(_Field);
            if (prop == null || !prop.isArray)
            {
                Debug.LogWarning($"[PrototypeBuilder] Field '{_Field}' is not an array on {_Target.GetType().Name}.");
                return;
            }

            prop.arraySize = _Values.Length;
            for (int i = 0; i < _Values.Length; i++)
            {
                prop.GetArrayElementAtIndex(i).objectReferenceValue = _Values[i];
            }
            so.ApplyModifiedProperties();
        }
    }
}
