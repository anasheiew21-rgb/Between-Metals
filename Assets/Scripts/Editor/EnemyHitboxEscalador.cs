using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Agranda (o vuelve a achicar) la hitbox de todos los enemigos de la escena abierta, multiplicando
// el collider que YA tienen por un factor. A diferencia de EnemySetupFixer -que vuelve a medir la
// capsula sobre los Renderer del modelo y por lo tanto la deja clavada al tamano de la malla- esta
// herramienta no mide nada: toma lo que hay y lo escala. Es la forma de darle al jugador margen de
// golpe sin tocar el modelo ni la escala del enemigo.
//
// Que se escala en cada enemigo:
//  - CapsuleCollider: radius, height y center (el center proporcional al alto, para que los pies
//    sigan apoyados en el origen del padre, que es donde el NavMeshAgent pone al enemigo).
//  - SphereCollider: radius y center (los enemigos extra todavia son la esfera placeholder que
//    deja EnemigosExtraBuilder).
//
// Lo que NO hace falta tocar a mano: el NavMeshAgent, la distancia de frenado y la distancia de
// ataque los deriva EnemyAI del collider en tiempo de ejecucion (SincronizarFormaDelAgente,
// SincronizarDistanciaDeFrenado, RadioPropio), y el radio del agente queda igual topeado por el
// radio con el que se horneo el NavMesh, asi que una hitbox mas gorda no traba al enemigo en los
// pasillos.
//
// Igual que EnemySetupFixer: no guarda la escena (se revisa y se guarda a mano) y todo entra en un
// solo paso de Undo.
public static class EnemyHitboxEscalador
{
    const string MenuBase = "Between Metals/Enemigos/Agrandar hitbox/";

    // Techo de cordura: una hitbox mas grande que esto ya no es "margen de golpe", es un enemigo que
    // empuja al jugador desde media sala (el CharacterController del jugador choca contra el radio).
    const float RadioMaximoRazonable = 6f;

    [MenuItem(MenuBase + "x1.15 (un poco)")]
    public static void Agrandar115() => Escalar(1.15f);

    [MenuItem(MenuBase + "x1.25 (recomendado)")]
    public static void Agrandar125() => Escalar(1.25f);

    [MenuItem(MenuBase + "x1.5 (bastante)")]
    public static void Agrandar150() => Escalar(1.5f);

    [MenuItem(MenuBase + "x0.8 (deshacer un paso)")]
    public static void Achicar080() => Escalar(0.8f);

    public static void Escalar(float factor)
    {
        if (factor <= 0f)
        {
            Debug.LogWarning("EnemyHitboxEscalador: el factor tiene que ser mayor que 0.");
            return;
        }

        EnemyAI[] enemigos = Object.FindObjectsByType<EnemyAI>(FindObjectsInactive.Include);
        if (enemigos.Length == 0)
        {
            Debug.LogWarning("EnemyHitboxEscalador: no hay ningun EnemyAI en la escena abierta.");
            return;
        }

        int grupo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName($"Agrandar hitbox de los enemigos x{factor:0.00}");

        int escalados = 0;
        var avisos = new List<string>();

        foreach (EnemyAI ia in enemigos)
        {
            if (EscalarEnemigo(ia.gameObject, factor, avisos)) escalados++;
        }

        Undo.CollapseUndoOperations(grupo);

        if (escalados > 0) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        foreach (string aviso in avisos) Debug.LogWarning($"EnemyHitboxEscalador: {aviso}");

        Debug.Log($"EnemyHitboxEscalador: {escalados} de {enemigos.Length} enemigos con la hitbox " +
            $"multiplicada x{factor:0.00}. La escena NO se guardo: revisala y guardala a mano.");
    }

    static bool EscalarEnemigo(GameObject enemigo, float factor, List<string> avisos)
    {
        CapsuleCollider capsula = enemigo.GetComponent<CapsuleCollider>();
        if (capsula != null)
        {
            float radioNuevo = capsula.radius * factor;
            float escalaXZ = Mathf.Max(Mathf.Abs(enemigo.transform.lossyScale.x),
                                       Mathf.Abs(enemigo.transform.lossyScale.z));
            if (radioNuevo * escalaXZ > RadioMaximoRazonable)
            {
                avisos.Add($"'{enemigo.name}' quedaria con {radioNuevo * escalaXZ:0.00} m de radio en mundo " +
                    $"(tope {RadioMaximoRazonable} m): se deja como estaba. Con un radio asi el enemigo " +
                    "empuja al jugador desde lejos en vez de darle margen de golpe.");
                return false;
            }

            Undo.RecordObject(capsula, "Agrandar hitbox");

            float radioViejo = capsula.radius;
            float altoViejo = capsula.height;
            float altoNuevo = altoViejo * factor;

            // El center sube junto con el alto para que el borde de abajo no se hunda en el piso:
            // en los enemigos que arma EnemySetupFixer el center es (0, alto/2, 0) justamente para
            // que la capsula arranque en el origen del padre.
            Vector3 centro = capsula.center;
            capsula.center = new Vector3(centro.x * factor, centro.y * factor, centro.z * factor);
            capsula.height = altoNuevo;
            capsula.radius = radioNuevo;

            Debug.Log($"EnemyHitboxEscalador: '{enemigo.name}' capsula " +
                $"radio {radioViejo:0.00} -> {capsula.radius:0.00} m, " +
                $"alto {altoViejo:0.00} -> {altoNuevo:0.00} m.");
            return true;
        }

        SphereCollider esfera = enemigo.GetComponent<SphereCollider>();
        if (esfera != null)
        {
            Undo.RecordObject(esfera, "Agrandar hitbox");
            float radioViejo = esfera.radius;
            esfera.radius = radioViejo * factor;
            esfera.center *= factor;

            Debug.Log($"EnemyHitboxEscalador: '{enemigo.name}' esfera placeholder " +
                $"radio {radioViejo:0.00} -> {esfera.radius:0.00} m.");
            return true;
        }

        avisos.Add($"'{enemigo.name}' no tiene ni CapsuleCollider ni SphereCollider: no hay hitbox que " +
            "agrandar. Corré Between Metals > Enemigos > \"Corregir modelo, hitbox y audio\" primero.");
        return false;
    }
}
