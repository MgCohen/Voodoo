using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IMapService
{
    int RegisterEntity(GameObject gameObject);
    int UpdateEntity(int lastMapKey, Vector3 mappedTransformPosition, int mapIndex);
    void UnregisterEntity(int lastMapKey, int mapIndex);
    public void FindEntities(Vector3 _Position, float _SqrRadius, ref List<GameObject> _Results, int _Layer = -1);
}
