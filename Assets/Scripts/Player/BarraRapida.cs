using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// Barra de acceso rapido: cuatro casillas fijas, una por tecla (1, 2, 3 y 4), para equipar o usar
// cosas sin abrir el inventario completo con TAB.
//
// Esta clase es solo la LOGICA (que hay en cada casilla, que tecla la dispara, que pasa al
// apretarla). El dibujo lo hace PlayerUI, que se engancha a los eventos de aca: mismo reparto que
// PlayerStats/PlayerUI, y por el mismo motivo -que se pueda probar la mecanica sin una pantalla.
//
// Las casillas son FIJAS y se asignan en el Inspector (o las deja puestas ProgresionBuilder), no se
// llenan solas con lo que entra al inventario: "la tecla 1 es la espada" solo sirve como reflejo si
// la tecla 1 es SIEMPRE la espada. Una casilla cuyo item el jugador no tiene se muestra en gris y
// su tecla no hace nada.
//
// Que hace cada tecla lo decide el item, no esta clase: si EquipoJugador dice que sabe empunarlo
// (PuedeEquipar), la tecla lo saca a la mano o lo guarda; si no, se usa por el inventario
// (Inventory.UseItem), que es lo que dispara los efectos de EfectosDeItem -curarse con la pocion,
// etc.- y lo consume si corresponde.
[DisallowMultipleComponent]
public class BarraRapida : MonoBehaviour
{
    /// <summary>Cantidad de casillas. Fija: es la cantidad de teclas que tiene la barra.</summary>
    public const int Casillas = 4;

    [Header("Casillas")]
    [Tooltip("Un ItemData por casilla, en orden de tecla. Vacio = casilla sin asignar.")]
    [SerializeField] private ItemData[] items = new ItemData[Casillas];

