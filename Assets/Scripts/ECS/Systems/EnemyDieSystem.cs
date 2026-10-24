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

    public struct MarkedForDeath : IComponentData, IEnableableComponent
    {
        public DeathType Value;
    }

    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
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
            var job = new EnemyDieJob{};

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        partial struct EnemyDieJob : IJobEntity
        {

            void Execute([EntityIndexInQuery] int sortKey, Entity entity, in MarkedForDeath markedForDeath)
            {
                switch (markedForDeath.Value)
                {
                    case DeathType.Unexist:
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