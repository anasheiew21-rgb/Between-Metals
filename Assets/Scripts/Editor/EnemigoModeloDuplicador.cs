using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;

// Le pone a los enemigos 2 y 3 (los que EnemigosExtraBuilder deja como esferas placeholder) el
// MISMO modelo y las MISMAS animaciones que el enemigo 1, copiandolos del enemigo que ya esta
// armado en la escena.
//
// Por que copiar del enemigo 1 y no instanciar el FBX de cero, que es lo que ya hace
// EnemyModelSetup: porque el modelo del enemigo 1 no es el FBX crudo. Tiene encima el trabajo de
// ajuste que vive en la ESCENA y no en el asset -el giro en Y que corrige para donde mira el FBX,
// la altura a la que quedan los pies, la escala, el material asignado a mano, el controller del
// Animator, Apply Root Motion apagado-. Instanciando el FBX otra vez todo eso se perderia y habria
// que volver a ajustarlo enemigo por enemigo. Copiando el del enemigo 1, los tres se ven y se
// animan igual por construccion, y cualquier ajuste futuro sobre el enemigo 1 se puede propagar
// volviendo a correr esto.
//
// Es el mismo criterio que ya usa EnemigosExtraBuilder con los componentes de logica (copia los
// valores de EnemyAI/EnemyHealth/EnemyAnimator del original con "Paste Component Values"): esto es
// la pata que faltaba, la parte visual.
//
// Lo que NO hace: alinear, remedir la hitbox ni rutear el audio. Eso ya lo hace
// EnemySetupFixer.CorregirEnemigos() para todos los EnemyAI de la escena, y se lo llama al final
// en vez de duplicar esa logica aca.
//
// Menu: Between Metals > Enemigos
public static class EnemigoModeloDuplicador
{
    const string NombreModelo = "Modelo"; // el mismo que usa EnemyModelSetup
    const string MenuDuplicar = "Between Metals/Enemigos/Duplicar modelo del enemigo 1 en los demas";

    [MenuItem(MenuDuplicar, priority = 302)]
    static void DuplicarDesdeMenu()
    {
        Duplicar();
    }

    /// <summary>
    /// Copia el modelo del enemigo ya armado a los que no tienen. <paramref name="corregirDespues"/>
    /// en false lo deja para quien llame (BuildEnemySetup ya corre EnemySetupFixer al final de su
    /// secuencia y no hace falta correrlo dos veces).
    /// </summary>
    public static void Duplicar(bool corregirDespues = true)
    {
        EnemyAI[] enemigos = Object.FindObjectsByType<EnemyAI>(FindObjectsInactive.Include);
        if (enemigos.Length == 0)
        {
            EditorUtility.DisplayDialog("Duplicar modelo del enemigo",
                "No hay ningun EnemyAI en la escena abierta.", "OK");
            return;
        }

        Transform plantilla = BuscarModeloPlantilla(enemigos);
        if (plantilla == null)
        {
            EditorUtility.DisplayDialog("Duplicar modelo del enemigo",
                "Ningun enemigo de la escena tiene todavia un hijo \"" + NombreModelo + "\" con Animator.\n\n" +
                "Corré antes Between Metals > Enemigos > Build + Setup completo para armar el enemigo 1.", "OK");
            return;
        }

        var sinModelo = new List<EnemyAI>();
        foreach (EnemyAI ia in enemigos)
        {
            if (ia.transform == plantilla.parent) continue;                    // es el enemigo plantilla
            if (ia.transform.Find(NombreModelo) != null) continue;             // ya tiene modelo
            sinModelo.Add(ia);
        }

        if (sinModelo.Count == 0)
        {
            Debug.Log("EnemigoModeloDuplicador: todos los enemigos de la escena ya tienen su modelo, " +
                "no se toco nada.");
            return;
        }

        Undo.IncrementCurrentGroup();
        int grupoUndo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Duplicar modelo del enemigo");

        foreach (EnemyAI ia in sinModelo)
        {
            CopiarModelo(plantilla, ia.gameObject);
            Debug.Log($"EnemigoModeloDuplicador: modelo de {plantilla.parent.name} copiado a {ia.name}.", ia);
        }

        // Alineacion, hitbox, material, audio y el puente de Animation Events: ya existe y corre
        // sobre todos los enemigos de la escena.
        if (corregirDespues) EnemySetupFixer.CorregirEnemigos();

        Undo.CollapseUndoOperations(grupoUndo);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Debug.Log($"EnemigoModeloDuplicador: {sinModelo.Count} enemigo(s) con el modelo de " +
            $"{plantilla.parent.name}. Revisa la escena y guardala (Ctrl+S).");
    }

    // El enemigo "ya armado": el primero que tiene un hijo llamado Modelo con un Animator. Se pide
    // el Animator y no solo el nombre para no tomar como plantilla un enemigo a medio hacer (un
    // hijo Modelo sin Animator no trae ninguna animacion que copiar).
    static Transform BuscarModeloPlantilla(EnemyAI[] enemigos)
    {
        foreach (EnemyAI ia in enemigos)
        {
            Transform modelo = ia.transform.Find(NombreModelo);
            if (modelo != null && modelo.GetComponent<Animator>() != null) return modelo;
        }
        return null;
    }

