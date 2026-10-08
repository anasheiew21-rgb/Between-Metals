using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Autotest de editor de la lógica de la interfaz del inventario (RF06, HU-05, #16).
// Casos CP-UI-01..13 sobre InventoryPanelState, con tiempo simulado.
// No abre ni guarda escenas y no crea assets en disco.
// Batch: Unity.exe -batchmode -nographics -projectPath <ruta> -executeMethod InventoryUISelfTest.RunAllAndExit -logFile <log>
public static class InventoryUISelfTest
{
    const string Tag = "[InventoryUISelfTest]";
    const string KeyLabel = "Tab";

    static int passed;
    static int failed;

    [MenuItem("Between Metals/Tests/Interfaz Inventario")]
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

        Run("CP-UI-01", "Toggle abre el panel y un segundo Toggle lo cierra", f =>
        {
            InventoryPanelState st = f.State();
            if (!st.Toggle() || !st.IsOpen) return "el primer Toggle no abrió";
            if (st.Toggle() || st.IsOpen) return "el segundo Toggle no cerró";
            return null;
        });

        Run("CP-UI-02", "Con el panel cerrado, SelectSlot devuelve false y no cambia la selección", f =>
        {
            InventoryPanelState st = f.State("A", "B", "C");
            if (st.SelectSlot(1)) return "SelectSlot devolvió true";
            if (f.inv.GetSelectedItem() != null) return "cambió la selección";
            return null;
        });

        Run("CP-UI-03", "Con el panel abierto y 3 ítems, SelectSlot(1) selecciona el segundo", f =>
        {
            InventoryPanelState st = f.State("A", "B", "C");
            st.Toggle();
            if (!st.SelectSlot(1)) return "SelectSlot devolvió false";
            if (f.inv.GetSelectedItem() != f.items[1]) return "no quedó seleccionado el segundo ítem";
            if (st.SelectedIndex != 1) return $"SelectedIndex = {st.SelectedIndex}";
            return null;
        });

        Run("CP-UI-04", "SelectSlot sobre un lugar vacío (5, con 3 ítems) devuelve false", f =>
        {
            InventoryPanelState st = f.State("A", "B", "C");
            st.Toggle();
            if (st.SelectSlot(5)) return "SelectSlot(5) devolvió true";
            if (f.inv.GetSelectedItem() != null) return "cambió la selección";
            return null;
        });

        Run("CP-UI-05", "Cycle(+1) sin selección va al primero y desde el último vuelve al primero", f =>
        {
            InventoryPanelState st = f.State("A", "B", "C");
            st.Toggle();
            if (!st.Cycle(1) || f.inv.GetSelectedItem() != f.items[0]) return "Cycle(+1) sin selección no fue al primero";
            st.SelectSlot(2);
            if (!st.Cycle(1) || f.inv.GetSelectedItem() != f.items[0]) return "Cycle(+1) desde el último no volvió al primero";
            return null;
        });

        Run("CP-UI-06", "Cycle(-1) sin selección va al último", f =>
        {
            InventoryPanelState st = f.State("A", "B", "C");
            st.Toggle();
            if (!st.Cycle(-1)) return "Cycle(-1) devolvió false";
            return f.inv.GetSelectedItem() == f.items[2] ? null : "no fue al último";
        });

        Run("CP-UI-07", "Cycle con inventario vacío devuelve false sin excepción", f =>
        {
            InventoryPanelState st = f.State();
            st.Toggle();
            if (st.Cycle(1)) return "Cycle(+1) devolvió true";
            if (st.Cycle(-1)) return "Cycle(-1) devolvió true";
            return null;
        });

        Run("CP-UI-08", "UseSelected con consumeOnUse=true quita el ítem y avisa \"Usaste <nombre>\"", f =>
        {
            InventoryPanelState st = f.State("A", "B");
            st.Toggle();
            st.SelectSlot(0);
            if (!st.UseSelected(5f)) return "UseSelected devolvió false";
            if (f.inv.Count != 1 || f.inv.HasItem(f.items[0])) return $"el ítem no se quitó (Count = {f.inv.Count})";
            string msg = st.GetMessage(5.5f);
            return msg == "Usaste A" ? null : $"mensaje = '{msg}'";
        });

        Run("CP-UI-09", "SetBlocked(true) cierra el panel y Toggle devuelve false mientras siga bloqueado", f =>
        {
            InventoryPanelState st = f.State("A");
            st.Toggle();
            st.SetBlocked(true);
            if (st.IsOpen) return "el panel sigue abierto";
            if (st.Toggle() || st.IsOpen) return "Toggle abrió estando bloqueado";
            st.SetBlocked(false);
            if (!st.Toggle()) return "Toggle no abrió al desbloquear";
            return null;
        });

        Run("CP-UI-10", "Con el panel bloqueado, SelectSlot, Cycle y UseSelected no hacen nada", f =>
        {
            InventoryPanelState st = f.State("A", "B");
            st.Toggle();
            st.SelectSlot(0);
            st.SetBlocked(true);
            if (st.SelectSlot(1)) return "SelectSlot devolvió true";
            if (st.Cycle(1)) return "Cycle devolvió true";
            if (st.UseSelected(1f)) return "UseSelected devolvió true";
            if (f.inv.GetSelectedItem() != f.items[0]) return "cambió la selección";
            if (f.inv.Count != 2) return $"Count = {f.inv.Count}";
            return null;
        });

        Run("CP-UI-11", "NotifyItemAdded pone la pista de la tecla solo la primera vez", f =>
        {
            InventoryPanelState st = f.State();
            ItemData a = f.Item("A"), b = f.Item("B");
            st.NotifyItemAdded(a, 0f);
            string first = st.GetMessage(0.1f);
            if (first != "Recogiste A — " + KeyLabel + ": inventario") return $"primer mensaje = '{first}'";
            st.NotifyItemAdded(b, 1f);
            string second = st.GetMessage(1.1f);
            return second == "Recogiste B" ? null : $"segundo mensaje = '{second}'";
        });

        Run("CP-UI-12", "GetMessage devuelve el texto antes de 2 s y vacío después", f =>
        {
            InventoryPanelState st = f.State();
            st.NotifyItemAdded(f.Item("A"), 10f);
            if (st.GetMessage(11.9f).Length == 0) return "vacío a los 1.9 s";
            string after = st.GetMessage(12.1f);
            return after.Length == 0 ? null : $"a los 2.1 s sigue '{after}'";
        });

        Run("CP-UI-13", "Después de Unbind, agregar un ítem no cambia el mensaje", f =>
        {
            InventoryPanelState st = f.State();
            f.now = 0f;
            f.inv.AddItem(f.Item("A"));
            string bound = st.GetMessage(0.1f);
            if (!bound.StartsWith("Recogiste A")) return $"antes de Unbind el evento no avisó (mensaje = '{bound}')";

            st.Unbind();
            f.now = 0.5f;
            f.inv.AddItem(f.Item("B"));
            string after = st.GetMessage(0.6f);
            return after == bound ? null : $"el mensaje cambió a '{after}'";
        });

        // CP-UI-14..18: InventoryUI.EnsureExists y la resuscripcion a sceneLoaded (HU-05, #16).
        // A diferencia de los casos de arriba, estos corren contra la escena real de la corrida
        // batch (InventoryUI busca PlayerStats/InventoryUI con FindAnyObjectByType, no recibe
        // nada inyectado), asi que usan UiFixture para medir solo lo que cada caso crea y
        // destruyen exactamente eso al final, sin asumir que la escena estaba vacia.

        Run("CP-UI-14", "con un PlayerStats en la escena y sin InventoryUI, EnsureExists crea exactamente 1 InventoryUI", () =>
        {
            UiFixture fx = null;
            try
            {
                fx = new UiFixture();
                if (fx.CountNewInventoryUI() != 0) return "ya habia un InventoryUI nuevo antes de llamar a EnsureExists";

                fx.CreatePlayerStats();
                InventoryUI.EnsureExists();

                int count = fx.CountNewInventoryUI();
                return count == 1 ? null : $"se crearon {count} InventoryUI (se esperaba 1)";
            }
            finally
            {
                fx?.Destroy();
            }
        });

        Run("CP-UI-15", "llamar EnsureExists dos veces deja 1 solo InventoryUI", () =>
        {
            UiFixture fx = null;
            try
            {
                fx = new UiFixture();
                fx.CreatePlayerStats();
                InventoryUI.EnsureExists();
                InventoryUI.EnsureExists();

                int count = fx.CountNewInventoryUI();
                return count == 1 ? null : $"hay {count} InventoryUI despues de llamar dos veces (se esperaba 1)";
            }
            finally
            {
                fx?.Destroy();
            }
        });

        Run("CP-UI-16", "despues de destruir el InventoryUI, EnsureExists crea uno nuevo", () =>
        {
            UiFixture fx = null;
            try
            {
                fx = new UiFixture();
                fx.CreatePlayerStats();
                InventoryUI.EnsureExists();

                InventoryUI[] first = fx.NewInventoryUIs();
                if (first.Length != 1) return $"setup invalido: {first.Length} InventoryUI antes de destruir (se esperaba 1)";
                UnityEngine.Object.DestroyImmediate(first[0].gameObject);

                InventoryUI.EnsureExists();
                int count = fx.CountNewInventoryUI();
                return count == 1 ? null : $"hay {count} InventoryUI despues de recrear (se esperaba 1)";
            }
            finally
            {
                fx?.Destroy();
            }
        });

        Run("CP-UI-17", "sin PlayerStats en la escena, EnsureExists no crea nada", () =>
        {
            UiFixture fx = null;
            try
            {
                fx = new UiFixture();
                // No se crea ningun PlayerStats: si la escena de la corrida batch ya tuviera uno
                // (por ejemplo si quedo abierta Prototype.unity o InventoryTest.unity), este caso
                // no es representativo y se reporta en vez de dar un resultado enganoso.
                if (UnityEngine.Object.FindAnyObjectByType<PlayerStats>() != null)
                    return "la escena de la corrida batch ya tiene un PlayerStats; este caso no se puede validar aqui";

                InventoryUI.EnsureExists();
                int count = fx.CountNewInventoryUI();
                return count == 0 ? null : $"se crearon {count} InventoryUI sin PlayerStats en la escena";
            }
            finally
            {
                fx?.Destroy();
            }
        });

        Run("CP-UI-18", "invocar el arranque dos veces (simulando dos arranques sin recarga de dominio) no duplica la suscripcion a sceneLoaded", () =>
        {
            // Disparar un sceneLoaded real de forma confiable en modo batch, sin guardar ni
            // cargar una escena, no es viable aca: en su lugar se verifica, por reflexion, que el
            // campo interno que respalda el evento SceneManager.sceneLoaded solo contiene la
            // suscripcion de InventoryUI una vez despues de invocar el arranque (AutoCrear, que
            // hace -= seguido de += antes de suscribirse) dos veces seguidas.
            FieldInfo sceneLoadedField = typeof(SceneManager).GetField("sceneLoaded", BindingFlags.NonPublic | BindingFlags.Static);
            if (sceneLoadedField == null)
                return "no se pudo ubicar por reflexion el campo interno de SceneManager.sceneLoaded (ver nota en el reporte)";

            MethodInfo autoCrear = typeof(InventoryUI).GetMethod("AutoCrear", BindingFlags.NonPublic | BindingFlags.Static);
            if (autoCrear == null) return "no se encontro InventoryUI.AutoCrear por reflexion";

            Delegate original = (Delegate)sceneLoadedField.GetValue(null);
            UiFixture fx = null;
            try
            {
                fx = new UiFixture();
                autoCrear.Invoke(null, null);
                autoCrear.Invoke(null, null);

                Delegate after = (Delegate)sceneLoadedField.GetValue(null);
                int matches = 0;
                if (after != null)
                {
                    foreach (Delegate d in after.GetInvocationList())
                    {
                        if (d.Method.DeclaringType == typeof(InventoryUI)) matches++;
                    }
                }
                return matches == 1 ? null : $"la suscripcion de InventoryUI aparece {matches} veces en SceneManager.sceneLoaded (se esperaba 1)";
            }
            finally
            {
                sceneLoadedField.SetValue(null, original); // deja SceneManager.sceneLoaded como estaba antes del caso
                fx?.Destroy();
            }
        });

        Debug.Log($"{Tag} RESULT: {passed} passed, {failed} failed");
        return failed == 0;
    }

    // Cada caso recibe nada y devuelve null si pasa, o el detalle del fallo. Para CP-UI-14..18,
    // que no usan la Fixture de InventoryPanelState de abajo.
    static void Run(string id, string description, Func<string> test)
    {
        string error;
        try
        {
            error = test();
        }
        catch (Exception e)
        {
            error = $"excepción {e.GetType().Name}: {e.Message}";
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

    // Soporte de CP-UI-14..18: mide y limpia solo los InventoryUI/PlayerStats que cada caso crea,
    // sin asumir que la escena de la corrida batch estaba vacia.
    class UiFixture
    {
        readonly List<GameObject> created = new List<GameObject>();
        readonly HashSet<InventoryUI> baselineUi;

        public UiFixture()
        {
            baselineUi = new HashSet<InventoryUI>(UnityEngine.Object.FindObjectsByType<InventoryUI>(FindObjectsInactive.Include));
        }

        public PlayerStats CreatePlayerStats()
        {
            GameObject go = new GameObject("CP_UI_PlayerStats");
            go.hideFlags = HideFlags.None;
            PlayerStats stats = go.AddComponent<PlayerStats>();
            created.Add(go);
            return stats;
        }

        public InventoryUI[] NewInventoryUIs()
        {
            List<InventoryUI> list = new List<InventoryUI>();
            foreach (InventoryUI ui in UnityEngine.Object.FindObjectsByType<InventoryUI>(FindObjectsInactive.Include))
            {
                if (!baselineUi.Contains(ui)) list.Add(ui);
            }
            return list.ToArray();
        }

        public int CountNewInventoryUI() => NewInventoryUIs().Length;

        public void Destroy()
        {
            foreach (InventoryUI ui in NewInventoryUIs())
            {
                if (ui != null) UnityEngine.Object.DestroyImmediate(ui.gameObject);
            }
            foreach (GameObject go in created)
            {
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            }
        }
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

    class Fixture
    {
        public readonly Inventory inv;
        public readonly List<ItemData> items = new List<ItemData>();
        public float now;

        readonly GameObject go;
        readonly List<ItemData> created = new List<ItemData>();
        InventoryPanelState state;

        public Fixture()
        {
            go = new GameObject("InventoryUISelfTest") { hideFlags = HideFlags.HideAndDontSave };
            inv = go.AddComponent<Inventory>();
        }

        // Agrega los ítems indicados y después crea el estado, para que esos AddItem no generen avisos.
        public InventoryPanelState State(params string[] itemNames)
        {
            foreach (string n in itemNames)
            {
                ItemData item = Item(n);
                items.Add(item);
                inv.AddItem(item);
            }
            state = new InventoryPanelState(inv, KeyLabel, () => now);
            return state;
        }

        public ItemData Item(string itemName)
        {
            ItemData item = ScriptableObject.CreateInstance<ItemData>();
            item.hideFlags = HideFlags.HideAndDontSave;
            item.name = itemName;
            item.itemId = itemName;
            item.itemName = itemName;
            item.consumeOnUse = true;
            created.Add(item);
            return item;
        }

        public void Destroy()
        {
            state?.Dispose();
            foreach (ItemData item in created)
            {
                if (item != null) UnityEngine.Object.DestroyImmediate(item);
            }
            if (go != null) UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
