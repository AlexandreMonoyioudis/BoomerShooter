using Unity.Entities;
using Unity.Mathematics;

public struct SpawnEnemyComponent : IComponentData
{
    public Entity prefab;
    public float3 spawnPos;
    public float nextSpawnTime;
    public float spawnRate;
    public TransformAuthoring player;
}
