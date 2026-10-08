using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// Lo que el jugador lleva en la mano (HU-14). Hoy las dos armas que vende el comerciante: la daga
// (cuerpo a cuerpo) y la ballesta (a distancia).
//
// Comprarlas NO las empuna: el arma queda guardada en el Inventory y el jugador la saca con su tecla
// de la barra rapida (BarraRapida llama a Alternar), que es lo que tambien la vuelve a guardar.
// PlayerCombat le pregunta a este componente QUE hay en la mano para saber que hace el ataque: un
// golpe de daga, un tiro de ballesta o un golpe a mano limpia.
//
// SOLO UNA COSA EN LA MANO a la vez: sacar la ballesta guarda la daga sola, y al revés. Es lo que
// hace que PlayerCombat no tenga que decidir entre dos armas y que la barra rapida pueda marcar una
// sola casilla como equipada.
//
// No toca el inventario: solo escucha OnItemRemoved, igual que EfectosDeItem escucha OnItemUsed.
// Asi venderle un arma al comerciante la saca de la mano sola, sin que ShopManager tenga que
// avisarle nada.
//
// Las armas se reconocen por ItemData.itemId (las constantes IdArma e IdBallesta), no por una
// referencia de asset: este componente se instala solo en cada carga de escena, cuando no hay nadie
// que le pueda asignar la referencia desde el Inspector. Mismo contrato que usa EfectosDeItem con
// sus items.
//
// Los prefabs son opcionales: mientras no haya modelo se arma un placeholder por codigo (un cubo
// alargado tipo hoja). Al asignar el prefab definitivo no hay que cambiar nada mas.
[DisallowMultipleComponent]
public class EquipoJugador : MonoBehaviour
{
    /// <summary>itemId del ItemData de la daga. Es el contrato con el asset Assets/Items/Arma.asset.</summary>
    public const string IdArma = "arma";

    /// <summary>itemId del ItemData de la ballesta. Contrato con Assets/Items/Ballesta.asset.</summary>
    public const string IdBallesta = "ballesta";

    const string NombreAncla = "Mano";

    [Header("Referencias")]
    [Tooltip("Si se deja vacio se busca un Inventory en este objeto y, si no hay, en la escena.")]
    [SerializeField] private Inventory inventario;

    [Tooltip("Si se deja vacio se usa la camara del jugador (MouseLook) y se le crea un hijo 'Mano'.")]
    [SerializeField] private Transform ancla;

    [Header("Arma (daga, cuerpo a cuerpo)")]
    [Tooltip("Opcional. Modelo del arma. Si se deja vacio se crea un cubo placeholder.")]
    [SerializeField] private GameObject prefabArma;

    [Tooltip("Posicion del arma respecto de la camara: X a la derecha, Y abajo, Z hacia adelante.")]
    [SerializeField] private Vector3 posicionEnMano = new Vector3(0.3f, -0.25f, 0.5f);

    [SerializeField] private Vector3 rotacionEnMano = new Vector3(0f, -8f, 8f);

    [Header("Ballesta (a distancia)")]
    [Tooltip("Opcional. Modelo de la ballesta. Si se deja vacio se crea el mismo cubo placeholder " +
             "que el arma; el componente Ballesta (el que dispara) se agrega igual.")]
    [SerializeField] private GameObject prefabBallesta;

    [Tooltip("Mas al centro y mas adelante que la daga: una ballesta se sostiene con las dos manos " +
             "y apuntando, no colgando del costado.")]
    [SerializeField] private Vector3 posicionBallesta = new Vector3(0.18f, -0.22f, 0.45f);

    [SerializeField] private Vector3 rotacionBallesta = new Vector3(0f, -3f, 0f);

    [Header("Placeholder (solo si no hay prefab del arma)")]
    [Tooltip("Medidas del cubo placeholder: angosto y largo, para que se lea como una hoja.")]
    [SerializeField] private Vector3 escalaPlaceholder = new Vector3(0.07f, 0.1f, 0.85f);

    [SerializeField] private Color colorPlaceholder = new Color(0.75f, 0.78f, 0.82f);

    Inventory suscrito;
    Transform anclaResuelta;
    GameObject armaInstanciada;
    string idEquipado;
    ItemData itemEquipado;

    /// <summary>
    /// El ItemData de lo que hay en la mano. Puede ser null aun con un arma equipada, si se equipo
    /// por id (Equipar() sin parametros); para preguntar QUE hay en la mano estan las propiedades de
    /// abajo y EstaEquipado.
    /// </summary>
    public ItemData ItemEquipado => itemEquipado;

    /// <summary>Verdadero si hay CUALQUIER arma en la mano (daga o ballesta).</summary>
    public bool HayAlgoEnMano => armaInstanciada != null;

    /// <summary>
    /// Verdadero si el jugador tiene la DAGA en la mano. Es lo que lee PlayerCombat para saber si el
    /// golpe cuerpo a cuerpo hace el daño con arma: con la ballesta en la mano da false, porque el
    /// ataque pasa a ser un tiro y no un golpe.
    /// </summary>
    public bool ArmaEquipada => idEquipado == IdArma;

    /// <summary>Verdadero si el jugador tiene la BALLESTA en la mano.</summary>
    public bool BallestaEquipada => idEquipado == IdBallesta;

    /// <summary>
    /// El componente que dispara, si lo que hay en la mano es la ballesta. null en cualquier otro
    /// caso. Es por aca que PlayerCombat rutea el ataque al tiro.
    /// </summary>
    public Ballesta Ballesta => BallestaEquipada && armaInstanciada != null
        ? armaInstanciada.GetComponent<Ballesta>()
        : null;

    /// <summary>Objeto del arma en la mano, o null si no hay ninguna. Lo usan las pruebas.</summary>
    public GameObject ArmaInstanciada => armaInstanciada;

