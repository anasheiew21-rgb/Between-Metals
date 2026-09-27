using UnityEngine;
using UnityEngine.Events;

// Ítem del mapa que se recoge con la interacción del equipo (RF07, HU-06). No tiene input propio:
// PlayerInteraction lo detecta con su raycast, muestra TextoPrompt y llama a Interactuar() con 'E'.
// Necesita un Collider (en este objeto o en un hijo) para que ese raycast lo alcance.
public class ItemPickup : MonoBehaviour, IInteractable
{
    [Tooltip("Ítem que se agrega al inventario al recoger este objeto.")]
    [SerializeField] private ItemData item;

    [Tooltip("Opcional. Si se deja vacío, se busca un Inventory en la escena la primera vez que hace falta.")]
    [SerializeField] private Inventory targetInventory;

    [Tooltip("Opcional. Sonido que se reproduce en la posición del objeto al recogerlo.")]
    [SerializeField] private AudioClip pickupSound;

    [Tooltip("Se invoca una vez al recoger el ítem (para objetivos, puzzles, etc.).")]
    [SerializeField] private UnityEvent onPickedUp = new UnityEvent();

    private bool collected;
    private Inventory resolvedInventory;
    private bool inventorySearched;

    /// <summary>
    /// Texto del cartel de interacción. Vacío si ya se recogió o no tiene ítem.
    /// PromptInteraccion lo muestra tal cual, por eso incluye "Presiona E".
    /// </summary>
    public string TextoPrompt
    {
        get
        {
            if (collected || item == null) return string.Empty;

            Inventory inventory = ResolveInventory();
            if (inventory != null && inventory.IsFull) return "Inventario lleno";

            return "Presiona E para recoger " + item.itemName;
        }
    }

    /// <summary>
    /// Agrega el ítem al inventario. Si entra, lo marca como recogido, reproduce el sonido,
    /// invoca onPickedUp y desactiva el objeto. Si el inventario está lleno, no hace nada.
    /// </summary>
    public void Interactuar()
    {
        if (collected) return;

        if (item == null)
        {
            Debug.LogWarning($"ItemPickup '{name}': no tiene un ItemData asignado.", this);
            return;
        }

        Inventory inventory = ResolveInventory();
        if (inventory == null) return; // el aviso ya lo dio ResolveInventory

        if (!inventory.AddItem(item)) return; // inventario lleno: el objeto queda en el mapa

        collected = true;

        if (pickupSound != null) AudioSource.PlayClipAtPoint(pickupSound, transform.position);

        onPickedUp?.Invoke();

        // Se desactiva en vez de destruirse, para que otros sistemas puedan seguir referenciándolo.
        gameObject.SetActive(false);
    }

    // targetInventory tiene prioridad. Si no hay, se busca en la escena una sola vez y se cachea;
    // si no aparece ninguno se avisa una sola vez (TextoPrompt puede leerse seguido).
    private Inventory ResolveInventory()
    {
        if (targetInventory != null) return targetInventory;
        if (inventorySearched) return resolvedInventory;

        inventorySearched = true;
        resolvedInventory = FindAnyObjectByType<Inventory>();

        if (resolvedInventory == null)
        {
            Debug.LogWarning($"ItemPickup '{name}': no se encontró ningún Inventory en la escena y no hay targetInventory asignado.", this);
        }
        return resolvedInventory;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (item == null)
        {
            Debug.LogWarning($"ItemPickup '{name}': falta asignar el ItemData.", this);
        }

        if (GetComponentInChildren<Collider>(true) == null)
        {
            Debug.LogWarning($"ItemPickup '{name}': no tiene Collider (ni en hijos); el raycast de PlayerInteraction no lo va a detectar.", this);
        }
    }
#endif
}
