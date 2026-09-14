using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Extensions;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace ECS
{
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [UpdateAfter(typeof(BuildPhysicsWorld))]
    public partial struct PlayerMovementSystem : ISystem
    {
        private EntityQuery _query;

        public void OnCreate(ref SystemState state)
        {
            _query = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    ComponentType.ReadWrite<LocalTransform>(),
                    ComponentType.ReadWrite<PhysicsVelocity>(),
                    ComponentType.ReadOnly<PlayerInputData>(),
                    ComponentType.ReadWrite<PlayerData>(),
                    ComponentType.ReadOnly<LocalToWorld>(),
                    ComponentType.ReadOnly<PhysicsCollider>(),
                    ComponentType.ReadOnly<PhysicsMass>(),
                    ComponentType.ReadWrite<AllyPos>(),
                }
            });

            state.RequireForUpdate(_query);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var collisionWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;

            var cwCopy = collisionWorld.Clone();

            var job = new PlayerMoveJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                CollisionWorld = cwCopy,
            };

            state.Dependency = job.Schedule(_query, state.Dependency);
        }

        [BurstCompile(FloatPrecision.Medium, FloatMode.Fast)]
        public partial struct PlayerMoveJob : IJobEntity
        {
            public float DeltaTime;
            public CollisionWorld CollisionWorld;

            void Execute(Entity entity,
                         ref LocalTransform transform,
                         ref PhysicsVelocity v,
                         in PlayerInputData input,
                         ref PlayerData m,
                         in LocalToWorld ltw,
                         in PhysicsCollider physCollider,
                         in PhysicsMass mass,
                         ref AllyPos pos)
            {
                bool grounded = false;

                float3 groundCheckOffset = new float3(0f, .05f, 0f);
                float groundCheckDistance = 1.5f;

                float3 startPos = transform.Position + groundCheckOffset;
                float3 endPos = startPos + new float3(0f, -groundCheckDistance, 0f);

                var colliderBlob = physCollider.Value;

                if (colliderBlob.IsCreated)
                {

                    const int PlayerLayerIndex = 6;
                    uint playerLayerMask = 1u << PlayerLayerIndex;
                    var startTransform = new RigidTransform(transform.Rotation, startPos);
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

                    if (m.jumpCooldown > 0.3f)
                    {
                        if (CollisionWorld.CastRay(rayInput, out var hit))
                        {
                            if (hit.Entity != entity) grounded = true;
                        }
                    }
                    else m.jumpCooldown += DeltaTime;
                }
             


                float2 raw = input.MoveAction;

                float3 forward = ltw.Forward;
                float3 right = ltw.Right;

                float3 moveDir = forward * raw.y + right * raw.x;

                if (grounded && input.JumpAction)
                {
                    m.jumped = true;
                    v.Linear = new float3(0, 0, 0);
                    v.ApplyLinearImpulse(mass, new float3(moveDir.x * m.speed, 20, moveDir.z * m.speed));
                }
                else if (grounded)
                    v.Linear = new float3(moveDir.x * m.speed,
                                          v.Linear.y,
                                          moveDir.z * m.speed);
                else {
                    float linearDrag = 1.0f;

                    if (v.Linear.y > 0)
                        v.Linear += new float3(
                            moveDir.x * m.speed * DeltaTime * 1.5f,
                            0,
                            moveDir.z * m.speed * DeltaTime * 1.5f);

                    else
                        v.Linear += new float3(
                            moveDir.x * m.speed * DeltaTime * 1.5f,
                            v.Linear.y * 2.5f * DeltaTime,
                            moveDir.z * m.speed * DeltaTime * 1.5f);

                    // linear damping: v += (-k * v) * dt  => v *= (1 - k * dt)
                    float scale = 1f - linearDrag * DeltaTime;
                    scale = math.max(scale, 0f); // avoid negative scale if k*dt > 1
                    v.Linear *= scale;
                    if (math.lengthsq(v.Linear) < 1e-6f) v.Linear = float3.zero;
                }

                v.Angular = float3.zero;

                quaternion targetRot = quaternion.Euler(0, math.radians(input.Yaw), 0);
                transform.Rotation = targetRot;
                pos.Value = transform.Position;
            }
        }
    }
}
