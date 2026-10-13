using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
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

                float3 spawnPos = spawner.ValueRO.SpawnPos;
                ecb.SetComponent(newEntity, new LocalTransform
                {
                    Position = spawnPos,
                    Rotation = quaternion.identity,
                    Scale = 1f
                });

                ecb.AddComponent(newEntity, new EnemyComponent
                {
                    moveDirection = new Random(0xABCDEFu).NextFloat3(),
                    targetPos = spawnPos,
                    moveSpeed = 5f,
                    hp = 3
                });

                spawner.ValueRW.NextSpawnTime = (float)SystemAPI.Time.ElapsedTime + 2f;
                ecb.Playback(state.EntityManager);
            }

            ecb.Dispose();
        }
    }
}
