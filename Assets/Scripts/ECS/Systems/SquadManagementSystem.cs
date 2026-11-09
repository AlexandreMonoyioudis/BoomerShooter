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

            foreach (var (squad, minionsBuffer) in SystemAPI.Query<RefRO<SquadComponent>, DynamicBuffer<MinionReference>>())
            {
                for (int i = minionsBuffer.Length - 1; i >= 0; i--)
                {
                    Entity minionEntity = minionsBuffer[i].MinionEntity;

                    if (entityManager.HasComponent<MarkedForDeath>(minionEntity) &&
                        entityManager.IsComponentEnabled<MarkedForDeath>(minionEntity))
                    {
                        minionsBuffer.RemoveAt(i);
                    }
                }

                int minionCount = minionsBuffer.Length;
                if (minionCount == 0) continue;

                NativeArray<float3> positionArray = GeneratePositionArray(squad.ValueRO.targetPos, minionCount);

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

    public struct SmartEnemy : IComponentData { }

    public struct SquadReferenceElement : IBufferElementData
    {
        public Entity SquadEntity;
    }

    public struct SquadComponent : IComponentData
    {
        public int groupIndex;
        public float3 targetPos;
        public float3 targetEnemy;
    }

    public struct MinionReference : IBufferElementData
    {
        public Entity MinionEntity;
    }
}