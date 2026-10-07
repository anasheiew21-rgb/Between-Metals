using UnityEngine;

// Moneda tirada en el mapa (RF09). No detecta nada por sí sola: PlayerInteraction es quien apunta
// con un raycast, muestra TextoPrompt y llama a Interactuar() con 'E', mismo patrón que ItemPickup,
// NPCMerchant y ExitTrigger. Necesita un Collider (en este objeto o en un hijo) para que ese
// raycast la alcance; el prefab Moneda ya lo trae.
//
// A diferencia de ItemPickup, la moneda NO es un ItemData y no pasa por el Inventory: el oro ya
// vive en PlayerStats (Oro/AgregarOro/GastarOro), así que meterla al inventario sería un segundo
// sistema de dinero y además le gastaría uno de los 10 lugares al jugador. Acá solo se suma oro.
//
// Tampoco usa OnTriggerEnter ni auto-recolección: todo lo que se recoge en este juego se recoge
// con 'E', y agregar un segundo mecanismo haría que dos cosas parecidas se comporten distinto.
public class MonedaPickup : MonoBehaviour, IInteractable
{
    [Tooltip("Oro que suma esta moneda al recogerla.")]
    [Min(1)] [SerializeField] private int valor = 5;

    [Tooltip("Opcional. Si se deja vacío, se busca un PlayerStats en la escena cuando hace falta.")]
    [SerializeField] private PlayerStats stats;

    [Tooltip("Opcional. Sonido que se reproduce en la posición de la moneda al recogerla.")]
    [SerializeField] private AudioClip sonidoRecoger;

    private bool recogida;
    private PlayerStats statsResuelto;
    private bool avisoSinStats;

    /// <summary>
    /// Oro que entrega, nunca menor que 1. [Min(1)] solo cubre el Inspector; un valor inválido que
    /// llegue por otro camino se recorta acá, igual que Inventory.Capacity y
    /// ActivadorSenalAmbiental.EventosRequeridos.
    /// </summary>
    public int Valor => Mathf.Max(1, valor);

    /// <summary>Verdadero desde que se recogió. Es permanente: no vuelve a dar oro.</summary>
    public bool Recogida => recogida;

    /// <summary>
    /// Texto del cartel de interacción, con el oro que entrega. Vacío si ya se recogió, para que el
    /// cartel desaparezca (mismo criterio que ItemPickup.TextoPrompt).
    /// </summary>
    public string TextoPrompt => recogida ? string.Empty : "Presiona E para recoger " + Valor + " de oro";

    /// <summary>
    /// Suma el oro al jugador y desactiva la moneda. Si ya se recogió no hace nada; si no hay ningún
    /// PlayerStats avisa y deja la moneda en el mapa, sin marcarla como recogida.
    /// </summary>
    public void Interactuar()
    {
        if (recogida) return;

        PlayerStats jugador = ResolverStats();
        if (jugador == null)
        {
            // Una sola vez: TextoPrompt e Interactuar pueden consultarse muchas veces seguidas.
            if (!avisoSinStats)
            {
                avisoSinStats = true;
                Debug.LogError($"MonedaPickup '{name}': no se encontró ningún PlayerStats en la escena y no hay uno asignado; la moneda no se puede recoger.", this);
            }
            return;
        }

        // Recién acá se marca como recogida: así un fallo de arriba no se come la moneda. El orden
        // importa porque AgregarOro ya disparó AlCambiarOro y el HUD puede estar leyendo el estado.
        jugador.AgregarOro(Valor);
        recogida = true;

        if (sonidoRecoger != null) AudioSource.PlayClipAtPoint(sonidoRecoger, transform.position);

        // Se desactiva en vez de destruirse, igual que ItemPickup: así otros sistemas pueden seguir
        // referenciándola (y el raycast de PlayerInteraction deja de pegarle, que es lo que hace
        // desaparecer el cartel en el frame siguiente).
        gameObject.SetActive(false);
    }

    // No se cachea el fallo: si todavía no hay jugador en la escena, el próximo intento vuelve a
    // buscar. El raycast apunta a una moneda a la vez, así que buscar de nuevo no cuesta nada.
    private PlayerStats ResolverStats()
    {
        if (stats != null) return stats;
        if (statsResuelto != null) return statsResuelto;

        statsResuelto = FindAnyObjectByType<PlayerStats>();
        return statsResuelto;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Mismo aviso que ItemPickup.OnValidate: sin Collider el raycast de PlayerInteraction no la
        // detecta y la moneda queda decorativa. No se usa [RequireComponent(typeof(Collider))]
        // porque Collider es abstracta y Unity no puede agregarla sola.
        if (GetComponentInChildren<Collider>(true) == null)
        {
            Debug.LogWarning($"MonedaPickup '{name}': no tiene Collider (ni en hijos); el raycast de PlayerInteraction no la va a detectar.", this);
        }
    }
#endif
}
