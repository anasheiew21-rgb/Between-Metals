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

    [Tooltip("Opcional. Sonido que se reproduce en la posición del objeto al recogerlo. Si se deja " +
             "vacío se usa el del ItemData y, si ese tampoco está, el de BibliotecaDeSonidos.")]
    [SerializeField] private AudioClip pickupSound;

    [Tooltip("Se invoca una vez al recoger el ítem (para objetivos, puzzles, etc.).")]
    [SerializeField] private UnityEvent onPickedUp = new UnityEvent();

    private bool collected;
    private Inventory resolvedInventory;
    private bool inventorySearched;

    private void Awake()
    {
        MostrarModelo();
    }

    /// <summary>
    /// Pone el modelo 3D del ItemData en lugar del cubo gris del prefab base. Sin modelo asignado
    /// no hace nada y se sigue viendo el cubo, que es el comportamiento de antes.
    /// </summary>
    private void MostrarModelo()
    {
        if (item == null || item.modelo3D == null) return;

        // El MeshRenderer propio se APAGA en vez de destruirse: el cubo es tambien lo que define
        // el BoxCollider del prefab base, que es con lo que el raycast de PlayerInteraction apunta
        // al item. Apagandolo se va el cubo de la vista pero el objeto se sigue pudiendo señalar,
        // y con una caja pareja para todos los items en vez de una por forma.
        MeshRenderer propio = GetComponent<MeshRenderer>();
        if (propio != null) propio.enabled = false;

        GameObject modelo = Instantiate(item.modelo3D, transform);
        modelo.name = "Modelo";
        modelo.transform.localPosition = Vector3.zero;
        modelo.transform.localRotation = Quaternion.identity;

        // Los modelos estan hechos en metros de verdad (una poción mide 0,25 m), pero el prefab
        // base tiene la raiz escalada a 0,3 para achicar el cubo. Sin esto, el modelo heredaria esa
        // escala y se veria tres veces mas chico de lo que se diseño. Se contrarresta la escala
        // acumulada para que el modelo quede, en el mundo, del tamaño con el que se construyo.
        Vector3 acumulada = transform.lossyScale;
        modelo.transform.localScale = new Vector3(
            Mathf.Approximately(acumulada.x, 0f) ? 1f : 1f / acumulada.x,
            Mathf.Approximately(acumulada.y, 0f) ? 1f : 1f / acumulada.y,
            Mathf.Approximately(acumulada.z, 0f) ? 1f : 1f / acumulada.z);
    }

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

        // Inventario lleno: el objeto queda en el mapa y el jugador se entera por el aviso de P-09,
        // que si no solo veria que apretar E no hace nada.
        if (!inventory.AddItem(item))
        {
            AvisosUI.Alertar("Inventario lleno");
            return;
        }

        collected = true;

        // Suena desde donde estaba el objeto y ruteado al grupo sfx del mixer (por eso
        // BibliotecaDeSonidos y no AudioSource.PlayClipAtPoint, que no pasa por el mixer y el
        // slider "Efectos" del menu no lo afectaba).
        BibliotecaDeSonidos.ReproducirEnPunto(SonidoDeRecogida(), transform.position);

        onPickedUp?.Invoke();

        // Se desactiva en vez de destruirse, para que otros sistemas puedan seguir referenciándolo.
        gameObject.SetActive(false);
    }

    // Tres niveles, del mas especifico al mas general: lo que diga ESTA instancia en la escena,
    // lo que diga el ItemData (vale para todas las copias de ese item) y, si no hay nada, el que
    // le toca al itemId por convencion de nombre de archivo (ver BibliotecaDeSonidos). Siempre hay
    // sonido: el ultimo escalon es el generico.
    private AudioClip SonidoDeRecogida()
    {
        if (pickupSound != null) return pickupSound;
        if (item != null && item.sonido != null) return item.sonido;
        return BibliotecaDeSonidos.SonidoDeItem(item != null ? item.itemId : null);
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
