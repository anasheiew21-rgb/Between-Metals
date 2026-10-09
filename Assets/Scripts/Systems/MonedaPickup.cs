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

    [Tooltip("Opcional. Sonido que se reproduce en la posición de la moneda al recogerla. Si se deja " +
             "vacío se usa Assets/Audio/Resources/Items/pickup_moneda.wav.")]
    [SerializeField] private AudioClip sonidoRecoger;

    private bool recogida;
    private PlayerStats statsResuelto;
    private bool avisoSinStats;

    // Material y textura generados por codigo (una sola vez, compartidos por todas las monedas de
    // la partida: mismo criterio que BibliotecaDeSonidos.ObtenerClipGenerado con el golpe de
    // mecanismo de SenalAmbiental). Moneda.prefab trae el material default de URP -gris liso, sin
    // textura, porque nunca se le asigno ninguno- y esto lo reemplaza sin tocar el prefab ni
    // depender de un archivo de imagen con licencia ajena.
    private static Material materialDorado;
    private static Texture2D texturaDorada;

    void Awake()
    {
        ConstruirModelo();
    }

    // Reemplaza el disco liso que trae Moneda.prefab (un solo cilindro aplastado, MeshFilter y
    // MeshRenderer directo en la raiz) por un perfil de 3 niveles -borde, cara y emblema- armado con
    // primitivas de Unity en tiempo de ejecucion. Mismo criterio que EquipoJugador.CrearPlaceholder
    // con la espada: nace por codigo, sin depender de ningun archivo ni tocar el prefab en disco. El
    // Collider y este mismo script, que viven en la raiz, no se tocan.
    void ConstruirModelo()
    {
        // La raiz del prefab trae una escala no uniforme (0.25, 0.08, 0.25: ancha y chata). Las
        // piezas de abajo estan pensadas en METROS DE MUNDO, y un hijo hereda la escala del padre, asi
        // que hay que compensarla para que no salgan deformadas (una pieza "redonda" se veria ovalada
        // si no se corrige el eje Y, que la raiz achica mucho mas que X/Z).
        Vector3 escalaRaiz = transform.lossyScale;
        Vector3 compensacion = new Vector3(
            Mathf.Abs(escalaRaiz.x) > 0.0001f ? 1f / escalaRaiz.x : 1f,
            Mathf.Abs(escalaRaiz.y) > 0.0001f ? 1f / escalaRaiz.y : 1f,
            Mathf.Abs(escalaRaiz.z) > 0.0001f ? 1f / escalaRaiz.z : 1f);

        if (TryGetComponent(out MeshFilter filtroViejo)) Destruir(filtroViejo);
        if (TryGetComponent(out MeshRenderer rendererViejo)) Destruir(rendererViejo);

        // Centrado en el pivote (mismo eje que tenia el disco original, que iba de -0.08 a 0.08):
        // ancho abajo (el borde), angosto y mas alto en el medio (la cara), un bulto chico arriba
        // (el emblema). El perfil escalonado es lo que lee como "moneda" y no como "ficha lisa".
        CrearPieza("Borde", new Vector3(0f, -0.045f, 0f), new Vector3(0.25f, 0.035f, 0.25f), compensacion);
        CrearPieza("Cara", new Vector3(0f, 0.02f, 0f), new Vector3(0.19f, 0.03f, 0.19f), compensacion);
        CrearPieza("Emblema", new Vector3(0f, 0.065f, 0f), new Vector3(0.07f, 0.015f, 0.07f), compensacion);
    }

    void CrearPieza(string nombre, Vector3 posicionMetros, Vector3 escalaMetros, Vector3 compensacion)
    {
        GameObject pieza = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pieza.name = nombre;
        pieza.transform.SetParent(transform, false);
        pieza.transform.localPosition = Vector3.Scale(posicionMetros, compensacion);
        pieza.transform.localScale = Vector3.Scale(escalaMetros, compensacion);

        // Sin Collider propio: el BoxCollider de la raiz ya cubre el bulto entero y es con el que
        // apunta el raycast de PlayerInteraction. Uno por pieza solo agregaria trabajo al motor de
        // fisica sin sumar nada (mismo criterio que ModelosItemsBuilder.GuardarPrefab).
        if (pieza.TryGetComponent(out Collider col)) Destruir(col);

        // Sin sombras: un objeto de 25 cm tirado en el piso de un pasillo no la necesita, mismo
        // criterio que EquipoJugador.CrearPlaceholder y ModelosItemsBuilder con los props chicos.
        Renderer renderer = pieza.GetComponent<Renderer>();
        renderer.sharedMaterial = ObtenerMaterialDorado();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    // Destroy() tira excepcion en modo edicion (MonedaSelfTest instancia el prefab real para
    // probarlo, y eso corre Awake sin estar en Play Mode): fuera de Play va DestroyImmediate, mismo
    // criterio que EquipoJugador.Destruir.
    static void Destruir(Object objeto)
    {
        if (objeto == null) return;
        if (Application.isPlaying) Destroy(objeto);
        else DestroyImmediate(objeto);
    }

    /// <summary>
    /// Oro que entrega, nunca menor que 1. [Min(1)] solo cubre el Inspector; un valor inválido que
    /// llegue por otro camino se recorta acá, igual que Inventory.Capacity y
    /// ActivadorSenalAmbiental.EventosRequeridos.
    /// </summary>
    public int Valor => Mathf.Max(1, valor);

    /// <summary>Verdadero desde que se recogió. Es permanente: no vuelve a dar oro.</summary>
    public bool Recogida => recogida;

    /// <summary>
    /// Cambia el oro que entrega esta moneda. La usa BotinEnemigo para que un mismo prefab de
    /// moneda sirva para enemigos que valen distinto, sin duplicar assets. Una moneda ya recogida
    /// se ignora: su valor ya se cobró y cambiarlo no significaría nada.
    /// </summary>
    public void Configurar(int oroQueEntrega)
    {
        if (recogida) return;

        valor = Mathf.Max(1, oroQueEntrega);
    }

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

        // El fallback (Items/pickup_moneda) hace que la moneda suene sin cablear nada: BotinEnemigo
        // instancia el prefab en tiempo de ejecucion y ahi no hay Inspector donde asignarle un clip.
        // Pasa por BibliotecaDeSonidos y no por AudioSource.PlayClipAtPoint para que el slider
        // "Efectos" del menu lo afecte (PlayClipAtPoint no pasa por el mixer).
        BibliotecaDeSonidos.ReproducirEnPunto(
            sonidoRecoger != null ? sonidoRecoger : BibliotecaDeSonidos.Clip(BibliotecaDeSonidos.Moneda),
            transform.position);

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

    // ---------------------------------------------------------------
    // Material y textura dorada (generados por codigo, una sola vez)
    // ---------------------------------------------------------------

    private static Material ObtenerMaterialDorado()
    {
        if (materialDorado != null) return materialDorado;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        materialDorado = new Material(shader) { name = "Moneda_Dorada (generado)" };

        Texture2D textura = ObtenerTexturaDorada();
        if (materialDorado.HasProperty("_BaseMap")) materialDorado.SetTexture("_BaseMap", textura);
        if (materialDorado.HasProperty("_MainTex")) materialDorado.SetTexture("_MainTex", textura);

        // Metal pulido: sube el brillo especular para que lea como oro y no como plastico pintado.
        if (materialDorado.HasProperty("_Metallic")) materialDorado.SetFloat("_Metallic", 0.85f);
        if (materialDorado.HasProperty("_Smoothness")) materialDorado.SetFloat("_Smoothness", 0.55f);
        if (materialDorado.HasProperty("_Glossiness")) materialDorado.SetFloat("_Glossiness", 0.55f);

        return materialDorado;
    }

    private static Texture2D ObtenerTexturaDorada()
    {
        if (texturaDorada != null) return texturaDorada;

        // 32x32 alcanza de sobra para una moneda de 25 cm que se ve un instante en el piso de un
        // pasillo oscuro: mas resolucion seria gastar memoria de texturas sin que se note, contra la
        // restriccion de PCs de gama baja del proyecto (ver CLAUDE.md).
        const int lado = 32;
        texturaDorada = new Texture2D(lado, lado, TextureFormat.RGBA32, mipChain: false)
        {
            name = "Moneda_Dorada (generada)",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
        };

        // Oro cepillado: una base calida con vetas y un ruido fino determinista (misma semilla
        // siempre, mismo criterio que BibliotecaDeSonidos.ObtenerClipGenerado con el golpe de
        // mecanismo). No se ata a ninguna coordenada de UV en particular -el cilindro integrado de
        // Unity separa la tapa de la banda lateral en islas distintas- asi que el patron es
        // direccionalmente parejo y se ve bien sin importar en que isla caiga cada pixel.
        var azar = new System.Random(5309);
        Color oroOscuro = new Color(0.52f, 0.38f, 0.09f);
        Color oroClaro = new Color(0.95f, 0.80f, 0.32f);

        for (int y = 0; y < lado; y++)
        {
            for (int x = 0; x < lado; x++)
            {
                // Vetas diagonales suaves (el "cepillado") mas un ruido fino que rompe la uniformidad.
                float veta = Mathf.Sin((x + y) * 0.9f) * 0.5f + 0.5f;
                float ruido = (float)(azar.NextDouble() * 2.0 - 1.0) * 0.08f;

                Color color = Color.Lerp(oroOscuro, oroClaro, Mathf.Clamp01(veta + ruido));
                texturaDorada.SetPixel(x, y, color);
            }
        }

        texturaDorada.Apply();
        return texturaDorada;
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
