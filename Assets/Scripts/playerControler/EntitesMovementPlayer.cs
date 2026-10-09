using System.Collections;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ECS
{
    public class EntitesMovementPlayer : MonoBehaviour
    {
        [Header("Input Actions")]
        [SerializeField] private InputActionReference moveAction, lookAction, jumpAction;

        [Header("Camera & Animation")]
        [SerializeField] private Camera mainCam;
        [SerializeField] private Animator animator;
        [SerializeField] private Transform hips;
        [SerializeField] private Vector3 startOffset;
        [SerializeField] private float sensitivity = 50f;
        [SerializeField] private float minPitch = -45f;
        [SerializeField] private float maxPitch = 45f;

        [Header("Turning animation")]
        [SerializeField] private float hipTurnSpeed = 10f;
        [SerializeField] private float tiltSpeed = 10f;

        private Transform mainCamTransform;
        private Vector3 offset;
        private Entity entity = Entity.Null;
        private EntityManager em;
        private EntityQuery inputQuery;

        private float fov;
        private float pitch;
        private float yaw;
        private float currentTiltX;
        private float currentTiltZ;
        private MoveState moveState;
        private Coroutine jumpCoroutine;
        private enum MoveState { idle, forwards, left, right, backwards }

        private readonly int groundedHash = Animator.StringToHash("isGrounded");
        private readonly int jumpedHash = Animator.StringToHash("Jumped");

        private void Start()
        {
            moveAction.action.Enable();
            jumpAction.action.Enable();
            lookAction.action.Enable();

            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;

            mainCamTransform = mainCam.transform;
            fov = mainCam.fieldOfView;
            offset = startOffset;

            pitch = transform.localEulerAngles.x;
            yaw = transform.localEulerAngles.y;
            if (pitch > 180f) pitch -= 360f;

            em = World.DefaultGameObjectInjectionWorld.EntityManager;

            inputQuery = em.CreateEntityQuery(
                ComponentType.ReadWrite<PlayerInputData>(),
                ComponentType.ReadWrite<PlayerData>(),
                ComponentType.ReadOnly<PhysicsVelocity>(),
                ComponentType.ReadOnly<LocalToWorld>()
            );

            StartCoroutine(FindPlayer());
        }

        private void OnDestroy()
        {
            moveAction.action.Disable();
            jumpAction.action.Disable();
            lookAction.action.Disable();

            if (inputQuery != default)
            {
                inputQuery.Dispose();
            }
        }

        private IEnumerator FindPlayer()
        {
            while (entity == Entity.Null)
            {
                if (!inputQuery.IsEmptyIgnoreFilter)
                {
                    using NativeArray<Entity> arr = inputQuery.ToEntityArray(Allocator.Temp);
                    if (arr.Length > 0)
                    {
                        entity = arr[0];

                        em.SetComponentData(entity, new PlayerInputData
                        {
                            sensitivity = sensitivity,
                            minPitch = minPitch,
                            maxPitch = maxPitch
                        });

                        Debug.Log("Player FOUND: " + entity);
                        yield break;
                    }
                }
                yield return null;
            }
        }

        private void Update()
        {
            if (entity == Entity.Null) return;

            if (!em.Exists(entity))
            {
                entity = Entity.Null;
                StartCoroutine(FindPlayer());
                return;
            }

            // Read Inputs
            Vector2 lookVector = lookAction.action.ReadValue<Vector2>();
            Vector2 move = moveAction.action.ReadValue<Vector2>();
            bool isJumping = jumpAction.action.ReadValue<float>() > 0.5f;
            float moveSqrMag = move.sqrMagnitude;

            // Fetch Data
            var ltw = em.GetComponentData<LocalToWorld>(entity);
            var m = em.GetComponentData<PlayerData>(entity);
            var velocity = em.GetComponentData<PhysicsVelocity>(entity).Linear;

            // Camera Position Calculation
            Vector3 targetPos = (Vector3)ltw.Position +
                (Vector3)ltw.Right * offset.x +
                Vector3.up * Mathf.Max(offset.y, startOffset.y) +
                (Vector3)ltw.Forward * offset.z;

            transform.position = targetPos;

            // Camera Rotation Calculation
            pitch = Mathf.Clamp(pitch - (lookVector.y * sensitivity * Time.deltaTime), minPitch, maxPitch);
            yaw += lookVector.x * sensitivity * Time.deltaTime;

            // Lerp the X and Z tilts
            float targetTiltX = move.y * moveSqrMag * 6f;
            float targetTiltZ = -move.x * moveSqrMag * 6f;

            currentTiltX = Mathf.Lerp(currentTiltX, targetTiltX, tiltSpeed * Time.deltaTime);
            currentTiltZ = Mathf.Lerp(currentTiltZ, targetTiltZ, tiltSpeed * Time.deltaTime);

            transform.rotation = Quaternion.Euler(currentTiltX, yaw, currentTiltZ);
            mainCamTransform.rotation = Quaternion.Euler(pitch, yaw, 0f);

            // Animation & Jump Logic
            if (m.jumped)
            {
                if (jumpCoroutine != null) StopCoroutine(jumpCoroutine);
                jumpCoroutine = StartCoroutine(PlayerJumpedRoutine());

                m.jumped = false;
                em.SetComponentData(entity, m);
            }

            animator.SetBool(groundedHash, m.grounded);

            // Hip Logic
            if (moveSqrMag > 0.01f)
            {
                Vector3 moveDirection = transform.right * move.x + transform.forward * move.y;
                if (moveDirection != Vector3.zero)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(moveDirection.normalized, Vector3.up);
                    hips.rotation = Quaternion.Slerp(hips.rotation, targetRotation, hipTurnSpeed * Time.deltaTime);
                }
            }

            moveState = GetMoveState(move, moveSqrMag);

            // Set Input Data
            em.SetComponentData(entity, new PlayerInputData
            {
                MoveAction = move,
                Yaw = yaw,
                JumpAction = isJumping,
                sensitivity = sensitivity,
                minPitch = minPitch,
                maxPitch = maxPitch
            });

            // FOV Calculation
            float flatVelocitySqr = (velocity.x * velocity.x) + (velocity.z * velocity.z);
            float targetFov = Mathf.Clamp(fov + (flatVelocitySqr / 32f), 75f, 130f);
            mainCam.fieldOfView = Mathf.Lerp(mainCam.fieldOfView, targetFov, 10f * Time.deltaTime);
        }

        private MoveState GetMoveState(Vector2 move, float sqrMag, float deadzone = 0.1f)
        {
            if (sqrMag <= deadzone * deadzone) return MoveState.idle;

            float ax = Mathf.Abs(move.x);
            float ay = Mathf.Abs(move.y);

            if (ax > ay) return move.x > 0f ? MoveState.right : MoveState.left;
            return move.y > 0f ? MoveState.forwards : MoveState.backwards;
        }

        private IEnumerator PlayerJumpedRoutine()
        {
            animator.SetTrigger(jumpedHash);
            bool changeFov = moveState != MoveState.idle;

            for (float i = 0; i < 0.1f; i += Time.deltaTime)
            {
                offset.y += Time.deltaTime * 5f;
                if (changeFov) mainCam.fieldOfView += Time.deltaTime * 20f;
                yield return null;
            }

            for (float i = 0; i < 1f; i += Time.deltaTime)
            {
                offset.y -= Time.deltaTime * 0.5f;
                if (changeFov) mainCam.fieldOfView -= Time.deltaTime * 2f;
                yield return null;
            }

            offset = startOffset;
            jumpCoroutine = null;
        }
    }
}