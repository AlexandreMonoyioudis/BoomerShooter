using ECS;
using System.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.InputSystem;
using UnityEngine.VFX;

public class PlayerWeaponController : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionReference gunAction;
    [SerializeField] private InputActionReference cannonAction;

    [Header("IK / Rigging")]
    [SerializeField] private TwoBoneIKConstraint[] arms;
    [SerializeField] private TwoBoneIKConstraint cannon;

    [Header("Animator")]
    [SerializeField] private Animator animator;

    [Header("VFX")]
    [SerializeField] private VisualEffect[] effects;

    [Header("Cannon")]
    [SerializeField] private Transform[] cannonTranform;

    [Header("Blend")]
    [SerializeField] private float blendSpeed = 20f;
    [SerializeField] private float aimThreshold = 0.95f;

    [Header("Raycast")]
    [SerializeField] private float rayDistance = 150f;
    [SerializeField] private Transform target;

    private float armTargetWeight = 0f;
    private float cannonTargetWeight = 0f;
    private Coroutine fireCoroutineA;
    private Coroutine fireCoroutineB;

    private Entity cachedPlayerEntity = Entity.Null;

    // Helper property to safely fetch active EntityManager dynamically
    private EntityManager EntityManager
    {
        get
        {
            if (World.DefaultGameObjectInjectionWorld != null)
            {
                return World.DefaultGameObjectInjectionWorld.EntityManager;
            }
            return default;
        }
    }

    private void OnEnable()
    {
        gunAction.action.started += OnGunStarted;
        gunAction.action.canceled += OnGunCanceled;

        //cannonAction.action.started += OnCannonStarted;
        //cannonAction.action.canceled += OnCannonCanceled;

        gunAction.action.Enable();
        cannonAction.action.Enable();
        setCannonPos();
    }

    private void OnDisable()
    {
        gunAction.action.started -= OnGunStarted;
        gunAction.action.canceled -= OnGunCanceled;

        //cannonAction.action.started -= OnCannonStarted;
        //cannonAction.action.canceled -= OnCannonCanceled;

        gunAction.action.Disable();
        cannonAction.action.Disable();
    }

    private void Update()
    {
        float newArmWeight = Mathf.Lerp(arms.Length > 0 ? arms[0].weight : 0f, armTargetWeight, blendSpeed * Time.deltaTime);
        setArmsWeight(newArmWeight);

        cannon.weight = Mathf.Lerp(cannon.weight, cannonTargetWeight, blendSpeed * Time.deltaTime);
    }

    private void OnGunStarted(InputAction.CallbackContext ctx)
    {
        animator.SetBool("shooting", true);
        armTargetWeight = 1f;

        fireCoroutineA = StartCoroutine(TryFireCoroutine(effects[0], 0f, 0.2f));
        fireCoroutineB = StartCoroutine(TryFireCoroutine(effects[1], 0.1f, 0.2f));
    }

    private void OnGunCanceled(InputAction.CallbackContext ctx)
    {
        animator.SetBool("shooting", false);
        armTargetWeight = 0f;

        if (fireCoroutineA != null)
        {
            StopCoroutine(fireCoroutineA);
            effects[0].enabled = false;
            effects[0].gameObject.SetActive(false);
            fireCoroutineA = null;
        }
        if (fireCoroutineB != null)
        {
            StopCoroutine(fireCoroutineB);
            effects[1].enabled = false;
            effects[1].gameObject.SetActive(false);
            fireCoroutineB = null;
        }
    }

    private void OnCannonStarted(InputAction.CallbackContext ctx)
    {
        CancelInvoke();
        cannonTargetWeight = 1f;
        if (cannonTranform != null && cannonTranform.Length >= 2)
        {
            cannonTranform[0].localRotation = Quaternion.Euler(0f, 0f, 40f);
            cannonTranform[1].localRotation = Quaternion.Euler(90f, 0f, 0f);
            cannonTranform[2].localRotation = Quaternion.Euler(50f, 0f, 0f);
        }

    }

    private void OnCannonCanceled(InputAction.CallbackContext ctx)
    {
        cannonTargetWeight = 0f;
        Invoke(nameof(setCannonPos), 0.6f);
    }

    private void setCannonPos()
    {
        cannon.weight = 0f;
        if (cannonTranform != null && cannonTranform.Length >= 2)
        {
            cannonTranform[0].localRotation = Quaternion.Euler(0f, 0f, 0f);
            cannonTranform[1].localRotation = Quaternion.Euler(-70f, 0f, 0f);
            cannonTranform[2].localRotation = Quaternion.Euler(-100f, 0f, 0f);
        }
    }

    private void setArmsWeight(float value)
    {
        for (int i = 0; i < arms.Length; i++)
            arms[i].weight = value;
    }

    private IEnumerator TryFireCoroutine(VisualEffect vfxEffect, float initialDelay, float repeatInterval)
    {
        Transform rayOrigin = vfxEffect.transform;
        vfxEffect.gameObject.SetActive(true);
        Light light = vfxEffect.GetComponent<Light>();

        if (initialDelay > 0f)
            yield return new WaitForSeconds(initialDelay);

        var wait = new WaitForSeconds(repeatInterval / 2);

        while (true)
        {
            float currentArmWeight = (arms.Length > 0) ? arms[0].weight : 0f;
            if (currentArmWeight >= aimThreshold)
            {
                vfxEffect.enabled = true;
                if (light != null) light.enabled = true;

                Vector3 origin = rayOrigin.position;
                Vector3 direction = -rayOrigin.forward;

                ShootRay(origin, direction * rayDistance);
                Debug.DrawRay(origin, direction * rayDistance, Color.red, 1f);
            }

            yield return wait;
            if (light != null) light.enabled = false;
            yield return wait;
        }
    }

    private bool TryGetPlayerEntity(out Entity playerEntity)
    {
        var em = EntityManager;
        if (em == default)
        {
            playerEntity = Entity.Null;
            return false;
        }

        if (cachedPlayerEntity != Entity.Null &&
            em.Exists(cachedPlayerEntity) &&
            em.HasComponent<PlayerInputData>(cachedPlayerEntity))
        {
            playerEntity = cachedPlayerEntity;
            return true;
        }

        using EntityQuery query = em.CreateEntityQuery(typeof(PlayerInputData));

        if (query.IsEmpty)
        {
            cachedPlayerEntity = Entity.Null;
            playerEntity = Entity.Null;
            return false;
        }

        cachedPlayerEntity = query.GetSingletonEntity();
        playerEntity = cachedPlayerEntity;
        return true;
    }

    public void ShootRay(Vector3 origin, Vector3 direction)
    {
        var em = EntityManager;

        if (!TryGetPlayerEntity(out Entity playerEntity))
        {
            Debug.LogWarning("Player Entity with PlayerInputData not found.");
            return;
        }

        if (!em.HasComponent<PlayerData>(playerEntity))
        {
            Debug.LogError("BulletMarkPrefabComponent missing on Player Entity. Ensure PlayerWeaponAuthoring is attached and baked!");
            return;
        }

        Entity bulletMarksEntity = em.GetComponentData<PlayerData>(playerEntity).bulletMark;
        Entity bulletTrailVFXEntity = em.GetComponentData<PlayerData>(playerEntity).bulletTrailVFX;

        if (!em.HasBuffer<RayParam>(playerEntity))
        {
            em.AddBuffer<RayParam>(playerEntity);
        }

        DynamicBuffer<RayParam> buffer = em.GetBuffer<RayParam>(playerEntity);

        buffer.Add(new RayParam
        {
            Origin = origin,
            Direction = math.normalize((float3)direction),
            MaxDistance = rayDistance,
            Filter = CollisionFilter.Default,
            damage = 1,
            Prefab = bulletMarksEntity,
            PrefabVFX = bulletTrailVFXEntity,
            VfxSpeed = 600
        });
    }
}