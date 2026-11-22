using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace ECS
{
    partial struct SmartEnemySystem : ISystem
    {
        private const float CircleRadius = 100f;
        private const float CircleAngularSpeed = 0.5f; // radians per second
        private const float StepSpeed = 8f;          // units per second for Blob / Line

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SmartEnemy>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            float elapsedTime = (float)SystemAPI.Time.ElapsedTime;

            foreach (var (_, squadBuffer) in SystemAPI.Query<RefRO<SmartEnemy>, DynamicBuffer<SquadReferenceElement>>())
            {
                for (int i = 0; i < squadBuffer.Length; i++)
                {
                    Entity squadEntity = squadBuffer[i].SquadEntity;

                    if (!SystemAPI.HasComponent<SquadMovementComponent>(squadEntity))
                        continue;

                    RefRW<SquadMovementComponent> squadMovement = SystemAPI.GetComponentRW<SquadMovementComponent>(squadEntity);

                    // Use targetEnemy from SquadComponent if present, otherwise use the current targetPos as the anchor
                    float3 targetCenter = SystemAPI.HasComponent<SquadComponent>(squadEntity)
                        ? SystemAPI.GetComponentRO<SquadComponent>(squadEntity).ValueRO.targetEnemy
                        : squadMovement.ValueRO.targetPos;

                    float3 currentPos = squadMovement.ValueRO.targetPos;
                    float3 nextPos = currentPos;

                    switch (squadMovement.ValueRO.formation)
                    {
                        case SquadFormationType.Circle:
                            {
                                // Stagger angle per squad index/entity so multiple squads don't stack on the exact same spot
                                float angle = elapsedTime * CircleAngularSpeed + i * 1.1f + squadEntity.Index * 0.37f;
                                nextPos = targetCenter + new float3(
                                    math.cos(angle) * CircleRadius,
                                    0f,
                                    math.sin(angle) * CircleRadius
                                );
                                break;
                            }

                        case SquadFormationType.Blob:
                            {
                                // Run away from targetCenter
                                float3 awayDir = currentPos - targetCenter;
                                awayDir.y = 0f;

                                if (math.lengthsq(awayDir) < 0.0001f)
                                {
                                    // Fallback direction if directly on top of the target
                                    float fallbackAngle = i * 1.1f + squadEntity.Index * 0.37f;
                                    awayDir = new float3(math.cos(fallbackAngle), 0f, math.sin(fallbackAngle));
                                }
                                else
                                {
                                    awayDir = math.normalize(awayDir);
                                }

                                nextPos = currentPos + awayDir * StepSpeed * dt;
                                break;
                            }

                        case SquadFormationType.Line:
                            {
                                float3 toTarget = targetCenter - currentPos;
                                toTarget.y = 0f;
                                float distSq = math.lengthsq(toTarget);
                                float step = StepSpeed * dt;

                                if (distSq > step * step)
                                {
                                    nextPos = currentPos + math.normalize(toTarget) * step;
                                }
                                else
                                {
                                    nextPos = new float3(targetCenter.x, currentPos.y, targetCenter.z);
                                }
                                break;
                            }
                    }

                    squadMovement.ValueRW.targetPos = nextPos;
                }
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }

    public struct SmartEnemy : IComponentData { }

    public struct SquadReferenceElement : IBufferElementData
    {
        public Entity SquadEntity;
    }
}