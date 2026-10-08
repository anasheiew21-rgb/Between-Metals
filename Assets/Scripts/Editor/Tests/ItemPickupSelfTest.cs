using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

// Autotest de editor de la recolección de ítems (RF07, HU-06). Casos CP-PICK-01..08.
// No abre ni guarda escenas y no crea assets en disco: todo vive en memoria y se destruye
// al terminar cada caso.
// Batch: Unity.exe -batchmode -nographics -projectPath <ruta> -executeMethod ItemPickupSelfTest.RunAllAndExit -logFile <log>
public static class ItemPickupSelfTest
{
    const string Tag = "[ItemPickupSelfTest]";
    const string NoInventoryWarning = "no se encontró ningún Inventory";

    static int passed;
    static int failed;

    [MenuItem("Between Metals/Tests/Recoleccion")]
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

        Run("CP-PICK-01", "Con espacio, Interactuar agrega el ítem, desactiva el objeto e invoca onPickedUp una vez", f =>
        {
            Inventory inv = f.NewInventory();
            ItemData a = f.Item("Llave");
            ItemPickup p = f.NewPickup(a, inv);
            p.Interactuar();
            if (inv.Count != 1 || !inv.HasItem(a)) return $"Count = {inv.Count}, HasItem = {inv.HasItem(a)}";
            if (p.gameObject.activeSelf) return "el objeto sigue activo";
            if (f.pickedUpCount != 1) return $"onPickedUp invocado {f.pickedUpCount} veces";
            return null;
        });

        Run("CP-PICK-02", "Con el inventario lleno no agrega, sigue activo, no invoca onPickedUp y avisa en TextoPrompt", f =>
        {
            Inventory inv = f.NewInventory(capacity: 1);
            inv.AddItem(f.Item("Otro"));
            ItemData a = f.Item("Llave");
            ItemPickup p = f.NewPickup(a, inv);
            p.Interactuar();
            if (inv.Count != 1 || inv.HasItem(a)) return $"Count = {inv.Count}, HasItem = {inv.HasItem(a)}";
            if (!p.gameObject.activeSelf) return "el objeto se desactivó";
            if (f.pickedUpCount != 0) return $"onPickedUp invocado {f.pickedUpCount} veces";
            if (p.TextoPrompt != "Inventario lleno") return $"TextoPrompt = '{p.TextoPrompt}'";
            return null;
        });

        Run("CP-PICK-03", "Con espacio, TextoPrompt contiene item.itemName", f =>
        {
            ItemPickup p = f.NewPickup(f.Item("Llave oxidada"), f.NewInventory());
            string prompt = p.TextoPrompt;
            return prompt.Contains("Llave oxidada") ? null : $"TextoPrompt = '{prompt}'";
        });

        Run("CP-PICK-04", "Interactuar dos veces agrega un solo ítem", f =>
        {
            Inventory inv = f.NewInventory();
            ItemPickup p = f.NewPickup(f.Item("Llave"), inv);
            p.Interactuar();
            p.Interactuar();
            if (inv.Count != 1) return $"Count = {inv.Count}";
            if (f.pickedUpCount != 1) return $"onPickedUp invocado {f.pickedUpCount} veces";
            return null;
        });

        Run("CP-PICK-05", "Con item null, Interactuar no lanza excepción ni agrega nada", f =>
        {
            Inventory inv = f.NewInventory();
            ItemPickup p = f.NewPickup(null, inv);
            p.Interactuar();
            if (inv.Count != 0) return $"Count = {inv.Count}";
            if (!p.gameObject.activeSelf) return "el objeto se desactivó";
            if (f.pickedUpCount != 0) return $"onPickedUp invocado {f.pickedUpCount} veces";
            return null;
        });

        Run("CP-PICK-06", "Sin Inventory en la escena ni targetInventory, no lanza excepción, sigue activo y avisa una sola vez", f =>
        {
            Inventory existing = UnityEngine.Object.FindAnyObjectByType<Inventory>();
            if (existing != null) return $"precondición: ya hay un Inventory en la escena ('{existing.name}')";

            ItemPickup p = f.NewPickup(f.Item("Llave"), null);
            int warnings = f.CountLogs(NoInventoryWarning, () =>
            {
                _ = p.TextoPrompt;
                p.Interactuar();
                p.Interactuar();
                _ = p.TextoPrompt;
            });
            if (!p.gameObject.activeSelf) return "el objeto se desactivó";
            if (f.pickedUpCount != 0) return $"onPickedUp invocado {f.pickedUpCount} veces";
            if (warnings != 1) return $"se emitieron {warnings} avisos de inventario faltante (se esperaba 1)";
            return null;
        });

