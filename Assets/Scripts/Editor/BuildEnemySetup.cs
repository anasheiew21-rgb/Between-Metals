using UnityEditor;
using UnityEditor.SceneManagement;

// Orquesta de punta a punta lo que antes eran dos pasos manuales (Animacion/Build Animator
// Controller + Between Metals/Enemigos/Reemplazar esfera por modelo): abre Prototype.unity,
// arma Creature.controller y aplica el modelo+Animator a cada EnemyAI, despues guarda la escena.
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

        EditorSceneManager.SaveOpenScenes();
    }
}
