using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace ECS
{
    public class PlayerAuthoring : MonoBehaviour
    {
        [SerializeField] public GameObject prefab;
        [SerializeField] public GameObject bulletMarkPrefab;
        [SerializeField] public GameObject bulletVFXPrefab;

        class PlayerBaker : Baker<PlayerAuthoring>
        {
            public override void Bake(PlayerAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.None);

                AddComponent(entity, new PlayerSpawnerComponent
                {
                    Prefab = GetEntity(authoring.prefab, TransformUsageFlags.Dynamic),
                    SpawnPos = authoring.transform.position,
                    BulletMarkPrefab = GetEntity(authoring.bulletMarkPrefab, TransformUsageFlags.Dynamic),
                    BulletVFXPrefab = GetEntity(authoring.bulletVFXPrefab, TransformUsageFlags.Dynamic),
                }
                );
            }
        }
    }
}


public struct PlayerSpawnerComponent : IComponentData
{
    public Entity Prefab;
    public Entity BulletMarkPrefab;
    public Entity BulletVFXPrefab;
    public float3 SpawnPos;
}