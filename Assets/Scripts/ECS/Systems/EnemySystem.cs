using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace ECS
{
    [BurstCompile]
    public partial struct EnemyMovementSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            EnemyMovementJob job = new EnemyMovementJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }
    }

    [BurstCompile]
    public partial struct EnemyMovementJob : IJobEntity
    {
        public float DeltaTime;

        void Execute(in LocalTransform transform,
                     ref PhysicsVelocity physicsVelocity,
                     ref PhysicsMass physicsMass,
                     ref EnemyMovementControler enemyMovementControler,
                     in EnemyMovement enemyComponent)
        {
            physicsMass.InverseInertia.x = 0f;
            physicsMass.InverseInertia.z = 0f;

            if (enemyComponent.hp <= 0)
                return;

            float3 toTarget = enemyMovementControler.targetPos - transform.Position;
            float distSq = math.lengthsq(toTarget);

            if (distSq <= 0.0001f)
            {
                physicsVelocity.Linear = new float3(0f, physicsVelocity.Linear.y, 0f);
                physicsVelocity.Angular = float3.zero;
                return;
            }

            float dist = math.sqrt(distSq);
            float3 dir = toTarget / dist;

            float maxSpeed = enemyComponent.moveSpeed;
            float accel = 10f;

            float3 targetVel = dir * maxSpeed;

            // Preserve the Y-axis velocity (gravity, vertical forces)
            float3 newVel = math.lerp(physicsVelocity.Linear, targetVel, accel * DeltaTime);
            newVel.y = physicsVelocity.Linear.y;

            // Clamp horizontal speed only
            float2 horizontalVel = new float2(newVel.x, newVel.z);
            if (math.lengthsq(horizontalVel) > maxSpeed * maxSpeed)
            {
                horizontalVel = math.normalize(horizontalVel) * maxSpeed;
                newVel.x = horizontalVel.x;
                newVel.z = horizontalVel.y;
            }

            physicsVelocity.Linear = newVel;

            float3 forward = transform.Forward();
            float dot = (forward.x * dir.x) + (forward.z * dir.z);
            float cross = (forward.z * dir.x) - (forward.x * dir.z);
            float deltaYaw = math.atan2(cross, dot);

            float maxAngular = 4f; // rad/s
            float desiredAngularSpeed = deltaYaw / math.max(DeltaTime, 0.0001f);
            float appliedAngular = math.clamp(desiredAngularSpeed, -maxAngular, maxAngular);

            physicsVelocity.Angular = new float3(0f, appliedAngular, 0f);
        }
    }

    public struct EnemyMovement : IComponentData
    {
        public float moveSpeed;
        public float hp;
    }

    public struct EnemyMovementControler : IComponentData
    {
        public float3 targetPos;
    }
}