using UnityEngine;

// Comerciante del laberinto. No detecta nada por si solo: PlayerInteraction es quien apunta con
// un raycast y llama a Interactuar() cuando el jugador esta mirandolo y presiona 'E'.
// Necesita un Collider (puede ser el de su propio modelo) para que ese raycast le pegue.
[RequireComponent(typeof(Collider))]
public class NPCMerchant : MonoBehaviour, IInteractable
{
    [Header("Comerciante")]
    [SerializeField] private string nombreComerciante = "Comerciante";

    [Tooltip("Si se deja vacio, busca un ShopManager en este mismo GameObject")]
    [SerializeField] private ShopManager tienda;

    public string TextoPrompt => "Presiona E para comerciar con " + nombreComerciante;

    void Awake()
    {
        if (tienda == null) tienda = GetComponent<ShopManager>();
    }

    public void Interactuar()
    {
        if (tienda == null)
        {
            Debug.LogWarning("NPCMerchant: no hay una ShopManager asignada (ni en el Inspector ni en este GameObject).");
            return;
        }

        tienda.AlternarTienda();
    }
}
