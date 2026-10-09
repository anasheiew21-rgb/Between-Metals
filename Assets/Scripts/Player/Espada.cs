using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// La espada del jugador (cuerpo a cuerpo, mano derecha), vista desde el objeto del arma que
// instancia EquipoJugador. Mismo patron que Ballesta: equiparla la trae a la mano y guardarla se la
// lleva, asi que "tener la espada equipada" y "poder pegar con arco" son la misma cosa.
//
// Antes el golpe cuerpo a cuerpo (con o sin arma) era un SphereCast instantaneo en el mismo frame
// del clic (ver PlayerCombat.GolpearAManoLimpia, que sigue siendo asi a mano limpia). Con la espada
// en la mano, PlayerCombat delega aca: el golpe pasa a tener tres fases -preparacion, ventana activa
// y recuperacion- que:
//
//   * detectan un ARCO de ataque real (angulo + alcance desde la camara hasta la SUPERFICIE del
//     objetivo, no su pivote -ver PalparObjetivos-) en vez de un solo punto instantaneo;
//   * solo pueden conectar durante la ventana activa, no en todo el swing;
//   * no le pegan dos veces al mismo objetivo en un mismo golpe (HashSet de lo ya golpeado, se
//     limpia al arrancar cada swing).
//
// Sin Animator -el proyecto todavia no tiene uno para el arma, ver el comentario de PlayerCombat-:
// el swing se anima por codigo, moviendo la propia espada entre unas pocas poses locales relativas a
// la que le deja EquipoJugador. Nada de brillos, colores ni particulas: un tajo corto y seco, acorde
// a la sobriedad de la estetica de terror industrial del juego.
[DisallowMultipleComponent]
public class Espada : MonoBehaviour
{
    [Header("Arco de ataque")]
    [Tooltip("Metros desde la camara hasta la SUPERFICIE del objetivo (no su pivote/centro) hasta " +
             "donde llega el filo.")]
    [SerializeField] private float alcance = 2.2f;

    [Tooltip("Margen extra (metros) sumado a 'alcance' para la esfera de busqueda de candidatos: " +
             "tiene que ser mayor o igual al radio del cuerpo mas grande que se quiera poder tocar, " +
             "o la esfera se vuelve el cuello de botella antes de llegar al filtro de arco real.")]
    [SerializeField] private float margenDeteccion = 1f;

    [Tooltip("Angulo TOTAL (grados), centrado en hacia donde mira la camara, en el que el golpe puede conectar.")]
    [SerializeField] private float anguloAtaque = 100f;

    [Tooltip("Capas contra las que puede conectar el golpe.")]
    [SerializeField] private LayerMask capasAtaque = ~0;

    [Header("Tiempos del golpe")]
    [Tooltip("Segundos de preparacion (el swing hacia atras) antes de que el filo pueda pegar.")]
    [SerializeField] private float tiempoPreparacion = 0.1f;

    [Tooltip("Segundos durante los que el golpe puede conectar (la ventana activa).")]
    [SerializeField] private float tiempoActivo = 0.16f;

    [Tooltip("Segundos en los que la espada vuelve a la pose de guardia.")]
    [SerializeField] private float tiempoRecuperacion = 0.18f;

    [Header("Pose de guardia (reposo)")]
    [Tooltip("Giro LOCAL (grados) sumado al que deja EquipoJugador, para que la espada descanse " +
             "con el filo hacia arriba -en guardia- en vez de colgando hacia adelante.")]
    [SerializeField] private Vector3 giroGuardia = new Vector3(-60f, 15f, -10f);

    [Tooltip("Desplazamiento LOCAL (metros) sumado a la posicion de EquipoJugador para la guardia.")]
    [SerializeField] private Vector3 posicionGuardia = new Vector3(0.03f, 0.08f, -0.05f);

    [Header("Swing (sin Animator): preparacion -> barrido -> guardia")]
    [Tooltip("Giro LOCAL (grados), relativo a la guardia, al final de la preparacion: el arma se " +
             "levanta y se lleva hacia un costado antes de barrer.")]
    [SerializeField] private Vector3 giroPreparacion = new Vector3(15f, -55f, -25f);

    [Tooltip("Desplazamiento LOCAL (metros), relativo a la guardia, al final de la preparacion.")]
    [SerializeField] private Vector3 posicionPreparacion = new Vector3(0.1f, 0.06f, -0.12f);

    [Tooltip("Giro LOCAL (grados), relativo a la guardia, al final del barrido: el tajo cruzando " +
             "hacia el otro costado.")]
    [SerializeField] private Vector3 giroActivo = new Vector3(-20f, 60f, 25f);

    [Tooltip("Desplazamiento LOCAL (metros), relativo a la guardia, al final del barrido.")]
    [SerializeField] private Vector3 posicionActivo = new Vector3(-0.16f, -0.08f, 0.16f);