    /// <summary>Se dispara al equipar (true) y al desequipar (false) un arma.</summary>
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
    /// Pone la DAGA en la mano, tenga o no su ItemData a mano. Se mantiene sin parametros por
    /// compatibilidad: es la firma que puede estar enganchada en un UnityEvent de la escena. Para
    /// elegir que arma, usar la sobrecarga.
    /// </summary>
    public void Equipar() => EquiparPorId(IdArma, BuscarEnInventario(IdArma));

    /// <summary>
    /// Pone esa arma en la mano, guardando primero lo que hubiera. Si ya era la que estaba en la
    /// mano no hace nada. Es publica para poder equipar desde un UnityEvent o desde las pruebas sin
    /// pasar por el inventario.
    /// </summary>
    public void Equipar(ItemData item)
    {
        if (!PuedeEquipar(item)) return;
        EquiparPorId(item.itemId, item);
    }

    // El estado se lleva por itemId y no por referencia al ItemData: asi Equipar() sin parametros
    // sigue pudiendo sacar la daga aunque nadie tenga el asset a mano, que es como funcionaba antes
    // de que existiera la segunda arma. Es el mismo contrato por itemId que usa todo el resto de
    // esta clase (ver EsEquipable).
    void EquiparPorId(string id, ItemData item)
    {
        if (!EsEquipable(id)) return;
        if (idEquipado == id) return;

        Transform mano = ResolverAncla();
        if (mano == null)
        {
            Debug.LogWarning("EquipoJugador: no se encontro la camara del jugador; el arma no se puede mostrar.", this);
            return;
        }

        // Una sola cosa en la mano: lo que haya se guarda sin avisar por el evento, porque el evento
        // de este cambio lo dispara el equipado de abajo y dos avisos seguidos solo harian que la UI
        // se redibuje dos veces.
        Guardar(avisar: false);

        bool esBallesta = id == IdBallesta;
        GameObject prefab = esBallesta ? prefabBallesta : prefabArma;

        armaInstanciada = prefab != null ? Instantiate(prefab) : CrearPlaceholder();
        armaInstanciada.name = esBallesta ? "Ballesta" : "Arma";
        armaInstanciada.transform.SetParent(mano, false);
        armaInstanciada.transform.localPosition = esBallesta ? posicionBallesta : posicionEnMano;
        armaInstanciada.transform.localRotation = Quaternion.Euler(esBallesta ? rotacionBallesta : rotacionEnMano);

        // El componente que dispara se autoagrega si el prefab no lo trae, por el mismo motivo que
        // EnemyHealth se autoagrega EnemyHitFeedback: asi la ballesta funciona igual con su modelo
        // definitivo, con un prefab al que alguien se olvido de ponerle el componente, o con el
        // placeholder que se arma por codigo y no tiene prefab ninguno.
        if (esBallesta && armaInstanciada.GetComponent<Ballesta>() == null)
        {
            armaInstanciada.AddComponent<Ballesta>();
        }

        idEquipado = id;
        itemEquipado = item;
        AlCambiarArma?.Invoke(true);
    }

    /// <summary>Saca de la mano lo que haya y lo destruye. Si no hay nada, no hace nada.</summary>
    public void Desequipar() => Guardar(avisar: true);

    void Guardar(bool avisar)
    {
        if (armaInstanciada == null)
        {
            idEquipado = null;
            itemEquipado = null;
            return;
        }

        Destruir(armaInstanciada);
        armaInstanciada = null;
        idEquipado = null;
        itemEquipado = null;

        if (avisar) AlCambiarArma?.Invoke(false);
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
    public bool PuedeEquipar(ItemData item) => item != null && EsEquipable(item.itemId);

    /// <summary>
    /// Verdadero si ESE item es el que esta ahora mismo en la mano. Lo consulta BarraRapida para
    /// marcar una sola casilla como equipada: preguntar "hay algo en la mano" no alcanza desde que
    /// hay mas de un arma, porque encenderia la casilla de la daga con la ballesta empunada.
    /// </summary>
    public bool EstaEquipado(ItemData item) => item != null && idEquipado == item.itemId;

    /// <summary>
    /// Saca esa arma a la mano, o la guarda si ya estaba en la mano. Con la OTRA arma empunada, la
    /// cambia (una sola cosa en la mano). Equipar exige tenerla en el inventario: comprarla solo la
    /// guarda, y recien la tecla de la barra rapida la empuna.
    /// Devuelve false si el item no es equipable o si el jugador no lo tiene.
    /// </summary>
    public bool Alternar(ItemData item)
    {
        if (!PuedeEquipar(item)) return false;

        if (EstaEquipado(item))
        {
            Desequipar();
            return true;
        }

        // Se chequea el inventario ANTES de guardar lo que haya en la mano: si el jugador no tiene
        // esta arma, la tecla no tiene que hacer nada, y menos que menos dejarlo con la mano vacia.
        if (BuscarEnInventario(item.itemId) == null) return false;

        EquiparPorId(item.itemId, item);
        return true;
    }

    void AlQuitarItem(ItemData item)
    {
        if (!PuedeEquipar(item)) return;

        // Se vuelve a consultar el inventario en vez de desequipar de una: el jugador podria tener
        // dos copias de esa arma y haber vendido solo una.
        if (EstaEquipado(item) && BuscarEnInventario(item.itemId) == null) Desequipar();
    }

    static bool EsEquipable(string id) => id == IdArma || id == IdBallesta;

    ItemData BuscarEnInventario(string id)
    {
        Inventory inv = suscrito ?? ResolverInventario();
        if (inv == null) return null;

        foreach (ItemData item in inv.Items)
        {
            if (item != null && item.itemId == id) return item;
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
