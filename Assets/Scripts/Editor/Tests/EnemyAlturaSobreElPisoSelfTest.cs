using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

// Self-test de la altura a la que queda el enemigo sobre el piso.
//
// El bug que fija: los enemigos aparecian flotando. El NavMeshAgent ubica el objeto a "superficie
// del NavMesh + baseOffset", asi que baseOffset es LA propiedad que decide la altura — y nadie la
// estaba tocando. EnemyAI sincronizaba el radio y el alto del agente con el cuerpo del enemigo pero
// se olvidaba de la altura, asi que quedaba el valor que Unity deja al agregar el componente (0.5 en
// los enemigos extra de Prototype.unity). Mismo patron de fallo que tenia stoppingDistance.
//
// Lo delicado, y por lo que estos casos valen la pena: el valor correcto NO es el mismo para los dos
// tipos de enemigo que hay en la escena. Un 0 fijo habria enterrado las esferas placeholder y un 0.5
// fijo deja flotando al que tiene modelo. De ahi que se mida en vez de ponerse a mano.
//
// Batch: Unity.exe -batchmode -nographics -projectPath <ruta> -executeMethod EnemyAlturaSobreElPisoSelfTest.RunAllAndExit -logFile <log>
public static class EnemyAlturaSobreElPisoSelfTest
{
    const string Tag = "[EnemyAlturaSobreElPisoSelfTest]";
    const float Tolerancia = 0.001f;

    static int passed;
    static int failed;

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

    [MenuItem("Between Metals/Tests/Enemigo: altura sobre el piso Self-Test")]
    public static void Run()
    {
        RunAll();
    }