    // Esfera de deteccion compartida: el golpe de una espada se resuelve entero dentro de su propia
    // corrutina (no hay dos golpes en curso a la vez, ver EstaAtacando), asi que un solo buffer
    // alcanza y no se asigna memoria por frame.
    static readonly Collider[] bufferDeteccion = new Collider[16];

    readonly HashSet<IDamageable> golpeadosEnEsteSwing = new HashSet<IDamageable>();

    Camera camara;
    Transform cuerpoDelJugador;
    Vector3 posicionBase;
    Quaternion rotacionBase;
    Vector3 posicionGuardiaAbsoluta;
    Quaternion rotacionGuardiaAbsoluta;
    bool poseCapturada;
    Coroutine golpeEnCurso;

    /// <summary>Verdadero mientras el golpe (preparacion + ventana activa + recuperacion) esta en curso.</summary>
    public bool EstaAtacando => golpeEnCurso != null;

    // La guardia se aplica apenas la espada entra en juego (no recien al primer golpe): es la pose
    // en la que el jugador la ve colgando mientras camina, no solo mientras ataca.
    void Start()
    {
        CapturarPoseDeGuardia();
    }

    /// <summary>
    /// Arranca el golpe. Devuelve false sin hacer nada si ya hay uno en curso: PlayerCombat no
    /// deberia llegar a pedirlo (su propio cooldown ya es mas largo que un swing completo), pero la
    /// guarda queda igual, mismo criterio defensivo que Ballesta.PuedeDisparar.
    /// </summary>
    public bool Golpear(float dano)
    {
        if (EstaAtacando) return false;

        CapturarPoseDeGuardia();
        golpeEnCurso = StartCoroutine(SwingCompleto(dano));
        return true;
    }

    // La base es la pose que le dejo EquipoJugador (posicionEnMano/rotacionEnMano) al equipar; la
    // guardia es esa base mas el giro/desplazamiento de este componente, para que la espada
    // descanse con el filo hacia arriba en vez de colgando hacia adelante. Se captura (y se APLICA)
    // una sola vez: asi la guardia se ve apenas se equipa la espada, no recien al primer golpe.
    //
    // Se llama desde Start() y, por las dudas, otra vez desde Golpear() (es idempotente via
    // poseCapturada): asi la guardia se ve apenas el arma entra en juego, y si algo llamara a
    // Golpear antes de que Start corra (no deberia pasar en Play Mode) la pose base ya esta puesta
    // de cualquier forma, porque EquipoJugador la asigna ANTES de agregar este componente.
    void CapturarPoseDeGuardia()
    {
        if (poseCapturada) return;

        posicionBase = transform.localPosition;
        rotacionBase = transform.localRotation;

        posicionGuardiaAbsoluta = posicionBase + posicionGuardia;
        rotacionGuardiaAbsoluta = rotacionBase * Quaternion.Euler(giroGuardia);

        transform.localPosition = posicionGuardiaAbsoluta;
        transform.localRotation = rotacionGuardiaAbsoluta;

        poseCapturada = true;
    }

    IEnumerator SwingCompleto(float dano)
    {
        golpeadosEnEsteSwing.Clear();

        Vector3 posPreparacion = posicionGuardiaAbsoluta + posicionPreparacion;
        Quaternion rotPreparacion = rotacionGuardiaAbsoluta * Quaternion.Euler(giroPreparacion);

        Vector3 posActiva = posicionGuardiaAbsoluta + posicionActivo;
        Quaternion rotActiva = rotacionGuardiaAbsoluta * Quaternion.Euler(giroActivo);

        yield return Fase(posicionGuardiaAbsoluta, rotacionGuardiaAbsoluta, posPreparacion, rotPreparacion, tiempoPreparacion);

        // Ventana activa: el filo "palpa" el arco en cada frame mientras barre de la pose de
        // preparacion a la pose activa, no de una sola vez al final.
        float transcurrido = 0f;
        while (transcurrido < tiempoActivo)
        {
            float t = tiempoActivo <= 0f ? 1f : transcurrido / tiempoActivo;
            transform.localPosition = Vector3.Lerp(posPreparacion, posActiva, t);
            transform.localRotation = Quaternion.Slerp(rotPreparacion, rotActiva, t);

            PalparObjetivos(dano);

            transcurrido += Time.deltaTime;
            yield return null;
        }
        transform.localPosition = posActiva;
        transform.localRotation = rotActiva;
        PalparObjetivos(dano); // el frame exacto en el que el tajo termina no se puede perder por redondeo

        yield return Fase(posActiva, rotActiva, posicionGuardiaAbsoluta, rotacionGuardiaAbsoluta, tiempoRecuperacion);

        golpeEnCurso = null;
    }

    IEnumerator Fase(Vector3 posDesde, Quaternion rotDesde, Vector3 posHasta, Quaternion rotHasta, float duracion)
    {
        if (duracion <= 0f)
        {
            transform.localPosition = posHasta;
            transform.localRotation = rotHasta;
            yield break;
        }

        float transcurrido = 0f;
        while (transcurrido < duracion)
        {
            float t = transcurrido / duracion;
            transform.localPosition = Vector3.Lerp(posDesde, posHasta, t);
            transform.localRotation = Quaternion.Slerp(rotDesde, rotHasta, t);
            transcurrido += Time.deltaTime;
            yield return null;
        }
        transform.localPosition = posHasta;
        transform.localRotation = rotHasta;
    }

