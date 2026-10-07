using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Autotest de editor de los efectos de usar un ítem (RF06/HU-05). Casos CP-EFFECT-01..10.
// No abre ni guarda escenas y no crea assets en disco: todo lo que crea vive en memoria con
// HideAndDontSave y se destruye al terminar cada caso.
//
// El armado reproduce la jerarquía real de Prototype.unity (Inventory en el objeto "Player" y
// PlayerStats en su hijo "PlayerController") para que los casos ejerciten de verdad la resolución
// de referencias de EfectosDeItem, en vez de inyectárselas.
//
// Batch: Unity.exe -batchmode -nographics -projectPath <ruta> -executeMethod EfectosDeItemSelfTest.RunAllAndExit -logFile <log>
public static class EfectosDeItemSelfTest
{
    const string Tag = "[EfectosDeItemSelfTest]";
    const float Tolerancia = 0.001f;

    static int passed;
    static int failed;

    [MenuItem("Between Metals/Tests/Efectos de Items")]
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

        Run("CP-EFFECT-01", "Con la vida reducida, usar una poción la sube exactamente lo que cura la poción", f =>
        {
            f.Armar();
            f.stats.TakeDamage(60f); // 100 -> 40

            ItemData pocion = f.Item(EfectosDeItem.IdPocionDeVida);
            f.inv.AddItem(pocion);
            f.inv.SelectItem(0);

            if (!f.inv.UseSelected()) return "UseSelected devolvió false";

            return f.VidaEs(40f + f.efectos.CuracionPocion);
        });

        Run("CP-EFFECT-02", "Con la vida cerca del máximo, usar una poción no la pasa de maxHealth", f =>
        {
            f.Armar();
            f.stats.TakeDamage(5f); // 100 -> 95, y la poción cura 40

            ItemData pocion = f.Item(EfectosDeItem.IdPocionDeVida);
            f.inv.AddItem(pocion);
            f.inv.SelectItem(0);

            if (!f.inv.UseSelected()) return "UseSelected devolvió false";

            return f.VidaEs(f.stats.MaxHealth);
        });

        Run("CP-EFFECT-03", "Con 2 pociones, usar una deja exactamente 1 y cura una sola dosis", f =>
        {
            f.Armar();
            f.stats.TakeDamage(60f); // 100 -> 40

            ItemData pocion = f.Item(EfectosDeItem.IdPocionDeVida);
            f.inv.AddItem(pocion);
            f.inv.AddItem(pocion);
            f.inv.SelectItem(0);

            if (!f.inv.UseSelected()) return "UseSelected devolvió false";

            if (f.inv.CountOf(pocion) != 1) return $"quedaron {f.inv.CountOf(pocion)} pociones (se esperaba 1)";
            if (f.inv.Count != 1) return $"el inventario quedó con {f.inv.Count} ítems (se esperaba 1)";

            return f.VidaEs(40f + f.efectos.CuracionPocion);
        });

        Run("CP-EFFECT-04", "Usar una ración de comida sube la vida lo que cura la ración, y se consume", f =>
        {
            f.Armar();
            f.stats.TakeDamage(60f); // 100 -> 40

            ItemData racion = f.Item(EfectosDeItem.IdRacionDeComida);
            f.inv.AddItem(racion);
            f.inv.SelectItem(0);

            if (!f.inv.UseSelected()) return "UseSelected devolvió false";
            if (f.inv.HasItem(racion)) return "la ración no se consumió";

            return f.VidaEs(40f + f.efectos.CuracionRacion);
        });

        Run("CP-EFFECT-05", "Un ítem sin efecto conocido no lanza excepción ni toca la vida", f =>
        {
            f.Armar();
            f.stats.TakeDamage(60f); // 100 -> 40

            // consumeOnUse = false, como el ItemData real de la Antorcha.
            ItemData antorcha = f.Item("antorcha", consumeOnUse: false);
            f.inv.AddItem(antorcha);

            ItemData sinId = f.Item(string.Empty);
            f.inv.AddItem(sinId);

            f.inv.SelectItem(0);
            if (!f.inv.UseSelected()) return "UseSelected de la antorcha devolvió false";
            if (!f.inv.HasItem(antorcha)) return "la antorcha se consumió aunque consumeOnUse es false";

            f.inv.SelectItem(1);
            if (!f.inv.UseSelected()) return "UseSelected del ítem sin itemId devolvió false";

            return f.VidaEs(40f);
        });

