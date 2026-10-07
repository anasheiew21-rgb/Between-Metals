using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Autotest de editor de las monedas tiradas en el mapa (RF09). Casos CP-COIN-01..12.
// No abre ni guarda escenas y no crea assets en disco: lo que crea vive en memoria con
// HideAndDontSave y se destruye al terminar cada caso. Lo único que lee de disco es el prefab
// Assets/Prefabs/Items/Moneda.prefab, y solo para comprobarlo (nunca lo escribe).
//
// El armado reproduce la jerarquía real de Prototype.unity ("Player" con el Inventory y su hijo
// "PlayerController" con el PlayerStats).
//
// Batch: Unity.exe -batchmode -nographics -projectPath <ruta> -executeMethod MonedaSelfTest.RunAllAndExit -logFile <log>
public static class MonedaSelfTest
{
    const string Tag = "[MonedaSelfTest]";
    const string RutaPrefab = "Assets/Prefabs/Items/Moneda.prefab";

    static int passed;
    static int failed;

    [MenuItem("Between Metals/Tests/Monedas")]
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

        Run("CP-COIN-01", "Con 0 de oro, recoger una moneda de 5 deja el oro en 5", f =>
        {
            f.Armar();
            if (f.stats.Oro != 0) return $"el jugador arrancó con {f.stats.Oro} de oro (se esperaba 0)";

            MonedaPickup moneda = f.NewMoneda(5);
            moneda.Interactuar();

            if (!moneda.Recogida) return "la moneda no quedó marcada como recogida";
            if (moneda.gameObject.activeSelf) return "la moneda sigue activa después de recogerla";

            return f.OroEs(5);
        });

        Run("CP-COIN-02", "Con 5 de oro, recoger una moneda de 10 deja el oro en 15", f =>
        {
            f.Armar();
            f.stats.AgregarOro(5);

            MonedaPickup moneda = f.NewMoneda(10);
            moneda.Interactuar();

            return f.OroEs(15);
        });

        Run("CP-COIN-03", "Interactuar() dos veces sobre la misma moneda paga una sola vez", f =>
        {
            f.Armar();

            MonedaPickup moneda = f.NewMoneda(7);
            moneda.Interactuar();
            moneda.Interactuar();
            moneda.Interactuar();

            if (!string.IsNullOrEmpty(moneda.TextoPrompt)) return $"TextoPrompt sigue mostrando '{moneda.TextoPrompt}' después de recogerla";

            return f.OroEs(7);
        });

        Run("CP-COIN-04", "Recoger una moneda no mete nada en el Inventory", f =>
        {
            f.Armar(conInventario: true);
            if (f.inv.Count != 0) return $"el inventario arrancó con {f.inv.Count} ítems";

            MonedaPickup moneda = f.NewMoneda(5);
            moneda.Interactuar();

            if (f.inv.Count != 0) return $"el inventario quedó con {f.inv.Count} ítems (se esperaba 0)";
            if (f.inv.Items.Count != 0) return $"Items tiene {f.inv.Items.Count} elementos (se esperaba 0)";

            // Que el oro haya subido confirma que la moneda sí se recogió y el caso prueba algo.
            return f.OroEs(5);
        });

        Run("CP-COIN-05", "Sin ningún PlayerStats, recoger no revienta y la moneda queda sin recoger", f =>
        {
            if (UnityEngine.Object.FindAnyObjectByType<PlayerStats>() != null)
                return "la escena de la corrida batch ya tiene un PlayerStats; este caso no se puede validar aquí";

            // Sin stats serializado y sin PlayerStats encontrable: MonedaPickup tiene que avisar por
            // consola y salir. Se silencia el logger solo durante la llamada, porque ese LogError es
            // el comportamiento esperado y si no ensuciaría el log de la corrida.
            MonedaPickup moneda = f.NewMonedaSuelta(5);

            bool logPrevio = Debug.unityLogger.logEnabled;
            Debug.unityLogger.logEnabled = false;
            try
            {
                moneda.Interactuar();
            }
            finally
            {
                Debug.unityLogger.logEnabled = logPrevio;
            }

            if (moneda.Recogida) return "la moneda se marcó como recogida sin haber pagado nada";
            if (!moneda.gameObject.activeSelf) return "la moneda se desactivó sin haber pagado nada";
            if (string.IsNullOrEmpty(moneda.TextoPrompt)) return "TextoPrompt quedó vacío: el cartel no volvería a aparecer";

            return null;
        });

