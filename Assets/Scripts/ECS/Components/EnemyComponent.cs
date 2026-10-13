using Unity.Entities;
using Unity.Mathematics;
public struct EnemyComponent : IComponentData
{
    public float3 moveDirection;
    public float3 targetPos;
    public float moveSpeed;
    public float hp;
}