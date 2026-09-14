using Unity.Burst;
using Unity.Entities;

namespace ECS
{
    public enum DeathType
    {
        Unexist,
        Explode,
        Collapse
    }

    public struct MarkedForDeath : IComponentData
    {
        public DeathType Value;
    }

    [BurstCompile]
    public partial struct EnemyDieSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MarkedForDeath>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            var job = new EnemyDieJob
            {
                ECB = ecb
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        partial struct EnemyDieJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter ECB;

            void Execute([EntityIndexInQuery] int sortKey, Entity entity, in MarkedForDeath markedForDeath)
            {
                switch (markedForDeath.Value)
                {
                    case DeathType.Unexist:
                        ECB.DestroyEntity(sortKey, entity);
                        break;

                    case DeathType.Explode:
                        break;

                    case DeathType.Collapse:
                        break;
                }
            }
        }
    }
}