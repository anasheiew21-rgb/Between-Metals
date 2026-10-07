using System;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

// Puerta de bisagra que se abre con 'E' (RF07/RF08). No detecta nada por si sola: PlayerInteraction
// es quien apunta con un raycast, muestra TextoPrompt y llama a Interactuar(), mismo patron que
// ItemPickup, NPCMerchant y MonedaPickup. El Collider vive en la hoja (el hijo "Hoja"), asi que el
// raycast le pega ahi y encuentra este componente con GetComponentInParent.
//
// Jerarquia que espera (la arma ProgresionBuilder):
//   Puerta_XX            <- este componente, parado en el BORDE del hueco (la bisagra)
//   └── Hoja             <- cubo de AnchoCalle x AltoMuro x grosor, corrido medio ancho en +X local
// Girar este objeto en Y hace que la hoja barra el pasillo, porque su pivote queda en la bisagra.
//
// NavMesh: el NavMesh se hornea una sola vez al cargar (NavMeshRuntimeBuilder), asi que la hoja se
// excluye del horneado (NavMeshModifier.ignoreFromBuild) y el corte real lo hace un NavMeshObstacle
// con carving, encendido solo con la puerta cerrada. Mismo mecanismo que BarreraDinamica: con la
// puerta cerrada los enemigos no pueden atravesarla, y al abrirse recuperan el paso.
public class PuertaInteractuable : MonoBehaviour, IInteractable
{
    [Header("Llave")]
    [Tooltip("Item que hace falta tener en el inventario para abrir. Vacio = la puerta se abre sin llave.")]
    [SerializeField] private ItemData llaveRequerida;

    [Tooltip("Si es verdadero, la llave se gasta al abrir la puerta y desaparece del inventario.")]
    [SerializeField] private bool consumirLlave = true;

    [Tooltip("Opcional. Si se deja vacio, se busca un Inventory en la escena cuando hace falta.")]
    [SerializeField] private Inventory inventarioJugador;

    [Header("Apertura")]
    [Tooltip("Grados que gira la hoja al abrirse. Negativo = gira para el otro lado.")]
    [Range(-170f, 170f)] [SerializeField] private float anguloAbierta = 90f;

    [Tooltip("Segundos que tarda el giro completo.")]
    [Min(0.1f)] [SerializeField] private float duracionApertura = 1.2f;

    [Header("Audio")]
    [Tooltip("Opcional. Suena al abrirse.")]
    [SerializeField] private AudioClip sonidoAbrir;

    [Tooltip("Opcional. Suena cuando el jugador intenta abrir sin tener la llave.")]
    [SerializeField] private AudioClip sonidoCerrado;

    [Header("Eventos")]
    [Tooltip("Se invoca una sola vez, en el momento en que la puerta empieza a abrirse.")]
    [SerializeField] private UnityEvent alAbrirse = new UnityEvent();

    // Texto del cartel cuando falta la llave. El nombre se lee del ItemData, para no repetirlo en
    // el Inspector y que quede siempre sincronizado con el asset.
    const string TextoSinLlave = "Necesitas ";

    Transform hoja;
    Collider[] collidersHoja = Array.Empty<Collider>();
    NavMeshObstacle obstaculo;
    Quaternion rotacionCerrada;
    Quaternion rotacionAbierta;
    float avance;            // 0 = cerrada, 1 = abierta
    bool abriendo;
    Inventory inventarioResuelto;
    bool avisoSinInventario;

    /// <summary>Verdadero desde que termino de abrirse. Es permanente: la puerta no vuelve a cerrarse.</summary>
    public bool Abierta => avance >= 1f;

    /// <summary>Verdadero mientras la hoja esta girando.</summary>
    public bool Abriendo => abriendo;

    /// <summary>Item que pide esta puerta, o null si no pide ninguno.</summary>
    public ItemData LlaveRequerida => llaveRequerida;

    /// <summary>Se dispara una vez, al empezar la apertura. Para objetivos, audio ambiente, etc.</summary>
    public event Action AlAbrirse;

    /// <summary>
    /// Texto del cartel de interaccion. Vacio con la puerta ya abierta (asi el cartel desaparece,
    /// mismo criterio que ItemPickup/MonedaPickup) y distinto segun el jugador tenga o no la llave.
    /// </summary>
    public string TextoPrompt
    {
        get
        {
            if (Abierta || abriendo) return string.Empty;
            if (llaveRequerida == null) return "Presiona E para abrir";

            return TieneLlave()
                ? "Presiona E para abrir con " + llaveRequerida.itemName
                : TextoSinLlave + llaveRequerida.itemName;
        }
    }

    void Awake()
    {
        hoja = transform.Find("Hoja");
        if (hoja == null)
        {
            Debug.LogError($"PuertaInteractuable '{name}': falta el hijo 'Hoja' (el cubo de la puerta).", this);
            enabled = false;
            return;
        }

        collidersHoja = hoja.GetComponentsInChildren<Collider>(true);

        rotacionCerrada = transform.localRotation;
        rotacionAbierta = rotacionCerrada * Quaternion.Euler(0f, anguloAbierta, 0f);

        PrepararNavMesh();
        AplicarEstado();
    }

    void Update()
    {
        if (!abriendo) return;

        avance = Mathf.MoveTowards(avance, 1f, Time.deltaTime / Mathf.Max(0.1f, duracionApertura));

        // SmoothStep en vez de un Lerp pelado: la hoja arranca y frena despacio, que es como se
        // mueve una puerta de verdad. El avance lineal se guarda aparte para que la duracion siga
        // siendo exacta.
        transform.localRotation = Quaternion.Slerp(rotacionCerrada, rotacionAbierta, Mathf.SmoothStep(0f, 1f, avance));

        if (avance >= 1f)
        {
            abriendo = false;
            AplicarEstado();
        }
    }

