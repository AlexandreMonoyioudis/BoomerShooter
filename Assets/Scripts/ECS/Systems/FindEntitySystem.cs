using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine; // Required for Debug.Log

namespace ECS
{
    [BurstCompile]
    public partial struct FindEntitySystem : ISystem
    {
        private EntityQuery m_AllyQuery;
        private EntityQuery m_EnemyQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // Cache the Ally query
            m_AllyQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<Ally>(),
                ComponentType.ReadOnly<LocalTransform>()
            );

            // Cache an Enemy query just for counting/debugging purposes
            m_EnemyQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<SquadComponent>()
            );
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            NativeArray<LocalTransform> allyTransforms = m_AllyQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);

            FindClosestAllyJob findClosestAllyJob = new FindClosestAllyJob
            {
                AllyTransforms = allyTransforms
            };

            state.Dependency = findClosestAllyJob.ScheduleParallel(state.Dependency);
            allyTransforms.Dispose(state.Dependency);
        }
    }

    [BurstCompile]
    public partial struct FindClosestAllyJob : IJobEntity
    {
        [ReadOnly] public NativeArray<LocalTransform> AllyTransforms;

        [BurstCompile]
        public void Execute(ref SquadComponent squadComponent)
        {
            if (AllyTransforms.Length == 0) return;

            float minDistanceSq = float.MaxValue;
            float3 closestPosition = squadComponent.targetPos;

            for (int i = 0; i < AllyTransforms.Length; i++)
            {
                float3 allyPos = AllyTransforms[i].Position;

                float distSq = math.distancesq(squadComponent.targetPos, allyPos);

                if (distSq < minDistanceSq)
                {
                    minDistanceSq = distSq;
                    closestPosition = allyPos;
                }
            }

            squadComponent.targetEnemy = closestPosition;
        }
    }
}