        Run("CP-EFFECT-06", "Con el jugador muerto, usar una poción no lo revive", f =>
        {
            f.Armar();
            f.stats.TakeDamage(999f); // 100 -> 0
            if (f.stats.EstaViva) return "el jugador sigue vivo después de 999 de daño: el caso no prueba nada";

            ItemData pocion = f.Item(EfectosDeItem.IdPocionDeVida);
            f.inv.AddItem(pocion);
            f.inv.SelectItem(0);

            if (!f.inv.UseSelected()) return "UseSelected devolvió false";
            if (f.stats.EstaViva) return "la poción revivió al jugador";

            return f.VidaEs(0f);
        });

        Run("CP-EFFECT-07", "Reconectar() varias veces no duplica el listener: una poción cura una sola vez", f =>
        {
            f.Armar();
            f.stats.TakeDamage(60f); // 100 -> 40

            // Simula reinstalaciones/recargas: cada una vuelve a resolver y a suscribirse.
            f.efectos.Reconectar();
            f.efectos.Reconectar();
            f.efectos.Reconectar();

            if (f.efectos.InventarioSuscrito != f.inv) return "quedó suscrito a otro inventario (o a ninguno)";

            ItemData pocion = f.Item(EfectosDeItem.IdPocionDeVida);
            f.inv.AddItem(pocion);
            f.inv.SelectItem(0);

            if (!f.inv.UseSelected()) return "UseSelected devolvió false";

            // Con el handler registrado dos veces la vida saltaría a 100 (40 + 40 + 40, recortado).
            return f.VidaEs(40f + f.efectos.CuracionPocion);
        });

        Run("CP-EFFECT-08", "EnsureExists() dos veces deja un solo EfectosDeItem, sobre el objeto del Inventory", f =>
        {
            InstalacionFixture fx = null;
            try
            {
                fx = new InstalacionFixture();
                if (fx.NoSePuedeValidar(out string motivo)) return motivo;

                Inventory inventario = fx.CrearInventoryVisible();

                EfectosDeItem.EnsureExists();
                EfectosDeItem.EnsureExists();

                EfectosDeItem[] nuevos = fx.NuevosEfectos();
                if (nuevos.Length != 1) return $"hay {nuevos.Length} EfectosDeItem (se esperaba 1)";
                if (nuevos[0].gameObject != inventario.gameObject)
                    return $"se instaló en '{nuevos[0].gameObject.name}' y no en el objeto del Inventory";

                return null;
            }
            finally
            {
                fx?.Destroy();
            }
        });

        Run("CP-EFFECT-09", "Desconectar() deja de aplicar efectos, pero el consumo del inventario sigue igual", f =>
        {
            f.Armar();
            f.stats.TakeDamage(60f); // 100 -> 40

            f.efectos.Desconectar();
            if (f.efectos.InventarioSuscrito != null) return "InventarioSuscrito no quedó en null";

            ItemData pocion = f.Item(EfectosDeItem.IdPocionDeVida);
            f.inv.AddItem(pocion);
            f.inv.SelectItem(0);

            if (!f.inv.UseSelected()) return "UseSelected devolvió false";
            if (f.inv.HasItem(pocion)) return "el consumo del inventario dejó de funcionar al desconectarse";

            return f.VidaEs(40f);
        });

        Run("CP-EFFECT-10", "CuracionDe mapea los dos itemId conocidos (con espacios y otra capitalización) y 0 para el resto", f =>
        {
            f.Armar();
            EfectosDeItem e = f.efectos;

            if (!Cerca(e.CuracionDe(EfectosDeItem.IdPocionDeVida), e.CuracionPocion))
                return $"CuracionDe(\"{EfectosDeItem.IdPocionDeVida}\") = {e.CuracionDe(EfectosDeItem.IdPocionDeVida)}";
            if (!Cerca(e.CuracionDe(EfectosDeItem.IdRacionDeComida), e.CuracionRacion))
                return $"CuracionDe(\"{EfectosDeItem.IdRacionDeComida}\") = {e.CuracionDe(EfectosDeItem.IdRacionDeComida)}";
            if (!Cerca(e.CuracionDe("  pocion_vida  "), e.CuracionPocion))
                return "CuracionDe no toleró espacios alrededor del itemId";
            if (!Cerca(e.CuracionDe("Pocion_Vida"), e.CuracionPocion))
                return "CuracionDe no toleró otra capitalización del itemId";

            if (!Cerca(e.CuracionDe("antorcha"), 0f)) return "CuracionDe(\"antorcha\") no devolvió 0";
            if (!Cerca(e.CuracionDe(string.Empty), 0f)) return "CuracionDe(\"\") no devolvió 0";
            if (!Cerca(e.CuracionDe(null), 0f)) return "CuracionDe(null) no devolvió 0";

            return null;
        });

