using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Extensions;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace ECS
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(PhysicsSystemGroup))]
    public partial struct PlayerMovementSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsWorldSingleton>();
            state.RequireForUpdate<PlayerInputData>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var collisionWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;

            var job = new PlayerMoveJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                CollisionWorld = collisionWorld
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile(FloatPrecision.Medium, FloatMode.Fast)]
        public partial struct PlayerMoveJob : IJobEntity
        {
            public float DeltaTime;

            [ReadOnly] public CollisionWorld CollisionWorld;

            void Execute(
                Entity entity,
                ref LocalTransform transform,
                ref PhysicsVelocity v,
                in PlayerInputData input,
                ref PlayerData m,
                in PhysicsCollider physCollider,
                in PhysicsMass mass,
                ref AllyPos pos)
            {
                // 1. Calculate Rotation & Direction Vectors via math.mul
                quaternion targetRot = quaternion.Euler(0f, math.radians(input.Yaw), 0f);
                transform.Rotation = targetRot;

                // Multiply rotation by unit direction vectors for Forward and Right
                float3 forward = math.mul(targetRot, new float3(0f, 0f, 1f));
                float3 right = math.mul(targetRot, new float3(1f, 0f, 0f));
                float3 moveDir = forward * input.MoveAction.y + right * input.MoveAction.x;

                // 2. Ground Raycast Check
                bool grounded = false;

                if (physCollider.IsValid)
                {
                    if (m.jumpCooldown > 0.3f)
                    {
                        float3 startPos = transform.Position + new float3(0f, 0.05f, 0f);
                        float3 endPos = startPos - new float3(0f, 1.5f, 0f);

                        const uint playerLayerMask = 1u << 6; // Layer 6
                        var rayInput = new RaycastInput
                        {
                            Start = startPos,
                            End = endPos,
                            Filter = new CollisionFilter
                            {
                                BelongsTo = playerLayerMask,
                                CollidesWith = ~playerLayerMask
                            }
                        };

                        if (CollisionWorld.CastRay(rayInput, out var hit))
                        {
                            if (hit.Entity != entity)
                            {
                                grounded = true;
                            }
                        }
                    }
                    else
                    {
                        m.jumpCooldown += DeltaTime;
                    }
                }

                if (grounded && input.JumpAction)
                {
                    m.jumped = true;
                    m.grounded = false;
                    m.jumpCooldown = 0f;

                    v.Linear = float3.zero;
                    v.ApplyLinearImpulse(mass, new float3(moveDir.x * m.speed, 20f, moveDir.z * m.speed));
                }
                else if (grounded)
                {
                    m.grounded = true; // Set grounded to true when on solid ground
                    v.Linear = new float3(moveDir.x * m.speed, v.Linear.y, moveDir.z * m.speed);
                }
                else
                {
                    m.grounded = false; // Set grounded to false when in mid-air

                    // Mid-Air Control
                    float airControlMultiplier = 1.5f;
                    v.Linear.x += moveDir.x * m.speed * DeltaTime * airControlMultiplier;
                    v.Linear.z += moveDir.z * m.speed * DeltaTime * airControlMultiplier;

                    // Fall Gravity Acceleration
                    if (v.Linear.y < 0f)
                    {
                        v.Linear.y += v.Linear.y * 2.5f * DeltaTime;
                    }

                        // Apply air resistance strictly on horizontal XZ plane
                        float linearDrag = 1.0f;
                    float scale = math.max(1f - linearDrag * DeltaTime, 0f);
                    v.Linear.x *= scale;
                    v.Linear.z *= scale;

                    if (math.lengthsq(v.Linear.xz) < 1e-6f)
                    {
                        v.Linear.x = 0f;
                        v.Linear.z = 0f;
                    }
                }

                v.Angular = float3.zero;
                pos.Value = transform.Position;
            }
        }
    }
}