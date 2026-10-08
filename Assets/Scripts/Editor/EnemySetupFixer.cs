using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

// Corrige, sobre los enemigos de la escena abierta, los tres desfasajes que no se pueden arreglar
// solo desde los scripts porque viven en los datos de la escena (issue de modelo/hitbox/audio):
//
//  1. El modelo 3D hijo con posicion/rotacion propias y Apply Root Motion prendido: la malla se
//     desplaza por su cuenta y deja atras al NavMeshAgent y a la hitbox. Se recentra en X/Z, se le
//     apoyan los pies en el origen del padre (que es donde el agente lo pone sobre el piso) y se
//     apaga el root motion. El giro en Y se respeta: suele ser la correccion de "para donde mira"
//     el FBX, que no tiene por que ser +Z.
//  2. La capsula medida con el modelo SIN escalar (y encima desactivada): queda del tamano
//     equivocado y PlayerCombat no puede pegarle. Se vuelve a medir con la escala actual, se centra
//     sobre el eje del agente y se activa.
//  3. El modelo blanco: se le asigna el material ya importado a los SkinnedMeshRenderer y, de
//     paso, al campo enemyMaterial de EnemyAI (su red de seguridad en tiempo de ejecucion).
//
// Ademas deja el AudioSource 3D listo (ruteado al grupo sfx del mixer) y el puente de Animation
// Events en el objeto del Animator. No guarda la escena: se revisa y se guarda a mano.
public static class EnemySetupFixer
{
    const string MaterialPorDefecto = "Assets/TripoModels/alien_creature_3d_model_2/Materials/alien_creature_3d_model_2.mat";
    const string MenuCorregir = "Between Metals/Enemigos/Corregir modelo, hitbox y audio";

    // Si el modelo termina midiendo mas/menos que esto, casi siempre es la escala del FBX mal
    // puesta (importado en centimetros, por ejemplo) y conviene mirarlo antes de seguir.
    const float AltoMaximoEsperado = 4f;
    const float AltoMinimoEsperado = 0.5f;

    static readonly Vector3[] Esquinas =
    {
        new Vector3(-1f, -1f, -1f), new Vector3(1f, -1f, -1f),
        new Vector3(-1f, 1f, -1f), new Vector3(1f, 1f, -1f),
        new Vector3(-1f, -1f, 1f), new Vector3(1f, -1f, 1f),
        new Vector3(-1f, 1f, 1f), new Vector3(1f, 1f, 1f),
    };

