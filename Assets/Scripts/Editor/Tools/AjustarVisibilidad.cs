using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Herramienta de un solo uso (issue #60, PR #61): aplica a Prototype.unity los valores de
// visibilidad (luz ambiente, niebla, sol y linterna) que el usuario eligio probando en Play.
// Se deja commiteada para trazabilidad; no esta pensada para correr de nuevo salvo que se
// quieran otros valores (en ese caso, se ajustan las constantes de arriba y se vuelve a correr).
// Pensada para -batchmode -executeMethod desde la linea de comandos.
public static class AjustarVisibilidad
{
    const string RutaEscena = "Assets/Scenes/Prototype.unity";

    static readonly Color ColorLuzAmbientalNuevo = new Color(30f / 255f, 30f / 255f, 40f / 255f, 1f);
    const float DensidadNieblaNueva = 0.03f;
    const float IntensidadSolNueva = 0.1f;

    const float IntensityLinternaNueva = 20f;
    const float RangeLinternaNueva = 20f;
    const float SpotAngleLinternaNuevo = 55f;

    public static void Ejecutar()
    {
        Scene escena;
        try
        {
            escena = EditorSceneManager.OpenScene(RutaEscena, OpenSceneMode.Single);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("AjustarVisibilidad: no se pudo abrir " + RutaEscena + ": " + ex.Message);
            Salir(1);
            return;
        }

        EnvironmentManager environmentManager = BuscarUnico<EnvironmentManager>(escena, out int cantidadEnvironment);
        if (cantidadEnvironment != 1)
        {
            Debug.LogError($"AjustarVisibilidad: se esperaba exactamente 1 EnvironmentManager y se encontraron {cantidadEnvironment}. No se modifica ni se guarda la escena.");
            Salir(1);
            return;
        }

        FlashlightController flashlightController = BuscarUnico<FlashlightController>(escena, out int cantidadFlashlight);
        if (cantidadFlashlight != 1)
        {
            Debug.LogError($"AjustarVisibilidad: se esperaba exactamente 1 FlashlightController y se encontraron {cantidadFlashlight}. No se modifica ni se guarda la escena.");
            Salir(1);
            return;
        }

        Light linterna = flashlightController.GetComponent<Light>();
        if (linterna == null)
        {
            Debug.LogError("AjustarVisibilidad: el FlashlightController no tiene un componente Light. No se modifica ni se guarda la escena.");
            Salir(1);
            return;
        }

        // EnvironmentManager: solo los 3 campos aprobados. colorNiebla, modoNiebla y colorSol
        // quedan intactos.
        SerializedObject soEnvironment = new SerializedObject(environmentManager);

        SerializedProperty propColor = soEnvironment.FindProperty("colorLuzAmbiental");
        SerializedProperty propDensidad = soEnvironment.FindProperty("densidadNiebla");
        SerializedProperty propSol = soEnvironment.FindProperty("intensidadSol");

        Color colorAnterior = propColor.colorValue;
        float densidadAnterior = propDensidad.floatValue;
        float solAnterior = propSol.floatValue;

        propColor.colorValue = ColorLuzAmbientalNuevo;
        propDensidad.floatValue = DensidadNieblaNueva;
        propSol.floatValue = IntensidadSolNueva;

        soEnvironment.ApplyModifiedPropertiesWithoutUndo();

        Debug.Log($"AjustarVisibilidad: EnvironmentManager.colorLuzAmbiental {colorAnterior} -> {ColorLuzAmbientalNuevo}");
        Debug.Log($"AjustarVisibilidad: EnvironmentManager.densidadNiebla {densidadAnterior} -> {DensidadNieblaNueva}");
        Debug.Log($"AjustarVisibilidad: EnvironmentManager.intensidadSol {solAnterior} -> {IntensidadSolNueva}");

        // FlashlightController no controla por codigo la intensidad, el alcance ni el angulo del
        // Light (Start/Update solo prenden o apagan segun la tecla de linterna), asi que los
        // valores van directo en el componente Light.
        SerializedObject soLinterna = new SerializedObject(linterna);

        SerializedProperty propIntensity = soLinterna.FindProperty("m_Intensity");
        SerializedProperty propRange = soLinterna.FindProperty("m_Range");
        SerializedProperty propSpotAngle = soLinterna.FindProperty("m_SpotAngle");

        float intensityAnterior = propIntensity.floatValue;
        float rangeAnterior = propRange.floatValue;
        float spotAngleAnterior = propSpotAngle.floatValue;

        propIntensity.floatValue = IntensityLinternaNueva;
        propRange.floatValue = RangeLinternaNueva;
        propSpotAngle.floatValue = SpotAngleLinternaNuevo;

        soLinterna.ApplyModifiedPropertiesWithoutUndo();

        Debug.Log($"AjustarVisibilidad: Flashlight Light.intensity {intensityAnterior} -> {IntensityLinternaNueva}");
        Debug.Log($"AjustarVisibilidad: Flashlight Light.range {rangeAnterior} -> {RangeLinternaNueva}");
        Debug.Log($"AjustarVisibilidad: Flashlight Light.spotAngle (outer) {spotAngleAnterior} -> {SpotAngleLinternaNuevo}");

        // EnvironmentManager vuelca sus campos serializados a RenderSettings y a la luz direccional
        // en AplicarOscuridadEnEditor() (normalmente corre en Update() gracias a [ExecuteInEditMode]).
        // Se invoca explicitamente aca para no depender de que el loop del Editor tickee en modo batch.
        environmentManager.AplicarOscuridadEnEditor();

        bool guardo = EditorSceneManager.SaveScene(escena);
        if (!guardo)
        {
            Debug.LogError("AjustarVisibilidad: EditorSceneManager.SaveScene devolvio false.");
            Salir(1);
            return;
        }

        Debug.Log("AjustarVisibilidad: RESULT OK - valores de visibilidad aplicados y escena guardada.");
        Salir(0);
    }

    // Busca por tipo entre TODOS los GameObject de la escena (incluidos inactivos), para no
    // depender de Object.FindObjectsByType (que no informa si hay mas de una coincidencia con el
    // detalle de la busqueda acotada a esta escena).
    static T BuscarUnico<T>(Scene escena, out int cantidadEncontrada) where T : Component
    {
        T encontrado = null;
        int cantidad = 0;

        foreach (GameObject raiz in escena.GetRootGameObjects())
        {
            foreach (T componente in raiz.GetComponentsInChildren<T>(true))
            {
                cantidad++;
                if (encontrado == null) encontrado = componente;
            }
        }

        cantidadEncontrada = cantidad;
        return encontrado;
    }

    static void Salir(int codigo)
    {
        EditorApplication.Exit(codigo);
    }
}
