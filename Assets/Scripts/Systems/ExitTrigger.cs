using UnityEngine;
using UnityEngine.SceneManagement;

// Salida del laberinto (HU-12/13). No detecta nada por si sola: PlayerInteraction es quien apunta
// con un raycast y llama a Interactuar() cuando el jugador esta mirandola y presiona 'E', mismo
// patron que NPCMerchant. Necesita un Collider (puede ser el de su propio modelo, o uno marcado
// como trigger si se prefiere que no bloquee el paso) para que ese raycast le pegue.
//
// El mapa ya trae, generado por MapaBuilder, un marcador vacio llamado "Punto_Salida" en la
// posicion real de la salida exterior (sin Collider: solo se usaba como referencia visual/de
// testeo de NavMesh). AutoInstalar aprovecha ese marcador para autoinstalarse un Collider y este
// componente en tiempo de ejecucion, para que la salida quede jugable sin tocar el Editor a mano.
[RequireComponent(typeof(Collider))]
public class ExitTrigger : MonoBehaviour, IInteractable
{
    const string NombreMarcador = "Punto_Salida";

    public string TextoPrompt => "Presiona E para salir";

    // Se recrea en cada carga de escena: Reiniciar recarga la escena y RuntimeInitializeOnLoadMethod
    // corre una sola vez (mismo motivo que GameOverUI/Menu/PromptInteraccion, ver #55).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInstalar()
    {
        Instalar();
        SceneManager.sceneLoaded -= OnSceneLoadedInstalar;
        SceneManager.sceneLoaded += OnSceneLoadedInstalar;
    }

    static void OnSceneLoadedInstalar(Scene s, LoadSceneMode m) => Instalar();

    static void Instalar()
    {
        GameObject marca = GameObject.Find(NombreMarcador);
        if (marca == null) return; // esta escena no tiene el marcador (ej. una futura escena de menu)
        if (marca.GetComponent<ExitTrigger>() != null) return;

        if (marca.GetComponent<Collider>() == null)
        {
            SphereCollider collider = marca.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = 1.5f;
        }

        marca.AddComponent<ExitTrigger>();
    }

    public void Interactuar()
    {
        GameManager.Ganar();
    }
}
