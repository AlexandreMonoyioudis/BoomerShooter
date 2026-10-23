using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace ECS
{
    partial struct SquadManagementSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SquadComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            EntityManager entityManager = state.EntityManager;

            // Iterate over all active squads in the game simultaneously
            foreach (var (squad, minionsBuffer) in SystemAPI.Query<RefRW<SquadComponent>, DynamicBuffer<MinionReference>>())
            {
                // 1. Clean up dead or dying minions from the buffer (iterate backwards to safely remove elements)
                for (int i = minionsBuffer.Length - 1; i >= 0; i--)
                {
                    Entity minionEntity = minionsBuffer[i].MinionEntity;

                    // Check if the minion is marked for death (HasComponent safely returns false if the entity is already destroyed)
                    if (entityManager.HasComponent<MarkedForDeath>(minionEntity) &&
                        entityManager.IsComponentEnabled<MarkedForDeath>(minionEntity))
                    {
                        minionsBuffer.RemoveAt(i);
                    }
                }

                int minionCount = minionsBuffer.Length;
                if (minionCount == 0) continue;

                // 2. Check if target moved OR if casualties occurred requiring formation re-evaluation
                bool targetMoved = !math.all(squad.ValueRO.targetPos == squad.ValueRO.lastTargetPos);
                squad.ValueRW.lastTargetPos = squad.ValueRO.targetPos;

                // Generate formation positions for this squad's surviving minions around its specific target
                NativeArray<float3> positionArray = GeneratePositionArray(squad.ValueRO.targetPos, minionCount);

                // Update each surviving minion assigned to this squad
                for (int i = 0; i < minionCount; i++)
                {
                    Entity minionEntity = minionsBuffer[i].MinionEntity;
                    if (entityManager.HasComponent<EnemyMovementControler>(minionEntity))
                    {
                        var minionMove = entityManager.GetComponentData<EnemyMovementControler>(minionEntity);
                        minionMove.targetPos = positionArray[i];
                        entityManager.SetComponentData(minionEntity, minionMove);
                    }
                }

                positionArray.Dispose();

            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        private static NativeArray<float3> GeneratePositionArray(float3 targetPosition, int positionCount)
        {
            NativeArray<float3> positionArray = new NativeArray<float3>(positionCount, Allocator.Temp);
            if (positionCount == 0) return positionArray;

            positionArray[0] = targetPosition;
            if (positionCount == 1) return positionArray;

            float ringSize = 20f;
            int ring = 0;
            int positionIndex = 1;

            while (positionIndex < positionCount)
            {
                int ringPositionCount = 3 + ring * 2;
                for (int i = 0; i < ringPositionCount; i++)
                {
                    float angle = i * (math.PI / ringPositionCount);
                    float3 ringPosition = math.rotate(quaternion.RotateY(angle), new float3(ringSize * (ring + 1), 0, 0)) + targetPosition;

                    positionArray[positionIndex] = ringPosition;
                    positionIndex++;

                    if (positionIndex >= positionCount) break;
                }
                ring++;
            }
            return positionArray;
        }
    }

    // 1. Root Smart Enemy component
    public struct SmartEnemy : IComponentData { }

    // 2. Attached to Smart Enemy: References its squad entities
    public struct SquadReferenceElement : IBufferElementData
    {
        public Entity SquadEntity;
    }

    // 3. Attached to a Squad Entity: Manages its own group target and minions
    public struct SquadComponent : IComponentData
    {
        public int groupIndex;
        public float3 targetPos;
        public float3 lastTargetPos; // Used for dirty-checking
    }

    // 4. Attached to Squad Entity: Dynamic buffer tracking its specific minions
    public struct MinionReference : IBufferElementData
    {
        public Entity MinionEntity;
    }
}