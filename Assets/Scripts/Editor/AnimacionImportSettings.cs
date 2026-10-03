using UnityEditor;
using UnityEngine;

// Configura automaticamente (solo en la primera importacion) como Humanoid los FBX de animacion
// (Assets/Animacion) y los modelos de personajes (Assets/Modelo personajes), para que el
// retargeting entre clips y modelo funcione sin pasos manuales en el Editor.
public class AnimacionImportSettings : AssetPostprocessor
{
    void OnPreprocessModel()
    {
        bool esCarpetaHumanoide = assetPath.StartsWith("Assets/Animacion/")
            || assetPath.StartsWith("Assets/Modelo personajes/");
        if (!esCarpetaHumanoide) return;
        if (!assetImporter.importSettingsMissing) return;

        var mi = (ModelImporter)assetImporter;
        bool isAnim = true;
        mi.animationType = ModelImporterAnimationType.Human;
        mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        mi.importAnimation = isAnim;
    }
}
