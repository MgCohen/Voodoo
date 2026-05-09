using UnityEngine;

namespace Prototype.Shared
{
    public static class PrototypeSkinSet
    {
        public const int Count = 12;
        public const int Columns = 3;
        public const int Rows = 4;

        public static readonly Color[] Colors = new Color[]
        {
            new Color(0.20f, 0.60f, 1.00f), // blue
            new Color(0.20f, 0.90f, 0.50f), // green
            new Color(1.00f, 0.45f, 0.20f), // orange
            new Color(0.95f, 0.25f, 0.60f), // pink
            new Color(0.60f, 0.30f, 0.95f), // purple
            new Color(1.00f, 0.90f, 0.25f), // yellow
        };

        public static int PrefabIndex(int _SkinIndex, int _PrefabCount)
        {
            if (_PrefabCount <= 0) return 0;
            return (_SkinIndex / Colors.Length) % _PrefabCount;
        }

        public static Color ColorFor(int _SkinIndex)
        {
            return Colors[_SkinIndex % Colors.Length];
        }
    }
}
