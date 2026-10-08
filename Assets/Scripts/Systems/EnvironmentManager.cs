using System.Collections;
using UnityEngine;

// Director de ambiente para el terror: fuerza una noche/oscuridad permanente, sin ciclo dia/noche
// y sin dejar que el Skybox aporte luz ambiental. No solo se aplica al arrancar: cualquier otro
// script (o el propio Skybox) que intente tocar la luz ambiental, el Sol o la niebla es pisado en
// el siguiente Update, asi que la escena nunca puede salir de la noche. [ExecuteInEditMode] para
// poder previsualizar la oscuridad en la vista Scene sin dar Play.
//
// Decision de diseno de kdg (issue #60): el laberinto va sin techo, asi que el cielo tiene que
// quedar cubierto por la niebla en vez de taparlo con geometria. AplicarCieloYReflejos() apaga el
// Skybox, deja los reflejos de entorno en negro y pinta el fondo de camara del color de la niebla,
// pero SOLO en Play: en modo edicion no debe tocar ni el Skybox ni la camara, para que abrir/guardar
// la escena en el Editor no la altere.
[ExecuteInEditMode]
public class EnvironmentManager : MonoBehaviour
{
    [Header("Luz ambiental (noche permanente)")]
    [SerializeField] private Color colorLuzAmbiental = new Color(0.01f, 0.01f, 0.03f, 1f);

    [Header("Niebla nocturna")]
    [SerializeField] private Color colorNiebla = Color.black;
    [SerializeField] private FogMode modoNiebla = FogMode.ExponentialSquared;
    [Range(0f, 0.5f)] [SerializeField] private float densidadNiebla = 0.08f;

    [Header("Sol/Luna (Directional Light)")]
    [Tooltip("Si se deja vacio, busca la primera luz de tipo Directional en la escena")]
    [SerializeField] private Light sol;
    [Range(0f, 0.1f)] [SerializeField] private float intensidadSol = 0.01f;
    [SerializeField] private Color colorSol = new Color(0.1f, 0.15f, 0.3f);

    void Awake()
    {
        if (sol == null) sol = BuscarSol();
    }

    void OnEnable()
    {
        // Se recrea con cada carga de escena (el objeto vive en la propia escena), asi que esto
        // corre tanto en el primer Play como despues de cada Reiniciar.
        AplicarCieloYReflejos();

        if (Application.isPlaying) StartCoroutine(ReintentarCamarasSiHaceFalta());
    }

    void Start()
    {
        AplicarOscuridadEnEditor();
    }

    // Se reaplica todos los frames (en Play y en Editor gracias a [ExecuteInEditMode]) para que
    // ningun otro script, ni el Skybox, pueda sacar la escena de la oscuridad total.
    void Update()
    {
        AplicarOscuridadEnEditor();
    }

    // Apaga el Skybox y los reflejos de entorno, y pinta el fondo de camara del color de la niebla,
    // para que el cielo no rompa la oscuridad. Solo actua en Play (Application.isPlaying); en modo
    // edicion es un no-op para no ensuciar la escena guardada. "forzarParaPruebas" existe unicamente
    // para que el self-test pueda ejercitarlo fuera de Play mode.
    public void AplicarCieloYReflejos(bool forzarParaPruebas = false)
    {
        if (!Application.isPlaying && !forzarParaPruebas) return;

        RenderSettings.skybox = null;
        RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;
        RenderSettings.customReflectionTexture = null;
        DynamicGI.UpdateEnvironment();

        AplicarColorDeFondoACamaras();
    }

    // Reintenta unos pocos frames por si la camara todavia no existe cuando este objeto se habilita
    // (por ejemplo, si algo la instancia despues). No se queda buscando para siempre.
    IEnumerator ReintentarCamarasSiHaceFalta()
    {
        const int intentosMaximos = 10;
        for (int intento = 0; intento < intentosMaximos && Application.isPlaying; intento++)
        {
            if (AplicarColorDeFondoACamaras()) yield break;
            yield return null;
        }
    }

    // Camera.main y cualquier otra camara activa de la escena: clear flags a Color Solido con el
    // color de niebla configurado, para que no quede ningun resto de Skybox visible en el fondo.
    bool AplicarColorDeFondoACamaras()
    {
        Camera[] camaras = FindObjectsByType<Camera>();
        if (camaras.Length == 0) return false;

        foreach (Camera camara in camaras)
        {
            camara.clearFlags = CameraClearFlags.SolidColor;
            camara.backgroundColor = colorNiebla;
        }
        return true;
    }

    // Fuerza el ambiente a oscuridad total: anula el aporte de luz e intensidad de reflejos del
    // Skybox, deja la luz ambiental casi negra, el Sol/Luna al minimo y la niebla cerrando la
    // visibilidad. Se puede disparar a mano desde el Editor sin dar Play.
    [ContextMenu("Aplicar Oscuridad En Editor")]
    public void AplicarOscuridadEnEditor()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = colorLuzAmbiental;
        RenderSettings.ambientSkyColor = colorLuzAmbiental;
        RenderSettings.ambientEquatorColor = colorLuzAmbiental;
        RenderSettings.ambientGroundColor = colorLuzAmbiental;
        RenderSettings.ambientIntensity = 0f;
        RenderSettings.reflectionIntensity = 0f;

        RenderSettings.fog = true;
        RenderSettings.fogColor = colorNiebla;
        RenderSettings.fogMode = modoNiebla;
        RenderSettings.fogDensity = densidadNiebla;

        if (sol == null) sol = BuscarSol();
        if (sol != null)
        {
            sol.type = LightType.Directional;
            sol.intensity = intensidadSol;
            sol.color = colorSol;
        }
    }

    static Light BuscarSol()
    {
        foreach (Light luz in FindObjectsByType<Light>())
        {
            if (luz.type == LightType.Directional) return luz;
        }
        return null;
    }
}
