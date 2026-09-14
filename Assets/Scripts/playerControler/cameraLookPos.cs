using UnityEngine;

public class cameraLookPos : MonoBehaviour
{
    [SerializeField] private Transform lookTarget;
    [SerializeField] private float distance = 500f;
    [SerializeField] private float smoothSpeed = 20f;

    private void Update()
    {
        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, distance))
        {
            lookTarget.position = Vector3.Lerp(lookTarget.position, hit.point, smoothSpeed * Time.deltaTime);
        }
        else
        {
            Vector3 rayEnd = transform.position + transform.forward * distance;
            lookTarget.position = Vector3.Lerp(lookTarget.position, rayEnd, smoothSpeed * Time.deltaTime);
        }
    }
}
