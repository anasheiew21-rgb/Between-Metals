using UnityEngine;

// Capa puramente visual del comerciante: cada cierto tiempo lo saca del Idle con uno de los gestos
// del pack de Mixamo (Assets/Animacion_Comerciante) y le hace un gesto de saludo cuando el jugador
// abre la tienda. No sabe nada de comercio: solo lee ShopManager.HayTiendaAbierta y le habla al
// Animator del modelo, el mismo desacople que usa EnemyAnimator con EnemyAI.
//
// Los gestos se disparan por nombre de estado (CrossFade), no por parametros, asi el controller del
// comerciante no necesita ni un trigger ni transiciones de entrada: alcanza con que cada gesto
// tenga su transicion de vuelta al Idle.
//
// Todo se mide en unscaledDeltaTime porque ShopManager congela el juego con Time.timeScale = 0
// mientras la tienda esta abierta (ver ShopManager.AbrirTienda). Por la misma razon el Animator del
// modelo tiene que estar en Update Mode = Unscaled Time, o el enano se queda tieso justo cuando el
// jugador lo tiene delante.
public class ComercianteGestos : MonoBehaviour
{
    [Header("Animator")]
    [Tooltip("Si se deja vacio, busca el Animator en los hijos (el objeto 'Modelo')")]
    [SerializeField] private Animator animator;

    [Tooltip("Nombre del estado de reposo en el Animator Controller. Los gestos solo salen desde " +
             "ahi, para no cortar otro gesto a mitad de camino")]
    [SerializeField] private string estadoIdle = "Idle";

    [Header("Gestos de relleno")]
    [Tooltip("Nombres de los estados de gesto en el controller. Se elige uno al azar cada vez")]
    [SerializeField] private string[] gestos =
    {
        "weight_shift",
        "being_cocky",
        "thoughtful_head_shake",
        "look_away_gesture"
    };

    [Tooltip("Segundos de espera entre gestos: se sortea un valor en este rango cada vez")]
    [SerializeField] private float esperaMinima = 6f;
    [SerializeField] private float esperaMaxima = 14f;

    [Header("Al abrir la tienda")]
    [Tooltip("Gesto que hace al abrirse el panel. Vacio = no hace ninguno")]
    [SerializeField] private string gestoSaludo = "acknowledging";

    [Header("Transicion")]
    [Tooltip("Segundos de mezcla al entrar a un gesto")]
    [SerializeField] private float duracionMezcla = 0.25f;

    float tiempoRestante;
    bool tiendaAbiertaElFrameAnterior;

    void Start()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator == null)
        {
            Debug.LogWarning("ComercianteGestos: no hay un Animator en " + name + " ni en sus hijos; " +
                             "no se van a reproducir gestos.");
            enabled = false;
            return;
        }

        // El comerciante no se desplaza: si el modelo vino del FBX con Apply Root Motion prendido,
        // los gestos lo irian corriendo de a poco fuera del collider con el que PlayerInteraction
        // lo detecta, y en algun momento deja de poder abrirse la tienda. Misma red de seguridad
        // que EnemyAnimator.Start.
        animator.applyRootMotion = false;

        ProgramarProximoGesto();
    }

    void Update()
    {
        DetectarAperturaDeTienda();

        // Con la tienda abierta no se meten gestos de relleno: el jugador esta leyendo el panel y
        // un movimiento de fondo solo distrae. El saludo de arriba es la unica excepcion.
        if (ShopManager.HayTiendaAbierta) return;

        if (gestos == null || gestos.Length == 0) return;

        tiempoRestante -= Time.unscaledDeltaTime;
        if (tiempoRestante > 0f) return;

        ProgramarProximoGesto();
        Reproducir(gestos[Random.Range(0, gestos.Length)]);
    }

    void DetectarAperturaDeTienda()
    {
        bool abierta = ShopManager.HayTiendaAbierta;

        // Solo en el frame en que pasa de cerrada a abierta, para que el saludo salga una vez por
        // visita y no en bucle mientras el panel sigue en pantalla.
        if (abierta && !tiendaAbiertaElFrameAnterior) Reproducir(gestoSaludo);

        tiendaAbiertaElFrameAnterior = abierta;
    }

    void ProgramarProximoGesto()
    {
        float minimo = Mathf.Max(0f, esperaMinima);
        tiempoRestante = Random.Range(minimo, Mathf.Max(minimo, esperaMaxima));
    }

    /// <summary>
    /// Pide que el enano haga ese gesto, por nombre de estado del Animator Controller. Publico para
    /// que DialogoComerciante pueda acompanar una frase con el gesto que le corresponde. Si hay otro
    /// gesto corriendo se ignora el pedido: no se corta uno por la mitad.
    /// </summary>
    public void Reproducir(string estado)
    {
        if (string.IsNullOrEmpty(estado)) return;

        // Si ya hay un gesto corriendo (o la transicion de vuelta al Idle todavia no termino) se
        // deja pasar el turno en vez de cortarlo por la mitad.
        if (animator.IsInTransition(0)) return;
        if (!animator.GetCurrentAnimatorStateInfo(0).IsName(estadoIdle)) return;

        if (!animator.HasState(0, Animator.StringToHash(estado)))
        {
            Debug.LogWarning("ComercianteGestos: el Animator Controller no tiene un estado llamado '" +
                             estado + "'; se ignora. Revisa que el nombre coincida con el del estado " +
                             "en la ventana Animator.");
            return;
        }

        // CrossFadeInFixedTime y no CrossFade porque duracionMezcla esta en segundos; el overload
        // de CrossFade toma la duracion normalizada al largo del clip destino.
        animator.CrossFadeInFixedTime(estado, duracionMezcla);
    }
}
