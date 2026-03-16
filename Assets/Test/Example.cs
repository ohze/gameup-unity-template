using UnityEngine;
using GameUp.Core;

public class Example : MonoBehaviour
{

    [Button]
    private void SpawnObject(GameObject prefab)
    {
        GUPool.Spawn(prefab);
    }
}