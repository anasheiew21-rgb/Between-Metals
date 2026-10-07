using UnityEngine;
using UnityEngine.Rendering;

// Haz de luz vertical estilo "beacon" de Minecraft: una columna que sale del piso y se dispara
// hacia arriba, pensada para que el jugador ubique las puertas desde lejos sin tener que recorrer
// el laberinto entero a ciegas. Los muros del laberinto miden 8 m (MapaLayout.AltoMuro), asi que
// cualquier altura mayor ya asoma por encima y se ve desde otro pasillo.
//
// Como esta hecho, y por que asi:
//   - Dos cilindros concentricos, un nucleo fino y brillante y un halo mas ancho y tenue. Es lo
//     que hace que se lea como un haz y no como un palo: el beacon de Minecraft tiene la misma
//     estructura.
//   - Material Unlit con blending ADITIVO: no consume ni una luz en tiempo real (el proyecto
//     apunta a PCs de pocos recursos) y, siendo aditivo, se suma a lo que haya detras en vez de
//     taparlo, que es como se ve la luz de verdad.
//   - Sin ZWrite y con el cull apagado: el haz no escribe profundidad (no tapa nada que este
//     detras) y se ve igual desde adentro.
//   - El test de profundidad SI queda activo, a proposito: los muros tapan el haz. Lo que se ve
//     desde otro pasillo es la parte que asoma por arriba del muro, igual que en Minecraft.
//   - La niebla del EnvironmentManager lo desvanece con la distancia sola, sin codigo extra.
//
// La geometria se arma en Awake y no en la escena: son dos cilindros y dos materiales por baliza,
// y asi el archivo de escena no engorda con mallas que de todas formas se generan igual siempre.
// En el Editor, sin Play, lo que se ve es el gizmo (OnDrawGizmos).
[DisallowMultipleComponent]
public class Baliza : MonoBehaviour
{
    [Header("Haz")]
    [SerializeField] private Color color = new Color(1f, 0.85f, 0.4f);

    [Tooltip("Largo del haz en metros. Los muros del laberinto miden 8, asi que con menos de eso no asoma.")]
    [Min(1f)] [SerializeField] private float altura = 50f;

    [Min(0.05f)] [SerializeField] private float diametroNucleo = 0.35f;
    [Min(0.05f)] [SerializeField] private float diametroHalo = 1.2f;

    [Tooltip("Brillo del nucleo. Con blending aditivo, esto multiplica el color: 1 = a full.")]
    [Range(0f, 1f)] [SerializeField] private float brilloNucleo = 0.9f;

    [Range(0f, 1f)] [SerializeField] private float brilloHalo = 0.22f;

    [Header("Pulso")]
    [Tooltip("Ciclos por segundo del latido. En 0 el haz queda fijo y Update no hace nada.")]
    [Min(0f)] [SerializeField] private float velocidadPulso = 0.4f;

    [Tooltip("Cuanto sube y baja el brillo con el latido, como fraccion del brillo base.")]
    [Range(0f, 1f)] [SerializeField] private float amplitudPulso = 0.25f;

    [Header("Luz en la base")]
    [Tooltip("Luz real en el piso, para que la puerta se vea de cerca. Apagala si hace falta rendimiento: el haz se ve igual.")]
    [SerializeField] private bool luzEnLaBase = true;

    [Min(0.1f)] [SerializeField] private float rangoLuz = 7f;
    [Min(0f)] [SerializeField] private float intensidadLuz = 1.5f;
    [Min(0f)] [SerializeField] private float alturaLuz = 1.5f;

    [Header("Puerta")]
    [Tooltip("Opcional. La puerta que marca esta baliza, para poder apagarla cuando se abra.")]
    [SerializeField] private PuertaInteractuable puerta;

    [Tooltip("Si esta activo y hay una puerta asignada, el haz se apaga en cuanto esa puerta se abre.")]
    [SerializeField] private bool apagarAlAbrirse;

    Renderer rendererNucleo, rendererHalo;
    Material materialNucleo, materialHalo;
    Light luz;
    bool apagada;

    /// <summary>Verdadero si el haz esta prendido.</summary>
    public bool Encendida => !apagada;

    void Awake()
    {
        ConstruirHaz();

        if (puerta != null && apagarAlAbrirse) puerta.AlAbrirse += Apagar;
    }

    void OnDestroy()
    {
        if (puerta != null) puerta.AlAbrirse -= Apagar;

        // Los materiales se crean por instancia (cada baliza tiene su color), asi que hay que
        // destruirlos a mano: Unity no recoge materiales creados por codigo.
        Destruir(materialNucleo);
        Destruir(materialHalo);
    }

    void Update()
    {
        // Sin latido no hay nada que recalcular: el color ya quedo escrito en Awake.
        if (apagada || velocidadPulso <= 0f || amplitudPulso <= 0f) return;

        // Time.time y no unscaledTime: con el juego en pausa el haz se congela como todo lo demas.
        float latido = 1f + Mathf.Sin(Time.time * velocidadPulso * Mathf.PI * 2f) * amplitudPulso;
        AplicarColor(latido);
    }

    /// <summary>Apaga el haz (y la luz de la base). No se puede volver a prender: es de una sola vez.</summary>
    public void Apagar()
    {
        if (apagada) return;

        apagada = true;
        if (rendererNucleo != null) rendererNucleo.enabled = false;
        if (rendererHalo != null) rendererHalo.enabled = false;
        if (luz != null) luz.enabled = false;
    }

