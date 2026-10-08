using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Autotest de editor del núcleo del inventario (RF06, HU-05/HU-06). Casos CP-INV-01..14.
// No abre ni guarda escenas y no crea assets en disco: todo lo que crea vive en memoria
// con HideAndDontSave y se destruye al terminar cada caso.
// Batch: Unity.exe -batchmode -nographics -projectPath <ruta> -executeMethod InventorySelfTest.RunAllAndExit -logFile <log>
public static class InventorySelfTest
{
    const string Tag = "[InventorySelfTest]";

    static int passed;
    static int failed;

    [MenuItem("Between Metals/Tests/Inventario")]
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

        Run("CP-INV-01", "AddItem válido agrega y dispara OnItemAdded y OnInventoryChanged en orden", f =>
        {
            ItemData a = f.Item("a");
            f.Listen();
            if (!f.inv.AddItem(a)) return "AddItem devolvió false";
            if (f.inv.Count != 1) return $"Count = {f.inv.Count}";
            return f.Expect("Added:a", "Changed");
        });

        Run("CP-INV-02", "Con capacidad 2 llena, AddItem devuelve false y no dispara eventos", f =>
        {
            f.SetCapacity(2);
            f.inv.AddItem(f.Item("a"));
            f.inv.AddItem(f.Item("b"));
            f.Listen();
            if (f.inv.AddItem(f.Item("c"))) return "AddItem devolvió true";
            if (f.inv.Count != 2) return $"Count = {f.inv.Count}";
            return f.Expect();
        });

        Run("CP-INV-03", "AddItem(null) devuelve false y no dispara eventos", f =>
        {
            f.Listen();
            if (f.inv.AddItem(null)) return "AddItem devolvió true";
            if (f.inv.Count != 0) return $"Count = {f.inv.Count}";
            return f.Expect();
        });

        Run("CP-INV-04", "Items muestra todos los ítems en orden", f =>
        {
            ItemData a = f.Item("a"), b = f.Item("b"), c = f.Item("c");
            f.inv.AddItem(a);
            f.inv.AddItem(b);
            f.inv.AddItem(c);
            IReadOnlyList<ItemData> items = f.inv.Items;
            if (items.Count != 3) return $"Items.Count = {items.Count}";
            if (items[0] != a || items[1] != b || items[2] != c) return "orden incorrecto";
            return null;
        });

        Run("CP-INV-05", "Items no se puede castear a List<ItemData>", f =>
        {
            f.inv.AddItem(f.Item("a"));
            return (f.inv.Items as List<ItemData>) == null ? null : "el cast devolvió la lista interna";
        });

        Run("CP-INV-06", "SelectItem dispara OnSelectionChanged una vez y no al repetir el índice", f =>
        {
            f.inv.AddItem(f.Item("a"));
            f.inv.AddItem(f.Item("b"));
            f.Listen();
            if (!f.inv.SelectItem(1)) return "primer SelectItem devolvió false";
            if (!f.inv.SelectItem(1)) return "SelectItem repetido devolvió false";
            return f.Expect("Sel:1");
        });

        Run("CP-INV-07", "SelectItem fuera de rango devuelve false y no cambia la selección", f =>
        {
            ItemData a = f.Item("a");
            f.inv.AddItem(a);
            f.inv.SelectItem(0);
            f.Listen();
            if (f.inv.SelectItem(5)) return "SelectItem(5) devolvió true";
            if (f.inv.SelectItem(-2)) return "SelectItem(-2) devolvió true";
            if (f.inv.GetSelectedItem() != a) return "la selección cambió";
            return f.Expect();
        });

        Run("CP-INV-08", "RemoveAt del seleccionado deja la selección en -1 y dispara OnSelectionChanged(-1)", f =>
        {
            f.inv.AddItem(f.Item("a"));
            f.inv.AddItem(f.Item("b"));
            f.inv.SelectItem(1);
            f.Listen();
            if (!f.inv.RemoveAt(1)) return "RemoveAt devolvió false";
            if (f.inv.GetSelectedItem() != null) return "sigue habiendo selección";
            return f.Expect("Removed:b", "Sel:-1", "Changed");
        });

