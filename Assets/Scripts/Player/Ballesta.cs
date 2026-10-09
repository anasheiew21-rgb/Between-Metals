using UnityEngine;

// La ballesta que vende el comerciante, vista desde la mano del jugador. Va en el objeto del arma
// que instancia EquipoJugador (no en el jugador): equiparla la trae y guardarla se la lleva, asi que
// "tener ballesta en la mano" y "poder disparar" son la misma cosa y no hay un estado mas que
// mantener sincronizado.
//
// Quien aprieta el gatillo es PlayerCombat, no esta clase: el input del ataque es uno solo
// (KeyBindings.Action.Attack, clic izquierdo por defecto y remapeable desde el menu de opciones) y
// PlayerCombat lo rutea al arma que corresponda -golpe de daga o tiro de ballesta-. Si la ballesta
// leyera el clic por su cuenta, un jugador que se remapeo el ataque a otra tecla seguiria disparando
// con el clic, y al equipar la ballesta se dispararia Y se pegaria un golpe con el mismo clic.
//
// MUNICION INFINITA: no hay contador, ni item de flechas en el inventario, ni nada que recargar. Lo
// unico que limita el tiro es 'cadencia'. Esta escrito aca para que quede claro que es una decision
// y no un pendiente.
[DisallowMultipleComponent]
public class Ballesta : MonoBehaviour
{
    [Header("Tiro")]
    [Tooltip("Segundos entre tiro y tiro. Una ballesta se vuelve a tensar a mano: con un valor bajo " +
             "se convierte en ametralladora y el arma deja de tener contra.")]
    [SerializeField] private float cadencia = 1.2f;

    [Tooltip("Daño de un flechazo. Contra los 500 de vida de un enemigo: 5 tiros. El golpe con la " +
             "daga hace 75 (7 golpes), pero es cuerpo a cuerpo y gasta estamina.")]
    [SerializeField] private float dano = 100f;

    [Tooltip("Metros por segundo de la flecha.")]
    [SerializeField] private float velocidadFlecha = 45f;

    [Tooltip("Capas contra las que choca la flecha.")]
    [SerializeField] private LayerMask capasFlecha = ~0;

    [Header("Flecha")]
    [Tooltip("Opcional. Prefab de la flecha. Si se deja vacio se arma un cilindro placeholder por " +
             "codigo (mismo criterio que el arma de EquipoJugador).")]
    [SerializeField] private GameObject prefabFlecha;

    [Tooltip("A que distancia de la camara nace la flecha. Tiene que ser mayor que el radio del " +
             "CharacterController del jugador, o la flecha nacera dentro de su propio cuerpo.")]
    [SerializeField] private float distanciaDeSalida = 0.8f;

    [Header("Placeholder (solo si no hay prefabFlecha)")]
    [Tooltip("Largo y grosor del cilindro placeholder, en metros.")]
    [SerializeField] private float largoPlaceholder = 0.5f;

    [SerializeField] private float grosorPlaceholder = 0.022f;

    [SerializeField] private Color colorPlaceholder = new Color(0.62f, 0.55f, 0.42f);

    private float cooldownRestante;
    private Camera camaraCacheada;

    /// <summary>Verdadero si la cadencia ya permite otro tiro.</summary>
    public bool PuedeDisparar => cooldownRestante <= 0f;

    /// <summary>Segundos que faltan para poder disparar de nuevo. 0 si ya se puede.</summary>
    public float CooldownRestante => Mathf.Max(0f, cooldownRestante);

    /// <summary>Daño de un flechazo. Lo lee la UI (y las pruebas) sin disparar.</summary>
    public float Dano => dano;

    void Update()
    {
        if (cooldownRestante > 0f) cooldownRestante -= Time.deltaTime;
    }

    /// <summary>
    /// Dispara una flecha desde la camara del jugador. Devuelve false, sin hacer nada, si todavia
    /// corre la cadencia o si no se encontro la camara.
    /// </summary>
    public bool Disparar()
    {
        if (cooldownRestante > 0f) return false;

        Camera camara = ResolverCamara();
        if (camara == null)
        {
            Debug.LogWarning("Ballesta: no se encontro la camara del jugador; el tiro no sale.", this);
            return false;
        }

        cooldownRestante = cadencia;

        // La flecha sale de la CAMARA y no de la boca del arma, aunque el arma se vea a la derecha
        // de la pantalla. Es a proposito: lo que el jugador apunta es el centro de la pantalla, y una
        // flecha que naciera en la boca del modelo saldria desplazada 30 cm a la derecha y erraria
        // todo lo que estuviera cerca. Mismo criterio que el SphereCast de PlayerCombat, que tambien
        // sale de la camara y no del arma.
        Transform ojo = camara.transform;
        Vector3 origen = OrigenDelTiro(ojo);

        GameObject objeto = prefabFlecha != null
            ? Instantiate(prefabFlecha, origen, Quaternion.LookRotation(ojo.forward, Vector3.up))
            : CrearPlaceholder(origen, ojo.forward);
        objeto.name = "Flecha";

        // Ojo si algun dia se le pone un padre: la flecha tiene que quedar SUELTA en la escena. Si
        // colgara del arma o del jugador, caminar o guardar la ballesta se llevaria de paseo las
        // flechas que estan en vuelo.
        Flecha flecha = objeto.GetComponent<Flecha>() ?? objeto.AddComponent<Flecha>();
        flecha.Lanzar(ojo.forward, velocidadFlecha, dano, DuenioDelTiro(ojo), capasFlecha);

        return true;
    }

