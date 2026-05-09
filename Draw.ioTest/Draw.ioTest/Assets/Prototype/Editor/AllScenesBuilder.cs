using UnityEditor;
using UnityEngine;

namespace Prototype.EditorTools
{
    public static class AllScenesBuilder
    {
        [MenuItem("Tools/Prototype/Build All Scenes")]
        public static void BuildAll()
        {
            AtlasSceneBuilder.Build();
            FlipbookSceneBuilder.Build();
            Direct3DSceneBuilder.Build();
            Debug.Log("[PrototypeBuilder] All three scenes built.");
        }
    }
}
