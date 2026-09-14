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
        [SerializeField] private InputActionReference moveAction, lookAction, jumpAction;
        [SerializeField] private Vector3 startoffset;
        private Vector3 offset;
        private Entity entity = Entity.Null;
        private EntityManager em;
        private EntityQuery inputQuery;
        private float fov;
        private Vector3 velocity;
        private enum MoveState {idle, fowards, left, right, backwards }
        private MoveState moveState;

        [SerializeField] private float sensitivity = 50f;
        [SerializeField] private float minPitch = -45f;
        [SerializeField] private float maxPitch = 45f;

        [SerializeField] private float pitch;
        [SerializeField] private float yaw;
        [SerializeField] private Camera mainCam;

        void Start()
        {
            moveAction?.action.Enable();
            jumpAction?.action.Enable();
            lookAction?.action.Enable();

            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;


            fov = mainCam.fieldOfView;
            offset = startoffset;
            yaw = transform.localEulerAngles.x;
            pitch = transform.localEulerAngles.y;
            if (pitch > 180f) pitch -= 360f;

            StartCoroutine(FindPlayer());
        }

        private void OnDestroy()
        {
            moveAction?.action.Disable();
            jumpAction?.action.Disable();
            lookAction?.action.Disable();
        }

        IEnumerator FindPlayer()
        {
            while (true)
            {
                em = World.DefaultGameObjectInjectionWorld.EntityManager;
                inputQuery = em.CreateEntityQuery(ComponentType.ReadWrite<PlayerInputData>());
                if (inputQuery.IsEmptyIgnoreFilter == false)
                {
                    try
                    {
                        entity = inputQuery.GetSingletonEntity();
                        em.SetComponentData(entity, new PlayerInputData
                        {
                            sensitivity = sensitivity,
                            minPitch = minPitch,
                            maxPitch = maxPitch
                        });
                        Debug.Log("Player FOUND (singleton): " + entity);
                        yield break;
                    }
                    catch
                    {
                        // Not a singleton — fall back to ToEntityArray
                    }

                    using var arr = inputQuery.ToEntityArray(Allocator.Temp);
                    if (arr.Length > 0)
                    {
                        entity = arr[0];

                        em.SetComponentData(entity, new PlayerInputData
                        {
                            sensitivity = sensitivity,
                            minPitch = minPitch,
                            maxPitch = maxPitch,
                        });
                        Debug.Log("Player FOUND (array): " + entity);
                        yield break;
                    }
                }

                Debug.LogWarning("Player not found yet...");
                yield return null;
            }
        }

        void Update()
        {
            if (entity == Entity.Null)
                return;

            Vector2 lookVector = lookAction.action.ReadValue<Vector2>();

            var ltw = em.GetComponentData<LocalToWorld>(entity);
            Vector3 targetPos = (Vector3)ltw.Position +
            (Vector3)ltw.Right * offset.x + Vector3.up * Mathf.Max(offset.y, startoffset.y) +
            (Vector3)ltw.Forward * offset.z;
            transform.position = targetPos;

            pitch += -lookVector.y * sensitivity * Time.deltaTime;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            yaw += lookVector.x * sensitivity * Time.deltaTime;

            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            Camera.main.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

            PlayerData m = em.GetComponentData<PlayerData>(entity);
            if (m.jumped == true)
            {
                StartCoroutine(nameof(playerJumped));
                m.jumped = false;
            }
            Vector2 move = moveAction.action.ReadValue<Vector2>();
            moveState = GetMoveState(move);
            var input = new PlayerInputData
            {
                MoveAction = moveAction.action.ReadValue<Vector2>(),
                Yaw = yaw,
                JumpAction = jumpAction.action.ReadValue<float>() == 1,
            };

            // 2. We already verified entity isn't Null at the top, so we can just set data safely
            em.SetComponentData(entity, input);
            em.SetComponentData(entity, m);

            velocity = em.GetComponentData<PhysicsVelocity>(entity).Linear;
            float velContribution = new Vector2(velocity.x, velocity.y).sqrMagnitude / 16f;
            float targetFov = fov + velContribution / 2;
            targetFov = Mathf.Clamp(targetFov, 75, 130);
            mainCam.fieldOfView = Mathf.Lerp(mainCam.fieldOfView, targetFov, 10 * Time.deltaTime);
        }

        private MoveState GetMoveState(Vector2 move, float deadzone = 0.1f)
        {
            if (move.sqrMagnitude <= deadzone * deadzone) return MoveState.idle;

            float ax = Mathf.Abs(move.x);
            float ay = Mathf.Abs(move.y);

            if (ax > ay)
            {
                return move.x > 0f ? MoveState.right : MoveState.left;
            }
            else
            {
                return move.y > 0f ? MoveState.fowards : MoveState.backwards;
            }
        }

        IEnumerator playerJumped() {
            bool changeFov = true;
            if (moveState == MoveState.idle) changeFov = false;

            float i = 0;
            for (; i < .1f; i += Time.deltaTime)
            {
                offset = new Vector3(offset.x, offset.y + (Time.deltaTime * 5f), offset.z);
                if (changeFov) mainCam.fieldOfView += Time.deltaTime * 20f;
                yield return null;
            }
            i = 0;
            for (; i < 1; i+=Time.deltaTime)
            {
                offset = new Vector3(offset.x, offset.y - (Time.deltaTime * .5f), offset.z);
                if (changeFov) mainCam.fieldOfView -= Time.deltaTime*2f;
                yield return null;
            }
            offset = startoffset;
        }

    }
}
