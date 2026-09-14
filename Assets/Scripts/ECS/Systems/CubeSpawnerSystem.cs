using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace ECS
{


    [BurstCompile]
    public partial struct CubeSpawnerSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<CubeSpawnerComponent>(out Entity spawnerEntity))
                return;

            RefRW<CubeSpawnerComponent> spawner = SystemAPI.GetComponentRW<CubeSpawnerComponent>(spawnerEntity);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            if (spawner.ValueRW.NextSpawnTime < SystemAPI.Time.ElapsedTime)
            {
                Entity newEntity = ecb.Instantiate(spawner.ValueRO.Prefab);


                ecb.SetName(newEntity, new FixedString64Bytes("Enemy"));
                RuntimeColliderCache.EnsureBoxCollider(newEntity, state.EntityManager);
                var colliderBlob = RuntimeColliderCache.BoxColliderBlob;

                float3 spawnPos = spawner.ValueRO.SpawnPos;
                ecb.SetComponent(newEntity, new LocalTransform
                {
                    Position = spawnPos,
                    Rotation = quaternion.identity,
                    Scale = 1f
                });

                ecb.AddComponent(newEntity, new PhysicsCollider { Value = colliderBlob });

                var mass = PhysicsMass.CreateDynamic(colliderBlob.Value.MassProperties, 1f); // mass = 1

                ecb.AddComponent(newEntity, mass);

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

                ecb.AddComponent(newEntity, new PhysicsGravityFactor { Value = 1f });

                ecb.AddSharedComponent(newEntity, new PhysicsWorldIndex { Value = 0 });
                ecb.AddComponent(newEntity, new EnemyComponent
                {
                    moveDirection = new Random(0xABCDEFu).NextFloat3(),
                    moveSpeed = 5f,
                    hp = 3
                });

                ecb.AddComponent(newEntity, new EnemyTag());

                spawner.ValueRW.NextSpawnTime = (float)SystemAPI.Time.ElapsedTime + 10f;

                ecb.Playback(state.EntityManager);
            }

            ecb.Dispose();
        }
    }
}
