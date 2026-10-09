using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Autotest de editor del arco de ataque de la espada (CP-ESP-01..07): la geometria del cono
// (angulo + alcance) y la exclusion del propio cuerpo del jugador, que es exactamente lo que le
// faltaba al golpe cuerpo a cuerpo de siempre (PlayerCombat.GolpearAManoLimpia tenia el mismo bug
// hasta este cambio: el SphereCast nace DENTRO del CharacterController del jugador -la camara esta
// a ~13 cm de su eje, bien dentro del radio de 0.5 m- y sin excluirlo el golpe se resolvia contra el
// propio cuerpo y nunca llegaba al enemigo de enfrente).
//
// Lo que este autotest NO cubre, y hay que probar en Play Mode: el swing en si (la corrutina
// SwingCompleto, que depende de Time.deltaTime avanzando frame a frame) y el Physics.OverlapSphere
// de la ventana activa (PalparObjetivos), que necesita una escena de fisica real. Mismo criterio y
// mismo motivo que BallestaSelfTest usa con el vuelo de la flecha.
//
// Batch: Unity.exe -batchmode -nographics -projectPath <ruta> -executeMethod EspadaSelfTest.RunAllAndExit -logFile <log>
public static class EspadaSelfTest
{
    const string Tag = "[EspadaSelfTest]";

    static int passed;
    static int failed;

    [MenuItem("Between Metals/Tests/Espada (arco de ataque)")]
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

        var temporales = new System.Collections.Generic.List<GameObject>();

        try
        {
            Run("CP-ESP-01", "Un objetivo de frente y dentro del alcance cae dentro del arco", () =>
            {
                bool dentro = Espada.DentroDelArco(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 1.5f), 1.8f, 100f);
                return dentro ? null : "un objetivo justo de frente, mas cerca que el alcance, quedo fuera del arco";
            });

            Run("CP-ESP-02", "Un objetivo mas lejos que el alcance queda fuera, aunque este de frente", () =>
            {
                bool dentro = Espada.DentroDelArco(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 3f), 1.8f, 100f);
                return dentro ? "un objetivo a 3 m con alcance 1.8 quedo adentro del arco" : null;
            });

            Run("CP-ESP-03", "Un objetivo de costado (90 grados) queda fuera del cono aunque este en rango", () =>
            {
                // Cono total de 100 grados: a 90 grados del frente queda bien afuera de los 50 de
                // cada lado.
                bool dentro = Espada.DentroDelArco(Vector3.zero, Vector3.forward, new Vector3(1.5f, 0f, 0f), 1.8f, 100f);
                return dentro ? "un objetivo a 90 grados del frente quedo dentro de un cono de 100 grados" : null;
            });

            Run("CP-ESP-04", "Un objetivo encimado con el origen (distancia ~0) cae dentro del arco", () =>
            {
                // Sin esto, la direccion hacia el objetivo seria el vector cero y Vector3.Angle no
                // tendria nada que medir: tiene que darse por dentro, no por fuera.
                bool dentro = Espada.DentroDelArco(Vector3.zero, Vector3.forward, new Vector3(0f, 0.00001f, 0f), 1.8f, 100f);
                return dentro ? null : "un objetivo practicamente encimado quedo fuera del arco";
            });

            Run("CP-ESP-05", "El propio CharacterController del jugador nunca es un objetivo valido", () =>
            {
                GameObject cuerpo = NuevoGO("CP_ESP_Cuerpo", temporales);
                CharacterController cc = cuerpo.AddComponent<CharacterController>();

                GameObject camaraGO = NuevoGO("CP_ESP_Camara", temporales);
                camaraGO.transform.SetParent(cuerpo.transform, false);

                GameObject mano = NuevoGO("CP_ESP_Mano", temporales);
                mano.transform.SetParent(camaraGO.transform, false);

                GameObject armaGO = NuevoGO("CP_ESP_Arma", temporales);
                armaGO.transform.SetParent(mano.transform, false);
                Espada espada = armaGO.AddComponent<Espada>();

                // CharacterController hereda de Collider, así que es, literalmente, el Collider que
                // el SphereCast/OverlapSphere del jugador podria llegar a tocar.
                bool esDelJugador = (bool)Invocar(espada, "EsDelJugador", cc);
                return esDelJugador ? null : "el CharacterController del propio jugador no se reconocio como 'del jugador'";
            });

            Run("CP-ESP-06", "Un Collider ajeno (un enemigo cualquiera) SI puede ser objetivo", () =>
            {
                GameObject cuerpo = NuevoGO("CP_ESP_Cuerpo2", temporales);
                cuerpo.AddComponent<CharacterController>();

                GameObject camaraGO = NuevoGO("CP_ESP_Camara2", temporales);
                camaraGO.transform.SetParent(cuerpo.transform, false);

                GameObject mano = NuevoGO("CP_ESP_Mano2", temporales);
                mano.transform.SetParent(camaraGO.transform, false);

                GameObject armaGO = NuevoGO("CP_ESP_Arma2", temporales);
                armaGO.transform.SetParent(mano.transform, false);
                Espada espada = armaGO.AddComponent<Espada>();

                GameObject enemigoGO = NuevoGO("CP_ESP_Enemigo", temporales);
                CapsuleCollider capsula = enemigoGO.AddComponent<CapsuleCollider>();

                bool esDelJugador = (bool)Invocar(espada, "EsDelJugador", capsula);
                return esDelJugador ? "un collider de un enemigo sin relacion se reconocio como 'del jugador'" : null;
            });

            Run("CP-ESP-07", "Una espada recien agregada no esta atacando todavia", () =>
            {
                GameObject armaGO = NuevoGO("CP_ESP_ArmaSola", temporales);
                Espada espada = armaGO.AddComponent<Espada>();

                return espada.EstaAtacando ? "EstaAtacando dio true sin haber arrancado ningun golpe" : null;
            });
        }
        finally
        {
            foreach (GameObject go in temporales)
            {
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            }
        }

        Debug.Log($"{Tag} RESULT: {passed} passed, {failed} failed");
        return failed == 0;
    }

    static GameObject NuevoGO(string nombre, System.Collections.Generic.List<GameObject> temporales)
    {
        var go = new GameObject(nombre);
        temporales.Add(go);
        return go;
    }

    const BindingFlags Privados = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    static object Invocar(object objetivo, string metodo, params object[] args)
    {
        MethodInfo info = objetivo.GetType().GetMethod(metodo, Privados);
        if (info == null) throw new MissingMethodException(objetivo.GetType().Name, metodo);

        try
        {
            return info.Invoke(objetivo, args);
        }
        catch (TargetInvocationException e) when (e.InnerException != null)
        {
            throw e.InnerException;
        }
    }

    static void Run(string id, string description, Func<string> test)
    {
        string error;
        try
        {
            error = test();
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
}
