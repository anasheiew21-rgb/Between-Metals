using UnityEditor;
using UnityEditor.SceneManagement;

// Orquesta de punta a punta lo que antes eran pasos manuales (Animacion/Build Animator
// Controller + Reemplazar esfera por modelo + Corregir modelo, hitbox y audio): abre Prototype.unity,
// arma Creature.controller, aplica el modelo+Animator a cada EnemyAI, le copia a los enemigos
// extra el modelo ya ajustado del primero (EnemigoModeloDuplicador), corrige el alineamiento
// modelo/hitbox, el material y el audio (EnemySetupFixer), y despues guarda la escena.
// Pensado tambien para -executeMethod BuildEnemySetup.Run en modo batch (CI/automatizacion).
public static class BuildEnemySetup
{
    const string ScenePath = "Assets/Scenes/Prototype.unity";

    [MenuItem("Between Metals/Enemigos/Build + Setup completo")]
    public static void Run()
    {
        EditorSceneManager.OpenScene(ScenePath);

        AnimacionControllerBuilder.Build();

        // El Animation Event del golpe va en el importador de los FBX de ataque, asi que es
        // independiente de la escena y puede ir antes que todo lo demas. Sin el, EnemyAI aplica el
        // dano por un temporizador ciego en vez de en el frame del impacto (ver EventoGolpeSetup).
        EventoGolpeSetup.Agregar();
        // ANTES de EnemyModelSetup: los dos saben darle un modelo a un enemigo que no tiene, pero
        // el duplicador lo copia del enemigo 1 ya ajustado y EnemyModelSetup instancia el FBX
        // crudo. Al revés, EnemyModelSetup les pondría el FBX a los enemigos 2 y 3 primero y el
        // duplicador no encontraría nada que hacer.
        //
        // En una escena donde todavía no hay NINGÚN modelo esto no hace nada (no hay de dónde
        // copiar) y los arma EnemyModelSetup, que es el comportamiento de siempre.
        EnemigoModeloDuplicador.Duplicar(corregirDespues: false); // EnemySetupFixer corre abajo

        EnemyModelSetup.ReemplazarEsferaPorModelo();
        EnemySetupFixer.CorregirEnemigos();

        EditorSceneManager.SaveOpenScenes();
    }
}
