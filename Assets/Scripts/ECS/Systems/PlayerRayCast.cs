using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace ECS
{
    // --- DATA COMPONENTS & BUFFERS ---

    public struct RayParam : IBufferElementData
    {
        public float3 Origin;
        public float3 Direction;
        public float MaxDistance;
        public CollisionFilter Filter;
        public int damage;
        public Entity Prefab;
        public Entity PrefabVFX;
        public float VfxSpeed;
    }

    public struct BulletMarkPoolElement : IBufferElementData
    {
        public Entity DecalEntity;
        public Entity DecalEntityVFX;
    }

    public struct VfxLerpData : IComponentData
    {
        public float3 Start;
        public float3 Target;
        public float Speed;
        public float Progress; // Normalized 0.0 to 1.0
        public bool IsActive;
        public bool InitializePosition;
    }

    // --- SYSTEM 1: RAYCAST & POOL MANAGER ---

    [BurstCompile]
    public partial struct PlayerRayCast : ISystem
    {
        private const int MAX_BULLET_MARKS = 64;
        private bool _isPoolInitialized;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RayParam>();
            state.RequireForUpdate<PhysicsWorldSingleton>();
            _isPoolInitialized = false;
        }

        public void OnUpdate(ref SystemState state)
        {
            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();

            // Ensure Singleton Pool entity exists
            if (!SystemAPI.HasSingleton<BulletMarkPoolElement>())
            {
                Entity poolEntity = state.EntityManager.CreateEntity();
                state.EntityManager.AddBuffer<BulletMarkPoolElement>(poolEntity);
            }

            Entity poolSingletonEntity = SystemAPI.GetSingletonEntity<BulletMarkPoolElement>();

            // PHASE 1: Pre-warm pool outside query bounds
            if (!_isPoolInitialized)
            {
                Entity decalPrefab = Entity.Null;
                Entity vfxPrefab = Entity.Null;
                bool foundPrefab = false;

                foreach (var rayBuffer in SystemAPI.Query<DynamicBuffer<RayParam>>())
                {
                    for (int i = 0; i < rayBuffer.Length; i++)
                    {
                        if (rayBuffer[i].Prefab != Entity.Null || rayBuffer[i].PrefabVFX != Entity.Null)
                        {
                            decalPrefab = rayBuffer[i].Prefab;
                            vfxPrefab = rayBuffer[i].PrefabVFX;
                            foundPrefab = true;
                            break;
                        }
                    }
                    if (foundPrefab) break;
                }

                if (foundPrefab)
                {
                    InitializePool(ref state, poolSingletonEntity, decalPrefab, vfxPrefab);
                    _isPoolInitialized = true;
                }
            }

            // PHASE 2: Execute Raycast Job
            var hitResults = new NativeList<DecalHitInfo>(Allocator.TempJob);

            var job = new RaycastJob
            {
                CollisionWorld = physicsWorld.CollisionWorld,
                EnemyLookup = SystemAPI.GetComponentLookup<EnemyComponent>(isReadOnly: true),
                HitResults = hitResults,
                ECB = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter()
            };

            state.Dependency = job.Schedule(state.Dependency);
            state.Dependency.Complete();

            // PHASE 3: Apply Pool Updates and Component Changes
            DynamicBuffer<BulletMarkPoolElement> markPool = state.EntityManager.GetBuffer<BulletMarkPoolElement>(poolSingletonEntity);

            if (markPool.Length > 0 && hitResults.Length > 0)
            {
                for (int i = 0; i < hitResults.Length; i++)
                {
                    var hit = hitResults[i];
                    var poolElement = markPool[0];
                    markPool.RemoveAt(0);

                    // Reposition Decal at impact point
                    if (poolElement.DecalEntity != Entity.Null && hit.HasDecal)
                    {
                        state.EntityManager.SetComponentData(poolElement.DecalEntity, hit.DecalTransform);
                    }

                    // Initialize Lerp Data on the pooled VFX entity
                    if (poolElement.DecalEntityVFX != Entity.Null)
                    {
                        state.EntityManager.SetComponentData(poolElement.DecalEntityVFX, hit.VfxInitialTransform);
                        state.EntityManager.SetComponentData(poolElement.DecalEntityVFX, new VfxLerpData
                        {
                            Start = hit.VfxStart,
                            Target = hit.VfxTarget,
                            Speed = hit.VfxSpeed,
                            Progress = 0f,
                            IsActive = true,
                            InitializePosition = true
                        });
                    }

                    markPool.Add(poolElement);
                }
            }

            hitResults.Dispose();
        }

        private void InitializePool(ref SystemState state, Entity poolSingletonEntity, Entity decalPrefab, Entity vfxPrefab)
        {
            LocalTransform hiddenTransform = LocalTransform.FromPositionRotationScale(
                new float3(0, -9999f, 0),
                quaternion.identity,
                0.01f
            );

            NativeArray<Entity> decalEntities = new NativeArray<Entity>(MAX_BULLET_MARKS, Allocator.Temp);
            NativeArray<Entity> vfxEntities = new NativeArray<Entity>(MAX_BULLET_MARKS, Allocator.Temp);

            if (decalPrefab != Entity.Null)
            {
                state.EntityManager.Instantiate(decalPrefab, decalEntities);
            }

            if (vfxPrefab != Entity.Null)
            {
                state.EntityManager.Instantiate(vfxPrefab, vfxEntities);

                for (int i = 0; i < MAX_BULLET_MARKS; i++)
                {
                    Entity vfx = vfxEntities[i];
                    if (!state.EntityManager.HasComponent<VfxLerpData>(vfx))
                    {
                        state.EntityManager.AddComponentData(vfx, new VfxLerpData { IsActive = false, InitializePosition = false });
                    }
                }
            }

            DynamicBuffer<BulletMarkPoolElement> markPool = state.EntityManager.GetBuffer<BulletMarkPoolElement>(poolSingletonEntity);

            for (int i = 0; i < MAX_BULLET_MARKS; i++)
            {
                Entity decal = decalPrefab != Entity.Null ? decalEntities[i] : Entity.Null;
                Entity vfx = vfxPrefab != Entity.Null ? vfxEntities[i] : Entity.Null;

                if (decal != Entity.Null)
                {
                    state.EntityManager.SetComponentData(decal, hiddenTransform);
                }

                if (vfx != Entity.Null)
                {
                    state.EntityManager.SetComponentData(vfx, hiddenTransform);
                }

                markPool.Add(new BulletMarkPoolElement
                {
                    DecalEntity = decal,
                    DecalEntityVFX = vfx
                });
            }

            decalEntities.Dispose();
            vfxEntities.Dispose();
        }

        public struct DecalHitInfo
        {
            public bool HasDecal;
            public LocalTransform DecalTransform;
            public LocalTransform VfxInitialTransform;
            public float3 VfxStart;
            public float3 VfxTarget;
            public float VfxSpeed;
        }

        [BurstCompile]
        partial struct RaycastJob : IJobEntity
        {
            [ReadOnly] public CollisionWorld CollisionWorld;
            [ReadOnly] public ComponentLookup<EnemyComponent> EnemyLookup;
            public NativeList<DecalHitInfo> HitResults;
            public EntityCommandBuffer.ParallelWriter ECB;

            public void Execute([EntityIndexInQuery] int sortKey, Entity entity, DynamicBuffer<RayParam> rayBuffer)
            {
                for (int i = rayBuffer.Length - 1; i >= 0; i--)
                {
                    var rp = rayBuffer[i];
                    var input = new RaycastInput
                    {
                        Start = rp.Origin,
                        End = rp.Origin + rp.Direction * rp.MaxDistance,
                        Filter = rp.Filter
                    };

                    bool hasHit = CollisionWorld.CastRay(input, out RaycastHit hit);
                    float3 endPosition = hasHit ? hit.Position : input.End;

                    float3 rayDir = endPosition - rp.Origin;
                    float distance = math.length(rayDir);
                    quaternion vfxRotation = distance > 0.001f
                        ? quaternion.LookRotationSafe(math.normalize(rayDir), math.up())
                        : quaternion.identity;

                    DecalHitInfo hitInfo = new DecalHitInfo
                    {
                        HasDecal = false,
                        VfxStart = rp.Origin,
                        VfxTarget = endPosition,
                        VfxSpeed = rp.VfxSpeed,
                        VfxInitialTransform = LocalTransform.FromPositionRotationScale(rp.Origin, vfxRotation, 1.0f)
                    };

                    if (hasHit)
                    {
                        Entity hitEntity = hit.Entity;

                        if (EnemyLookup.HasComponent(hitEntity))
                        {
                            EnemyComponent enemy = EnemyLookup[hitEntity];
                            float newHp = enemy.hp - rp.damage;

                            enemy.hp = newHp;
                            ECB.SetComponent(sortKey, hitEntity, enemy);

                            if (newHp <= 0f)
                            {
                                ECB.AddComponent(sortKey, hitEntity, new MarkedForDeath
                                {
                                    Value = DeathType.Unexist
                                });
                            }
                        }
                        else
                        {
                            if (rp.Prefab != Entity.Null)
                            {
                                float3 surfaceNormal = math.normalize(hit.SurfaceNormal);
                                float3 spawnPosition = hit.Position + (surfaceNormal * 0.01f);
                                float3 up = math.abs(surfaceNormal.y) > 0.99f ? math.forward() : math.up();
                                quaternion rotation = quaternion.LookRotationSafe(surfaceNormal, up);
                                rotation = math.mul(rotation, quaternion.RotateX(math.radians(90f)));

                                hitInfo.HasDecal = true;
                                hitInfo.DecalTransform = LocalTransform.FromPositionRotationScale(spawnPosition, rotation, 0.1f);
                            }
                        }
                    }

                    HitResults.Add(hitInfo);
                    rayBuffer.RemoveAt(i);
                }
            }
        }
    }

    // --- SYSTEM 2: EXECUTES FRAME-BY-FRAME LERP FOR VFX ---

    [BurstCompile]
    public partial struct VfxLerpSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            new VfxLerpJob
            {
                DeltaTime = deltaTime
            }.ScheduleParallel();
        }

        [BurstCompile]
        partial struct VfxLerpJob : IJobEntity
        {
            public float DeltaTime;

            public void Execute(ref LocalTransform transform, ref VfxLerpData lerpData)
            {
                if (!lerpData.IsActive) return;

                // 1. Set position to ray start on the first frame and wait for the next frame to start moving
                if (lerpData.InitializePosition)
                {
                    transform.Position = lerpData.Start;
                    lerpData.InitializePosition = false;
                    return;
                }

                float totalDistance = math.distance(lerpData.Start, lerpData.Target);

                if (totalDistance < 0.001f)
                {
                    transform.Position = lerpData.Target;
                    lerpData.IsActive = false;
                    return;
                }

                float progressStep = (lerpData.Speed * DeltaTime) / totalDistance;

                if (lerpData.Progress + progressStep >= 1.0f)
                {
                    transform.Position = lerpData.Target;
                    lerpData.Progress = 1.0f;
                    lerpData.IsActive = false;
                }
                else
                {
                    lerpData.Progress += progressStep;
                    transform.Position = math.lerp(lerpData.Start, lerpData.Target, lerpData.Progress);
                }
            }
        }
    }
}