        Debug.Log($"{Tag} RESULT: {passed} passed, {failed} failed");
        return failed == 0;
    }

    static bool Cerca(float a, float b) => Mathf.Abs(a - b) <= Tolerancia;

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

    // Jerarquía igual a la de Prototype.unity: "Player" (Inventory + EfectosDeItem) > "PlayerController"
    // (PlayerStats). Nada se inyecta por Inspector: así los casos prueban también cómo EfectosDeItem
    // encuentra el Inventory (GetComponent) y el PlayerStats (GetComponentInChildren).
    class Fixture
    {
        readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();

        public Inventory inv;
        public PlayerStats stats;
        public EfectosDeItem efectos;

        public void Armar(float curacionPocion = 40f, float curacionRacion = 15f)
        {
            GameObject jugador = NewGameObject("CP_EFFECT_Player");
            inv = jugador.AddComponent<Inventory>();

            GameObject cuerpo = new GameObject("CP_EFFECT_PlayerController") { hideFlags = HideFlags.HideAndDontSave };
            cuerpo.transform.SetParent(jugador.transform);
            stats = cuerpo.AddComponent<PlayerStats>();

            // AddComponent no dispara Awake() de forma sincrónica fuera de Play Mode (mismo problema
            // que documentan GameManagerSelfTest CP-GM-09 y EnemyCombatAudioSelfTest): sin esto
            // currentHealth se queda en 0 y TakeDamage/Curar se irían por el guard de EstaViva.
            InvocarPrivado(stats, "Awake");

            efectos = jugador.AddComponent<EfectosDeItem>();

            // Las cantidades se fijan por SerializedObject para que los casos no dependan de los
            // valores por defecto del componente (mismo criterio que SenalAmbientalSelfTest).
            SerializedObject so = new SerializedObject(efectos);
            RequireProperty(so, "curacionPocion").floatValue = curacionPocion;
            RequireProperty(so, "curacionRacion").floatValue = curacionRacion;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Reconectar() es parte de la API pública justamente para esto: fuera de Play Mode
            // Unity no llama OnEnable al agregar el componente.
            efectos.Reconectar();
        }

        public ItemData Item(string id, bool consumeOnUse = true)
        {
            ItemData item = ScriptableObject.CreateInstance<ItemData>();
            item.hideFlags = HideFlags.HideAndDontSave;
            item.name = string.IsNullOrEmpty(id) ? "sin_id" : id;
            item.itemId = id;
            item.itemName = item.name;
            item.consumeOnUse = consumeOnUse;
            created.Add(item);
            return item;
        }

        public string VidaEs(float esperada)
        {
            return Cerca(stats.CurrentHealth, esperada)
                ? null
                : $"CurrentHealth = {stats.CurrentHealth} (se esperaba {esperada})";
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

    // Soporte de CP-EFFECT-08: EnsureExists() busca con FindAnyObjectByType, que no devuelve objetos
    // HideAndDontSave, así que este caso necesita objetos visibles (hideFlags = None). Mide por
    // diferencia contra una línea base para no asumir que la escena de la corrida batch está vacía.
    class InstalacionFixture
    {
        readonly List<GameObject> created = new List<GameObject>();
        readonly HashSet<EfectosDeItem> baseline;

        public InstalacionFixture()
        {
            baseline = new HashSet<EfectosDeItem>(UnityEngine.Object.FindObjectsByType<EfectosDeItem>(FindObjectsInactive.Include));
        }

        // Si la escena de la corrida ya trae un Inventory o un EfectosDeItem, EnsureExists podría
        // engancharse a ese y el caso no mediría lo que dice medir.
        public bool NoSePuedeValidar(out string motivo)
        {
            if (UnityEngine.Object.FindAnyObjectByType<Inventory>() != null)
            {
                motivo = "la escena de la corrida batch ya tiene un Inventory; este caso no se puede validar aquí";
                return true;
            }

            if (baseline.Count > 0)
            {
                motivo = "la escena de la corrida batch ya tiene un EfectosDeItem; este caso no se puede validar aquí";
                return true;
            }

            motivo = null;
            return false;
        }

        public Inventory CrearInventoryVisible()
        {
            GameObject go = new GameObject("CP_EFFECT_InstalacionPlayer") { hideFlags = HideFlags.None };
            created.Add(go);
            return go.AddComponent<Inventory>();
        }

        public EfectosDeItem[] NuevosEfectos()
        {
            List<EfectosDeItem> lista = new List<EfectosDeItem>();
            foreach (EfectosDeItem e in UnityEngine.Object.FindObjectsByType<EfectosDeItem>(FindObjectsInactive.Include))
            {
                if (!baseline.Contains(e)) lista.Add(e);
            }
            return lista.ToArray();
        }

        public void Destroy()
        {
            foreach (EfectosDeItem e in NuevosEfectos())
            {
                if (e != null) UnityEngine.Object.DestroyImmediate(e.gameObject);
            }
            foreach (GameObject go in created)
            {
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