    [Tooltip("Tecla de cada casilla, en el mismo orden.")]
    [SerializeField] private KeyCode[] teclas = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4 };

    [Tooltip("Teclas alternativas (el teclado numerico), en el mismo orden. Se pueden dejar vacias.")]
    [SerializeField] private KeyCode[] teclasAlternativas = { KeyCode.Keypad1, KeyCode.Keypad2, KeyCode.Keypad3, KeyCode.Keypad4 };

    [Header("Referencias")]
    [Tooltip("Si se deja vacio se busca un Inventory en este objeto y, si no hay, en la escena.")]
    [SerializeField] private Inventory inventario;

    [Tooltip("Si se deja vacio se busca un EquipoJugador en este objeto o en la escena.")]
    [SerializeField] private EquipoJugador equipo;

    Inventory inventarioResuelto;
    EquipoJugador equipoResuelto;

    /// <summary>Se dispara cuando una tecla hizo algo: equipar, guardar o usar. Recibe la casilla (0..3).</summary>
    public event Action<int> AlUsarCasilla;

    /// <summary>Se dispara cuando una tecla no pudo hacer nada (casilla vacia, o item que no se tiene).</summary>
    public event Action<int> AlFallarCasilla;

    // Se instala sola sobre el objeto que tiene el Inventory y se recrea en cada carga de escena
    // (mismo patron y mismo motivo que PlayerUI/EfectosDeItem/EquipoJugador, ver #55).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCrear()
    {
        EnsureExists();
        SceneManager.sceneLoaded -= AlCargarEscena;
        SceneManager.sceneLoaded += AlCargarEscena;
    }

    static void AlCargarEscena(Scene escena, LoadSceneMode modo) => EnsureExists();

    public static void EnsureExists()
    {
        if (FindAnyObjectByType<BarraRapida>() != null) return;

        Inventory inventario = FindAnyObjectByType<Inventory>();
        if (inventario == null) return; // escena sin jugador (por ejemplo el menu principal)

        inventario.gameObject.AddComponent<BarraRapida>();
    }

    void Update()
    {
        // Mismos guards que PlayerInteraction y PlayerCombat: con el menu, la tienda o el
        // inventario abiertos, las teclas de la barra no tienen que disparar nada.
        if (Menu.IsOpen || ShopManager.HayTiendaAbierta || InventoryUI.IsOpen) return;

        for (int i = 0; i < Casillas; i++)
        {
            if (TeclaApretada(i)) UsarCasilla(i);
        }
    }

    /// <summary>Item de una casilla, o null si esta vacia o el indice es invalido.</summary>
    public ItemData ItemDe(int casilla)
    {
        return casilla >= 0 && items != null && casilla < items.Length ? items[casilla] : null;
    }

    /// <summary>Tecla de una casilla, o KeyCode.None si el indice es invalido.</summary>
    public KeyCode TeclaDe(int casilla)
    {
        return casilla >= 0 && teclas != null && casilla < teclas.Length ? teclas[casilla] : KeyCode.None;
    }

    /// <summary>
    /// Verdadero si el jugador tiene en el inventario el item de esa casilla. La UI lo usa para
    /// dibujar la casilla en gris cuando no se puede usar.
    /// </summary>
    public bool Disponible(int casilla)
    {
        ItemData item = ItemDe(casilla);
        if (item == null) return false;

        Inventory inv = ResolverInventario();
        return inv != null && inv.HasItem(item);
    }

    /// <summary>Verdadero si el item de esa casilla esta ahora mismo en la mano del jugador.</summary>
    public bool Equipado(int casilla)
    {
        ItemData item = ItemDe(casilla);
        if (item == null) return false;

        EquipoJugador eq = ResolverEquipo();
        return eq != null && eq.PuedeEquipar(item) && eq.ArmaEquipada;
    }

    /// <summary>Cuantas unidades de ese item tiene el jugador. 0 si no tiene ninguna.</summary>
    public int Cantidad(int casilla)
    {
        ItemData item = ItemDe(casilla);
        if (item == null) return 0;

        Inventory inv = ResolverInventario();
        return inv != null ? inv.CountOf(item) : 0;
    }

    /// <summary>
    /// Dispara la casilla, como si se hubiera apretado su tecla. Si el item se puede empunar, lo
    /// saca a la mano o lo guarda; si no, lo usa por el inventario. Devuelve false, sin hacer nada,
    /// si la casilla esta vacia o el jugador no tiene ese item.
    /// </summary>
    public bool UsarCasilla(int casilla)
    {
        ItemData item = ItemDe(casilla);
        if (item == null)
        {
            AlFallarCasilla?.Invoke(casilla);
            return false;
        }

        Inventory inv = ResolverInventario();
        if (inv == null || !inv.HasItem(item))
        {
            AlFallarCasilla?.Invoke(casilla);
            return false;
        }

        EquipoJugador eq = ResolverEquipo();
        bool hizoAlgo = eq != null && eq.PuedeEquipar(item)
            ? eq.Alternar(item)
            : inv.UseItem(item);

        if (hizoAlgo) AlUsarCasilla?.Invoke(casilla);
        else AlFallarCasilla?.Invoke(casilla);

        return hizoAlgo;
    }

    bool TeclaApretada(int casilla)
    {
        if (teclas != null && casilla < teclas.Length && teclas[casilla] != KeyCode.None
            && Input.GetKeyDown(teclas[casilla]))
        {
            return true;
        }

        return teclasAlternativas != null && casilla < teclasAlternativas.Length
            && teclasAlternativas[casilla] != KeyCode.None
            && Input.GetKeyDown(teclasAlternativas[casilla]);
    }

    // Se cachean aparte de los campos serializados, para no escribir nunca un campo serializado
    // desde codigo (en modo edicion ensuciaria la escena, mismo criterio que EfectosDeItem).
    Inventory ResolverInventario()
    {
        if (inventario != null) return inventario;
        if (inventarioResuelto != null) return inventarioResuelto;

        inventarioResuelto = GetComponent<Inventory>() ?? FindAnyObjectByType<Inventory>();
        return inventarioResuelto;
    }

    EquipoJugador ResolverEquipo()
    {
        if (equipo != null) return equipo;
        if (equipoResuelto != null) return equipoResuelto;

        equipoResuelto = GetComponent<EquipoJugador>() ?? FindAnyObjectByType<EquipoJugador>();
        return equipoResuelto;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // El largo de los arrays es parte del contrato con la UI: cuatro casillas, cuatro teclas.
        if (items == null || items.Length != Casillas) System.Array.Resize(ref items, Casillas);
        if (teclas == null || teclas.Length != Casillas) System.Array.Resize(ref teclas, Casillas);
        if (teclasAlternativas == null || teclasAlternativas.Length != Casillas) System.Array.Resize(ref teclasAlternativas, Casillas);
    }
#endif
}
