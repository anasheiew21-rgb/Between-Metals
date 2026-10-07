using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

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
