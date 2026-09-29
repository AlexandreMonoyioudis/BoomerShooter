using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

namespace ECS
{
    public class PlayerAuthoring : MonoBehaviour
    {
        [Header("Prefabs")]
        [SerializeField] public GameObject bulletMarkPrefab;
        [SerializeField] public GameObject bulletVFXPrefab;

        [Header("Movement Settings")]
        public float speed = 20f;
        public float jumpHeight = 10f;
        public float gravityFactor = 3f;

        class PlayerBaker : Baker<PlayerAuthoring>
        {
            public override void Bake(PlayerAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new AllyPos());
                AddComponent(entity, new PlayerInputData());

                AddComponent(entity, new PlayerData
                {
                    bulletMark = GetEntity(authoring.bulletMarkPrefab, TransformUsageFlags.Dynamic),
                    bulletTrailVFX = GetEntity(authoring.bulletVFXPrefab, TransformUsageFlags.Dynamic),
                    speed = authoring.speed,
                    damping = 0.01f,
                    jumpHeight = authoring.jumpHeight,
                    jumpCooldown = 0f,
                    jumped = false,
                });
            }
        }
    }
}
