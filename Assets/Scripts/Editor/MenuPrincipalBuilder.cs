using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Crea la escena del menu de inicio (Assets/Scenes/MenuPrincipal.unity) y la deja primera en las
// Build Settings, para que el juego arranque ahi y no directo en la partida.
//
// Menu: Between Metals > Menu > Crear escena de menu principal
//
// La escena es a proposito lo mas liviana posible: una camara que limpia a negro y un unico objeto
// con Menu (en modo Principal) + FondoMenu. Todo lo que se autoinstala en la escena de juego
// (PlayerUI, GestorBarreras, NavMeshRuntimeBuilder, ExitTrigger, EquipoJugador...) ya se saltea
// solo cuando no hay jugador ni mapa, asi que no hace falta excluir nada a mano.
public static class MenuPrincipalBuilder
{
    const string RutaEscenaMenu = "Assets/Scenes/MenuPrincipal.unity";
    const string RutaEscenaJuego = "Assets/Scenes/Prototype.unity";
    const string NombreEscenaJuego = "Prototype";
    const string RutaFondo = "Assets/UI/Fondo_MenuPrincipal.jpg";

    // El afiche ya trae el nombre del juego, asi que el menu apaga el suyo (titulo vacio) y baja
    // la columna de botones para no taparlo. Los numeros estan en px del lienzo de 1280x720:
    // el titulo del afiche termina cerca de y=240 y la tira de iconos de abajo arranca en y=612,
    // asi que los botones entran justo en el medio, sobre el pasillo.
    const float DesplazamientoBotonesConAfiche = 90f;
    const float VeloConAfiche = 0.3f;

    [MenuItem("Between Metals/Menu/Crear escena de menu principal", priority = 600)]
    static void Crear()
    {
        if (System.IO.File.Exists(RutaEscenaMenu))
        {
            bool sobrescribir = EditorUtility.DisplayDialog(
                "La escena ya existe",
                RutaEscenaMenu + " ya existe.\n\nSi la sobrescribis vas a perder la foto de fondo que le hayas asignado.",
                "Sobrescribir", "Cancelar");

            if (!sobrescribir) return;
        }

        // Antes de cambiar de escena, que el equipo pueda guardar lo que tenga sin terminar.
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ---- Camara ----
        var camaraGO = new GameObject("Camara_Menu");
        Camera camara = camaraGO.AddComponent<Camera>();
        camara.clearFlags = CameraClearFlags.SolidColor;
        camara.backgroundColor = new Color(0.02f, 0.02f, 0.03f, 1f);
        // Nada que renderizar en 3D: el menu es todo UI, asi que la camara no mira ninguna capa.
        // Asi no cuesta nada en una PC de pocos recursos.
        camara.cullingMask = 0;
        camaraGO.AddComponent<AudioListener>();
        camaraGO.tag = "MainCamera";

        // ---- Menu + fondo ----
        var menuGO = new GameObject("MenuPrincipal");
        var menu = menuGO.AddComponent<Menu>();
        menu.modo = Menu.Modo.Principal;
        menu.escenaDeJuego = NombreEscenaJuego;
        // Bajo a proposito: el velo negro del menu tiene que dejar ver la foto de fondo.
        menu.oscurecerFondo = 0.35f;
        menu.openOnStart = true;

        menuGO.AddComponent<FondoMenu>();

        if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");

        EditorSceneManager.SaveScene(escena, RutaEscenaMenu);
        RegistrarEnBuildSettings();

        Selection.activeGameObject = menuGO;

        Debug.Log(
            "MenuPrincipalBuilder: creada " + RutaEscenaMenu + " y puesta primera en las Build Settings.\n" +
            "  Para poner la foto de fondo: seleccioná 'MenuPrincipal' en la Hierarchy y arrastrá tu Sprite\n" +
            "  al campo 'Imagen de fondo' del componente FondoMenu (la textura tiene que estar importada\n" +
            "  como Sprite (2D and UI)).");
    }

    // Deja la foto de Assets/UI lista y puesta en el menu de inicio: la importa como Sprite (sin
    // eso el campo de FondoMenu no la acepta), la asigna, y acomoda el menu para que el afiche se
    // lea (titulo propio apagado, botones mas abajo, velo mas suave).
    //
    // Se hace por codigo y no a mano porque son cinco ajustes repartidos entre el importador, un
    // componente y otro, y olvidarse de uno da justo el sintoma confuso de "asigne la foto y no
    // se ve" o "se ve el titulo dos veces".
    [MenuItem("Between Metals/Menu/Asignar foto de fondo", priority = 602)]
    static void AsignarFondo()
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(RutaFondo);
        if (sprite == null)
        {
            // Si existe el archivo pero todavia no es un Sprite, se corrige el importador y se
            // vuelve a pedir: es el caso normal la primera vez, porque Unity importa los .jpg
            // como textura suelta.
            if (!ConfigurarComoSprite(RutaFondo))
            {
                EditorUtility.DisplayDialog("Foto de fondo",
                    "No se encontro " + RutaFondo + ".\n\nCopia ahi la imagen y volve a correr este comando.", "OK");
                return;
            }

            sprite = AssetDatabase.LoadAssetAtPath<Sprite>(RutaFondo);
            if (sprite == null)
            {
                Debug.LogError("MenuPrincipalBuilder: " + RutaFondo + " no se pudo importar como Sprite.");
                return;
            }
        }
        else
        {
            ConfigurarComoSprite(RutaFondo);
        }