    // Donde nace la flecha. Normalmente a 'distanciaDeSalida' de la camara, para que no aparezca
    // dentro del propio cuerpo del jugador. Pero en un laberinto el jugador se pega a las paredes, y
    // apuntando contra un muro a 30 cm esos 80 cm dejarian la flecha del OTRO lado del muro: naceria
    // pasada y se iria derecho, porque un raycast que arranca dentro de un collider no lo detecta.
    // Por eso el tramo hasta el punto de salida se barre antes y, si hay algo, la flecha nace justo
    // delante de lo que haya.
    Vector3 OrigenDelTiro(Transform ojo)
    {
        if (Physics.Raycast(ojo.position, ojo.forward, out RaycastHit estorbo, distanciaDeSalida,
                capasFlecha, QueryTriggerInteraction.Ignore))
        {
            // Un margen chico hacia atras, para que la flecha quede del lado de aca de la superficie
            // y su primer raycast la encuentre en el frame siguiente.
            return estorbo.point - ojo.forward * 0.05f;
        }

        return ojo.position + ojo.forward * distanciaDeSalida;
    }

    // El raiz del jugador, para que la flecha no se clave en el que la disparo. Se busca por el
    // CharacterController (que es lo que tiene el collider del cuerpo, ver PlayerController) subiendo
    // desde la camara; si no aparece, se usa la camara, que ya alcanza para no pegarse a si mismo.
    static Transform DuenioDelTiro(Transform ojo)
    {
        var cuerpo = ojo.GetComponentInParent<CharacterController>();
        return cuerpo != null ? cuerpo.transform : ojo;
    }

    // La camara del jugador es la que lleva MouseLook (la jerarquia que documenta CLAUDE.md), no
    // Camera.main: mismo criterio y mismo motivo que EquipoJugador.
    Camera ResolverCamara()
    {
        if (camaraCacheada != null) return camaraCacheada;

        // Primero hacia arriba: la ballesta cuelga de la mano, que cuelga de la camara, asi que lo
        // normal es encontrarla sin recorrer la escena.
        camaraCacheada = GetComponentInParent<Camera>();
        if (camaraCacheada != null) return camaraCacheada;

        var mouseLook = FindAnyObjectByType<MouseLook>();
        if (mouseLook != null) camaraCacheada = mouseLook.GetComponent<Camera>();

        return camaraCacheada ??= Camera.main;
    }

    // Cilindro delgado como flecha provisoria. El cilindro de Unity mide 1 m de diametro y 2 m de
    // alto a lo largo de SU EJE Y, pero una flecha tiene que apuntar a +Z (es el eje que
    // Flecha usa para volar), asi que el cuerpo va en un hijo girado 90 grados en X.
    GameObject CrearPlaceholder(Vector3 posicion, Vector3 direccion)
    {
        var raiz = new GameObject("Flecha");
        raiz.transform.SetPositionAndRotation(posicion, Quaternion.LookRotation(direccion, Vector3.up));

        GameObject cuerpo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cuerpo.name = "Cuerpo";
        cuerpo.transform.SetParent(raiz.transform, false);
        cuerpo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        cuerpo.transform.localScale = new Vector3(grosorPlaceholder, largoPlaceholder * 0.5f, grosorPlaceholder);

        // Sin Collider: Flecha resuelve el impacto con un raycast, no por contacto (ver su cabecera).
        if (cuerpo.TryGetComponent(out Collider collider)) Destroy(collider);

        if (cuerpo.TryGetComponent(out Renderer renderer))
        {
            renderer.sharedMaterial = MaterialPlaceholder();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        return raiz;
    }

    static Material materialPlaceholder;

    Material MaterialPlaceholder()
    {
        if (materialPlaceholder != null) return materialPlaceholder;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        materialPlaceholder = new Material(shader) { name = "FlechaPlaceholder (runtime)" };

        if (materialPlaceholder.HasProperty("_BaseColor")) materialPlaceholder.SetColor("_BaseColor", colorPlaceholder);
        if (materialPlaceholder.HasProperty("_Color")) materialPlaceholder.SetColor("_Color", colorPlaceholder);

        return materialPlaceholder;
    }
}