    /// <summary>
    /// Intenta abrir la puerta. Si pide llave y el jugador no la tiene, no pasa nada (el cartel ya
    /// se lo esta diciendo). Si la tiene y consumirLlave esta activo, la gasta. Una puerta ya
    /// abierta, o a medio abrir, ignora la interaccion.
    /// </summary>
    public void Interactuar()
    {
        if (Abierta || abriendo) return;

        if (llaveRequerida != null)
        {
            Inventory inventario = ResolverInventario();
            if (inventario == null || !inventario.HasItem(llaveRequerida))
            {
                ReproducirSfx(sonidoCerrado);
                return;
            }

            // Se gasta ANTES de abrir: si quitar la llave fallara, la puerta no se abre gratis.
            if (consumirLlave && !inventario.RemoveItem(llaveRequerida))
            {
                Debug.LogWarning($"PuertaInteractuable '{name}': no se pudo quitar '{llaveRequerida.itemName}' del inventario.", this);
                return;
            }
        }

        Abrir();
    }

    /// <summary>
    /// Abre la puerta sin pedir ni consumir llave. Para el boton secreto, un script de debug o un
    /// UnityEvent: la validacion de la llave vive en Interactuar(), no aca.
    /// </summary>
    public void Abrir()
    {
        if (Abierta || abriendo) return;

        abriendo = true;
        AplicarEstado();

        ReproducirSfx(sonidoAbrir);

        alAbrirse?.Invoke();
        AlAbrirse?.Invoke();

        PromptInteraccion.Instancia?.Ocultar();
    }

    // Mientras la hoja gira no es solida: un collider girando puede empujar o trabar al
    // CharacterController del jugador (mismo cuidado que toma BarreraDinamica.AplicarFisica).
    // El carving solo corta el NavMesh con la puerta cerrada; abierta, la hoja esta fuera del paso.
    void AplicarEstado()
    {
        bool solida = !abriendo;
        for (int i = 0; i < collidersHoja.Length; i++)
        {
            if (collidersHoja[i] != null) collidersHoja[i].enabled = solida;
        }

        if (obstaculo != null) obstaculo.enabled = !Abierta && !abriendo;
    }

    // El NavMeshObstacle va en la hoja (no en la raiz) para que el corte gire con ella.
    void PrepararNavMesh()
    {
        NavMeshModifier modificador = hoja.GetComponent<NavMeshModifier>();
        if (modificador == null) modificador = hoja.gameObject.AddComponent<NavMeshModifier>();
        modificador.ignoreFromBuild = true;

        obstaculo = hoja.GetComponent<NavMeshObstacle>();
        if (obstaculo == null) obstaculo = hoja.gameObject.AddComponent<NavMeshObstacle>();
        obstaculo.shape = NavMeshObstacleShape.Box;
        // La hoja ya viene escalada: el tamano del obstaculo se expresa en su espacio local.
        obstaculo.size = Vector3.one;
        obstaculo.center = Vector3.zero;
        obstaculo.carving = true;
        obstaculo.carveOnlyStationary = true;
    }

    bool TieneLlave()
    {
        Inventory inventario = ResolverInventario();
        return inventario != null && inventario.HasItem(llaveRequerida);
    }

    // Mismo criterio que ItemPickup.ResolveInventory: el campo del Inspector manda y, si no hay,
    // se busca una vez en la escena. No se cachea el fallo (TextoPrompt se lee todos los frames,
    // pero el raycast apunta a una puerta a la vez, asi que volver a buscar no cuesta nada).
    Inventory ResolverInventario()
    {
        if (inventarioJugador != null) return inventarioJugador;
        if (inventarioResuelto != null) return inventarioResuelto;

        inventarioResuelto = FindAnyObjectByType<Inventory>();
        if (inventarioResuelto == null && !avisoSinInventario)
        {
            avisoSinInventario = true;
            Debug.LogWarning($"PuertaInteractuable '{name}': no se encontro ningun Inventory en la escena.", this);
        }
        return inventarioResuelto;
    }

    void ReproducirSfx(AudioClip clip)
    {
        // Nada de audio en modo edicion: los self-tests invocan estos metodos por reflexion
        // (mismo cuidado que PlayerStats.ReproducirSfx y EnemyAI.ReproducirSfx).
        if (clip == null || !Application.isPlaying) return;
        AudioSource.PlayClipAtPoint(clip, transform.position);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (transform.Find("Hoja") == null)
        {
            Debug.LogWarning($"PuertaInteractuable '{name}': no tiene un hijo 'Hoja'; sin el no hay nada que girar ni Collider al que pegarle el raycast.", this);
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Linea desde la bisagra hasta donde va a quedar la hoja abierta, para ajustar
        // anguloAbierta sin tener que entrar a Play.
        Gizmos.color = new Color(0.2f, 0.9f, 0.3f, 0.9f);
        Vector3 bisagra = transform.position;
        Vector3 direccion = transform.rotation * Quaternion.Euler(0f, anguloAbierta, 0f) * Vector3.right;
        Gizmos.DrawLine(bisagra, bisagra + direccion * MapaLayout.AnchoCalle);
        Gizmos.DrawSphere(bisagra, 0.2f);
    }
#endif
}