        Run("CP-PICK-07", "Sin targetInventory, encuentra el Inventory de la escena y agrega el ítem", f =>
        {
            // HideFlags.None: objetos normales en la escena temporal del modo batch (nunca se guarda).
            Scene scene = SceneManager.GetActiveScene();
            Debug.Log($"{Tag} CP-PICK-07 escena activa: '{scene.path}' (name '{scene.name}', isLoaded {scene.isLoaded})");

            Inventory inv = f.NewInventory(flags: HideFlags.None);
            ItemData a = f.Item("Llave");
            ItemPickup p = f.NewPickup(a, null, HideFlags.None);

            Inventory found = UnityEngine.Object.FindAnyObjectByType<Inventory>();
            if (found != inv) return $"FindAnyObjectByType<Inventory>() devolvió {(found == null ? "null" : $"'{found.name}'")} en modo edición";

            p.Interactuar();
            if (inv.Count != 1 || !inv.HasItem(a)) return $"Count = {inv.Count}, HasItem = {inv.HasItem(a)}";
            if (p.gameObject.activeSelf) return "el objeto sigue activo";
            return null;
        });

        Run("CP-PICK-08", "Después de recoger, TextoPrompt devuelve cadena vacía", f =>
        {
            ItemPickup p = f.NewPickup(f.Item("Llave"), f.NewInventory());
            p.Interactuar();
            return p.TextoPrompt == string.Empty ? null : $"TextoPrompt = '{p.TextoPrompt}'";
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
        public int pickedUpCount;

        readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();

        public Inventory NewInventory(int capacity = 10, HideFlags flags = HideFlags.HideAndDontSave)
        {
            GameObject go = NewGameObject("SelfTest_Inventory", flags);
            Inventory inv = go.AddComponent<Inventory>();

            SerializedObject so = new SerializedObject(inv);
            RequireProperty(so, "capacity").intValue = capacity;
            so.ApplyModifiedPropertiesWithoutUndo();
            return inv;
        }

        public ItemData Item(string itemName)
        {
            ItemData item = ScriptableObject.CreateInstance<ItemData>();
            item.hideFlags = HideFlags.HideAndDontSave;
            item.name = itemName;
            item.itemId = itemName;
            item.itemName = itemName;
            created.Add(item);
            return item;
        }

        public ItemPickup NewPickup(ItemData item, Inventory target, HideFlags flags = HideFlags.HideAndDontSave)
        {
            GameObject go = NewGameObject("SelfTest_Pickup", flags);
            go.AddComponent<BoxCollider>();
            ItemPickup pickup = go.AddComponent<ItemPickup>();

            SerializedObject so = new SerializedObject(pickup);
            RequireProperty(so, "item").objectReferenceValue = item;
            RequireProperty(so, "targetInventory").objectReferenceValue = target;
            so.ApplyModifiedPropertiesWithoutUndo();

            // onPickedUp es privado (se conecta desde el Inspector); el test lo lee por reflexión.
            FieldInfo field = typeof(ItemPickup).GetField("onPickedUp", BindingFlags.Instance | BindingFlags.NonPublic);
            if (!(field?.GetValue(pickup) is UnityEvent onPickedUp))
                throw new InvalidOperationException("no se pudo leer el UnityEvent 'onPickedUp'");
            onPickedUp.AddListener(() => pickedUpCount++);

            return pickup;
        }

        // Cuenta los mensajes de log que contienen el texto mientras corre la acción.
        public int CountLogs(string contains, Action action)
        {
            int count = 0;
            Application.LogCallback handler = (message, stackTrace, type) =>
            {
                if (message.Contains(contains)) count++;
            };

            Application.logMessageReceived += handler;
            try
            {
                action();
            }
            finally
            {
                Application.logMessageReceived -= handler;
            }
            return count;
        }

        public void Destroy()
        {
            for (int i = created.Count - 1; i >= 0; i--)
            {
                if (created[i] != null) UnityEngine.Object.DestroyImmediate(created[i]);
            }
        }

        GameObject NewGameObject(string goName, HideFlags flags)
        {
            GameObject go = new GameObject(goName) { hideFlags = flags };
            created.Add(go);
            return go;
        }

        static SerializedProperty RequireProperty(SerializedObject so, string propertyName)
        {
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop == null) throw new InvalidOperationException($"no existe el campo serializado '{propertyName}'");
            return prop;
        }
    }
}
