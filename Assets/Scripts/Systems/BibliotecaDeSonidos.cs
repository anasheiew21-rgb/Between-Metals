using System.Collections.Generic;
using UnityEngine;

// Punto unico de acceso a los sonidos del juego. Los clips viven en Assets/Audio/Resources y se
// piden POR NOMBRE (Resources.Load), no por una referencia serializada en el Inspector.
//
// Por que por nombre y no con campos [SerializeField] en cada script: los sonidos los necesitan
// cosas que se crean en tiempo de ejecucion (el Menu se autoinstancia, la musica tambien, los
// pasos se agregan al jugador desde codigo) y objetos que viven en dos escenas distintas. Con
// referencias serializadas habria que cablear a mano cada escena y cada prefab, y cualquiera que
// agregue un enemigo o un item nuevo se olvidaria de alguna. Con esta convencion, dejar el .wav
// en la carpeta correcta ES el cableado. Mismo criterio que ya usa AudioPreferences para cargar
// MainMixer desde Resources.
//
// Los scripts que SI tienen un campo en el Inspector (ItemPickup.pickupSound, MonedaPickup.
// sonidoRecoger) lo siguen respetando: la biblioteca es el fallback, no un reemplazo.
//
// Para cambiar un sonido NO hace falta tocar C#: se reemplaza el .wav dejando el mismo nombre.
// Los .wav actuales se generan con Tools/GeneradorAudio (ver Docs/audio-y-modelo-enemigos.md).
public static class BibliotecaDeSonidos
{
    // Rutas relativas a Assets/Audio/Resources, sin extension (es lo que espera Resources.Load).
    public const string BotonClick = "UI/boton_click";
    public const string BotonHover = "UI/boton_hover";
    public const string BotonAtras = "UI/boton_atras";

    public const string MusicaSuspenso = "Music/suspenso_loop";

    public const string LinternaEncender = "Flashlight/linterna_on";
    public const string LinternaApagar = "Flashlight/linterna_off";

    public const string ItemGenerico = "Items/pickup_generico";
    public const string Moneda = "Items/pickup_moneda";

    // Prefijo de los sonidos por item: el archivo es "pickup_" + ItemData.itemId. Un item nuevo
    // con sonido propio es un .wav mas en Assets/Audio/Resources/Items, sin tocar codigo.
    const string PrefijoItem = "Items/pickup_";

    // Pasos: paso_1..paso_N. La cantidad se descubre cargando hasta que falta uno, asi agregar un
    // paso_5.wav lo suma a la rotacion sin cambiar esta constante.
    const string PrefijoPaso = "Player/paso_";
    const int MaximoPasos = 16; // techo de seguridad para no iterar al infinito si algo sale mal

    // Cache de clips. El valor puede ser null a proposito: significa "ya se busco y no esta", y
    // evita que un clip faltante dispare un Resources.Load por frame (los pasos y el hover de los
    // botones se piden muchas veces por segundo).
    static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
    static readonly HashSet<string> yaAvisados = new HashSet<string>();

    static AudioClip[] pasos;
    static AudioSource fuente2D;

    /// <summary>
    /// Clip de <paramref name="ruta"/> (relativa a Assets/Audio/Resources, sin extension), o null
    /// si no existe. Avisa una sola vez por ruta faltante: el juego sigue andando sin sonido.
    /// </summary>
    public static AudioClip Clip(string ruta)
    {
        if (string.IsNullOrEmpty(ruta)) return null;

        if (cache.TryGetValue(ruta, out AudioClip enCache)) return enCache;

        AudioClip clip = Resources.Load<AudioClip>(ruta);
        cache[ruta] = clip;

        if (clip == null && yaAvisados.Add(ruta))
        {
            Debug.LogWarning($"BibliotecaDeSonidos: no se encontro el clip 'Assets/Audio/Resources/{ruta}'. " +
                "Corré Tools/GeneradorAudio/generar.ps1 para regenerar los .wav del juego.");
        }

        return clip;
    }

    /// <summary>
    /// Sonido propio del item con ese itemId ("Items/pickup_&lt;itemId&gt;"), o el generico si ese
    /// item todavia no tiene uno. Asi cada item suena distinto sin que falte sonido nunca.
    /// </summary>
    public static AudioClip SonidoDeItem(string itemId)
    {
        if (!string.IsNullOrWhiteSpace(itemId))
        {
            AudioClip propio = Clip(PrefijoItem + itemId.Trim().ToLowerInvariant());
            if (propio != null) return propio;
        }

        return Clip(ItemGenerico);
    }

