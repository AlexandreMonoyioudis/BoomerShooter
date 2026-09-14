using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace ECS
{
    [BurstCompile]
    public partial struct EnemySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerInputData>();
            state.RequireForUpdate<EnemyComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            Entity playerEntity = SystemAPI.GetSingletonEntity<PlayerInputData>();
            float3 playerPos = SystemAPI.GetComponent<LocalTransform>(playerEntity).Position;

            var job = new EnemyMoveJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                playerPos = playerPos,
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }
    }

    [BurstCompile]
    public partial struct EnemyMoveJob : IJobEntity
    {
        public float DeltaTime;
        public float3 playerPos;

        void Execute(in LocalTransform transform,
                     ref PhysicsVelocity physicsVelocity,
                     in EnemyComponent enemyComponent)
        {
            float3 toPlayer = playerPos - transform.Position;
            float distSq = math.lengthsq(toPlayer);

            if (distSq <= 0.0001f)
            {
                physicsVelocity.Linear = new float3(0f, physicsVelocity.Linear.y, 0f);
                physicsVelocity.Angular = float3.zero;
                return;
            }

            float dist = math.sqrt(distSq);
            float3 dir = toPlayer / dist;

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

            // --- Yaw Rotation ---
            float3 forward = transform.Forward();
            float currentYaw = math.atan2(forward.x, forward.z);
            float desiredYaw = math.atan2(dir.x, dir.z);

            float rawDelta = desiredYaw - currentYaw;
            // Clean modulo angle wrapping without redundant sin/cos calls
            float deltaYaw = (rawDelta + math.PI) % (2f * math.PI) - math.PI;
            if (deltaYaw < -math.PI) deltaYaw += 2f * math.PI;

            float maxAngular = 4f; // rad/s
            float desiredAngularSpeed = deltaYaw / math.max(DeltaTime, 0.0001f);
            float appliedAngular = math.clamp(desiredAngularSpeed, -maxAngular, maxAngular);

            physicsVelocity.Angular = new float3(0f, appliedAngular, 0f);
        }
    }
}