    // Arco de ataque real: una esfera centrada en la camara (radio alcance + margenDeteccion, que
    // cubre CUALQUIER direccion dentro del alcance, no solo el frente) para encontrar candidatos, y
    // despues el filtro fino de angulo + distancia AL PUNTO MAS CERCANO de cada collider -no a su
    // pivote- para decidir si conecta de verdad.
    //
    // Por que al punto mas cercano y no al pivote: medido pivote-a-pivote, un enemigo con una hitbox
    // grande (la de la escena real mide ~0.78 m de radio) quedaba fuera de alcance aunque su cuerpo
    // ya estuviera a tiro -el jugador le estaba pegando al aire que hay hasta el CENTRO del bicho, no
    // a su piel-. Mismo tipo de bug (y mismo motivo) que documenta EnemyAI.DistanciaDeAtaque del
    // otro lado del combate.
    void PalparObjetivos(float dano)
    {
        Camera c = ResolverCamara();
        if (c == null) return;

        Vector3 origen = c.transform.position;
        Vector3 direccion = c.transform.forward;

        int cantidad = Physics.OverlapSphereNonAlloc(origen, alcance + margenDeteccion, bufferDeteccion,
            capasAtaque, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < cantidad; i++)
        {
            Collider candidato = bufferDeteccion[i];
            if (EsDelJugador(candidato)) continue; // nunca el propio cuerpo (ver PlayerCombat.EsDelJugador)

            // GetComponentInParent y no GetComponent: en los enemigos el EnemyHealth esta en la
            // raiz y el collider puede estar en un hijo del modelo (mismo criterio que PlayerCombat
            // y Flecha).
            IDamageable objetivo = candidato.GetComponentInParent<IDamageable>();
            if (objetivo == null || golpeadosEnEsteSwing.Contains(objetivo)) continue;

            // ClosestPoint: la superficie del collider mas cercana a la camara, no su centro. Si la
            // camara ya esta dentro del collider (encimados), ClosestPoint devuelve el propio origen.
            Vector3 puntoCercano = candidato.ClosestPoint(origen);
            if (!DentroDelArco(origen, direccion, puntoCercano, alcance, anguloAtaque)) continue;

            golpeadosEnEsteSwing.Add(objetivo);
            objetivo.TakeDamage(dano);
        }
    }

    /// <summary>
    /// Verdadero si 'punto' cae dentro del cono de ataque: a 'alcance' metros o menos de 'origen' y
    /// a 'anguloAtaque'/2 grados o menos de 'direccion'. 'punto' deberia ser el punto mas cercano del
    /// objetivo (Collider.ClosestPoint), no necesariamente su pivote -ver PalparObjetivos-, pero la
    /// funcion en si no sabe nada de Colliders: es pura y estatica para poder probarla sin escena ni
    /// Physics (EspadaSelfTest), mismo criterio que EnemyAI.JugadorEnRangoDeGolpe del lado del enemigo.
    /// </summary>
    public static bool DentroDelArco(Vector3 origen, Vector3 direccion, Vector3 punto, float alcance, float anguloAtaque)
    {
        Vector3 hacia = punto - origen;
        float distancia = hacia.magnitude;

        if (distancia > alcance) return false;
        if (distancia < 0.0001f) return true; // encimado: no hay direccion que medir

        return Vector3.Angle(direccion, hacia) <= anguloAtaque * 0.5f;
    }

    bool EsDelJugador(Collider col)
    {
        Transform raiz = ResolverCuerpoDelJugador();
        return raiz != null && col != null && (col.transform == raiz || col.transform.IsChildOf(raiz));
    }

    // La camara del jugador es la que lleva MouseLook (la jerarquia que documenta CLAUDE.md), no
    // Camera.main: mismo criterio que usan Ballesta y EquipoJugador.
    Camera ResolverCamara()
    {
        if (camara != null) return camara;

        camara = GetComponentInParent<Camera>();
        if (camara != null) return camara;

        MouseLook mouseLook = FindAnyObjectByType<MouseLook>();
        if (mouseLook != null) camara = mouseLook.GetComponent<Camera>();

        return camara;
    }

    // La espada cuelga de "Mano", hija de la camara, que a su vez es hija directa del cuerpo con el
    // CharacterController (ver CLAUDE.md): subiendo por los padres desde aca se llega derecho a ese
    // CharacterController, igual que hace Flecha.DuenioDelTiro desde la camara.
    Transform ResolverCuerpoDelJugador()
    {
        if (cuerpoDelJugador != null) return cuerpoDelJugador;

        CharacterController cc = GetComponentInParent<CharacterController>();
        cuerpoDelJugador = cc != null ? cc.transform : ResolverCamara()?.transform;
        return cuerpoDelJugador;
    }
}