    static bool RunAll()
    {
        passed = 0;
        failed = 0;

        var temporales = new List<GameObject>();

        try
        {
            Run("CP-ENE-22", "La esfera placeholder se apoya en el piso: baseOffset = su radio", () =>
            {
                // Los enemigos extra de la escena son una esfera primitiva cuyo PIVOTE es el centro.
                // Apoyarla quiere decir levantarla su radio; es el 0.5 que ya tenian serializado, o
                // sea que el arreglo no se los cambia (no es una regresion para ellos).
                GameObject go = CrearEsfera(temporales, diametro: 1f);
                NavMeshAgent agente = Preparar(go);

                return Cerca(agente.baseOffset, 0.5f, "baseOffset de una esfera de radio 0.5");
            });

            Run("CP-ENE-23", "Una esfera mas grande se apoya igual (el offset no es un numero fijo)", () =>
            {
                // Si alguien hubiera 'arreglado' esto con un 0.5 hardcodeado, este caso lo caza.
                GameObject go = CrearEsfera(temporales, diametro: 2f);
                NavMeshAgent agente = Preparar(go);

                return Cerca(agente.baseOffset, 1f, "baseOffset de una esfera de radio 1");
            });

            Run("CP-ENE-24", "Con el modelo en un hijo corrido hacia abajo, el offset compensa ese desfasaje", () =>
            {
                // Es el caso del enemigo con modelo de la escena: su hijo "Modelo" quedo en y
                // = -0.134, o sea 13 cm por debajo del pivote. Si no se compensara, el enemigo
                // quedaria enterrado esos 13 cm (y con el baseOffset viejo de 0.5, flotando 37 cm).
                GameObject go = CrearConModeloHijo(temporales, desplazamientoDelHijo: -0.134f);
                NavMeshAgent agente = Preparar(go);

                // El hijo es un cubo de 1 m centrado en su pivote: su base esta 0.5 por debajo de
                // su propio origen, que a su vez esta 0.134 por debajo del pivote del enemigo.
                return Cerca(agente.baseOffset, 0.5f + 0.134f, "baseOffset compensando el hijo corrido");
            });

            Run("CP-ENE-25", "ajusteAlturaSobreElPiso corre al enemigo esa cantidad", () =>
            {
                // La valvula de escape para los ultimos centimetros: los bounds de un
                // SkinnedMeshRenderer pueden ser mas grandes que la malla real, y entonces el
                // enemigo queda levantado de mas. Esto permite corregirlo sin tocar codigo.
                GameObject go = CrearEsfera(temporales, diametro: 1f);
                EnemyAI ia = go.GetComponent<EnemyAI>();
                Asignar(ia, "ajusteAlturaSobreElPiso", -0.08f);

                NavMeshAgent agente = Preparar(go);

                return Cerca(agente.baseOffset, 0.5f - 0.08f, "baseOffset con ajuste manual de -8 cm");
            });

            Run("CP-ENE-26", "Un enemigo sin ningun Renderer no se queda con un offset inventado", () =>
            {
                // Sin nada visible que apoyar, es mejor dejar el valor que haya que escribir un 0 a
                // lo bruto: un 0 enterraria a cualquier enemigo cuyo pivote no este en los pies.
                var go = new GameObject("CP_ENE_SinRenderer");
                temporales.Add(go);

                NavMeshAgent agente = go.AddComponent<NavMeshAgent>();
                agente.baseOffset = 0.77f; // valor centinela

                go.AddComponent<EnemyAI>().InicializarNavegacion();

                return Cerca(agente.baseOffset, 0.77f, "baseOffset de un enemigo sin Renderer");
            });

            Run("CP-ENE-27", "La escala del enemigo no descuadra la altura", () =>
            {
                // El enemigo de la escena esta escalado 1.2303 en Y. La medicion se hace en mundo y
                // relativa al pivote justamente para no tener que multiplicar por la escala a mano;
                // este caso verifica que esa cuenta siga saliendo con el objeto escalado.
                GameObject go = CrearEsfera(temporales, diametro: 1f);
                go.transform.localScale = new Vector3(1f, 2f, 1f);

                NavMeshAgent agente = Preparar(go);

                // Una esfera de radio 0.5 escalada x2 en Y mide 1 de radio vertical.
                return Cerca(agente.baseOffset, 1f, "baseOffset de una esfera escalada x2 en Y");
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

    // ------------------------------------------------------------------ escenarios

    // Esfera primitiva con EnemyAI, igual que los enemigos extra que coloca EnemigosExtraBuilder.
    static GameObject CrearEsfera(List<GameObject> temporales, float diametro)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "CP_ENE_Esfera";
        temporales.Add(go);

        go.transform.position = new Vector3(0f, 3f, 0f); // a una altura cualquiera: el offset es relativo
        go.transform.localScale = Vector3.one * diametro;
        go.AddComponent<EnemyAI>();

        return go;
    }

    // Enemigo con el cuerpo en un hijo, como el enemigo con modelo rigueado (padre = navegacion y
    // fisica, hijo = malla).
    static GameObject CrearConModeloHijo(List<GameObject> temporales, float desplazamientoDelHijo)
    {
        var padre = new GameObject("CP_ENE_ConModelo");
        temporales.Add(padre);
        padre.transform.position = new Vector3(0f, 3f, 0f);

        GameObject modelo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        modelo.name = "Modelo";
        modelo.transform.SetParent(padre.transform, false);
        modelo.transform.localPosition = new Vector3(0f, desplazamientoDelHijo, 0f);

        padre.AddComponent<EnemyAI>();
        return padre;
    }

    // Corre la inicializacion de navegacion y devuelve el agente que quedo.
    static NavMeshAgent Preparar(GameObject go)
    {
        go.GetComponent<EnemyAI>().InicializarNavegacion();
        return go.GetComponent<NavMeshAgent>();
    }

    // ------------------------------------------------------------------ helpers

    const BindingFlags Privados = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    static void Asignar(object objetivo, string campo, object valor)
    {
        FieldInfo info = objetivo.GetType().GetField(campo, Privados);
        if (info == null) throw new MissingFieldException(objetivo.GetType().Name, campo);
        info.SetValue(objetivo, valor);
    }

    static string Cerca(float actual, float esperado, string que)
    {
        return Mathf.Abs(actual - esperado) <= Tolerancia
            ? null
            : $"{que}: {actual:0.000} (se esperaba {esperado:0.000})";
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