        Run("CP-COIN-06", "Dos monedas independientes pagan cada una su propio valor", f =>
        {
            f.Armar();

            MonedaPickup primera = f.NewMoneda(5);
            MonedaPickup segunda = f.NewMoneda(10);

            primera.Interactuar();
            if (f.stats.Oro != 5) return $"después de la primera el oro era {f.stats.Oro} (se esperaba 5)";
            if (segunda.Recogida) return "recoger la primera marcó también la segunda";

            segunda.Interactuar();

            return f.OroEs(15);
        });

        Run("CP-COIN-07", "Sin referencia asignada, la moneda encuentra el PlayerStats de la jerarquía real del Player", f =>
        {
            EscenaFixture fx = null;
            try
            {
                fx = new EscenaFixture();
                if (fx.NoSePuedeValidar(out string motivo)) return motivo;

                // Player (Inventory) > PlayerController (PlayerStats), igual que Prototype.unity.
                PlayerStats visible = fx.CrearJugadorVisible();

                // La moneda no es hija del jugador y no tiene el campo 'stats' asignado: el único
                // camino posible es FindAnyObjectByType, que es el que usa ResolverStats().
                MonedaPickup moneda = fx.CrearMonedaVisible(9);
                moneda.Interactuar();

                if (!moneda.Recogida) return "la moneda no se recogió: no resolvió el PlayerStats";
                return visible.Oro == 9 ? null : $"Oro = {visible.Oro} (se esperaba 9)";
            }
            finally
            {
                fx?.Destroy();
            }
        });

        Run("CP-COIN-08", "Un valor inválido (0 o negativo) se recorta a 1, en el prompt y en lo que paga", f =>
        {
            f.Armar();

            MonedaPickup cero = f.NewMoneda(0);
            if (cero.Valor != 1) return $"con valor 0, Valor = {cero.Valor} (se esperaba 1)";
            if (!cero.TextoPrompt.Contains("1 de oro")) return $"TextoPrompt = '{cero.TextoPrompt}'";

            cero.Interactuar();
            if (f.stats.Oro != 1) return $"con valor 0 pagó {f.stats.Oro} (se esperaba 1)";

            MonedaPickup negativa = f.NewMoneda(-25);
            if (negativa.Valor != 1) return $"con valor -25, Valor = {negativa.Valor} (se esperaba 1)";

            negativa.Interactuar();

            return f.OroEs(2);
        });

        Run("CP-COIN-09", "AgregarOro dispara AlCambiarOro una sola vez por moneda, con el total nuevo", f =>
        {
            f.Armar();

            int avisos = 0;
            int ultimo = -1;
            Action<int> oyente = o => { avisos++; ultimo = o; };
            f.stats.AlCambiarOro += oyente;
            try
            {
                f.NewMoneda(5).Interactuar();
                if (avisos != 1) return $"AlCambiarOro se disparó {avisos} veces con una moneda (se esperaba 1)";
                if (ultimo != 5) return $"AlCambiarOro informó {ultimo} (se esperaba 5)";

                f.NewMoneda(3).Interactuar();
                if (avisos != 2) return $"AlCambiarOro se disparó {avisos} veces con dos monedas (se esperaba 2)";
                if (ultimo != 8) return $"AlCambiarOro informó {ultimo} (se esperaba 8)";

                // El oyente ya recogido no se vuelve a pagar: no hay un tercer aviso.
                MonedaPickup gastada = f.NewMoneda(100);
                gastada.Interactuar();
                gastada.Interactuar();

                return avisos == 3 ? null : $"AlCambiarOro se disparó {avisos} veces (se esperaba 3)";
            }
            finally
            {
                f.stats.AlCambiarOro -= oyente;
            }
        });

        Run("CP-COIN-10", "PlayerUI.EnsureExists() dos veces deja un solo HUD suscrito al oro", f =>
        {
            EscenaFixture fx = null;
            try
            {
                fx = new EscenaFixture();
                if (fx.NoSePuedeValidar(out string motivo)) return motivo;

                fx.CrearJugadorVisible();

                PlayerUI.EnsureExists();
                PlayerUI.EnsureExists();

                int cantidad = fx.NuevosPlayerUI().Length;
                return cantidad == 1 ? null : $"hay {cantidad} PlayerUI (se esperaba 1)";
            }
            finally
            {
                fx?.Destroy();
            }
        });

        Run("CP-COIN-11", "El prefab Moneda.prefab trae malla, material, Collider y MonedaPickup con valor válido", f =>
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RutaPrefab);
            if (prefab == null) return $"no se pudo cargar '{RutaPrefab}'";

            MonedaPickup pickup = prefab.GetComponent<MonedaPickup>();
            if (pickup == null) return "el prefab no tiene MonedaPickup";
            if (pickup.Valor < 1) return $"Valor = {pickup.Valor}";
            if (pickup.Recogida) return "el prefab viene marcado como recogido";

