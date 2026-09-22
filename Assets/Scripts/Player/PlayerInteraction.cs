using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Interacción")]
    [SerializeField] private float interactionDistance = 3f;

    [Header("Cámara")]
    [SerializeField] private Camera playerCamera;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            TryInteract();
        }
    }

    private void TryInteract()
{
    if (playerCamera == null)
    {
        Debug.LogWarning("PlayerInteraction: no hay una cámara asignada.");
        return;
    }

    Ray ray = new Ray(
        playerCamera.transform.position,
        playerCamera.transform.forward
    );

    Debug.DrawRay(
        ray.origin,
        ray.direction * interactionDistance,
        Color.red,
        2f
    );

    if (Physics.Raycast(
        ray,
        out RaycastHit hit,
        interactionDistance,
        Physics.AllLayers,
        QueryTriggerInteraction.Collide))
    {
        Debug.Log(
            $"Raycast golpeó: {hit.collider.name} | " +
            $"Objeto: {hit.collider.gameObject.name} | " +
            $"Distancia: {hit.distance:F2}"
        );

        if (hit.collider.TryGetComponent<IInteractable>(out IInteractable interactable))
        {
            Debug.Log($"Objeto interactuable detectado: {hit.collider.gameObject.name}");

            interactable.Interact();
            Debug.Log($"Interact() ejecutado sobre: {hit.collider.gameObject.name}");
        }
        else
        {
            Debug.Log($"Objeto NO interactuable: {hit.collider.gameObject.name}");
        }
    }
    else
    {
        Debug.Log("Raycast NO golpeó ningún Collider.");
    }
}
}