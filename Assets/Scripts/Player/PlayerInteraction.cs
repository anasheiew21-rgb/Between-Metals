using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Interacción")]
    [SerializeField] private float interactionDistance = 3f;

    [Header("Cámara")]
    [SerializeField] private Camera playerCamera;

    // A que IInteractable se le esta apuntando ahora mismo (o null si no hay ninguno en rango)
    private IInteractable objetivoActual;

    private void Update()
    {
        ActualizarObjetivo();

        // Con el menu de pausa abierto, 'E' no debe poder abrir la tienda (ni ninguna otra
        // interaccion): primero hay que cerrar la pausa.
        if (objetivoActual != null && !Menu.IsOpen && Input.GetKeyDown(KeyCode.E))
        {
            objetivoActual.Interactuar();
        }
    }

    // Se corre todos los frames (no solo al presionar E) para que el cartel de "Presiona E..."
    // aparezca apenas la camara apunta a algo interactuable, y desaparezca al dejar de mirarlo.
    private void ActualizarObjetivo()
    {
        IInteractable encontrado = null;

        if (playerCamera == null)
        {
            Debug.LogWarning("PlayerInteraction: no hay una cámara asignada.");
        }
        else
        {
            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

            if (Physics.Raycast(
                ray,
                out RaycastHit hit,
                interactionDistance,
                Physics.AllLayers,
                QueryTriggerInteraction.Collide))
            {
                // GetComponentInParent por si el Collider esta en un hijo (ej. el mesh) y el
                // componente interactuable esta en la raiz del objeto.
                encontrado = hit.collider.GetComponentInParent<IInteractable>();
            }
        }

        if (encontrado == objetivoActual) return;

        objetivoActual = encontrado;

        if (objetivoActual != null) PromptInteraccion.Instancia?.Mostrar(objetivoActual.TextoPrompt);
        else PromptInteraccion.Instancia?.Ocultar();
    }
}