    /// <summary>Los pasos disponibles (paso_1, paso_2, ...). Array vacio si no hay ninguno.</summary>
    public static AudioClip[] Pasos
    {
        get
        {
            if (pasos != null) return pasos;

            var encontrados = new List<AudioClip>();
            for (int i = 1; i <= MaximoPasos; i++)
            {
                AudioClip clip = Clip(PrefijoPaso + i);
                if (clip == null) break; // la numeracion es contigua: el primer hueco corta
                encontrados.Add(clip);
            }

            pasos = encontrados.ToArray();
            return pasos;
        }
    }

    /// <summary>
    /// Reproduce un sonido sin posicion en el mundo (UI, musica, avisos). Suena igual con el juego
    /// pausado, que es justo lo que hace falta para los botones del menu: el menu de pausa corre
    /// con Time.timeScale = 0.
    /// </summary>
    public static void Reproducir2D(AudioClip clip, float volumen = 1f)
    {
        if (clip == null) return;

        AudioSource fuente = Fuente2D;
        if (fuente == null) return;

        fuente.PlayOneShot(clip, Mathf.Clamp01(volumen));
    }

    /// <summary>Igual que la sobrecarga de arriba, pero buscando el clip por nombre.</summary>
    public static void Reproducir2D(string ruta, float volumen = 1f)
    {
        Reproducir2D(Clip(ruta), volumen);
    }

    /// <summary>
    /// Reproduce un sonido en un punto del mundo, con atenuacion por distancia y ruteado al grupo
    /// sfx del mixer (el slider "Efectos" del menu lo afecta). Para lo que pasa en una posicion
    /// concreta: recoger un item, una moneda.
    /// </summary>
    public static void ReproducirEnPunto(AudioClip clip, Vector3 posicion, float volumen = 1f)
    {
        if (clip == null) return;

        // AudioSource.PlayClipAtPoint crea su propio GameObject temporal y NO lo rutea al mixer,
        // asi que el objeto y el AudioSource se arman aca para poder pasarlo por el grupo sfx y
        // elegir el rolloff; Destroy con el largo del clip lo limpia solo.
        var temporal = new GameObject("SonidoTemporal_" + clip.name);
        temporal.transform.position = posicion;

        AudioSource fuente = temporal.AddComponent<AudioSource>();
        fuente.clip = clip;
        fuente.volume = Mathf.Clamp01(volumen);
        fuente.spatialBlend = 1f; // 3D: se oye desde donde esta el objeto
        fuente.rolloffMode = AudioRolloffMode.Logarithmic;
        fuente.minDistance = 1.5f;
        fuente.maxDistance = 20f;
        AudioPreferences.RutearASfx(fuente);
        fuente.Play();

        Object.Destroy(temporal, clip.length + 0.1f);
    }

    /// <summary>Igual que la sobrecarga de arriba, pero buscando el clip por nombre.</summary>
    public static void ReproducirEnPunto(string ruta, Vector3 posicion, float volumen = 1f)
    {
        ReproducirEnPunto(Clip(ruta), posicion, volumen);
    }

    // AudioSource 2D compartido, en un objeto que sobrevive al cambio de escena: el menu principal
    // y el menu de pausa estan en escenas distintas y los dos tocan los mismos sonidos de boton.
    static AudioSource Fuente2D
    {
        get
        {
            if (fuente2D != null) return fuente2D;

            var objeto = new GameObject("Sonidos2D");
            Object.DontDestroyOnLoad(objeto);

            fuente2D = objeto.AddComponent<AudioSource>();
            fuente2D.playOnAwake = false;
            fuente2D.spatialBlend = 0f;           // 2D: se oye igual desde cualquier lado
            fuente2D.ignoreListenerPause = true;  // sigue sonando si algun dia se pausa el listener
            AudioPreferences.RutearASfx(fuente2D);

            return fuente2D;
        }
    }

    // Los dominios de recarga del Editor (entrar y salir de Play) no resetean los estaticos por
    // defecto, y el AudioSource cacheado queda apuntando a un objeto destruido. Se limpia todo al
    // entrar a Play para que la primera partida y la decima se comporten igual.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reiniciar()
    {
        cache.Clear();
        yaAvisados.Clear();
        pasos = null;
        fuente2D = null;
    }
}
