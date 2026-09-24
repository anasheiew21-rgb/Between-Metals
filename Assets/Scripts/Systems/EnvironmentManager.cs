using UnityEngine;

// Director de ambiente para el terror: fuerza una noche/oscuridad permanente, sin ciclo dia/noche
// y sin dejar que el Skybox aporte luz ambiental. No solo se aplica al arrancar: cualquier otro
// script (o el propio Skybox) que intente tocar la luz ambiental, el Sol o la niebla es pisado en
// el siguiente Update, asi que la escena nunca puede salir de la noche. [ExecuteInEditMode] para
// poder previsualizar la oscuridad en la vista Scene sin dar Play.
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
        foreach (Light luz in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (luz.type == LightType.Directional) return luz;
        }
        return null;
    }
}
