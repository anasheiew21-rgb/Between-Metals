using UnityEngine;

// Botin que suelta un enemigo al morir (economia, RF09). Se engancha al evento
// EnemyHealth.AlMorir y deja una moneda en el piso: no suma el oro directo al jugador a proposito,
// porque todo lo que se recoge en este juego se recoge con 'E' (ver el comentario de MonedaPickup)
// y porque una moneda tirada le avisa al jugador que ese enemigo valia algo.
//
// EnemyHealth lo autoagrega igual que hace con EnemyHitFeedback, asi que "tener vida" ya alcanza
// para "soltar botin" y no hay que acordarse de ponerlo a mano en cada enemigo de la escena.
//
// prefabMoneda es opcional: mientras no haya modelo, se arma una moneda placeholder por codigo
// (un cubo chico sin iluminacion, que se ve igual en un pasillo oscuro). Al asignar el prefab
// definitivo, este script deja de crear el placeholder sin ningun otro cambio.
[DisallowMultipleComponent]
public class BotinEnemigo : MonoBehaviour
{
    [Header("Botin")]
    [Tooltip("Oro que entrega la moneda que suelta este enemigo.")]
    [Min(1)] [SerializeField] private int oro = 20;

    [Tooltip("Opcional. Prefab de la moneda. Si se deja vacio se crea un cubo brillante placeholder.")]
    [SerializeField] private MonedaPickup prefabMoneda;

    [Header("Donde cae")]
    [Tooltip("Altura sobre el piso a la que queda la moneda, en metros.")]
    [SerializeField] private float alturaSobreElPiso = 0.3f;

    [Tooltip("Capas que cuentan como piso para apoyar la moneda.")]
    [SerializeField] private LayerMask capasPiso = ~0;

    [Header("Placeholder (solo si no hay prefabMoneda)")]
    [SerializeField] private float ladoPlaceholder = 0.35f;
    [SerializeField] private Color colorPlaceholder = new Color(1f, 0.85f, 0.4f);

    // Un material compartido por todas las monedas placeholder: con uno por moneda, cada una
    // seria su propio draw call.
    static Material materialPlaceholder;

    EnemyHealth salud;
    bool yaSolto;

    /// <summary>Oro que entrega la moneda de este enemigo. Nunca menor que 1.</summary>
    public int Oro => Mathf.Max(1, oro);

    /// <summary>Verdadero desde que solto el botin. Es permanente: no suelta dos veces.</summary>
    public bool YaSolto => yaSolto;

    /// <summary>Ultima moneda que solto, o null si todavia no solto ninguna. Lo usan las pruebas.</summary>
    public MonedaPickup MonedaSoltada { get; private set; }

    void Awake()
    {
        salud = GetComponent<EnemyHealth>();
        if (salud != null) salud.AlMorir += Soltar;
    }

    void OnDestroy()
    {
        if (salud != null) salud.AlMorir -= Soltar;
    }

    /// <summary>
    /// Deja la moneda en el piso, debajo del enemigo. Se llama sola desde EnemyHealth.AlMorir;
    /// es publica para poder probarla y para engancharla a un UnityEvent si hiciera falta.
    /// </summary>
    public void Soltar()
    {
        if (yaSolto) return;
        yaSolto = true;

        MonedaPickup moneda = prefabMoneda != null
            ? Instantiate(prefabMoneda, PosicionDeCaida(), Quaternion.identity)
            : CrearPlaceholder(PosicionDeCaida());

        if (moneda == null) return;

        // El valor se fija aca y no en el prefab: asi un mismo prefab de moneda sirve para
        // enemigos que valen distinto, sin duplicar assets.
        moneda.Configurar(Oro);
        moneda.name = $"Moneda_{name}";
    }

    // Se apoya en el piso real en vez de usar la posicion del enemigo tal cual: un enemigo que
    // muere en una rampa o apenas despegado del suelo dejaria la moneda flotando.
    Vector3 PosicionDeCaida()
    {
        Vector3 desde = transform.position + Vector3.up * 1f;
        if (Physics.Raycast(desde, Vector3.down, out RaycastHit hit, 4f, capasPiso, QueryTriggerInteraction.Ignore))
        {
            return hit.point + Vector3.up * alturaSobreElPiso;
        }

        return transform.position + Vector3.up * alturaSobreElPiso;
    }

    // Cubo sin iluminacion: se ve igual con la linterna apagada y no cuesta una luz extra, que en
    // una PC de pocos recursos es justo lo que hay que evitar. El Collider va en trigger para que
    // la moneda no trabe al jugador; el raycast de PlayerInteraction la detecta igual
    // (usa QueryTriggerInteraction.Collide).
    MonedaPickup CrearPlaceholder(Vector3 posicion)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.position = posicion;
        go.transform.localScale = Vector3.one * Mathf.Max(0.05f, ladoPlaceholder);

        if (go.TryGetComponent(out Collider collider)) collider.isTrigger = true;

        if (go.TryGetComponent(out Renderer renderer))
        {
            renderer.sharedMaterial = MaterialPlaceholder();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        return go.AddComponent<MonedaPickup>();
    }

    // Unlit antes que Lit para ahorrar iluminacion por moneda (y porque "brillante" aca significa
    // que se vea sin luz, no que tenga emision real).
    Material MaterialPlaceholder()
    {
        if (materialPlaceholder != null) return materialPlaceholder;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Unlit/Color")
            ?? Shader.Find("Sprites/Default");

        materialPlaceholder = new Material(shader) { name = "MonedaPlaceholder (runtime)" };

        // URP usa _BaseColor y el pipeline integrado _Color: se setean los dos que existan.
        if (materialPlaceholder.HasProperty("_BaseColor")) materialPlaceholder.SetColor("_BaseColor", colorPlaceholder);
        if (materialPlaceholder.HasProperty("_Color")) materialPlaceholder.SetColor("_Color", colorPlaceholder);

        return materialPlaceholder;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (GetComponent<EnemyHealth>() == null)
        {
            Debug.LogWarning($"BotinEnemigo '{name}': este objeto no tiene EnemyHealth, asi que nunca se va a enterar de que murio.", this);
        }
    }
#endif
}
