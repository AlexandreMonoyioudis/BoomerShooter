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

            var spawner = SystemAPI.GetComponentRW<CubeSpawnerComponent>(spawnerEntity);

            if (spawner.ValueRO.NextSpawnTime > SystemAPI.Time.ElapsedTime)
                return;

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // 1. Create the Smart Enemy (Commander) Entity
            Entity smartEnemyEntity = ecb.CreateEntity();
            ecb.SetName(smartEnemyEntity, new FixedString64Bytes("SmartEnemy_Commander"));
            ecb.AddComponent<SmartEnemy>(smartEnemyEntity);

            // Add a dynamic buffer to hold references to its squad entities
            ecb.AddBuffer<SquadReferenceElement>(smartEnemyEntity);

            float3 baseSpawnPos = spawner.ValueRO.SpawnPos;
            int squadCount = 2;
            int enemiesPerSquad = 10; // Spawns 20 total enemies split across 2 squads

            for (int s = 0; s < squadCount; s++)
            {
                // 2. Create a separate Entity for this Squad
                Entity squadEntity = ecb.CreateEntity();
                ecb.SetName(squadEntity, new FixedString64Bytes($"Squad_{s}"));

                // Offset each squad's initial spawn point so they don't overlap
                float3 squadSpawnPos = baseSpawnPos + new float3(s * 8f, 0f, 0f);

                ecb.AddComponent(squadEntity, new SquadComponent
                {
                    groupIndex = s,
                    targetPos = squadSpawnPos,
                    lastTargetPos = float3.zero // Forces initial formation calculation
                });

                // Add a dynamic buffer to track this squad's minions
                ecb.AddBuffer<MinionReference>(squadEntity);

                // Register this squad to the Smart Enemy's buffer
                ecb.AppendToBuffer(smartEnemyEntity, new SquadReferenceElement { SquadEntity = squadEntity });

                // 3. Spawn minions belonging to this squad
                for (int i = 0; i < enemiesPerSquad; i++)
                {
                    Entity newEntity = ecb.Instantiate(spawner.ValueRO.Prefab);
                    ecb.SetName(newEntity, new FixedString64Bytes($"Enemy_Squad_{s}_{i}"));

                    // Offset minions slightly within the squad's area
                    float3 minionSpawnPos = squadSpawnPos + new float3(i * 25f, 0f, 2f);

                    ecb.SetComponent(newEntity, new LocalTransform
                    {
                        Position = minionSpawnPos,
                        Rotation = quaternion.identity,
                        Scale = 1f
                    });

                    ecb.AddComponent(newEntity, new EnemyMovement
                    {
                        moveSpeed = 10f,
                        hp = 3
                    });

                    ecb.AddComponent(newEntity, new EnemyMovementControler
                    {
                        targetPos = squadSpawnPos
                    });

                    ecb.AddComponent(newEntity, new MarkedForDeath { Value = DeathType.Unexist });
                    ecb.SetComponentEnabled<MarkedForDeath>(newEntity, false);

                    // Add this minion entity to the squad's dynamic buffer
                    ecb.AppendToBuffer(squadEntity, new MinionReference { MinionEntity = newEntity });
                }
            }

            spawner.ValueRW.NextSpawnTime = 99999999;

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}