    // ---------------------------------------------------------------
    // Construccion
    // ---------------------------------------------------------------

    void ConstruirHaz()
    {
        materialNucleo = CrearMaterialAditivo();
        materialHalo = CrearMaterialAditivo();

        rendererNucleo = CrearCilindro("Haz_Nucleo", diametroNucleo, materialNucleo);
        rendererHalo = CrearCilindro("Haz_Halo", diametroHalo, materialHalo);

        AplicarColor(1f);

        if (luzEnLaBase) CrearLuz();
    }

    // El cilindro primitivo de Unity mide 2 de alto y 1 de diametro, de ahi el /2 en la escala.
    Renderer CrearCilindro(string nombre, float diametro, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = nombre;
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, altura * 0.5f, 0f);
        go.transform.localScale = new Vector3(diametro, altura * 0.5f, diametro);

        // CRITICO: el cilindro primitivo viene con CapsuleCollider. Si se deja, el haz bloquea al
        // jugador y, peor, el raycast de PlayerInteraction le pega a el en vez de a la puerta.
        if (go.TryGetComponent(out Collider collider)) Destruir(collider);

        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        // Es un objeto emisivo: no tiene sentido que consulte light probes ni reflejos.
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

        return renderer;
    }

    void CrearLuz()
    {
        var go = new GameObject("Luz_Baliza");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, alturaLuz, 0f);

        luz = go.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.color = color;
        luz.range = rangoLuz;
        luz.intensity = intensidadLuz;
        // Sin sombras: una luz con sombras en tiempo real es justo lo que no puede pagar una PC
        // de pocos recursos, y aca solo hace falta que se vea la puerta.
        luz.shadows = LightShadows.None;
    }

    // Unlit + blending aditivo. Se escriben tanto las propiedades que lee el Inspector de URP
    // (_Surface/_Blend) como los estados crudos de blending (_SrcBlend/_DstBlend/_ZWrite/_Cull),
    // porque el shader de URP los declara como estados dinamicos y es lo que de verdad decide como
    // se dibuja; poner solo _Surface deja el material en opaco hasta que alguien abra el Inspector.
    Material CrearMaterialAditivo()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        var material = new Material(shader) { name = "Baliza (runtime)" };

        material.SetOverrideTag("RenderType", "Transparent");

        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);   // 1 = Transparent
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 2f);       // 2 = Additive
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.One);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.One);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Off);
        if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.DisableKeyword("_ALPHATEST_ON");
        material.renderQueue = (int)RenderQueue.Transparent;

        return material;
    }

    // Con blending aditivo el alfa no atenua nada (el factor de origen es One), asi que el brillo
    // se controla multiplicando el RGB. El alfa se escribe igual, por si alguien cambia el material
    // a transparencia normal desde el Inspector.
    void AplicarColor(float factor)
    {
        Pintar(materialNucleo, brilloNucleo * factor);
        Pintar(materialHalo, brilloHalo * factor);
    }

    void Pintar(Material material, float brillo)
    {
        if (material == null) return;

        brillo = Mathf.Max(0f, brillo);
        var c = new Color(color.r * brillo, color.g * brillo, color.b * brillo, Mathf.Clamp01(brillo));

        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", c);
        if (material.HasProperty("_Color")) material.SetColor("_Color", c);
    }

    // DestroyImmediate fuera de Play: Destroy() tira excepcion en modo edicion, y los self-tests
    // de Editor corren asi (mismo criterio que EquipoJugador.Destruir).
    static void Destruir(Object objeto)
    {
        if (objeto == null) return;

        if (Application.isPlaying) Destroy(objeto);
        else DestroyImmediate(objeto);
    }

#if UNITY_EDITOR
    // El haz solo existe en Play, asi que en el Editor el gizmo es lo unico que muestra donde esta
    // la baliza y hasta donde llega.
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(color.r, color.g, color.b, 0.9f);

        Vector3 abajo = transform.position;
        Vector3 arriba = abajo + Vector3.up * altura;
        Gizmos.DrawLine(abajo, arriba);

        // Marca de la altura de los muros: si el haz no pasa esa raya, no se ve desde otro pasillo.
        Vector3 alturaMuro = abajo + Vector3.up * MapaLayout.AltoMuro;
        Gizmos.DrawLine(alturaMuro + Vector3.right * 0.6f, alturaMuro - Vector3.right * 0.6f);
        Gizmos.DrawLine(alturaMuro + Vector3.forward * 0.6f, alturaMuro - Vector3.forward * 0.6f);

        DibujarCirculo(abajo, diametroHalo * 0.5f);
        DibujarCirculo(abajo, diametroNucleo * 0.5f);
    }

    static void DibujarCirculo(Vector3 centro, float radio)
    {
        const int lados = 16;
        Vector3 previo = centro + new Vector3(radio, 0f, 0f);

        for (int i = 1; i <= lados; i++)
        {
            float a = i / (float)lados * Mathf.PI * 2f;
            Vector3 actual = centro + new Vector3(Mathf.Cos(a) * radio, 0f, Mathf.Sin(a) * radio);
            Gizmos.DrawLine(previo, actual);
            previo = actual;
        }
    }
#endif
}
