using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Cambio puramente visual del comerciante: reemplaza la pildora placeholder (el MeshFilter/
// MeshRenderer con la capsula built-in que quedo de MapaBuilder) por el enano de Tripo, le arma un
// Animator Controller propio con el Idle de los enemigos mas los gestos del pack de Mixamo
// (Assets/Animacion_Comerciante) y le engancha ComercianteGestos.
//
// Mismo reparto de responsabilidades que EnemyModelSetup: el GameObject padre sigue siendo el que
// tiene NPCMerchant, ShopManager y el CapsuleCollider con el que PlayerInteraction lo detecta, y el
// modelo entra como hijo 'Modelo'. Nada de la tienda, el inventario ni la UI se toca.
//
// Por que un controller aparte y no Creature.controller: ese tiene Speed/Attack/Die y espera un
// EnemyAnimator alimentandolos (ver AnimacionControllerBuilder). El comerciante no tiene IA; le
// alcanza un Idle por defecto y gestos que ComercianteGestos dispara por nombre de estado.
public static class ComercianteModeloSetup
{
    const string RutaEnano = "Assets/TripoModels/dwarf_adventurer_3d_model/dwarf_adventurer_3d_model.fbx";
    const string CarpetaGestos = "Assets/Animacion_Comerciante";
    const string RutaIdle = "Assets/Animacion/idle.fbx";
    const string RutaController = "Assets/Animacion/Comerciante.controller";

    const string NombreHijoModelo = "Modelo";
    const string EstadoIdle = "Idle";

    // Los que disparan ComercianteGestos (relleno y saludo) y DialogoComerciante (compra, venta y
    // despedida). El pack trae 15; el resto queda importado pero fuera del controller, para no
    // cargarlo con estados que nunca se reproducen.
    static readonly string[] GestosUsados =
    {
        "acknowledging",
        "weight_shift",
        "being_cocky",
        "thoughtful_head_shake",
        "look_away_gesture",
        "happy_hand_gesture",  // DialogoComerciante.gestoCompra
        "dismissing_gesture"   // DialogoComerciante.gestoDespedida
    };

    // Altura del enano en metros. Los FBX de Tripo no traen una escala confiable, asi que no se
    // adivina: se miden los bounds del modelo y de ahi sale la escala (misma idea que ModeloUtils).
    const float AlturaEnano = 1.35f;

    // El pivote del comerciante esta en el CENTRO de la capsula de 2 m que tenia como placeholder
    // (CapsuleCollider con Height 2 y Center en cero), asi que el piso de la habitacion queda un
    // metro por debajo de su origen local. Ahi van los pies del enano.
    const float PisoLocal = -1f;

    // La habitacion se entra desde el oeste (MapaBuilder sube la escalera hacia Vector3.right), y
    // los modelos de Mixamo miran a +Z: girarlo -90 lo deja de frente al jugador que llega.
    const float GiroY = -90f;

    [MenuItem("Between Metals/Comerciante/Reemplazar pildora por enano")]
    public static void ReemplazarPildoraPorEnano()
    {
        // 1. El enano como Humanoid, para que los clips de Mixamo se le puedan retargetear.
        Avatar avatar = ConfigurarEnano();
        if (avatar == null) return;

        // 2. Los gestos como Humanoid copiando ESE avatar (son clips sin skin: no tienen geometria
        //    de la cual derivar uno propio).
        int gestosListos = ConfigurarGestos(avatar);

        // 3. El controller: Idle por defecto + un estado por gesto, cada uno volviendo al Idle.
        AnimatorController controller = ConstruirController();
        if (controller == null) return;

        // 4. La escena: saca la pildora, mete el modelo, acomoda collider y componentes.
        int comerciantes = AplicarEnEscena(controller, avatar);

        Debug.Log($"ComercianteModeloSetup: {comerciantes} comerciante(s) actualizado(s), " +
                  $"{gestosListos} gesto(s) importado(s) como Humanoid, controller en {RutaController}.");
    }

    // --- 1. Modelo del enano -------------------------------------------------------------------

    static Avatar ConfigurarEnano()
    {
        if (AssetImporter.GetAtPath(RutaEnano) is not ModelImporter importador)
        {
            Debug.LogError($"ComercianteModeloSetup: no se encontro el FBX del enano en {RutaEnano}.");
            return null;
        }

        bool cambio = false;

        // Tripo lo exporta como Generic aunque trae el rig de mixamorig completo; sin Humanoid no
        // hay retargeting posible desde los clips del pack.
        if (importador.animationType != ModelImporterAnimationType.Human)
        {
            importador.animationType = ModelImporterAnimationType.Human;
            cambio = true;
        }
        if (importador.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
        {
            importador.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            cambio = true;
        }

        // Reimportar es caro: solo si de verdad hay algo que cambiar.
        if (cambio) importador.SaveAndReimport();

        Avatar avatar = CargarAvatar(RutaEnano);
        if (avatar == null)
        {
            Debug.LogError($"ComercianteModeloSetup: {RutaEnano} no genero un Avatar. Abri su pestana " +
                           "Rig en el Inspector y revisa el mapeo de huesos (boton Configure...).");
            return null;
        }
        if (!avatar.isValid)
        {
            Debug.LogError($"ComercianteModeloSetup: el Avatar de {RutaEnano} es invalido. Abri Rig > " +
                           "Configure... y completa los huesos que falten.");
            return null;
        }

        return avatar;
    }

    // --- 2. Clips del pack de gestos -----------------------------------------------------------

    static int ConfigurarGestos(Avatar avatar)
    {
        if (!AssetDatabase.IsValidFolder(CarpetaGestos))
        {
            Debug.LogWarning($"ComercianteModeloSetup: no existe {CarpetaGestos}; no hay gestos que configurar.");
            return 0;
        }

        int listos = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { CarpetaGestos }))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            if (ConfigurarGesto(ruta, avatar)) listos++;
        }

        return listos;
    }

    static bool ConfigurarGesto(string ruta, Avatar avatar)
    {
        if (AssetImporter.GetAtPath(ruta) is not ModelImporter importador) return false;

        string nombre = System.IO.Path.GetFileNameWithoutExtension(ruta);
        bool cambio = false;

        if (importador.animationType != ModelImporterAnimationType.Human)
        {
            importador.animationType = ModelImporterAnimationType.Human;
            cambio = true;
        }
        // CopyFromOther y no CreateFromThisModel: son clips sin skin (Deformer 0), no tienen malla
        // de la cual derivar un avatar, y copiando el del enano el retargeting queda exacto.
        if (importador.avatarSetup != ModelImporterAvatarSetup.CopyFromOther)
        {
            importador.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            cambio = true;
        }
        if (importador.sourceAvatar != avatar)
        {
            importador.sourceAvatar = avatar;
            cambio = true;
        }
        if (!importador.importAnimation)
        {
            importador.importAnimation = true;
            cambio = true;
        }

        // Mixamo nombra todos sus clips "mixamo.com". Si se arrastran asi al Animator quedan varios
        // estados homonimos y ComercianteGestos no encuentra ninguno por nombre: se renombran al
        // nombre del archivo, que es la convencion que ya sigue Creature.controller.
        ModelImporterClipAnimation[] clips = importador.clipAnimations;
        if (clips == null || clips.Length == 0) clips = importador.defaultClipAnimations;

        if (clips != null && clips.Length > 0)
        {
            bool cambioClip = false;

            if (clips[0].name != nombre)
            {
                clips[0].name = nombre;
                cambioClip = true;
            }
            // Son gestos de una sola vez: en bucle el enano repetiria el mismo asentimiento sin
            // parar. El reposo continuo lo da el Idle, que es otro clip.
            if (clips[0].loopTime)
            {
                clips[0].loopTime = false;
                cambioClip = true;
            }

            if (cambioClip)
            {
                importador.clipAnimations = clips;
                cambio = true;
            }
        }

        if (cambio) importador.SaveAndReimport();
        return true;
    }

    // --- 3. Animator Controller ----------------------------------------------------------------

    static AnimatorController ConstruirController()
    {
        AnimationClip clipIdle = CargarClip(RutaIdle);
        if (clipIdle == null)
        {
            Debug.LogError($"ComercianteModeloSetup: no se pudo cargar el clip de {RutaIdle}.");
            return null;
        }

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(RutaController)
                         ?? AnimatorController.CreateAnimatorControllerAtPath(RutaController);
        AnimatorStateMachine sm = controller.layers[0].stateMachine;

        var estados = new Dictionary<string, AnimatorState>();
        foreach (ChildAnimatorState hijo in sm.states) estados[hijo.state.name] = hijo.state;

        // El Idle es el mismo clip que usan los enemigos, y ya viene con Loop Time puesto desde el
        // import (ver idle.fbx.meta); no se le toca nada a ese asset compartido.
        if (!estados.TryGetValue(EstadoIdle, out AnimatorState idle))
        {
            idle = sm.AddState(EstadoIdle, new Vector3(300f, 0f, 0f));
            estados[EstadoIdle] = idle;
        }
        idle.motion = clipIdle;
        sm.defaultState = idle;

        int fila = 0;
        foreach (string nombre in GestosUsados)
        {
            string ruta = $"{CarpetaGestos}/{nombre}.fbx";
            AnimationClip clip = CargarClip(ruta);
            if (clip == null)
            {
                Debug.LogWarning($"ComercianteModeloSetup: no se encontro el clip {ruta}; ese gesto se omite.");
                continue;
            }

            if (!estados.TryGetValue(nombre, out AnimatorState gesto))
            {
                gesto = sm.AddState(nombre, new Vector3(650f, fila * 70f - 140f, 0f));
                estados[nombre] = gesto;
            }
            gesto.motion = clip;
            fila++;

            // Sin transiciones de ENTRADA a proposito: ComercianteGestos entra por
            // CrossFadeInFixedTime(nombreDelEstado). Lo unico que hace falta en el grafo es la
            // vuelta al Idle, para que ningun gesto se quede colgado al terminar.
            AgregarVueltaAlIdle(gesto, idle);
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    static void AgregarVueltaAlIdle(AnimatorState gesto, AnimatorState idle)
    {
        if (gesto.transitions.Any(t => t.destinationState == idle)) return;

        AnimatorStateTransition vuelta = gesto.AddTransition(idle);
        vuelta.hasExitTime = true;
        vuelta.exitTime = 0.9f;
        vuelta.hasFixedDuration = true;
        vuelta.duration = 0.25f;
    }

    // --- 4. Escena ------------------------------------------------------------------------------

    static int AplicarEnEscena(AnimatorController controller, Avatar avatar)
    {
        var modelo = AssetDatabase.LoadAssetAtPath<GameObject>(RutaEnano);
        if (modelo == null) return 0;

        NPCMerchant[] comerciantes = Object.FindObjectsByType<NPCMerchant>(FindObjectsSortMode.None);
        if (comerciantes.Length == 0)
        {
            Debug.LogWarning("ComercianteModeloSetup: no hay ningun NPCMerchant en la escena abierta. " +
                             "Abri Assets/Scenes/Prototype.unity y volve a correr el menu.");
            return 0;
        }

        int actualizados = 0;
        foreach (NPCMerchant comerciante in comerciantes)
        {
            if (AplicarModelo(comerciante.gameObject, modelo, controller, avatar)) actualizados++;
        }

        if (actualizados > 0) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        return actualizados;
    }

    static bool AplicarModelo(GameObject comerciante, GameObject modelo, AnimatorController controller, Avatar avatar)
    {
        if (comerciante.transform.Find(NombreHijoModelo) != null)
        {
            Debug.Log($"{comerciante.name}: ya tiene el modelo, se omite.");
            return false;
        }

        Undo.RegisterFullObjectHierarchyUndo(comerciante, "Reemplazar pildora del comerciante");

        // La pildora vive en el PROPIO objeto del comerciante (MapaBuilder no le puso un hijo), asi
        // que lo que se borra son estos dos componentes. El CapsuleCollider NO se saca aca: es el
        // que NPCMerchant exige por [RequireComponent] y contra el que pega el raycast de
        // PlayerInteraction; mas abajo se le ajustan las medidas, nunca se destruye.
        var meshFilter = comerciante.GetComponent<MeshFilter>();
        if (meshFilter != null) Undo.DestroyObjectImmediate(meshFilter);
        var meshRenderer = comerciante.GetComponent<MeshRenderer>();
        if (meshRenderer != null) Undo.DestroyObjectImmediate(meshRenderer);

        var instancia = (GameObject)PrefabUtility.InstantiatePrefab(modelo);
        Undo.RegisterCreatedObjectUndo(instancia, "Instanciar modelo del comerciante");
        instancia.name = NombreHijoModelo;
        instancia.transform.SetParent(comerciante.transform, false);
        instancia.transform.localPosition = Vector3.zero;
        instancia.transform.localRotation = Quaternion.identity;
        instancia.transform.localScale = Vector3.one;

        // Se mide con escala 1 y de la medida sale la escala, en vez de hardcodearla. El transform
        // del comerciante queda intacto: ProgresionBuilder ubica el boton secreto relativo a el.
        Bounds bounds = BoundsLocales(instancia, comerciante.transform);
        float alto = Mathf.Max(bounds.size.y, 0.001f);
        float escala = AlturaEnano / alto;

        instancia.transform.localScale = Vector3.one * escala;
        instancia.transform.localRotation = Quaternion.Euler(0f, GiroY, 0f);
        // El pivote del FBX no esta necesariamente en los pies: se compensa para que la base del
        // modelo caiga en el piso de la habitacion.
        instancia.transform.localPosition = new Vector3(0f, PisoLocal - bounds.min.y * escala, 0f);

        AjustarCollider(comerciante, bounds, escala);
        AgregarAnimator(instancia, controller, avatar);

        if (comerciante.GetComponent<ComercianteGestos>() == null)
        {
            Undo.AddComponent<ComercianteGestos>(comerciante);
        }

        // Las frases. Va en el comerciante y no en el modelo porque escucha a ShopManager, que vive
        // aca; los gestos que acompanan cada frase los resuelve solo via GetComponent.
        if (comerciante.GetComponent<DialogoComerciante>() == null)
        {
            Undo.AddComponent<DialogoComerciante>(comerciante);
        }

        Debug.Log($"{comerciante.name}: enano colocado (medido {alto:0.00} m, escala {escala:0.000}).", comerciante);
        return true;
    }

    // Bounds de los Renderer del modelo en espacio local del comerciante. Se usan Renderer.bounds
    // y no los MeshFilter como ModeloUtils.Medir porque el enano es un SkinnedMeshRenderer y no
    // tiene ningun MeshFilter que medir.
    static Bounds BoundsLocales(GameObject modelo, Transform espacioLocal)
    {
        Renderer[] renderers = modelo.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            Debug.LogWarning($"ComercianteModeloSetup: '{modelo.name}' no tiene ningun Renderer; " +
                             "se deja la escala en 1.", modelo);
            return new Bounds(Vector3.zero, Vector3.one);
        }

        Bounds mundo = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) mundo.Encapsulate(renderers[i].bounds);

        Vector3 centro = espacioLocal.InverseTransformPoint(mundo.center);
        Vector3 tamano = espacioLocal.InverseTransformVector(mundo.size);
        tamano = new Vector3(Mathf.Abs(tamano.x), Mathf.Abs(tamano.y), Mathf.Abs(tamano.z));
        return new Bounds(centro, tamano);
    }

    // El collider de interaccion tiene que cubrir el cuerpo visible: si queda mas alto que el
    // enano, el jugador puede abrir la tienda apuntando al aire sobre su cabeza.
    static void AjustarCollider(GameObject comerciante, Bounds bounds, float escala)
    {
        var capsula = comerciante.GetComponent<CapsuleCollider>();
        if (capsula == null) capsula = Undo.AddComponent<CapsuleCollider>(comerciante);

        Undo.RecordObject(capsula, "Ajustar collider del comerciante");
        capsula.direction = 1; // Y
        capsula.height = AlturaEnano;
        capsula.radius = Mathf.Max(Mathf.Max(bounds.size.x, bounds.size.z) * escala * 0.5f, 0.1f);
        capsula.center = new Vector3(0f, PisoLocal + AlturaEnano * 0.5f, 0f);
        capsula.isTrigger = false;
    }

    static void AgregarAnimator(GameObject modelo, AnimatorController controller, Avatar avatar)
    {
        var animator = modelo.GetComponent<Animator>();
        if (animator == null) animator = Undo.AddComponent<Animator>(modelo);

        Undo.RecordObject(animator, "Configurar Animator del comerciante");
        animator.runtimeAnimatorController = controller;
        if (avatar != null) animator.avatar = avatar;

        // El comerciante no se desplaza: la animacion se queda en el lugar y el collider de
        // interaccion no se mueve de donde ProgresionBuilder espera que este.
        animator.applyRootMotion = false;

        // ShopManager congela el juego con Time.timeScale = 0 mientras la tienda esta abierta (ver
        // ShopManager.AbrirTienda). Con Update Mode Normal el enano se quedaria tieso justo cuando
        // el jugador lo tiene delante, asi que va en tiempo sin escalar.
        animator.updateMode = AnimatorUpdateMode.UnscaledTime;

        // Proyecto apuntado a PCs de pocos recursos: fuera de camara no vale la pena escribir los
        // transforms de los huesos.
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
    }

    // --- Helpers --------------------------------------------------------------------------------

    static AnimationClip CargarClip(string ruta)
    {
        return AssetDatabase.LoadAllAssetsAtPath(ruta).OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
    }

    static Avatar CargarAvatar(string ruta)
    {
        return AssetDatabase.LoadAllAssetsAtPath(ruta).OfType<Avatar>().FirstOrDefault();
    }
}