        Run("CP-INV-09", "RemoveAt antes de la selección la baja en 1 y mantiene el mismo ítem", f =>
        {
            ItemData a = f.Item("a"), b = f.Item("b"), c = f.Item("c");
            f.inv.AddItem(a);
            f.inv.AddItem(b);
            f.inv.AddItem(c);
            f.inv.SelectItem(2);
            f.Listen();
            if (!f.inv.RemoveAt(0)) return "RemoveAt devolvió false";
            if (f.inv.GetSelectedItem() != c) return "la selección no apunta al mismo ItemData";
            if (f.inv.Items[1] != c) return "c no quedó en el índice 1";
            return f.Expect("Removed:a", "Sel:1", "Changed");
        });

        Run("CP-INV-10", "UseSelected con consumeOnUse=true dispara OnItemUsed y quita el ítem", f =>
        {
            ItemData a = f.Item("a", consumeOnUse: true);
            f.inv.AddItem(a);
            f.inv.SelectItem(0);
            f.Listen();
            if (!f.inv.UseSelected()) return "UseSelected devolvió false";
            if (f.inv.Count != 0) return $"Count = {f.inv.Count}";
            return f.Expect("Used:a", "Removed:a", "Sel:-1", "Changed");
        });

        Run("CP-INV-11", "UseItem con consumeOnUse=false dispara OnItemUsed y no quita el ítem", f =>
        {
            ItemData a = f.Item("a", consumeOnUse: false);
            f.inv.AddItem(a);
            f.Listen();
            if (!f.inv.UseItem(a)) return "UseItem devolvió false";
            if (f.inv.Count != 1 || !f.inv.HasItem(a)) return "el ítem fue quitado";
            return f.Expect("Used:a");
        });

        Run("CP-INV-12", "Capacidad fijada en 0 hace que Capacity devuelva 1", f =>
        {
            f.SetCapacity(0);
            return f.inv.Capacity == 1 ? null : $"Capacity = {f.inv.Capacity}";
        });

        Run("CP-INV-13", "CountOf cuenta repetidos y devuelve 0 para null", f =>
        {
            ItemData a = f.Item("a");
            f.inv.AddItem(a);
            f.inv.AddItem(a);
            f.inv.AddItem(f.Item("b"));
            if (f.inv.CountOf(a) != 2) return $"CountOf(a) = {f.inv.CountOf(a)}";
            if (f.inv.CountOf(null) != 0) return $"CountOf(null) = {f.inv.CountOf(null)}";
            return null;
        });

        Run("CP-INV-14", "ClearSelection sin selección no dispara el evento", f =>
        {
            f.inv.AddItem(f.Item("a"));
            f.Listen();
            f.inv.ClearSelection();
            return f.Expect();
        });

        Debug.Log($"{Tag} RESULT: {passed} passed, {failed} failed");
        return failed == 0;
    }

    // Cada caso recibe un inventario nuevo y devuelve null si pasa, o el detalle del fallo.
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

        readonly GameObject go;
        readonly List<ItemData> createdItems = new List<ItemData>();
        readonly List<string> events = new List<string>();

        public Fixture()
        {
            go = new GameObject("InventorySelfTest") { hideFlags = HideFlags.HideAndDontSave };
            inv = go.AddComponent<Inventory>();
        }

        public ItemData Item(string id, bool consumeOnUse = true)
        {
            ItemData item = ScriptableObject.CreateInstance<ItemData>();
            item.hideFlags = HideFlags.HideAndDontSave;
            item.name = id;
            item.itemId = id;
            item.consumeOnUse = consumeOnUse;
            createdItems.Add(item);
            return item;
        }

        public void SetCapacity(int value)
        {
            SerializedObject so = new SerializedObject(inv);
            SerializedProperty prop = so.FindProperty("capacity");
            if (prop == null) throw new InvalidOperationException("no existe el campo serializado 'capacity'");
            prop.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Empieza a registrar eventos desde este punto, para ignorar los del armado del caso.
        public void Listen()
        {
            inv.OnItemAdded += i => events.Add("Added:" + i.itemId);
            inv.OnItemRemoved += i => events.Add("Removed:" + i.itemId);
            inv.OnItemUsed += i => events.Add("Used:" + i.itemId);
            inv.OnSelectionChanged += s => events.Add("Sel:" + s);
            inv.OnInventoryChanged += () => events.Add("Changed");
        }

        public string Expect(params string[] expected)
        {
            string got = string.Join(", ", events);
            string want = string.Join(", ", expected);
            return got == want ? null : $"eventos esperados [{want}], obtenidos [{got}]";
        }

        public void Destroy()
        {
            foreach (ItemData item in createdItems)
            {
                if (item != null) UnityEngine.Object.DestroyImmediate(item);
            }
            if (go != null) UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
