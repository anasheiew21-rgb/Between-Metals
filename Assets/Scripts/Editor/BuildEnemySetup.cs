using UnityEditor;
using UnityEditor.SceneManagement;

// Orquesta de punta a punta lo que antes eran pasos manuales (Animacion/Build Animator
// Controller + Reemplazar esfera por modelo + Corregir modelo, hitbox y audio): abre Prototype.unity,
// arma Creature.controller, aplica el modelo+Animator a cada EnemyAI, corrige el alineamiento
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
        EnemyModelSetup.ReemplazarEsferaPorModelo();
        EnemySetupFixer.CorregirEnemigos();

        EditorSceneManager.SaveOpenScenes();
    }
}
