using UnityEngine;

// Musica de fondo del juego: un loop de suspenso que arranca en el menu principal y sigue sonando
// sin cortarse al cargar la escena de juego, al morir o al reiniciar.
//
// Se autoinstancia con [RuntimeInitializeOnLoadMethod] y vive en DontDestroyOnLoad, mismo patron
// que Menu.AutoCreate: no hay que acordarse de agregarla a cada escena, y mas importante, la
// musica NO se reinicia en cada cambio de escena. Un AudioSource puesto a mano en Prototype.unity
// y otro en MenuPrincipal.unity volverian a empezar el tema desde cero cada vez que el jugador
// aprieta "Jugar" o "Reiniciar", que es exactamente lo que rompe el clima.
//
// El volumen lo maneja el slider "Musica" del menu, via el grupo music del mixer (AudioPreferences).
[DisallowMultipleComponent]
public class MusicaAmbiente : MonoBehaviour
{
    // Por debajo de los efectos a proposito: la musica es cama, no protagonista. Lo que el jugador
    // tiene que oir claro son los pasos del enemigo, no el drone.
    const float VolumenBase = 0.45f;

    // Fade de entrada: el loop arranca en silencio y sube, asi el juego no empieza con un golpe
    // de drone en el primer frame.
    const float DuracionFadeIn = 3f;

    static MusicaAmbiente instancia;

    AudioSource fuente;
    float volumenObjetivo = VolumenBase;

    /// <summary>La musica que esta sonando, o null si todavia no arranco.</summary>
    public static MusicaAmbiente Instancia => instancia;

    // AfterSceneLoad (y no BeforeSceneLoad) porque hace falta que la escena ya este cargada para
    // que haya un AudioListener: sin listener el AudioSource suena para nadie.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCrear()
    {
        if (instancia != null) return;
        if (FindAnyObjectByType<MusicaAmbiente>() != null) return;

        new GameObject("MusicaAmbiente").AddComponent<MusicaAmbiente>();
    }

    void Awake()
    {
        // Si ya hay una sonando (el jugador volvio al menu principal y esta escena trae otra),
        // esta copia se va sin cortar la que ya venia.
        if (instancia != null && instancia != this)
        {
            Destroy(gameObject);
            return;
        }

        instancia = this;
        DontDestroyOnLoad(gameObject);

        fuente = GetComponent<AudioSource>();
        if (fuente == null) fuente = gameObject.AddComponent<AudioSource>();

        fuente.clip = BibliotecaDeSonidos.Clip(BibliotecaDeSonidos.MusicaSuspenso);
        fuente.loop = true;
        fuente.playOnAwake = false;
        fuente.spatialBlend = 0f;          // 2D: la musica no tiene posicion en el laberinto
        fuente.ignoreListenerPause = true; // sigue sonando con el menu de pausa abierto
        fuente.volume = 0f;                // el fade de abajo la sube
        AudioPreferences.RutearAMusica(fuente);

        if (fuente.clip == null) return; // el aviso ya lo dio BibliotecaDeSonidos
        fuente.Play();
    }

    void OnDestroy()
    {
        if (instancia == this) instancia = null;
    }

    void Update()
    {
        if (fuente == null) return;

        // Time.unscaledDeltaTime y no deltaTime: con el menu de pausa abierto (timeScale 0) el
        // fade tiene que seguir avanzando igual, si no la musica se queda congelada en silencio
        // cuando el juego arranca con el menu abierto.
        float paso = Time.unscaledDeltaTime / Mathf.Max(DuracionFadeIn, 0.0001f);
        fuente.volume = Mathf.MoveTowards(fuente.volume, volumenObjetivo, paso);
    }

    /// <summary>
    /// Baja o sube la musica respecto de su volumen normal (0 = muda, 1 = normal). Para momentos
    /// en los que la musica tiene que dejar pasar otra cosa: una cinematica, un silencio antes de
    /// un susto. El cambio es gradual, no un corte.
    /// </summary>
    public void AjustarIntensidad(float factor)
    {
        volumenObjetivo = VolumenBase * Mathf.Clamp01(factor);
    }
}
