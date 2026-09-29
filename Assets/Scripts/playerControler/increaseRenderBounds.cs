using UnityEngine;

public class IncreaseRenderBounds : MonoBehaviour
{
    void Start()
    {
        Renderer[] allRenderers = GetComponentsInChildren<Renderer>();

        foreach (Renderer ren in allRenderers)
        {
            if (ren is MeshRenderer meshRen)
            {
                MeshFilter filter = meshRen.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                {
                    //meshRen.allowOcclusionWhenDynamic = false;
                    Bounds sharedBounds = filter.sharedMesh.bounds;
                    sharedBounds.extents = new Vector3(20f, 20f, 20f);
                    filter.sharedMesh.bounds = sharedBounds;
                }
            }
        }

        Destroy(this);
    }
}