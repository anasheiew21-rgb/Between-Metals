using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// Efecto de usar un ítem del inventario (RF06/HU-05). Inventory ya disparaba OnItemUsed y hasta
// ahora nadie lo escuchaba: usar una poción la gastaba sin curar nada. Esta clase es ese oyente, y
// el único lugar del proyecto donde vive el mapeo itemId -> efecto.
//
// A propósito NO toca el inventario: Inventory.UseAt() dispara OnItemUsed y recién después se
// ocupa del consumo (consumeOnUse -> RemoveAt del índice exacto, re-localizando el ítem si un
// listener movió la lista). Si desde acá se agregara o quitara algo, se le cambiaría la lista bajo
// los pies. Consumir "exactamente una unidad" ya es cosa suya; acá solo se aplica el efecto.
//
// Las cantidades son campos serializados de este componente y no de ItemData: así se ajustan sin
// tocar los assets ni una clase que usa todo el equipo. Son valores de prototipo pensados sobre
// los 100 de vida máxima que trae PlayerStats en la escena.
//
// Se instala sola sobre el objeto que tiene el Inventory y se recrea en cada carga de escena
// (mismo patrón y mismo motivo que PlayerUI/InventoryUI/VictoryUI: RuntimeInitializeOnLoadMethod
// corre una sola vez y "Reiniciar" recarga la escena, ver #55).
[DisallowMultipleComponent]
public class EfectosDeItem : MonoBehaviour
{
    // Ids de los ItemData de Assets/Items: son el contrato con esos assets.
    public const string IdPocionDeVida = "pocion_vida";
    public const string IdRacionDeComida = "racion_comida";

    [Header("Curación")]
    [Tooltip("Vida que restaura la Poción de vida. Prototipo: 40 sobre los 100 de maxHealth.")]
    [Min(0f)] [SerializeField] private float curacionPocion = 40f;

    [Tooltip("Vida que restaura la Ración de comida. No hay sistema de hambre: la ración cura menos que la poción.")]
    [Min(0f)] [SerializeField] private float curacionRacion = 15f;

    [Header("Referencias")]
    [Tooltip("Si se deja vacío se busca un Inventory en este objeto y, si no hay, en la escena")]
    [SerializeField] private Inventory inventario;

    [Tooltip("Si se deja vacío se busca un PlayerStats en este objeto, sus hijos, su padre o la escena")]
    [SerializeField] private PlayerStats stats;

    // Inventario al que está enganchado ahora mismo: hace falta recordarlo para desuscribirse del
    // correcto si cambia. El PlayerStats se cachea en un campo aparte y no en el serializado, para
    // no escribir nunca un campo serializado desde código (en modo edición ensuciaría la escena).
    Inventory suscrito;
    PlayerStats statsResuelto;
    bool avisoSinStats;

    /// <summary>Vida que restaura la poción. Expuesto para que las pruebas lean la configuración.</summary>
    public float CuracionPocion => curacionPocion;

    /// <summary>Vida que restaura la ración.</summary>
    public float CuracionRacion => curacionRacion;

    /// <summary>Inventario al que está suscrito, o null si no encontró ninguno.</summary>
    public Inventory InventarioSuscrito => suscrito;

    // Se recrea en cada carga de escena: Reiniciar recarga la escena y RuntimeInitializeOnLoadMethod
    // corre una sola vez (mismo motivo que GameOverUI/Menu/PromptInteraccion, ver #55).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInstalar()
    {
        EnsureExists();
        SceneManager.sceneLoaded -= AlCargarEscena;
        SceneManager.sceneLoaded += AlCargarEscena;
    }

    static void AlCargarEscena(Scene escena, LoadSceneMode modo) => EnsureExists();

    /// <summary>
    /// Agrega el componente al objeto que tiene el Inventory, si la escena tiene uno y todavía no
    /// hay ningún EfectosDeItem. No hace nada en una escena sin inventario (por ejemplo un menú).
    /// </summary>
    public static void EnsureExists()
    {
        if (FindAnyObjectByType<EfectosDeItem>() != null) return;

        Inventory encontrado = FindAnyObjectByType<Inventory>();
        if (encontrado == null) return; // sin inventario no hay OnItemUsed que escuchar

        encontrado.gameObject.AddComponent<EfectosDeItem>();
    }

