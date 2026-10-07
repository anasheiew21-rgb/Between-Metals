using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// Lo que el jugador lleva en la mano (HU-14). Hoy solo el arma que vende el comerciante.
//
// Comprarla NO la empuna: el arma queda guardada en el Inventory y el jugador la saca con su tecla
// de la barra rapida (BarraRapida llama a Alternar), que es lo que tambien la vuelve a guardar.
// PlayerCombat le pregunta a este componente si hay arma en la mano para saber cuanto dano hace
// cada golpe.
//
// No toca el inventario: solo escucha OnItemRemoved, igual que EfectosDeItem escucha OnItemUsed.
// Asi venderle el arma al comerciante la saca de la mano sola, sin que ShopManager tenga que
// avisarle nada.
//
// El arma se reconoce por ItemData.itemId (la constante IdArma), no por una referencia de asset:
// este componente se instala solo en cada carga de escena, cuando no hay nadie que le pueda
// asignar la referencia desde el Inspector. Mismo contrato que usa EfectosDeItem con sus items.
//
// prefabArma es opcional: mientras no haya modelo, se arma un placeholder por codigo (un cubo
// alargado tipo hoja). Al asignar el prefab definitivo no hay que cambiar nada mas.
[DisallowMultipleComponent]
public class EquipoJugador : MonoBehaviour
{
    /// <summary>itemId del ItemData del arma. Es el contrato con el asset Assets/Items/Arma.asset.</summary>
    public const string IdArma = "arma";

    const string NombreAncla = "Mano";

    [Header("Referencias")]
    [Tooltip("Si se deja vacio se busca un Inventory en este objeto y, si no hay, en la escena.")]
    [SerializeField] private Inventory inventario;

    [Tooltip("Si se deja vacio se usa la camara del jugador (MouseLook) y se le crea un hijo 'Mano'.")]
    [SerializeField] private Transform ancla;

    [Header("Arma")]
    [Tooltip("Opcional. Modelo del arma. Si se deja vacio se crea un cubo placeholder.")]
    [SerializeField] private GameObject prefabArma;

    [Tooltip("Posicion del arma respecto de la camara: X a la derecha, Y abajo, Z hacia adelante.")]
    [SerializeField] private Vector3 posicionEnMano = new Vector3(0.3f, -0.25f, 0.5f);

    [SerializeField] private Vector3 rotacionEnMano = new Vector3(0f, -8f, 8f);

    [Header("Placeholder (solo si no hay prefabArma)")]
    [Tooltip("Medidas del cubo placeholder: angosto y largo, para que se lea como una hoja.")]
    [SerializeField] private Vector3 escalaPlaceholder = new Vector3(0.07f, 0.1f, 0.85f);

    [SerializeField] private Color colorPlaceholder = new Color(0.75f, 0.78f, 0.82f);

    Inventory suscrito;
    Transform anclaResuelta;
    GameObject armaInstanciada;

    /// <summary>Verdadero si el jugador tiene el arma equipada en la mano.</summary>
    public bool ArmaEquipada => armaInstanciada != null;

    /// <summary>Objeto del arma en la mano, o null si no hay ninguna. Lo usan las pruebas.</summary>
    public GameObject ArmaInstanciada => armaInstanciada;

    /// <summary>Se dispara al equipar (true) y al desequipar (false) el arma.</summary>
    public event Action<bool> AlCambiarArma;

    // Se instala sola sobre el objeto que tiene el Inventory y se recrea en cada carga de escena
    // (mismo patron y mismo motivo que PlayerUI/EfectosDeItem: RuntimeInitializeOnLoadMethod corre
    // una sola vez y "Reiniciar" recarga la escena, ver #55).
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
        if (FindAnyObjectByType<EquipoJugador>() != null) return;

        Inventory inventario = FindAnyObjectByType<Inventory>();
        if (inventario == null) return; // escena sin jugador (por ejemplo el menu principal)

