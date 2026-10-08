using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Hace que darle Play en el Editor arranque siempre por el menu de inicio, sin importar que escena
// tengas abierta.
//
// En un build esto ya funciona solo: MenuPrincipal.unity es la escena 0 de las Build Settings. Pero
// el Editor no mira las Build Settings, arranca la escena abierta: con Prototype.unity abierta,
// Play te deja directamente dentro del laberinto y el menu no aparece nunca. Unity resuelve eso con
// EditorSceneManager.playModeStartScene, que es lo que se configura aca.
//
// Menu: Between Metals > Menu > Arrancar siempre en el menu (Play)   [con tilde cuando esta activo]
//
// Es una preferencia de trabajo, no un dato del proyecto, asi que vive en EditorPrefs (por persona
// y por maquina) y no en un asset: a quien este iterando sobre el laberinto le va a molestar tener
// que pasar por el menu en cada Play, y lo apaga sin tocarle el flujo al resto del equipo. Arranca
// encendida, que es el comportamiento que se espera por defecto.
[InitializeOnLoad]
public static class ArranqueEnMenu
{
    const string RutaEscenaMenu = "Assets/Scenes/MenuPrincipal.unity";
    const string RutaMenuItem = "Between Metals/Menu/Arrancar siempre en el menu (Play)";

    // El nombre del proyecto va en la clave porque EditorPrefs es global a la instalacion de Unity:
    // sin eso, dos proyectos abiertos con el mismo Editor se pisarian la preferencia.
    static string Clave => "BetweenMetals." + PlayerSettings.productName + ".ArrancarEnMenu";

    static bool Activo
    {
        get => EditorPrefs.GetBool(Clave, true);
        set => EditorPrefs.SetBool(Clave, value);
    }

    static ArranqueEnMenu()
    {
        // delayCall y no directo: en un recargado de dominio el AssetDatabase todavia puede no
        // estar listo, y LoadAssetAtPath devolveria null aunque la escena exista.
        EditorApplication.delayCall += () => Aplicar(Activo, avisar: false);
    }

    [MenuItem(RutaMenuItem, priority = 603)]
    static void Alternar()
    {
        Activo = !Activo;
        Aplicar(Activo, avisar: true);
    }

    // UnityEditor.Menu y no Menu a secas: en este proyecto Menu es el MonoBehaviour del menu de
    // pausa (Assets/Scripts/UI/Menu.cs) y se robaria el nombre.
    [MenuItem(RutaMenuItem, true)]
    static bool ValidarAlternar()
    {
        UnityEditor.Menu.SetChecked(RutaMenuItem, Activo);
        return true;
    }

    static void Aplicar(bool activo, bool avisar)
    {
        if (!activo)
        {
            EditorSceneManager.playModeStartScene = null;
            if (avisar) Debug.Log("ArranqueEnMenu: desactivado. Play arranca en la escena que tengas abierta.");
            return;
        }

        var escena = AssetDatabase.LoadAssetAtPath<SceneAsset>(RutaEscenaMenu);
        if (escena == null)
        {
            // Solo se avisa si lo pidio una persona desde el menu: en el arranque automatico un
            // aviso por cada recargado de dominio seria ruido puro.
            if (avisar)
            {
                Debug.LogWarning("ArranqueEnMenu: no se encontro " + RutaEscenaMenu +
                    ". Corre 'Between Metals > Menu > Crear escena de menu principal'.");
            }
            return;
        }

        EditorSceneManager.playModeStartScene = escena;
        if (avisar) Debug.Log("ArranqueEnMenu: activado. Play va a arrancar siempre en " + RutaEscenaMenu + ".");
    }
}
