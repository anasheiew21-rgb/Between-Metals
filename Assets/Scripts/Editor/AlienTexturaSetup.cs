using UnityEditor;
using UnityEngine;

// Pone la textura base color del alien rigueado (Assets/Modelo personajes/alien_creature_basecolor.png)
// en el material que usa el enemigo (el mismo que EnemySetupFixer asigna). Los mapas de normal y
// metal/rugosidad del material viejo pertenecen a otro UV, asi que se quitan para no deformar la luz.
public static class AlienTexturaSetup
{
    const string MaterialPath = "Assets/TripoModels/alien_creature_3d_model_2/Materials/alien_creature_3d_model_2.mat";
    const string TexturaPath = "Assets/Modelo personajes/alien_creature_basecolor.png";

    [MenuItem("Between Metals/Enemigos/Aplicar textura base del alien")]
    public static void Aplicar()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        var textura = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturaPath);

        if (material == null || textura == null)
        {
            Debug.LogError($"AlienTexturaSetup: falta material ({MaterialPath}) o textura ({TexturaPath}).");
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return;
        }

        material.SetTexture("_BaseMap", textura);
        material.SetTexture("_BumpMap", null);
        material.SetTexture("_MetallicGlossMap", null);
        material.DisableKeyword("_NORMALMAP");
        material.DisableKeyword("_METALLICSPECGLOSSMAP");
        material.SetColor("_BaseColor", Color.white);

        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        Debug.Log($"AlienTexturaSetup: textura '{textura.name}' aplicada a '{material.name}'.");

        if (Application.isBatchMode) EditorApplication.Exit(0);
    }
}