    void OnEnable()
    {
        Reconectar();
    }

    void OnDisable()
    {
        Desconectar();
    }

    /// <summary>
    /// Resuelve el inventario y queda suscrito a su OnItemUsed exactamente una vez. Lo llama
    /// OnEnable; es público porque fuera de Play Mode Unity no dispara OnEnable al agregar el
    /// componente y los self-tests necesitan dejarlo conectado sin dar Play.
    /// </summary>
    public void Reconectar()
    {
        Inventory encontrado = ResolverInventario();

        // Si cambió de inventario (recarga de escena, u otro Inventory en la escena), primero se
        // suelta el anterior.
        if (suscrito != null && suscrito != encontrado) Desconectar();
        if (encontrado == null) return;

        // -= antes de += : el mismo idiom que GameManager.AutoWire y ExitTrigger.AutoInstalar, para
        // que volver a llamar a Reconectar() no deje el handler registrado dos veces (y la poción
        // no cure el doble).
        encontrado.OnItemUsed -= AplicarEfecto;
        encontrado.OnItemUsed += AplicarEfecto;
        suscrito = encontrado;
    }

    /// <summary>Se suelta del inventario al que estaba enganchado. Lo llama OnDisable.</summary>
    public void Desconectar()
    {
        if (suscrito == null) return;

        suscrito.OnItemUsed -= AplicarEfecto;
        suscrito = null;
    }

    /// <summary>
    /// Vida que restaura un itemId, o 0 si ese ítem no tiene efecto de curación. Es el mapeo
    /// completo: agregar un ítem curativo nuevo es agregar una línea acá.
    /// </summary>
    public float CuracionDe(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return 0f;

        string id = itemId.Trim();

        // Sin distinguir mayúsculas: ItemData.OnValidate solo avisa cuando itemId está vacío, así
        // que un itemId con otra capitalización pasaría y el efecto se perdería en silencio.
        if (string.Equals(id, IdPocionDeVida, StringComparison.OrdinalIgnoreCase)) return curacionPocion;
        if (string.Equals(id, IdRacionDeComida, StringComparison.OrdinalIgnoreCase)) return curacionRacion;

        return 0f;
    }

    // Un itemId desconocido no es un error (las llaves, el arma e Item_Prueba no tienen efecto de
    // uso): se ignora en silencio, sin avisos que ensucien la consola cada vez que se usa
    // cualquier otra cosa.
    void AplicarEfecto(ItemData item)
    {
        if (item == null) return;

        float curacion = CuracionDe(item.itemId);
        if (curacion <= 0f) return;

        PlayerStats jugador = ResolverStats();
        if (jugador == null)
        {
            // Una sola vez: OnItemUsed puede dispararse muchas veces por partida.
            if (!avisoSinStats)
            {
                avisoSinStats = true;
                Debug.LogWarning($"EfectosDeItem: no se encontró ningún PlayerStats; '{item.itemId}' no pudo aplicar su efecto.", this);
            }
            return;
        }

        // Curar() ya recorta en maxHealth, ignora cantidades <= 0 y no hace nada si el jugador está
        // muerto: acá no hace falta repetir ninguna de esas validaciones.
        jugador.Curar(curacion);
    }

    Inventory ResolverInventario()
    {
        if (inventario != null) return inventario;

        Inventory propio = GetComponent<Inventory>();
        return propio != null ? propio : FindAnyObjectByType<Inventory>();
    }

    // En la escena el Inventory está en el objeto "Player" y el PlayerStats en su hijo
    // "PlayerController", de ahí que se mire primero hacia los hijos.
    PlayerStats ResolverStats()
    {
        if (stats != null) return stats;
        if (statsResuelto != null) return statsResuelto;

        statsResuelto = GetComponentInChildren<PlayerStats>(true);
        if (statsResuelto == null) statsResuelto = GetComponentInParent<PlayerStats>();
        if (statsResuelto == null) statsResuelto = FindAnyObjectByType<PlayerStats>();

        return statsResuelto;
    }
}