            if (prefab.GetComponentInChildren<Collider>(true) == null) return "el prefab no tiene Collider: el raycast no lo detectaría";

            MeshFilter filtro = prefab.GetComponentInChildren<MeshFilter>(true);
            if (filtro == null) return "el prefab no tiene MeshFilter";
            if (filtro.sharedMesh == null) return "el MeshFilter no tiene malla: el fileID de la malla built-in es incorrecto";

            MeshRenderer renderer = prefab.GetComponentInChildren<MeshRenderer>(true);
            if (renderer == null) return "el prefab no tiene MeshRenderer";
            if (renderer.sharedMaterial == null) return "el MeshRenderer no tiene material: el guid del material es incorrecto";

            return null;
        });

        Run("CP-COIN-12", "Una instancia del prefab, sin tocarla a mano, paga su oro y se desactiva", f =>
        {
            f.Armar();

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RutaPrefab);
            if (prefab == null) return $"no se pudo cargar '{RutaPrefab}'";

            MonedaPickup moneda = f.Instanciar(prefab);
            if (moneda == null) return "la instancia del prefab no tiene MonedaPickup";

            int esperado = moneda.Valor;
            moneda.Interactuar();

            if (!moneda.Recogida) return "la instancia no se marcó como recogida";
            if (moneda.gameObject.activeSelf) return "la instancia sigue activa";

            return f.OroEs(esperado);
        });

        Debug.Log($"{Tag} RESULT: {passed} passed, {failed} failed");
        return failed == 0;
    }

    // Cada caso recibe objetos nuevos y devuelve null si pasa, o el detalle del fallo.
    static void Run(string id, string description, Func<Fixture, string> test)
    {
        Fixture fixture = null;
        string error;
        try
        {
            fixture = new Fixture();
            error = test(fixture);
        }
        catch (TargetInvocationException e) when (e.InnerException != null)
        {
            error = $"excepción {e.InnerException.GetType().Name}: {e.InnerException.Message}";
        }
        catch (Exception e)
        {
            error = $"excepción {e.GetType().Name}: {e.Message}";
        }
        finally
        {
            fixture?.Destroy();
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

    // Jugador y monedas en memoria (HideAndDontSave). Las monedas reciben el PlayerStats por el campo
    // serializado: FindAnyObjectByType no devuelve objetos HideAndDontSave, así que ese camino se
    // prueba aparte en CP-COIN-07 con EscenaFixture.
    class Fixture
    {
        readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();

        public PlayerStats stats;
        public Inventory inv;

        public void Armar(bool conInventario = false)
        {
            GameObject jugador = NewGameObject("CP_COIN_Player");
            if (conInventario) inv = jugador.AddComponent<Inventory>();

            GameObject cuerpo = new GameObject("CP_COIN_PlayerController") { hideFlags = HideFlags.HideAndDontSave };
            cuerpo.transform.SetParent(jugador.transform);
            stats = cuerpo.AddComponent<PlayerStats>();

            // AddComponent no dispara Awake() de forma sincrónica fuera de Play Mode (mismo problema
            // que documentan GameManagerSelfTest CP-GM-09 y EnemyCombatAudioSelfTest).
            InvocarPrivado(stats, "Awake");
        }

        public MonedaPickup NewMoneda(int valor)
        {
            MonedaPickup moneda = NewMonedaSuelta(valor);

            SerializedObject so = new SerializedObject(moneda);
            RequireProperty(so, "stats").objectReferenceValue = stats;
            so.ApplyModifiedPropertiesWithoutUndo();

            return moneda;
        }

        // Moneda sin PlayerStats asignado, para el caso en el que no hay ninguno en la escena.
        public MonedaPickup NewMonedaSuelta(int valor)
        {
            GameObject go = NewGameObject("CP_COIN_Moneda");
            go.AddComponent<BoxCollider>(); // el raycast real necesita uno; OnValidate avisa si falta
            MonedaPickup moneda = go.AddComponent<MonedaPickup>();

            // [Min(1)] solo actúa en el Inspector, así que por acá entran los valores inválidos que
            // CP-COIN-08 necesita probar.
            SerializedObject so = new SerializedObject(moneda);
            RequireProperty(so, "valor").intValue = valor;
            so.ApplyModifiedPropertiesWithoutUndo();

            return moneda;
        }

        public MonedaPickup Instanciar(GameObject prefab)
        {
            GameObject go = UnityEngine.Object.Instantiate(prefab);
            go.hideFlags = HideFlags.HideAndDontSave;
            created.Add(go);

            MonedaPickup moneda = go.GetComponent<MonedaPickup>();
            if (moneda == null) return null;

            SerializedObject so = new SerializedObject(moneda);
            RequireProperty(so, "stats").objectReferenceValue = stats;
            so.ApplyModifiedPropertiesWithoutUndo();

            return moneda;
        }

        public string OroEs(int esperado)
        {
            return stats.Oro == esperado ? null : $"Oro = {stats.Oro} (se esperaba {esperado})";
        }

        public void Destroy()
        {
            for (int i = created.Count - 1; i >= 0; i--)
            {
                if (created[i] != null) UnityEngine.Object.DestroyImmediate(created[i]);
            }
        }

        GameObject NewGameObject(string goName)
        {
            GameObject go = new GameObject(goName) { hideFlags = HideFlags.HideAndDontSave };
            created.Add(go);
            return go;
        }

        static void InvocarPrivado(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (method == null) throw new InvalidOperationException($"no se encontró {target.GetType().Name}.{methodName} por reflexión");
            method.Invoke(target, null);
        }

        static SerializedProperty RequireProperty(SerializedObject so, string propertyName)
        {
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop == null) throw new InvalidOperationException($"no existe el campo serializado '{propertyName}'");
            return prop;
        }
    }

    // Soporte de CP-COIN-07 y CP-COIN-10: estos casos dependen de FindAnyObjectByType, que no
    // devuelve objetos HideAndDontSave, así que necesitan objetos visibles (hideFlags = None). Mide
    // por diferencia contra una línea base para no asumir que la escena de la corrida está vacía.
    class EscenaFixture
    {
        readonly List<GameObject> created = new List<GameObject>();
        readonly HashSet<PlayerUI> baseline;

        public EscenaFixture()
        {
            baseline = new HashSet<PlayerUI>(UnityEngine.Object.FindObjectsByType<PlayerUI>(FindObjectsInactive.Include));
        }

        public bool NoSePuedeValidar(out string motivo)
        {
            if (UnityEngine.Object.FindAnyObjectByType<PlayerStats>() != null)
            {
                motivo = "la escena de la corrida batch ya tiene un PlayerStats; este caso no se puede validar aquí";
                return true;
            }

            if (baseline.Count > 0)
            {
                motivo = "la escena de la corrida batch ya tiene un PlayerUI; este caso no se puede validar aquí";
                return true;
            }

            motivo = null;
            return false;
        }

        // Misma jerarquía que Prototype.unity: Player (Inventory) > PlayerController (PlayerStats).
        public PlayerStats CrearJugadorVisible()
        {
            GameObject jugador = new GameObject("CP_COIN_VisiblePlayer") { hideFlags = HideFlags.None };
            created.Add(jugador);
            jugador.AddComponent<Inventory>();

            GameObject cuerpo = new GameObject("CP_COIN_VisiblePlayerController") { hideFlags = HideFlags.None };
            cuerpo.transform.SetParent(jugador.transform);
            PlayerStats stats = cuerpo.AddComponent<PlayerStats>();

            MethodInfo awake = typeof(PlayerStats).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance);
            if (awake == null) throw new InvalidOperationException("no se encontró PlayerStats.Awake por reflexión");
            awake.Invoke(stats, null);

            return stats;
        }

        public MonedaPickup CrearMonedaVisible(int valor)
        {
            GameObject go = new GameObject("CP_COIN_VisibleMoneda") { hideFlags = HideFlags.None };
            created.Add(go);
            go.AddComponent<BoxCollider>();
            MonedaPickup moneda = go.AddComponent<MonedaPickup>();

            SerializedObject so = new SerializedObject(moneda);
            SerializedProperty prop = so.FindProperty("valor");
            if (prop == null) throw new InvalidOperationException("no existe el campo serializado 'valor'");
            prop.intValue = valor;
            so.ApplyModifiedPropertiesWithoutUndo();

            return moneda;
        }

        public PlayerUI[] NuevosPlayerUI()
        {
            List<PlayerUI> lista = new List<PlayerUI>();
            foreach (PlayerUI ui in UnityEngine.Object.FindObjectsByType<PlayerUI>(FindObjectsInactive.Include))
            {
                if (!baseline.Contains(ui)) lista.Add(ui);
            }
            return lista.ToArray();
        }

        public void Destroy()
        {
            foreach (PlayerUI ui in NuevosPlayerUI())
            {
                if (ui != null) UnityEngine.Object.DestroyImmediate(ui.gameObject);
            }
            foreach (GameObject go in created)
            {
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
