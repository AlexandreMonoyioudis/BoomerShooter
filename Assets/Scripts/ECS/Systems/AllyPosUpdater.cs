using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

[BurstCompile]
public partial struct AllyPosUpdater : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<AllyPos>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float3 camPos = SystemAPI.GetSingleton<AllyPos>().Value;
        PlayerPositionProvider.SetPosition(camPos);
    }
}

public partial struct PlayerPositionProvider
{
    public static float3 Position = new float3(0f, 0f, 0f);

    [BurstCompile]
    public static void SetPosition(float3 pos)
    {
        Position = pos;
    }
}


