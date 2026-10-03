using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Reemplaza la esfera placeholder de los EnemyAI de la escena abierta por el modelo rigueado
// (Assets/Modelo personajes/alien_creature_rigged.fbx) y le agrega el Animator con
// Creature.controller (ver AnimacionControllerBuilder) mas el EnemyAnimator que lo maneja.
public static class EnemyModelSetup
{
    const string ModelPrefabPath = "Assets/Modelo personajes/alien_creature_rigged.fbx";
    const string ControllerPath = "Assets/Animacion/Creature.controller";
    const string ModelChildName = "Modelo";

    [MenuItem("Between Metals/Enemigos/Reemplazar esfera por modelo")]
    public static void ReemplazarEsferaPorModelo()
    {
        var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPrefabPath);
        if (modelPrefab == null)
        {
            Debug.LogError($"No se encontro el prefab del modelo en {ModelPrefabPath}");
            return;
        }

        var enemigos = Object.FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        if (enemigos.Length == 0)
        {
            Debug.LogWarning("No se encontro ningun EnemyAI en la escena abierta.");
            return;
        }

        int actualizados = 0;
        foreach (var enemyAI in enemigos)
        {
            if (AplicarModelo(enemyAI.gameObject, modelPrefab)) actualizados++;
        }

        if (actualizados > 0) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"EnemyModelSetup: modelo aplicado a {actualizados} enemigo(s).");
    }

    static bool AplicarModelo(GameObject enemigo, GameObject modelPrefab)
    {
        if (enemigo.transform.Find(ModelChildName) != null)
        {
            Debug.Log($"{enemigo.name}: ya tiene el modelo, se omite.");
            return false;
        }

        Undo.RegisterFullObjectHierarchyUndo(enemigo, "Reemplazar modelo de enemigo");

        // Saca el mesh placeholder (la esfera de MeshFilter/MeshRenderer); el collider se ajusta
        // mas abajo en vez de sacarse antes, asi EnemyAI/PlayerCombat nunca se quedan sin
        // collider entre medio si algo falla en el resto del metodo.
        var meshFilter = enemigo.GetComponent<MeshFilter>();
        if (meshFilter != null) Undo.DestroyObjectImmediate(meshFilter);
        var meshRenderer = enemigo.GetComponent<MeshRenderer>();
        if (meshRenderer != null) Undo.DestroyObjectImmediate(meshRenderer);

        var modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab);
        Undo.RegisterCreatedObjectUndo(modelInstance, "Instanciar modelo de enemigo");
        modelInstance.name = ModelChildName;
        modelInstance.transform.SetParent(enemigo.transform, false);
        modelInstance.transform.localPosition = Vector3.zero;
        modelInstance.transform.localRotation = Quaternion.identity;

        Bounds boundsLocal = CalcularBoundsLocal(modelInstance, enemigo.transform);

        // El pivote del modelo no necesariamente esta en los pies: se baja/sube para que la base
        // de sus bounds coincida con el origen local del enemigo, que es donde el NavMeshAgent lo
        // ubica sobre el piso.
        float offsetPies = -boundsLocal.min.y;
        modelInstance.transform.localPosition = new Vector3(0, offsetPies, 0);
        boundsLocal.center += new Vector3(0, offsetPies, 0);

        AjustarCollider(enemigo, boundsLocal);
        AgregarAnimator(enemigo, modelInstance);

        if (enemigo.GetComponent<EnemyAnimator>() == null) Undo.AddComponent<EnemyAnimator>(enemigo);

        return true;
    }

    static void AgregarAnimator(GameObject enemigo, GameObject modelInstance)
    {
        var animator = modelInstance.GetComponent<Animator>();
        if (animator == null) animator = Undo.AddComponent<Animator>(modelInstance);

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogWarning($"{enemigo.name}: no se encontro {ControllerPath}. Corre antes el menu " +
                "\"Animacion/Build Animator Controller\" para generarlo.");
            return;
        }

        animator.runtimeAnimatorController = controller;
    }

    // Bounds combinados de todos los Renderer del modelo, pasados a espacio local del enemigo
    // (valido porque en este punto el enemigo mismo no tiene una rotacion/escala propia distinta
    // de la que ya tenia el placeholder).
    static Bounds CalcularBoundsLocal(GameObject modelo, Transform espacioLocal)
    {
        var renderers = modelo.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.one);

        Bounds mundo = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) mundo.Encapsulate(renderers[i].bounds);

        Vector3 centro = espacioLocal.InverseTransformPoint(mundo.center);
        Vector3 tamaño = espacioLocal.InverseTransformVector(mundo.size);
        tamaño = new Vector3(Mathf.Abs(tamaño.x), Mathf.Abs(tamaño.y), Mathf.Abs(tamaño.z));
        return new Bounds(centro, tamaño);
    }

    static void AjustarCollider(GameObject enemigo, Bounds boundsLocal)
    {
        var esfera = enemigo.GetComponent<SphereCollider>();
        if (esfera != null) Undo.DestroyObjectImmediate(esfera);

        var capsula = enemigo.GetComponent<CapsuleCollider>();
        if (capsula == null) capsula = Undo.AddComponent<CapsuleCollider>(enemigo);

        capsula.height = Mathf.Max(boundsLocal.size.y, 0.1f);
        capsula.radius = Mathf.Max(Mathf.Max(boundsLocal.size.x, boundsLocal.size.z) * 0.5f, 0.1f);
        capsula.center = boundsLocal.center;
    }
}