        inventario.gameObject.AddComponent<EquipoJugador>();
    }

    void Start()
    {
        Suscribir();
    }

    void OnDestroy()
    {
        Desuscribir();
    }

    /// <summary>
    /// Pone el arma en la mano. Si ya hay una, no hace nada. Es publica para poder equipar desde
    /// un UnityEvent o desde las pruebas sin pasar por el inventario.
    /// </summary>
    public void Equipar()
    {
        if (armaInstanciada != null) return;

        Transform mano = ResolverAncla();
        if (mano == null)
        {
            Debug.LogWarning("EquipoJugador: no se encontro la camara del jugador; el arma no se puede mostrar.", this);
            return;
        }

        armaInstanciada = prefabArma != null ? Instantiate(prefabArma) : CrearPlaceholder();
        armaInstanciada.name = "Arma";
        armaInstanciada.transform.SetParent(mano, false);
        armaInstanciada.transform.localPosition = posicionEnMano;
        armaInstanciada.transform.localRotation = Quaternion.Euler(rotacionEnMano);

        AlCambiarArma?.Invoke(true);
    }

    /// <summary>Saca el arma de la mano y la destruye. Si no hay ninguna, no hace nada.</summary>
    public void Desequipar()
    {
        if (armaInstanciada == null) return;

        Destruir(armaInstanciada);
        armaInstanciada = null;

        AlCambiarArma?.Invoke(false);
    }

    // Destroy() tira excepcion en modo edicion, y los self-tests de Editor corren justamente asi
    // (sin Play Mode): fuera de Play va DestroyImmediate.
    // (UnityEngine.Object explicito: con 'using System' de por medio, 'Object' seria ambiguo.)
    static void Destruir(UnityEngine.Object objeto)
    {
        if (objeto == null) return;

        if (Application.isPlaying) Destroy(objeto);
        else DestroyImmediate(objeto);
    }

    /// <summary>
    /// Verdadero si este componente sabe poner ese item en la mano. Lo consulta BarraRapida para
    /// decidir si una tecla equipa o si usa el item: asi la regla de "que cosas se empunan" vive
    /// aca y no repartida en la UI.
    /// </summary>
    public bool PuedeEquipar(ItemData item) => EsArma(item);

    /// <summary>
    /// Saca el arma a la mano, o la guarda si ya estaba en la mano. Equipar exige tenerla en el
    /// inventario: comprarla solo la guarda, y recien la tecla de la barra rapida la empuna.
    /// Devuelve false si el item no es equipable o si el jugador no lo tiene.
    /// </summary>
    public bool Alternar(ItemData item)
    {
        if (!PuedeEquipar(item)) return false;

        if (ArmaEquipada)
        {
            Desequipar();
            return true;
        }

        if (BuscarArmaEnInventario() == null) return false;

        Equipar();
        return true;
    }

    void AlQuitarItem(ItemData item)
    {
        // Se vuelve a consultar el inventario en vez de desequipar de una: el jugador podria tener
        // dos armas y haber vendido solo una.
        if (EsArma(item) && BuscarArmaEnInventario() == null) Desequipar();
    }

    static bool EsArma(ItemData item)
    {
        return item != null && item.itemId == IdArma;
    }

    ItemData BuscarArmaEnInventario()
    {
        if (suscrito == null) return null;

        foreach (ItemData item in suscrito.Items)
        {
            if (EsArma(item)) return item;
        }
        return null;
    }

    // El arma cuelga de un hijo "Mano" de la camara y no de la camara directa: asi se puede mover
    // la mano entera (futuro bobbing, retroceso del golpe) sin tocar la posicion del arma.
    Transform ResolverAncla()
    {
        if (ancla != null) return ancla;
        if (anclaResuelta != null) return anclaResuelta;

        Camera camara = CamaraDelJugador();
        if (camara == null) return null;

        Transform mano = camara.transform.Find(NombreAncla);
        if (mano == null)
        {
            mano = new GameObject(NombreAncla).transform;
            mano.SetParent(camara.transform, false);
        }

        anclaResuelta = mano;
        return anclaResuelta;
    }

    // La camara del jugador es la que lleva MouseLook (la jerarquia que documenta CLAUDE.md), no
    // Camera.main: en una escena con varias camaras main podria ser cualquiera.
    static Camera CamaraDelJugador()
    {
        MouseLook mouseLook = FindAnyObjectByType<MouseLook>();
        if (mouseLook != null)
        {
            Camera camara = mouseLook.GetComponent<Camera>();
            if (camara != null) return camara;
        }

        return Camera.main;
    }

    GameObject CrearPlaceholder()
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.localScale = escalaPlaceholder;

        // Sin Collider: el arma viaja pegada a la camara y un collider ahi chocaria con todo
        // (y el dano lo aplica PlayerCombat con su propio SphereCast, no por contacto).
        if (go.TryGetComponent(out Collider collider)) Destruir(collider);

        if (go.TryGetComponent(out Renderer renderer))
        {
            renderer.sharedMaterial = MaterialPlaceholder();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        return go;
    }

    static Material materialPlaceholder;

    Material MaterialPlaceholder()
    {
        if (materialPlaceholder != null) return materialPlaceholder;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        materialPlaceholder = new Material(shader) { name = "ArmaPlaceholder (runtime)" };

        if (materialPlaceholder.HasProperty("_BaseColor")) materialPlaceholder.SetColor("_BaseColor", colorPlaceholder);
        if (materialPlaceholder.HasProperty("_Color")) materialPlaceholder.SetColor("_Color", colorPlaceholder);

        return materialPlaceholder;
    }

    void Suscribir()
    {
        Inventory objetivo = ResolverInventario();
        if (objetivo == suscrito) return;

        Desuscribir();
        if (objetivo == null)
        {
            Debug.LogWarning("EquipoJugador: no se encontro ningun Inventory; el arma no se va a equipar sola.", this);
            return;
        }

        // Solo OnItemRemoved: agregar el arma al inventario NO la equipa (comprarla la guarda), pero
        // perderla -venderla al comerciante, tirarla- si tiene que sacarla de la mano.
        suscrito = objetivo;
        suscrito.OnItemRemoved += AlQuitarItem;
    }

    void Desuscribir()
    {
        if (suscrito == null) return;

        suscrito.OnItemRemoved -= AlQuitarItem;
        suscrito = null;
    }

    Inventory ResolverInventario()
    {
        if (inventario != null) return inventario;

        Inventory propio = GetComponent<Inventory>();
        return propio != null ? propio : FindAnyObjectByType<Inventory>();
    }
}