    static void CopiarModelo(Transform plantilla, GameObject destino)
    {
        Undo.RegisterFullObjectHierarchyUndo(destino, "Duplicar modelo del enemigo");

        QuitarPlaceholder(destino);

        GameObject copia = Instanciar(plantilla.gameObject);
        Undo.RegisterCreatedObjectUndo(copia, "Instanciar modelo de enemigo");

        copia.name = NombreModelo;
        copia.transform.SetParent(destino.transform, false);

        // Las tres cosas que viven en la escena y no en el asset del modelo: donde esta parado
        // respecto del enemigo, para donde mira y cuanto mide. Se copian tal cual del enemigo 1.
        copia.transform.localPosition = plantilla.localPosition;
        copia.transform.localRotation = plantilla.localRotation;
        copia.transform.localScale = plantilla.localScale;

        CopiarAnimator(plantilla, copia);
        CopiarMateriales(plantilla, copia);
        AsegurarEnemyAnimator(plantilla.parent, destino);
    }

    // Si el modelo de la plantilla es una instancia de prefab (es el caso: EnemyModelSetup
    // instancia alien_creature_rigged.fbx), la copia tambien se crea como instancia de ESE prefab,
    // asi no se pierde el vinculo y una reimportacion del FBX sigue llegando a los tres enemigos.
    // Si no lo es, se cae a un Instantiate comun, que copia todo igual pero sin vinculo.
    static GameObject Instanciar(GameObject modeloPlantilla)
    {
        GameObject origen = PrefabUtility.GetCorrespondingObjectFromSource(modeloPlantilla);
        if (origen != null) return (GameObject)PrefabUtility.InstantiatePrefab(origen);

        return Object.Instantiate(modeloPlantilla);
    }

    // La esfera primitiva que dejo EnemigosExtraBuilder. El SphereCollider NO se saca aca: lo
    // reemplaza por la capsula EnemySetupFixer al final, y asi el enemigo nunca se queda sin
    // collider en el medio (si algo fallara entre una cosa y la otra seria invulnerable).
    static void QuitarPlaceholder(GameObject enemigo)
    {
        MeshFilter filtro = enemigo.GetComponent<MeshFilter>();
        if (filtro != null) Undo.DestroyObjectImmediate(filtro);

        MeshRenderer renderer = enemigo.GetComponent<MeshRenderer>();
        if (renderer != null) Undo.DestroyObjectImmediate(renderer);
    }

    // "Paste Component Values" sobre el Animator: trae el Animator Controller (Creature.controller,
    // o sea TODAS las animaciones y sus transiciones), el Avatar, Apply Root Motion, el Culling
    // Mode y el Update Mode. Es el mismo mecanismo que usa EnemigosExtraBuilder para la logica.
    static void CopiarAnimator(Transform plantilla, GameObject copia)
    {
        Animator origen = plantilla.GetComponent<Animator>();
        if (origen == null) return;

        Animator destino = copia.GetComponent<Animator>();
        if (destino == null) destino = Undo.AddComponent<Animator>(copia);
        else Undo.RecordObject(destino, "Copiar Animator");

        ComponentUtility.CopyComponent(origen);
        ComponentUtility.PasteComponentValues(destino);
    }

    // El material del enemigo esta asignado sobre los renderers de la INSTANCIA de la escena (lo
    // hace EnemySetupFixer), no sobre el prefab, asi que una instancia nueva saldria con el
    // material del FBX. Se copia renderer por renderer, en el mismo orden: los dos vienen del
    // mismo prefab, asi que la jerarquia y el orden coinciden.
    //
    // Si no coincidieran no se fuerza nada: EnemySetupFixer corre despues y asigna el material por
    // su cuenta, que es la red de seguridad.
    static void CopiarMateriales(Transform plantilla, GameObject copia)
    {
        Renderer[] origen = plantilla.GetComponentsInChildren<Renderer>(true);
        Renderer[] destino = copia.GetComponentsInChildren<Renderer>(true);

        if (origen.Length != destino.Length || origen.Length == 0)
        {
            Debug.LogWarning($"EnemigoModeloDuplicador: la copia tiene {destino.Length} renderer(s) y la " +
                $"plantilla {origen.Length}; los materiales los va a asignar EnemySetupFixer.", copia);
            return;
        }

        for (int i = 0; i < origen.Length; i++)
        {
            Undo.RecordObject(destino[i], "Copiar materiales");
            destino[i].sharedMaterials = origen[i].sharedMaterials;
        }
    }

    // EnemyAnimator es el que traduce el estado de EnemyAI a los parametros del controller
    // (Speed/Attack/Die) y va en el objeto del enemigo, no en el del modelo. EnemigosExtraBuilder
    // ya lo copia, pero este menu tambien sirve para un enemigo agregado a mano, asi que se
    // verifica igual.
    static void AsegurarEnemyAnimator(Transform plantillaEnemigo, GameObject destino)
    {
        if (destino.GetComponent<EnemyAnimator>() != null) return;

        EnemyAnimator origen = plantillaEnemigo != null ? plantillaEnemigo.GetComponent<EnemyAnimator>() : null;
        EnemyAnimator copia = Undo.AddComponent<EnemyAnimator>(destino);

        if (origen == null) return;

        ComponentUtility.CopyComponent(origen);
        ComponentUtility.PasteComponentValues(copia);
    }

    // Variante para -executeMethod EnemigoModeloDuplicador.Run (batch/CI), al estilo de
    // BuildEnemySetup y EnemigosExtraBuilder.
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Prototype.unity");
        Duplicar();
        EditorSceneManager.SaveOpenScenes();
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }
}
