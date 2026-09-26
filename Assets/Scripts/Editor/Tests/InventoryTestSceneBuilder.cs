using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Escena de prueba aislada del inventario, la recoleccion y la interfaz (HU-05 #16, HU-06 #20 #21).
// No abre ni toca Prototype.unity ni ningun script existente: genera su propio prefab base de
// item recogible, sus propios ItemData de prueba y una escena separada en Assets/Tests/Inventario/,
// con un jugador replicado a partir de la jerarquia real de Prototype.unity (un solo
// CharacterController, en el objeto que PlayerController.cs usa con GetComponent).
// Batch: Unity.exe -batchmode -nographics -projectPath <ruta> -executeMethod InventoryTestSceneBuilder.BuildAndExit -logFile <log>
public static class InventoryTestSceneBuilder
{
    const string Tag = "[InventoryTestSceneBuilder]";

    // No es 'const': se compara en tiempo de ejecucion contra la ruta requerida antes de
    // guardar, para no depender de que nadie rompa esa comparacion en un futuro refactor.
    static readonly string ScenePath = "Assets/Tests/Inventario/InventoryTest.unity";
    const string RequiredScenePath = "Assets/Tests/Inventario/InventoryTest.unity";

    const string ItemsFolder = "Assets/Tests/Inventario/Items";
    const string PrefabFolder = "Assets/Prefabs/Items";
    const string PrefabPath = PrefabFolder + "/ItemPickup_Base.prefab";

    [MenuItem("Between Metals/Tests/Crear escena de prueba de inventario")]
    static void RunFromMenu()
    {
        Build();
    }

    public static void BuildAndExit()
    {
        bool ok = false;
        try
        {
            ok = Build();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }

        if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
    }

    static bool Build()
    {
        if (ScenePath != RequiredScenePath)
        {
            Debug.LogError($"{Tag} ruta de escena inesperada: '{ScenePath}'");
            return false;
        }

        GameObject prefab = BuildBasePrefab();

        string llavePath = CreateOrUpdateItem("TEST_Llave", "test_llave", "[TEST] Llave", false,
            "Ítem de prueba, no se consume al usarlo.");
        string vendaPath = CreateOrUpdateItem("TEST_Venda", "test_venda", "[TEST] Venda", true,
            "Ítem de prueba consumible.");
        string pilaPath = CreateOrUpdateItem("TEST_Pila", "test_pila", "[TEST] Pila", true,
            "Ítem de prueba consumible.");
        string monedaPath = CreateOrUpdateItem("TEST_Moneda", "test_moneda", "[TEST] Moneda", true,
            "Ítem de prueba para llenar el inventario.");

        // Se fuerza la importacion de los .asset recien creados antes de referenciarlos: si se
        // los sigue usando por la referencia en memoria de CreateAsset, un reimport disparado por
        // NewScene/InstantiatePrefab puede dejarla obsoleta y el campo 'item' queda sin asignar.
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        BuildFloor();
        BuildLight();
        BuildPlayer();
        BuildMenu();

        SpawnPickup(prefab, LoadItem(llavePath), new Vector3(-1.5f, 0.5f, 3f), "Pickup_Llave");
        SpawnPickup(prefab, LoadItem(vendaPath), new Vector3(1.5f, 0.5f, 5f), "Pickup_Venda");
        SpawnPickup(prefab, LoadItem(pilaPath), new Vector3(-1.5f, 0.5f, 7f), "Pickup_Pila");
        SpawnPickup(prefab, LoadItem(monedaPath), new Vector3(1.5f, 0.5f, 9f), "Pickup_Moneda");

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
        {
            Debug.LogError($"{Tag} no se pudo guardar la escena en '{ScenePath}'");
            return false;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return Validate();
    }

    // ---------------------------------------------------------------
    // Prefab base
    // ---------------------------------------------------------------
    static GameObject BuildBasePrefab()
    {
        EnsureFolder(PrefabFolder);

        GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        temp.name = "ItemPickup_Base";
        temp.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        temp.layer = 0; // Default

        // item vacio a proposito: es una plantilla. OnValidate va a avisar "falta asignar el
        // ItemData"; es esperado y no se corrige aca.
        temp.AddComponent<ItemPickup>();

        GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(temp, PrefabPath, out bool success);
        UnityEngine.Object.DestroyImmediate(temp);

        if (!success || prefabAsset == null)
        {
            throw new InvalidOperationException($"no se pudo guardar el prefab en '{PrefabPath}'");
        }

        return prefabAsset;
    }

    // ---------------------------------------------------------------
    // Items de prueba
    // ---------------------------------------------------------------
    static string CreateOrUpdateItem(string assetName, string itemId, string itemName, bool consumeOnUse, string description)
    {
        EnsureFolder(ItemsFolder);

        string path = $"{ItemsFolder}/{assetName}.asset";
        ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
        bool isNew = item == null;
        if (isNew) item = ScriptableObject.CreateInstance<ItemData>();

        item.itemId = itemId;
        item.itemName = itemName;
        item.consumeOnUse = consumeOnUse;
        item.description = description;
        item.icon = null;

        if (isNew) AssetDatabase.CreateAsset(item, path);
        else EditorUtility.SetDirty(item);

        return path;
    }

    static ItemData LoadItem(string path)
    {
        ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
        if (item == null) throw new InvalidOperationException($"no se pudo cargar el ItemData en '{path}'");
        return item;
    }

    // ---------------------------------------------------------------
    // Escena
    // ---------------------------------------------------------------
    static void BuildFloor()
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Suelo";
        floor.transform.position = Vector3.zero;
        floor.transform.localScale = new Vector3(2f, 1f, 2f); // plano de Unity = 10x10 -> 20x20
    }

    static void BuildLight()
    {
        GameObject lightGO = new GameObject("Directional Light");
        Light light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
    }

    // Replica la jerarquia de Prototype.unity: Player (raiz, con PlayerInteraction) >
    // PlayerController (CharacterController + PlayerController.cs + PlayerStats) > Main Camera
    // (MouseLook) > Flashlight. Un solo CharacterController, en PlayerController.
    static void BuildPlayer()
    {
        GameObject player = new GameObject("Player");
        player.tag = "Player";
        player.transform.position = new Vector3(0f, 1f, 0f);
        player.transform.rotation = Quaternion.identity;

        Inventory inventory = player.AddComponent<Inventory>();
        SerializedObject invSO = new SerializedObject(inventory);
        invSO.FindProperty("capacity").intValue = 3;
        invSO.ApplyModifiedPropertiesWithoutUndo();

        GameObject controllerGO = new GameObject("PlayerController");
        controllerGO.transform.SetParent(player.transform, false);
        controllerGO.AddComponent<CharacterController>();
        controllerGO.AddComponent<PlayerController>();
        controllerGO.AddComponent<PlayerStats>();

        GameObject cameraGO = new GameObject("Main Camera");
        cameraGO.tag = "MainCamera";
        cameraGO.transform.SetParent(controllerGO.transform, false);
        cameraGO.transform.localPosition = new Vector3(0f, 0.7f, 0.13f);
        Camera camera = cameraGO.AddComponent<Camera>();
        cameraGO.AddComponent<AudioListener>();
        cameraGO.AddComponent<MouseLook>();

        GameObject flashlightGO = new GameObject("Flashlight");
        flashlightGO.transform.SetParent(cameraGO.transform, false);
        flashlightGO.transform.localPosition = new Vector3(0f, 0f, 0.2f);
        Light flashlight = flashlightGO.AddComponent<Light>();
        flashlight.type = LightType.Spot;
        flashlight.range = 15f;
        flashlight.spotAngle = 30f;
        flashlight.intensity = 10f;
        flashlightGO.AddComponent<FlashlightController>();

        // PlayerInteraction va en la raiz del jugador, igual que en Prototype.unity: solo
        // necesita la referencia serializada a la camara, no un GetComponent local.
        PlayerInteraction interaction = player.AddComponent<PlayerInteraction>();
        SerializedObject interactionSO = new SerializedObject(interaction);
        interactionSO.FindProperty("playerCamera").objectReferenceValue = camera;
        interactionSO.ApplyModifiedPropertiesWithoutUndo();
    }

    // openOnStart = false: sin esto, Menu.AutoCrear pondria uno con el valor por defecto
    // (true) y la escena de prueba arrancaria pausada (Time.timeScale = 0).
    static void BuildMenu()
    {
        GameObject menuGO = new GameObject("Menu");
        Menu menu = menuGO.AddComponent<Menu>();
        menu.openOnStart = false;
    }

    static void SpawnPickup(GameObject prefab, ItemData item, Vector3 position, string name)
    {
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = name;
        instance.transform.position = position;

        ItemPickup pickup = instance.GetComponent<ItemPickup>();
        SerializedObject so = new SerializedObject(pickup);
        so.FindProperty("item").objectReferenceValue = item;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------------------------------------------------------------
    // Validacion
    // ---------------------------------------------------------------
    static bool Validate()
    {
        Inventory[] inventories = UnityEngine.Object.FindObjectsByType<Inventory>();
        if (inventories.Length != 1) return Fail($"se esperaba 1 Inventory, hay {inventories.Length}");
        if (inventories[0].Capacity != 3) return Fail($"Capacity = {inventories[0].Capacity}, se esperaba 3");

        ItemPickup[] pickups = UnityEngine.Object.FindObjectsByType<ItemPickup>();
        if (pickups.Length != 4) return Fail($"se esperaban 4 ItemPickup, hay {pickups.Length}");

        foreach (ItemPickup p in pickups)
        {
            SerializedObject so = new SerializedObject(p);
            if (so.FindProperty("item").objectReferenceValue == null)
                return Fail($"ItemPickup '{p.name}' no tiene item asignado");
            if (p.GetComponentInChildren<Collider>(true) == null)
                return Fail($"ItemPickup '{p.name}' no tiene Collider");
        }

        PlayerInteraction[] interactions = UnityEngine.Object.FindObjectsByType<PlayerInteraction>();
        if (interactions.Length != 1) return Fail($"se esperaba 1 PlayerInteraction, hay {interactions.Length}");

        SerializedObject interactionSO = new SerializedObject(interactions[0]);
        if (interactionSO.FindProperty("playerCamera").objectReferenceValue == null)
            return Fail("PlayerInteraction.playerCamera es null");

        CharacterController[] controllers = UnityEngine.Object.FindObjectsByType<CharacterController>();
        if (controllers.Length != 1) return Fail($"se esperaba 1 CharacterController, hay {controllers.Length}");

        AudioListener[] listeners = UnityEngine.Object.FindObjectsByType<AudioListener>();
        if (listeners.Length != 1) return Fail($"se esperaba 1 AudioListener, hay {listeners.Length}");

        Debug.Log($"{Tag} VALIDATION PASS");
        return true;
    }

    static bool Fail(string reason)
    {
        Debug.LogError($"{Tag} VALIDATION FAIL - {reason}");
        return false;
    }

    static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath)) return;

        string[] parts = folderPath.Split('/');
        string current = parts[0]; // "Assets"
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