        if (!System.IO.File.Exists(RutaEscenaMenu))
        {
            EditorUtility.DisplayDialog("Foto de fondo",
                "Todavia no existe " + RutaEscenaMenu + ".\n\nCorre primero 'Crear escena de menu principal'.", "OK");
            return;
        }

        // La escena del menu tiene que estar abierta para poder tocar sus componentes. Si hay otra
        // abierta con cambios, que el equipo decida antes de perderlos.
        if (SceneManager.GetActiveScene().path != RutaEscenaMenu)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(RutaEscenaMenu);
        }

        FondoMenu fondo = Object.FindAnyObjectByType<FondoMenu>();
        if (fondo == null)
        {
            Debug.LogError("MenuPrincipalBuilder: la escena del menu no tiene ningun FondoMenu.");
            return;
        }

        var soFondo = new SerializedObject(fondo);
        soFondo.FindProperty("imagenDeFondo").objectReferenceValue = sprite;
        // Contener y no Cubrir: el afiche tiene contenido pegado a los bordes (el escudo, el sello
        // de free-to-play, la tira de iconos) y recortarlo se comeria justo eso. Entra entero y las
        // franjas quedan del color de fondo, que es casi negro igual que el afiche.
        soFondo.FindProperty("modoEncuadre").enumValueIndex = (int)FondoMenu.Encuadre.Contener;
        soFondo.FindProperty("colorDeFondo").colorValue = new Color(0.02f, 0.02f, 0.03f, 1f);
        soFondo.ApplyModifiedProperties();

        Menu menu = Object.FindAnyObjectByType<Menu>();
        if (menu != null)
        {
            Undo.RecordObject(menu, "Acomodar menu para la foto");
            menu.gameTitle = string.Empty;   // el nombre del juego ya esta en la foto
            menu.oscurecerFondo = VeloConAfiche;
            menu.desplazamientoBotones = DesplazamientoBotonesConAfiche;
            EditorUtility.SetDirty(menu);
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        Debug.Log(
            "MenuPrincipalBuilder: foto de fondo puesta.\n" +
            "  " + RutaFondo + " -> importada como Sprite y asignada a FondoMenu\n" +
            "  Encuadre: Contener (entra entera, sin recortar los bordes del afiche)\n" +
            "  Menu: titulo propio apagado, velo " + VeloConAfiche + ", botones +" + DesplazamientoBotonesConAfiche + " px\n" +
            "  Escena guardada. Dale Play para verlo.");
    }

    // Un .jpg entra al proyecto como textura suelta: hasta que no sea Sprite (2D and UI), el campo
    // de FondoMenu no la acepta. Devuelve false si el archivo no existe.
    static bool ConfigurarComoSprite(string ruta)
    {
        if (AssetImporter.GetAtPath(ruta) is not TextureImporter importador) return false;

        bool cambio = false;

        if (importador.textureType != TextureImporterType.Sprite) { importador.textureType = TextureImporterType.Sprite; cambio = true; }
        if (importador.spriteImportMode != SpriteImportMode.Single) { importador.spriteImportMode = SpriteImportMode.Single; cambio = true; }
        if (!importador.sRGBTexture) { importador.sRGBTexture = true; cambio = true; }
        if (!importador.mipmapEnabled) { importador.mipmapEnabled = true; cambio = true; }
        // La foto es de 1536x1024: pedir mas de 2048 no agrega nada y ocuparia memoria de mas.
        if (importador.maxTextureSize > 2048) { importador.maxTextureSize = 2048; cambio = true; }
        // Comprimida: es un fondo a pantalla completa y sin comprimir se lleva varios MB de VRAM,
        // justo lo que no sobra en las PCs a las que apunta el proyecto.
        if (importador.textureCompression == TextureImporterCompression.Uncompressed)
        {
            importador.textureCompression = TextureImporterCompression.Compressed;
            cambio = true;
        }

        if (cambio) importador.SaveAndReimport();
        return true;
    }

    [MenuItem("Between Metals/Menu/Revisar Build Settings", priority = 601)]
    static void Revisar()
    {
        RegistrarEnBuildSettings();

        var lineas = new List<string>();
        for (int i = 0; i < EditorBuildSettings.scenes.Length; i++)
        {
            EditorBuildSettingsScene e = EditorBuildSettings.scenes[i];
            lineas.Add("  " + i + ": " + e.path + (e.enabled ? "" : "  (desactivada)"));
        }

        Debug.Log("Build Settings:\n" + string.Join("\n", lineas));
    }

    // El menu tiene que ser la escena 0 (la que carga el juego al arrancar) y la de juego tiene
    // que estar en la lista para que SceneManager.LoadScene("Prototype") la encuentre. Se respeta
    // cualquier otra escena que haya: solo se reordenan estas dos.
    static void RegistrarEnBuildSettings()
    {
        var escenas = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

        escenas.RemoveAll(e => e.path == RutaEscenaMenu || e.path == RutaEscenaJuego);

        if (System.IO.File.Exists(RutaEscenaJuego))
        {
            escenas.Insert(0, new EditorBuildSettingsScene(RutaEscenaJuego, true));
        }

        if (System.IO.File.Exists(RutaEscenaMenu))
        {
            escenas.Insert(0, new EditorBuildSettingsScene(RutaEscenaMenu, true));
        }

        EditorBuildSettings.scenes = escenas.ToArray();
    }
}
