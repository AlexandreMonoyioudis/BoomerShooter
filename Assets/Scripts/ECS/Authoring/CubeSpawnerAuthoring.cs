using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace ECS
{

    public class CubeSpawnerAuthoring : MonoBehaviour
    {
        [SerializeField] public int Count = 100;
        [SerializeField] public float SpawnRadius = 10f;
        [SerializeField]  public float CubeSize = 1f;
        [SerializeField] public GameObject prefab;

        class EnemyBaker : Baker<CubeSpawnerAuthoring>
        {
            public override void Bake(CubeSpawnerAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.None);

                AddComponent(entity, new CubeSpawnerComponent
                {
                    Count = authoring.Count,
                    SpawnRadius = authoring.SpawnRadius,
                    CubeSize = authoring.CubeSize,
                    Prefab = GetEntity(authoring.prefab, TransformUsageFlags.Dynamic),
                    SpawnPos = authoring.transform.position

                }
                );
            }
        }
    }
}

public struct CubeSpawnerComponent : IComponentData
{
    public int Count;
    public float SpawnRadius;
    public float CubeSize;
    public float NextSpawnTime;
    public float3 SpawnPos;
    public Entity Prefab;
}