    [MenuItem(MenuCorregir)]
    public static void CorregirEnemigos()
    {
        EnemyAI[] enemigos = Object.FindObjectsByType<EnemyAI>(FindObjectsInactive.Include);
        if (enemigos.Length == 0)
        {
            Debug.LogWarning("EnemySetupFixer: no hay ningun EnemyAI en la escena abierta.");
            return;
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPorDefecto) ?? BuscarMaterialDelEnemigo();
        if (material == null)
        {
            Debug.LogWarning($"EnemySetupFixer: no se encontro el material en {MaterialPorDefecto} " +
                "ni ningun material con 'alien' en el nombre. El resto de las correcciones se aplica igual.");
        }

        int corregidos = 0;
        foreach (EnemyAI ia in enemigos)
        {
            List<string> cambios = new List<string>();
            CorregirEnemigo(ia, material, cambios);

            if (cambios.Count == 0)
            {
                Debug.Log($"EnemySetupFixer: {ia.name} ya estaba bien, no se toco nada.", ia);
                continue;
            }

            corregidos++;
            Debug.Log($"EnemySetupFixer: {ia.name}\n  - {string.Join("\n  - ", cambios)}", ia);
        }

        if (corregidos > 0) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"EnemySetupFixer: {corregidos} de {enemigos.Length} enemigo(s) corregidos. " +
            "Revisa la escena y guardala (Ctrl+S) si esta como esperabas.");
    }

    static Material BuscarMaterialDelEnemigo()
    {
        foreach (string guid in AssetDatabase.FindAssets("alien t:Material"))
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (m != null) return m;
        }
        return null;
    }

    static void CorregirEnemigo(EnemyAI ia, Material material, List<string> cambios)
    {
        GameObject enemigo = ia.gameObject;
        Undo.RegisterFullObjectHierarchyUndo(enemigo, "Corregir enemigo");

        Animator animator = enemigo.GetComponentInChildren<Animator>();
        if (animator == null)
        {
            cambios.Add("NO tiene Animator en ningun hijo: corre antes 'Between Metals/Enemigos/Build + Setup completo'");
            return;
        }

        Transform modelo = animator.transform;
        if (modelo == enemigo.transform)
        {
            cambios.Add("el Animator esta en el MISMO objeto que EnemyAI: el modelo tiene que ser un hijo " +
                "(padre = navegacion + fisica, hijo = malla + Animator)");
        }

        ApagarRootMotion(animator, cambios);
        if (modelo != enemigo.transform) AlinearModelo(modelo, enemigo.transform, cambios);

        Bounds? medida = MedirBoundsLocal(modelo.gameObject, enemigo.transform);
        if (medida.HasValue) AjustarCapsula(enemigo, medida.Value, cambios);
        else cambios.Add("el modelo no tiene ningun Renderer, no se pudo medir la hitbox");

        SincronizarAgente(enemigo, cambios);
        AsignarMaterial(ia, modelo.gameObject, material, cambios);
        AsegurarAudioSource(enemigo, cambios);
        AsegurarPuenteDeEventos(animator.gameObject, cambios);
    }

    static void ApagarRootMotion(Animator animator, List<string> cambios)
    {
        if (!animator.applyRootMotion) return;

        Undo.RecordObject(animator, "Apagar root motion");
        animator.applyRootMotion = false;
        cambios.Add("Apply Root Motion apagado (el desplazamiento lo manda el NavMeshAgent, no la animacion)");
    }

    // Recentra el modelo sobre el eje del padre y le apoya los pies en el origen local. La rotacion
    // en Y se conserva (correccion de orientacion del FBX); X y Z se ceran, porque una malla
    // inclinada respecto de su capsula es justamente lo que se ve "desalineado".
    static void AlinearModelo(Transform modelo, Transform padre, List<string> cambios)
    {
        Undo.RecordObject(modelo, "Alinear modelo");

        Vector3 posicion = modelo.localPosition;
        if (Mathf.Abs(posicion.x) > 0.001f || Mathf.Abs(posicion.z) > 0.001f)
        {
            cambios.Add($"modelo recentrado en X/Z (estaba en {posicion.x:0.###}, {posicion.z:0.###})");
            modelo.localPosition = new Vector3(0f, posicion.y, 0f);
        }

        Vector3 euler = modelo.localEulerAngles;
        if (!Mathf.Approximately(NormalizarAngulo(euler.x), 0f) || !Mathf.Approximately(NormalizarAngulo(euler.z), 0f))
        {
            cambios.Add($"inclinacion del modelo corregida (X {euler.x:0.#}, Z {euler.z:0.#} -> 0); se conserva el giro en Y ({euler.y:0.#})");
            modelo.localEulerAngles = new Vector3(0f, euler.y, 0f);
        }

        // Los pies al origen del padre: ahi es donde el NavMeshAgent apoya al enemigo sobre el piso.
        Bounds? medida = MedirBoundsLocal(modelo.gameObject, padre);
        if (!medida.HasValue) return;

        float desfasaje = -medida.Value.min.y;
        if (Mathf.Abs(desfasaje) > 0.01f)
        {
            cambios.Add($"altura del modelo corregida {desfasaje:0.###} m para que los pies queden en el piso");
            modelo.localPosition += new Vector3(0f, desfasaje, 0f);
        }
    }

    static float NormalizarAngulo(float grados)
    {
        float a = Mathf.Repeat(grados + 180f, 360f) - 180f;
        return Mathf.Abs(a) < 0.1f ? 0f : a;
    }

    // Mide los Renderer del modelo (ya con su escala puesta) y devuelve el volumen que ocupan en el
    // espacio local del padre. Se transforman las 8 esquinas de cada caja en vez de la caja entera,
    // para no subestimar el tamano cuando el modelo tiene un giro propio en Y.
    static Bounds? MedirBoundsLocal(GameObject modelo, Transform espacio)
    {
        Renderer[] renderers = modelo.GetComponentsInChildren<Renderer>();
        bool hayAlguno = false;
        Bounds resultado = new Bounds();

        foreach (Renderer r in renderers)
        {
            Bounds mundo = r.bounds;
            for (int i = 0; i < Esquinas.Length; i++)
            {
                Vector3 esquina = mundo.center + Vector3.Scale(mundo.extents, Esquinas[i]);
                Vector3 enLocal = espacio.InverseTransformPoint(esquina);

                if (!hayAlguno)
                {
                    resultado = new Bounds(enLocal, Vector3.zero);
                    hayAlguno = true;
                }
                else
                {
                    resultado.Encapsulate(enLocal);
                }
            }
        }

        return hayAlguno ? resultado : (Bounds?)null;
    }

    // Capsula centrada sobre el eje del agente (X/Z en 0): una capsula corrida de ese eje es lo que
    // hace que la hitbox "se vaya para otro lado" cuando el enemigo gira.
    static void AjustarCapsula(GameObject enemigo, Bounds medida, List<string> cambios)
    {
        SphereCollider esfera = enemigo.GetComponent<SphereCollider>();
        if (esfera != null)
        {
            Undo.DestroyObjectImmediate(esfera);
            cambios.Add("SphereCollider placeholder eliminado");
        }

        CapsuleCollider capsula = enemigo.GetComponent<CapsuleCollider>();
        if (capsula == null)
        {
            capsula = Undo.AddComponent<CapsuleCollider>(enemigo);
            cambios.Add("CapsuleCollider agregado");
        }
        else
        {
            Undo.RecordObject(capsula, "Ajustar capsula");
        }

        float alto = Mathf.Max(0.1f, medida.size.y);
        float radio = Mathf.Max(0.1f, Mathf.Max(medida.size.x, medida.size.z) * 0.5f);
        radio = Mathf.Min(radio, alto * 0.5f); // mas que esto y Unity la trata como una esfera

        if (!capsula.enabled)
        {
            capsula.enabled = true;
            cambios.Add("CapsuleCollider estaba DESACTIVADO (PlayerCombat no podia pegarle): activado");
        }

        capsula.direction = 1; // eje Y
        capsula.height = alto;
        capsula.radius = radio;
        capsula.center = new Vector3(0f, alto * 0.5f, 0f);
        capsula.isTrigger = false;
        cambios.Add($"hitbox remedida con la escala actual: alto {alto:0.00} m, radio {radio:0.00} m, centrada en el eje");

        if (alto > AltoMaximoEsperado || alto < AltoMinimoEsperado)
        {
            cambios.Add($"OJO: el modelo mide {alto:0.00} m de alto. Si no es lo buscado, revisa la escala " +
                "del hijo del modelo (un FBX importado en centimetros suele quedar escalado a mano)");
        }
    }

    static void SincronizarAgente(GameObject enemigo, List<string> cambios)
    {
        NavMeshAgent agente = enemigo.GetComponent<NavMeshAgent>();
        if (agente == null) return; // EnemyAI lo crea y lo configura en juego (AsegurarAgente)

        CapsuleCollider capsula = enemigo.GetComponent<CapsuleCollider>();
        if (capsula == null) return;

        Undo.RecordObject(agente, "Sincronizar agente");
        Vector3 escala = enemigo.transform.lossyScale;
        float radioHorneado = NavMesh.GetSettingsByID(agente.agentTypeID).agentRadius;
        float radio = Mathf.Max(0.1f, capsula.radius * Mathf.Max(Mathf.Abs(escala.x), Mathf.Abs(escala.z)));
        if (radioHorneado > 0f) radio = Mathf.Min(radio, radioHorneado);

        agente.radius = radio;
        agente.height = Mathf.Max(radio * 2f, capsula.height * Mathf.Abs(escala.y));
        agente.updateRotation = false;
        cambios.Add($"NavMeshAgent sincronizado con la hitbox (radio {agente.radius:0.00}, alto {agente.height:0.00})");
    }

    // Asigna el material a cada slot de cada SkinnedMeshRenderer que este vacio o con el material
    // por defecto de Unity (el "Lit"/"Default-Material" que deja un FBX importado sin materiales:
    // el famoso modelo todo blanco), y lo deja tambien en EnemyAI.enemyMaterial.
    static void AsignarMaterial(EnemyAI ia, GameObject modelo, Material material, List<string> cambios)
    {
        if (material == null) return;

        SkinnedMeshRenderer[] mallas = modelo.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        int tocados = 0;

        foreach (SkinnedMeshRenderer malla in mallas)
        {
            Material[] actuales = malla.sharedMaterials;
            int slots = Mathf.Max(1, actuales.Length);
            Material[] nuevos = new Material[slots];
            bool cambio = false;

            for (int i = 0; i < slots; i++)
            {
                Material actual = i < actuales.Length ? actuales[i] : null;
                if (EsMaterialPorDefecto(actual))
                {
                    nuevos[i] = material;
                    cambio = true;
                }
                else
                {
                    nuevos[i] = actual;
                }
            }

            if (!cambio) continue;

            Undo.RecordObject(malla, "Asignar material del enemigo");
            malla.sharedMaterials = nuevos;
            tocados++;
        }

        if (tocados > 0) cambios.Add($"material '{material.name}' asignado a {tocados} SkinnedMeshRenderer");

        // Red de seguridad en tiempo de ejecucion (EnemyAI.AplicarMaterialAlModelo): el campo es
        // privado, asi que se escribe por SerializedObject.
        SerializedObject so = new SerializedObject(ia);
        SerializedProperty prop = so.FindProperty("enemyMaterial");
        if (prop != null && prop.objectReferenceValue == null)
        {
            prop.objectReferenceValue = material;
            so.ApplyModifiedProperties();
            cambios.Add($"EnemyAI.enemyMaterial = '{material.name}'");
        }
    }

    static bool EsMaterialPorDefecto(Material m)
    {
        if (m == null) return true;
        if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(m))
            && AssetDatabase.GetAssetPath(m).StartsWith("Library/unity default resources")) return true;
        return m.name == "Default-Material" || m.name == "Lit" || m.name == "No Name";
    }

    static void AsegurarAudioSource(GameObject enemigo, List<string> cambios)
    {
        AudioSource fuente = enemigo.GetComponent<AudioSource>();
        if (fuente == null)
        {
            fuente = Undo.AddComponent<AudioSource>(enemigo);
            cambios.Add("AudioSource agregado (3D, ruteado al grupo sfx)");
        }
        else
        {
            Undo.RecordObject(fuente, "Configurar AudioSource");
        }

        fuente.playOnAwake = false;
        fuente.loop = false;
        fuente.spatialBlend = 1f;
        fuente.rolloffMode = AudioRolloffMode.Logarithmic;
        fuente.minDistance = 2f;
        fuente.maxDistance = 25f;
        AudioPreferences.RutearASfx(fuente);
    }

    static void AsegurarPuenteDeEventos(GameObject objetoDelAnimator, List<string> cambios)
    {
        if (objetoDelAnimator.GetComponent<EnemyAnimationEvents>() != null) return;

        Undo.AddComponent<EnemyAnimationEvents>(objetoDelAnimator);
        cambios.Add($"EnemyAnimationEvents agregado en '{objetoDelAnimator.name}' (recibe el Animation Event OnAttackHit)");
    }
}
