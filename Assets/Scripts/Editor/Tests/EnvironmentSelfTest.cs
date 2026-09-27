using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

// Autotest de editor de EnvironmentManager (issue #60). Casos CP-ENV-01..06.
// Usa una escena temporal en memoria (nunca se guarda) para CP-ENV-01..04, y abre Prototype.unity
// sin guardarla para CP-ENV-05 (verifica que el piso duplicado ya no este) y CP-ENV-06 (verifica
// los valores de visibilidad aprobados por el usuario, PR #61).
// Batch: Unity.exe -batchmode -nographics -projectPath <ruta> -executeMethod EnvironmentSelfTest.RunAllAndExit -logFile <log>
public static class EnvironmentSelfTest
{
    const string Tag = "[EnvironmentSelfTest]";
    const string EscenaPrototipo = "Assets/Scenes/Prototype.unity";

    static int passed;
    static int failed;

    [MenuItem("Between Metals/Tests/Environment")]
    static void RunFromMenu()
    {
        RunAll();
    }

    public static void RunAllAndExit()
    {
        bool ok = false;
        try
        {
            ok = RunAll();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }

        if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
    }

    static bool RunAll()
    {
        passed = 0;
        failed = 0;

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Snapshot de los RenderSettings globales antes de ensuciarlos, para restaurarlos al final.
        Material skyboxOriginal = RenderSettings.skybox;
        UnityEngine.Rendering.DefaultReflectionMode modoReflejoOriginal = RenderSettings.defaultReflectionMode;
        Texture reflejoCustomOriginal = RenderSettings.customReflectionTexture;

        Material skyboxDePruebaForzado = new Material(Shader.Find("Skybox/Procedural"));
        Material skyboxDePruebaNormal = new Material(Shader.Find("Skybox/Procedural"));

        // ---- CP-ENV-01/02/03: llamada forzada (forzarParaPruebas: true) fuera de Play ----
        GameObject goManagerForzado = new GameObject("EnvironmentManager_Forzado");
        EnvironmentManager managerForzado = goManagerForzado.AddComponent<EnvironmentManager>();

        GameObject goCamaraForzada = new GameObject("Camara_Forzada");
        Camera camaraForzada = goCamaraForzada.AddComponent<Camera>();
        camaraForzada.clearFlags = CameraClearFlags.Skybox;
        camaraForzada.backgroundColor = Color.magenta;

        RenderSettings.skybox = skyboxDePruebaForzado;
        RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Skybox;

        managerForzado.AplicarCieloYReflejos(true);

        Color colorNieblaEsperado = (Color)GetPrivateField(managerForzado, "colorNiebla");

        Run("CP-ENV-01", "AplicarCieloYReflejos(forzado) deja la camara en SolidColor con el color de niebla", () =>
        {
            if (camaraForzada.clearFlags != CameraClearFlags.SolidColor)
                return $"clearFlags = {camaraForzada.clearFlags} (se esperaba SolidColor)";
            if (camaraForzada.backgroundColor != colorNieblaEsperado)
                return $"backgroundColor = {camaraForzada.backgroundColor} (se esperaba {colorNieblaEsperado})";
            return null;
        });

        Run("CP-ENV-02", "AplicarCieloYReflejos(forzado) deja RenderSettings.skybox en null", () =>
        {
            return RenderSettings.skybox == null
                ? null
                : $"RenderSettings.skybox = {RenderSettings.skybox.name} (se esperaba null)";
        });

        Run("CP-ENV-03", "AplicarCieloYReflejos(forzado) deja el modo de reflejo en Custom, sin textura (negro)", () =>
        {
            if (RenderSettings.defaultReflectionMode != UnityEngine.Rendering.DefaultReflectionMode.Custom)
                return $"defaultReflectionMode = {RenderSettings.defaultReflectionMode} (se esperaba Custom)";
            if (RenderSettings.customReflectionTexture != null)
                return "customReflectionTexture no es null";
            return null;
        });

        // ---- CP-ENV-04: en modo edicion real (sin forzar), no debe tocar nada ----
        GameObject goManagerNormal = new GameObject("EnvironmentManager_Normal");
        EnvironmentManager managerNormal = goManagerNormal.AddComponent<EnvironmentManager>();

        GameObject goCamaraNormal = new GameObject("Camara_Normal");
        Camera camaraNormal = goCamaraNormal.AddComponent<Camera>();
        camaraNormal.clearFlags = CameraClearFlags.Skybox;
        camaraNormal.backgroundColor = Color.magenta;

        RenderSettings.skybox = skyboxDePruebaNormal;
        RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Skybox;

        Run("CP-ENV-04", "Fuera de Play y sin forzar, AplicarCieloYReflejos() no toca el Skybox ni la camara", () =>
        {
            managerNormal.AplicarCieloYReflejos(); // sin forzarParaPruebas; Application.isPlaying es falso aca
            if (RenderSettings.skybox != skyboxDePruebaNormal) return "RenderSettings.skybox cambio";
            if (camaraNormal.clearFlags != CameraClearFlags.Skybox) return "clearFlags de la camara cambio";
            if (camaraNormal.backgroundColor != Color.magenta) return "backgroundColor de la camara cambio";
            return null;
        });

        UnityEngine.Object.DestroyImmediate(goManagerForzado);
        UnityEngine.Object.DestroyImmediate(goCamaraForzada);
        UnityEngine.Object.DestroyImmediate(goManagerNormal);
        UnityEngine.Object.DestroyImmediate(goCamaraNormal);
        UnityEngine.Object.DestroyImmediate(skyboxDePruebaForzado);
        UnityEngine.Object.DestroyImmediate(skyboxDePruebaNormal);

        // Restaura los RenderSettings globales que este test ensucio, antes de tocar otra escena.
        RenderSettings.skybox = skyboxOriginal;
        RenderSettings.defaultReflectionMode = modoReflejoOriginal;
        RenderSettings.customReflectionTexture = reflejoCustomOriginal;

        // ---- CP-ENV-05: Prototype.unity real, sin guardar ----
        Run("CP-ENV-05", "Prototype.unity tiene exactamente 1 \"Suelo_Plano_Principal\", 0 \"Suelo_Plano_Principal (1)\", y el piso conserva su MeshCollider", () =>
        {
            Scene escena = EditorSceneManager.OpenScene(EscenaPrototipo, OpenSceneMode.Single);

            int cantidadOriginal = 0;
            int cantidadDuplicado = 0;
            GameObject original = null;

            foreach (GameObject raiz in escena.GetRootGameObjects())
            {
                foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
                {
                    if (t.gameObject.name == "Suelo_Plano_Principal")
                    {
                        cantidadOriginal++;
                        original = t.gameObject;
                    }
                    else if (t.gameObject.name == "Suelo_Plano_Principal (1)")
                    {
                        cantidadDuplicado++;
                    }
                }
            }

            if (cantidadOriginal != 1) return $"hay {cantidadOriginal} objetos \"Suelo_Plano_Principal\" (se esperaba 1)";
            if (cantidadDuplicado != 0) return $"todavia hay {cantidadDuplicado} objeto(s) \"Suelo_Plano_Principal (1)\"";
            if (original.GetComponent<MeshCollider>() == null) return "el piso ya no tiene MeshCollider";
            return null;
        });

        // ---- CP-ENV-06: Prototype.unity real, sin guardar, valores de visibilidad aprobados ----
        Run("CP-ENV-06", "Prototype.unity, sin guardar, tiene los valores de visibilidad aprobados en EnvironmentManager y en la linterna (issue #60, PR #61)", () =>
        {
            const float tolerancia = 0.001f;

            Scene escena = EditorSceneManager.OpenScene(EscenaPrototipo, OpenSceneMode.Single);

            EnvironmentManager environmentManager = null;
            int cantidadEnvironment = 0;
            FlashlightController flashlightController = null;
            int cantidadFlashlight = 0;

            foreach (GameObject raiz in escena.GetRootGameObjects())
            {
                foreach (EnvironmentManager em in raiz.GetComponentsInChildren<EnvironmentManager>(true))
                {
                    cantidadEnvironment++;
                    if (environmentManager == null) environmentManager = em;
                }
                foreach (FlashlightController fc in raiz.GetComponentsInChildren<FlashlightController>(true))
                {
                    cantidadFlashlight++;
                    if (flashlightController == null) flashlightController = fc;
                }
            }

            if (cantidadEnvironment != 1) return $"hay {cantidadEnvironment} EnvironmentManager (se esperaba 1)";
            if (cantidadFlashlight != 1) return $"hay {cantidadFlashlight} FlashlightController (se esperaba 1)";

            Color colorEsperado = new Color(30f / 255f, 30f / 255f, 40f / 255f, 1f);
            Color colorActual = (Color)GetPrivateField(environmentManager, "colorLuzAmbiental");
            if (!ColoresCerca(colorActual, colorEsperado, tolerancia))
                return $"colorLuzAmbiental = {colorActual} (se esperaba {colorEsperado})";

            float densidadActual = (float)GetPrivateField(environmentManager, "densidadNiebla");
            if (Mathf.Abs(densidadActual - 0.03f) > tolerancia)
                return $"densidadNiebla = {densidadActual} (se esperaba 0.03)";

            float solActual = (float)GetPrivateField(environmentManager, "intensidadSol");
            if (Mathf.Abs(solActual - 0.1f) > tolerancia)
                return $"intensidadSol = {solActual} (se esperaba 0.1)";

            Light linterna = flashlightController.GetComponent<Light>();
            if (linterna == null) return "el FlashlightController no tiene un componente Light";

            if (Mathf.Abs(linterna.intensity - 20f) > tolerancia)
                return $"Flashlight Light.intensity = {linterna.intensity} (se esperaba 20)";
            if (Mathf.Abs(linterna.range - 20f) > tolerancia)
                return $"Flashlight Light.range = {linterna.range} (se esperaba 20)";
            if (Mathf.Abs(linterna.spotAngle - 55f) > tolerancia)
                return $"Flashlight Light.spotAngle = {linterna.spotAngle} (se esperaba 55)";

            return null;
        });

        Debug.Log($"{Tag} RESULT: {passed} passed, {failed} failed");
        return failed == 0;
    }

    static void Run(string id, string description, Func<string> test)
    {
        string error;
        try
        {
            error = test();
        }
        catch (TargetInvocationException e) when (e.InnerException != null)
        {
            error = $"excepcion {e.InnerException.GetType().Name}: {e.InnerException.Message}";
        }
        catch (Exception e)
        {
            error = $"excepcion {e.GetType().Name}: {e.Message}";
        }

        if (error == null)
        {
            passed++;
            Debug.Log($"{Tag} {id} PASS - {description}");
        }
        else
        {
            failed++;
            Debug.LogError($"{Tag} {id} FAIL - {description} - {error}");
        }
    }

    static object GetPrivateField(object obj, string nombre)
    {
        FieldInfo campo = obj.GetType().GetField(nombre, BindingFlags.Instance | BindingFlags.NonPublic);
        return campo?.GetValue(obj);
    }

    static bool ColoresCerca(Color a, Color b, float tolerancia)
    {
        return Mathf.Abs(a.r - b.r) <= tolerancia
            && Mathf.Abs(a.g - b.g) <= tolerancia
            && Mathf.Abs(a.b - b.b) <= tolerancia
            && Mathf.Abs(a.a - b.a) <= tolerancia;
    }
}
