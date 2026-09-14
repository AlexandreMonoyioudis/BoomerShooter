using Unity.Entities;
using Unity.Mathematics;

public struct PlayerData : IComponentData
{
    public Entity bulletMark;
    public Entity bulletTrailVFX;
    public float speed;
    public float damping;
    public float jumpHeight;
    public float jumpCooldown;
    public bool jumped;
}

public struct PlayerInputData : IComponentData
{
    public float2 MoveAction;
    public float Yaw;
    public float sensitivity;
    public float minPitch;
    public float maxPitch;
    public bool JumpAction;
}
