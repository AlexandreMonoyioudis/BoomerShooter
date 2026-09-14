using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace ECS
{
    public static class RuntimeColliderCache
    {
        public static BlobAssetReference<Collider> BoxColliderBlob;
        public static bool IsInitialized => BoxColliderBlob.IsCreated;

        public static void EnsureBoxCollider(Entity prefab, EntityManager em)
        {
            if (IsInitialized) return;

            if (!em.Exists(prefab))
            {
                UnityEngine.Debug.LogError($"EnsureBoxCollider: prefab entity {prefab} does not exist.");
                return;
            }

            if (!em.HasComponent<PhysicsCollider>(prefab))
            {
                UnityEngine.Debug.LogError($"EnsureBoxCollider: prefab entity {prefab} has no PhysicsCollider component.");
                return;
            }

            var prefabColliderComp = em.GetComponentData<PhysicsCollider>(prefab);
            var prefabBlob = prefabColliderComp.Value;

            if (!prefabBlob.IsCreated)
            {
                UnityEngine.Debug.LogError("EnsureBoxCollider: prefab collider blob is not created.");
                return;
            }

            BoxColliderBlob = prefabBlob;
        }
    }

    [BurstCompile]
    public partial struct PlayerSpawnerSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<PlayerSpawnerComponent>(out Entity spawnerEntity))
            {
                UnityEngine.Debug.LogError("Fails to get player");
                return;
            }

            RefRO<PlayerSpawnerComponent> spawner = SystemAPI.GetComponentRO<PlayerSpawnerComponent>(spawnerEntity);
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            Entity newEntity = ecb.Instantiate(spawner.ValueRO.Prefab);

            ecb.SetName(newEntity, new FixedString64Bytes("Player"));

            RuntimeColliderCache.EnsureBoxCollider(spawner.ValueRO.Prefab, state.EntityManager);

            float3 spawnPos = spawner.ValueRO.SpawnPos;
            ecb.SetComponent(newEntity, new LocalTransform
            {
                Position = spawnPos,
                Rotation = quaternion.identity,
                Scale = 1f
            });

            ecb.AddComponent(newEntity, new PhysicsVelocity
            {
                Linear = float3.zero,
                Angular = float3.zero
            });

            ecb.AddComponent(newEntity, new PhysicsDamping
            {
                Linear = 0.01f,
                Angular = 0.01f
            });

            ecb.AddComponent(newEntity, new AllyPos());

            ecb.AddComponent(newEntity, new PhysicsGravityFactor { Value = 3f });

            ecb.AddSharedComponent(newEntity, new PhysicsWorldIndex { Value = 0 });

            ecb.AddComponent(newEntity, new PlayerData
            {
                bulletMark = spawner.ValueRO.BulletMarkPrefab,
                bulletTrailVFX = spawner.ValueRO.BulletVFXPrefab,
                speed = 20f,
                damping = 0.01f,
                jumpHeight = 10f,
                jumpCooldown = .0f,
                jumped = false,
            });

            ecb.AddComponent(newEntity, new PlayerInputData { });

            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            state.Enabled = false;
        }
    }
}