using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Enemigo con maquina de estados: Patrulla (o Deambular, si su ruta no es valida) -> Persecucion
// -> Investigar -> Patrulla, mas ataque de cerca. HU-08 / RF10 / RF11 (issue #58).
// Migrado de Rigidbody a NavMeshAgent: usa el NavMesh que arma NavMeshRuntimeBuilder en tiempo de
// carga. El Rigidbody se mantiene en la escena (lo exige [RequireComponent]) pero se vuelve
// cinematico: ya no empuja el movimiento, solo evita que otros sistemas de fisica lo ignoren.
//
// Sincronizacion modelo <-> hitbox: el movimiento lo manda SIEMPRE este objeto (el padre, el que
// tiene NavMeshAgent + CapsuleCollider). El modelo 3D es un hijo que solo anima: por eso en Awake se
// le apaga Apply Root Motion, que es lo que hacia que la malla se adelantara o se fuera caminando
// lejos de su collider. El agente ademas copia el radio/alto de la capsula para que "lo que se ve" y
// "lo que choca" midan lo mismo.
//
// Combate: Atacar() ya no aplica dano al tocar el rango; arma un golpe pendiente que resuelve
// OnAttackHit(), llamado por el Animation Event del clip de ataque en el frame exacto del impacto
// (ver EnemyAnimationEvents, el puente desde el Animator del hijo hasta aca).
[RequireComponent(typeof(Rigidbody))]
public class EnemyAI : MonoBehaviour
{
    public enum Estado { Patrulla, Persecucion, Investigar }

    [Header("Patrulla")]
    [SerializeField] private Transform[] waypoints;
    [SerializeField] private float waitTimeAtWaypoint = 2f;
    [SerializeField] private float patrolSpeed = 2.5f;

    [Header("Deambular (si la ruta de patrulla no tiene 2 o mas waypoints validos)")]
    [SerializeField] private float radioDeambular = 25f;

    [Header("Deteccion (campo de vision)")]
    [SerializeField] private float detectionRadius = 10f;
    [SerializeField] private float fieldOfViewAngle = 100f;
    [Tooltip("Altura desde los pies del enemigo hasta donde 'mira'")]
    [SerializeField] private float alturaOjos = 1.6f;
    [Tooltip("Capas que puede bloquear la vision (muros, etc). Si el enemigo se tapa la vista a si mismo, sacale su propia capa de aca")]
    [SerializeField] private LayerMask capasBloqueoVision = ~0;

    [Header("Oido (RF11: la via principal es evadir o esconderse)")]
    [SerializeField] private float radioOido = 12f;
    [Tooltip("Velocidad horizontal (CharacterController) del jugador a partir de la cual el enemigo lo puede oir")]
    [SerializeField] private float umbralVelocidadRuido = 6f;

    [Header("Persecucion")]
    [SerializeField] private float chaseSpeed = 4.5f;
    [SerializeField] private float loseTargetDistance = 15f;
    [Tooltip("Cuantos segundos sin ver al jugador (estando cerca) hasta pasar a investigar")]
    [SerializeField] private float tiempoSinVerParaAbandonar = 3f;

    [Header("Investigacion (ultima posicion conocida)")]
    [SerializeField] private float tiempoInvestigacion = 3f;

    [Header("Ataque")]
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float attackDamage = 10f;
    [SerializeField] private float attackCooldown = 1.2f;
    [Tooltip("Angulo total (grados) por delante del enemigo en el que el golpe puede conectar")]
    [SerializeField] private float anguloAtaque = 120f;
    [Tooltip("Metros extra sobre attackRange que se perdonan al confirmar el golpe: el jugador se movio entre el inicio de la animacion y el frame del impacto")]
    [SerializeField] private float margenRangoGolpe = 0.5f;
    [Tooltip("Si el Animation Event OnAttackHit no llega en este tiempo (clip sin evento configurado), el golpe se resuelve igual para no perder el dano")]
    [SerializeField] private float tiempoMaximoEsperaGolpe = 0.5f;
    [Tooltip("Desactivalo cuando TODOS los clips de ataque ya tengan su Animation Event: el dano pasa a depender solo del frame de impacto")]
    [SerializeField] private bool golpeDeRespaldoSinEvento = true;

    [Tooltip("Apaga la colision fisica entre el enemigo y el jugador. Evita que el enemigo pueda " +
             "expulsar al jugador a traves del piso o las paredes empujandolo. En contra: el jugador " +
             "puede atravesar al enemigo caminando. El dano no depende del contacto, asi que el " +
             "combate funciona igual")]
    [SerializeField] private bool ignorarColisionConJugador = true;

    [Header("Movimiento / modelo 3D")]
    [Tooltip("Velocidad de giro del cuerpo hacia donde camina")]
    [SerializeField] private float velocidadGiro = 10f;
    [Tooltip("Apaga Apply Root Motion del Animator del modelo: el desplazamiento lo manda el NavMeshAgent, no la animacion")]
    [SerializeField] private bool desactivarRootMotion = true;
    [Tooltip("Material del enemigo. Si se asigna, se aplica a los SkinnedMeshRenderer del modelo en Awake (antes de que EnemyHitFeedback cachee el color), asi el modelo nunca queda blanco")]
    [SerializeField] private Material enemyMaterial;

    [Header("Audio")]
    [Tooltip("Si se deja vacio se busca/crea un AudioSource en este objeto al entrar en juego")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip attackSound;
    [Tooltip("Rugidos/gruñidos que suenan cada tanto mientras persigue al jugador")]
    [SerializeField] private AudioClip[] roarSounds;
    [SerializeField] private float minRoarInterval = 4f;
    [SerializeField] private float maxRoarInterval = 9f;
    [Range(0f, 1f)][SerializeField] private float volumenAtaque = 1f;
    [Range(0f, 1f)][SerializeField] private float volumenRugido = 1f;
    [Tooltip("Distancia a la que el sonido del enemigo deja de oirse (AudioSource 3D)")]
    [SerializeField] private float alcanceAudio = 25f;

    // Holgura entre "los dos cuerpos se tocan" y "el enemigo ataca". Sin ella el ataque se
    // dispararia justo en el limite del contacto, donde cualquier temblor del agente lo saca y lo
    // mete del rango.
    const float HolguraContacto = 0.2f;

    // Radio que se asume para el jugador si no se le encuentra CharacterController (no deberia
    // pasar: es el que mueve a PlayerController).
    const float RadioJugadorPorDefecto = 0.5f;

    const float IntervaloActualizarDestinoPersecucion = 0.2f;
    const float IntervaloDeteccion = 0.1f; // ~10 chequeos de vision por segundo (en vez de cada frame)
    const float TiempoMaximoDeambular = 15f;
    const float RadioBusquedaNavMesh = 3f;
    const int IntentosElegirPuntoDeambular = 10;
    const int MaxImpactosVision = 8;
    // Si el AudioSource esta ocupado con un sonido dominante (el ataque), el rugido no lo pisa: se
    // reintenta en este lapso corto en vez de perderse hasta el proximo intervalo completo.
    const float ReintentoRugidoOcupado = 0.25f;

    Estado estado = Estado.Patrulla;
    int indiceWaypoint;
    float tiempoEsperando;
    float tiempoSinVerJugador;
    float cooldownAtaqueRestante;
    float tiempoInvestigando;
    float tiempoDesdeUltimoDestino;
    float tiempoDesdeUltimaDeteccion;
    bool veAlJugadorCache;
    bool tienePuntoDeambular;
    float tiempoDeambulando;
    Vector3 puntoDeambular;
    Vector3 ultimaPosicionConocida;
    bool avisoNavMeshLogueado;
    bool avisoRutaLogueado;
    bool avisoEventoGolpeLogueado;
    bool avisoRadioAgenteLogueado;
    bool golpePendiente;
    float tiempoEsperandoGolpe;
    float tiempoHastaProximoRugido;

    Transform jugador;
    PlayerStats statsJugador;
    CharacterController controladorJugador;
    NavMeshAgent agent;
    Rigidbody rb;
    EnemyHealth salud;
    Animator animatorModelo;
    Collider[] propiosColliders; // para que el rayo de vision no se choque contra si mismo
    readonly List<Transform> rutaValida = new List<Transform>();
    readonly RaycastHit[] impactosVision = new RaycastHit[MaxImpactosVision];
    // NavMeshPath no se puede construir en un inicializador de campo (Unity lo prohibe fuera de
    // Awake/Start), asi que se crea de manera perezosa la primera vez que hace falta.
    NavMeshPath caminoTemporal;

    NavMeshPath CaminoTemporal()
    {
        if (caminoTemporal == null) caminoTemporal = new NavMeshPath();
        return caminoTemporal;
    }

    public int CantidadWaypointsValidos => rutaValida.Count;
    public bool EstaDeambulando => rutaValida.Count < 2;
    public Estado EstadoActual => estado;
    // Velocidad horizontal actual del agente (0 si todavia no esta sobre el NavMesh); la usa
    // EnemyAnimator para el blend de locomocion, igual que PlayerCombat expone AlAtacar para el
    // Animator del jugador.
    public float VelocidadActual => agent != null && agent.isOnNavMesh ? agent.velocity.magnitude : 0f;
    // Hay un ataque empezado esperando su frame de impacto (lo consultan las pruebas, CP-ENE-08+)
    public bool TieneGolpePendiente => golpePendiente;
    public float DanoDeAtaque => attackDamage;

    /// <summary>
    /// Distancia pivote-a-pivote a la que los cuerpos del enemigo y del jugador se tocan: la suma
    /// de los dos radios. Es el piso fisico de cualquier distancia de combate — mas cerca que esto
    /// no se pueden poner, porque sus colliders se lo impiden.
    /// </summary>
    public float SeparacionDeCuerpos => RadioPropio() + RadioDelJugador();

    /// <summary>
    /// Distancia pivote-a-pivote a la que el enemigo ataca.
    ///
    /// attackRange es lo que pide el diseño, pero NUNCA puede ser menor que lo que los dos cuerpos
    /// permiten. Este era el bug de fondo: medir el ataque entre pivotes ignorando que cada uno
    /// ocupa lugar. Con el enemigo de la escena (hitbox de 0.78 m de radio) y el jugador (0.5 +
    /// 0.08 de skin), sus pivotes no se pueden acercar a menos de ~1.36 m; pedir 1.2 m -lo que
    /// pedia stoppingDistance- es pedir algo fisicamente imposible, asi que el agente nunca
    /// "llegaba", seguia acelerando contra el jugador para siempre (la presion que lo atravesaba
    /// por el piso y las paredes) y el ataque quedaba colgado en el borde del rango.
    ///
    /// Tomando el mayor de los dos, el rango de ataque no puede quedar vacio por mas gorda que
    /// quede la hitbox del modelo, que es justo la fragilidad que lo rompio.
    /// </summary>
    public float DistanciaDeAtaque => Mathf.Max(attackRange, SeparacionDeCuerpos + HolguraContacto);

    public event System.Action AlAtacar;

    // El modelo y su material se resuelven en Awake, no en Start: EnemyHitFeedback cachea en su
    // Start el color del Renderer para volver a el despues del flash rojo, asi que si el material se
    // asignara en Start el flash podria "restaurar" el blanco del material por defecto.
    void Awake()
    {
        animatorModelo = GetComponentInChildren<Animator>();
        AplicarMaterialAlModelo();
        PrepararModelo();
        AsegurarPuenteDeEventos();
    }

    void Start()
    {
        // Se busca por PlayerStats en vez de por tag: en esta escena el objeto tageado "Player"
        // es una raiz que no se mueve, el que realmente camina es su hijo sin tag.
        statsJugador = FindAnyObjectByType<PlayerStats>();
        if (statsJugador != null)
        {
            jugador = statsJugador.transform;
            controladorJugador = statsJugador.GetComponent<CharacterController>();
        }

        propiosColliders = GetComponentsInChildren<Collider>();

        IgnorarColisionConJugador();

        InicializarNavegacion();
        AsegurarAudioSource();

        // HU-14: si el enemigo tiene EnemyHealth, la IA se apaga sola al morir. Sin ese
        // componente (prefabs viejos todavia sin combate) el enemigo sigue como antes.
        salud = GetComponent<EnemyHealth>();
        if (salud != null) salud.AlMorir += ManejarMuerte;
    }

    void OnDestroy()
    {
        if (salud != null) salud.AlMorir -= ManejarMuerte;
    }

    // Apaga la colision fisica entre el cuerpo del enemigo y el CharacterController del jugador.
    //
    // Por que: el enemigo es un Rigidbody cinematico movido por el NavMeshAgent, y un cinematico
    // que se mete dentro de un CharacterController no se frena — lo que cede es el jugador, que
    // Unity "despenetra" empujandolo. Contra una pared o una esquina no hay lugar donde empujarlo,
    // y termina saliendo del otro lado: el jugador atravesando el piso o los muros por presion.
    //
    // Es la red de seguridad, no el arreglo principal: el que evita el empuje es
    // SincronizarDistanciaDeFrenado(), que hace que el agente frene ANTES de encimarse. Esto cubre
    // lo que esa cuenta no puede cubrir — aparecer encima del jugador, que otro enemigo lo empuje
    // adentro, un frame de overshoot — donde el precio de fallar es atravesar el mapa.
    //
    // El contacto fisico entre los dos NO tiene ningun rol de juego: el dano lo aplica OnAttackHit()
    // por distancia y angulo, no por tocarse. El enemigo sigue chocando con el resto del mundo
    // (paredes, piso, otros enemigos) y el jugador tambien.
    //
    // A cambio, el jugador puede atravesar al enemigo caminando: el monstruo deja de ser solido.
    // Por eso es un toggle y no una decision tomada por el codigo — si se prefiere un enemigo
    // solido, se apaga ignorarColisionConJugador y queda solo la distancia de frenado.
    //
    // Lo que NO afecta: Physics.IgnoreCollision solo apaga los contactos entre ESE par de
    // colliders; las consultas siguen viendolos igual. El SphereCast con el que PlayerCombat le
    // pega al enemigo y el raycast de vision de PuedeVerAlJugador siguen funcionando.
    //
    // Es estado de PhysX y no del collider, asi que no sobrevive a recargar la escena: se vuelve a
    // pedir en cada Start, que es justo lo que hace falta.
    void IgnorarColisionConJugador()
    {
        if (!ignorarColisionConJugador) return;
        if (controladorJugador == null || propiosColliders == null) return;

        foreach (Collider propio in propiosColliders)
        {
            // Un trigger ya no genera contactos; pedirselo igual no rompe, pero se saltea para
            // dejar claro que esto es solo para los colliders solidos.
            if (propio == null || propio.isTrigger) continue;

            Physics.IgnoreCollision(propio, controladorJugador, true);
        }
    }

    // Detiene el NavMeshAgent y apaga este componente (Update deja de correr). EnemyHealth no
    // sabe nada de esto: mismo desacople que PlayerStats.AlMorir del lado del jugador.
    void ManejarMuerte()
    {
        if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
        // Un golpe a medio camino muere con el enemigo: si el clip de ataque ya estaba corriendo y
        // su Animation Event llega despues, OnAttackHit() lo va a ignorar.
        golpePendiente = false;
        enabled = false;
    }

    void Update()
    {
        if (jugador == null || agent == null) return;

        if (!agent.isOnNavMesh)
        {
            AsegurarSobreNavMesh();
            return;
        }

        if (cooldownAtaqueRestante > 0f) cooldownAtaqueRestante -= Time.deltaTime;

        // El golpe pendiente se vigila antes de la maquina de estados: si el jugador escapa a mitad
        // del swing, el ataque igual tiene que cerrarse (con o sin dano) en vez de quedar colgado.
        ActualizarGolpePendiente();

        // RF10: no hace falta chequear la vision cada frame, con ~10 veces por segundo alcanza.
        tiempoDesdeUltimaDeteccion += Time.deltaTime;
        if (tiempoDesdeUltimaDeteccion >= IntervaloDeteccion)
        {
            tiempoDesdeUltimaDeteccion = 0f;
            veAlJugadorCache = PuedeVerAlJugador();
        }
        bool veAlJugador = veAlJugadorCache;

        if (veAlJugador)
        {
            ultimaPosicionConocida = jugador.position;
            if (estado != Estado.Persecucion) CambiarA(Estado.Persecucion);
        }
        else if (estado == Estado.Patrulla)
        {
            RevisarOido();
        }

        switch (estado)
        {
            case Estado.Patrulla:
                Patrullar();
                break;

            case Estado.Persecucion:
                Perseguir(veAlJugador);
                break;

            case Estado.Investigar:
                Investigar();
                break;
        }
    }

    void CambiarA(Estado nuevo)
    {
        estado = nuevo;
        tiempoSinVerJugador = 0f;
        tiempoEsperando = 0f;
        tiempoInvestigando = 0f;
        tiempoDesdeUltimoDestino = 0f;
        tienePuntoDeambular = false;
        tiempoDeambulando = 0f;

        // Al pasar a persecucion el primer rugido sale enseguida (es el aviso de que te vio); los
        // siguientes van cada minRoarInterval..maxRoarInterval.
        if (nuevo == Estado.Persecucion) tiempoHastaProximoRugido = 0f;

        if (agent != null) agent.isStopped = false;
    }

    // ---------------------------------------------------------------
    // Navegacion (HU-08, #58)
    // ---------------------------------------------------------------

    // Publico y llamable repetidas veces (CP-ENE-07): agrega el NavMeshAgent si falta, lo ubica
    // sobre el NavMesh y reconstruye la ruta de patrulla valida. No agrega/quita componentes de
    // mas: el Rigidbody que ya trae el objeto solo se pasa a cinematico.
    public void InicializarNavegacion()
    {
        AsegurarAgente();
        AsegurarSobreNavMesh();
        ConstruirRutaValida();
    }

    void AsegurarAgente()
    {
        agent = GetComponent<NavMeshAgent>();
        if (agent == null) agent = gameObject.AddComponent<NavMeshAgent>();

        agent.speed = patrolSpeed;
        agent.acceleration = 12f;
        agent.updateRotation = false; // la rotacion la maneja MirarHacia() a mano, igual que antes

        SincronizarFormaDelAgente();

        // Despues de SincronizarFormaDelAgente y FUERA de ella: esa funcion se va por un early
        // return cuando el enemigo no tiene CapsuleCollider, que es el caso de los enemigos extra
        // (todavia son la esfera placeholder de EnemigosExtraBuilder). Si la distancia de frenado
        // se calculara ahi adentro, esos enemigos se quedarian con el stoppingDistance 0 que trae
        // Unity por defecto y entrarian hasta el centro del jugador: exactamente el bug que esto
        // viene a arreglar, pero solo en dos de los tres enemigos.
        SincronizarDistanciaDeFrenado();

        rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;
    }

    // "Lo que se ve" y "lo que choca" tienen que medir lo mismo: el agente copia el radio y el alto
    // de la capsula del enemigo (la que EnemySetupFixer calcula midiendo el modelo ya escalado) en
    // vez de quedarse con los 0.5 / 2 m por defecto, que con un modelo grande dejan a la malla
    // metiendose en las paredes mientras su hitbox va por otro lado.
    void SincronizarFormaDelAgente()
    {
        CapsuleCollider capsula = GetComponent<CapsuleCollider>();
        if (agent == null || capsula == null) return;

        Vector3 escala = transform.lossyScale;
        float escalaXZ = Mathf.Max(Mathf.Abs(escala.x), Mathf.Abs(escala.z));
        float radio = Mathf.Max(0.1f, capsula.radius * escalaXZ);
        float alto = Mathf.Max(radio * 2f, capsula.height * Mathf.Abs(escala.y));

        // El NavMesh se hornea con el radio del tipo de agente (NavMeshRuntimeBuilder usa el 0,
        // Humanoid): pedirle al agente un radio mas grande que ese no le abre mas espacio, solo lo
        // hace temblar contra las paredes y trabarse en los pasillos. Se respeta ese techo y se
        // avisa una vez, porque si la malla es mucho mas ancha va a rozar las paredes igual.
        float radioHorneado = NavMesh.GetSettingsByID(agent.agentTypeID).agentRadius;
        if (radioHorneado > 0f && radio > radioHorneado)
        {
            if (!avisoRadioAgenteLogueado)
            {
                avisoRadioAgenteLogueado = true;
                Debug.LogWarning($"{name}: el modelo mide {radio:0.00} m de radio pero el NavMesh esta " +
                    $"horneado para {radioHorneado:0.00} m. Se usa el del NavMesh; si la malla roza las " +
                    "paredes, hay que achicar el modelo o hornear el NavMesh con un agente mas ancho.", this);
            }
            radio = radioHorneado;
        }

        agent.radius = radio;
        agent.height = alto;
    }

    // El agente tiene que frenar donde el enemigo puede atacar, y no un poco mas adentro.
    //
    // Antes esto era attackRange - 0.3, un numero que no sabia nada del tamano de los dos cuerpos:
    // con la hitbox del enemigo de la escena pedia frenar a 1.2 m cuando sus colliders se tocan a
    // 1.36 m, o sea le pedia meterse dentro del jugador. El agente nunca daba por cumplido el
    // destino, seguia empujando a fondo, y esa presion constante es la que terminaba expulsando al
    // jugador por el piso o las paredes.
    //
    // Va despues de SincronizarFormaDelAgente porque, cuando el enemigo no tiene collider propio,
    // RadioPropio() se cae al radio del agente, que es lo que esa funcion acaba de dejar puesto.
    void SincronizarDistanciaDeFrenado()
    {
        if (agent == null) return;

        // Un pelo por debajo de la distancia de ataque, para que el frame en el que el agente da el
        // destino por alcanzado sea uno en el que el ataque ya conecta.
        agent.stoppingDistance = Mathf.Max(0.05f, DistanciaDeAtaque - 0.05f);
    }

    // Inicializacion perezosa: si el NavMesh todavia no estaba armado cuando el agente se creo,
    // se reintenta cada frame (desde Update) hasta que SamplePosition encuentre un punto cercano.
    void AsegurarSobreNavMesh()
    {
        if (agent == null || agent.isOnNavMesh) return;

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, RadioBusquedaNavMesh, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);

            // Si el enemigo arranca lejos del NavMesh (por ejemplo, con la Y de diseno en vez de
            // la de la geometria real del piso), Warp() solo no alcanza para que isOnNavMesh de
            // verdadero: hay que apagar y prender el agente ya con la posicion correcta para que
            // se vuelva a registrar sobre el NavMesh.
            if (!agent.isOnNavMesh)
            {
                agent.enabled = false;
                transform.position = hit.position;
                agent.enabled = true;
            }
        }
        else if (!avisoNavMeshLogueado)
        {
            avisoNavMeshLogueado = true;
            Debug.LogWarning($"{name}: no se encontro NavMesh a {RadioBusquedaNavMesh} m de {transform.position}. HU-08 #58");
        }
    }

    // Radio de busqueda generoso para corregir un waypoint cuya Y de diseno haya quedado muy lejos
    // del piso real (ej. un arrastre accidental en el Editor): RadioBusquedaNavMesh (3m) es el que
    // se usa para el resto de los chequeos finos, pero no alcanza para un desfasaje de decenas de
    // metros como el que puede dejar ese tipo de error.
    const float RadioCorreccionWaypointLejano = 40f;

    // Descarta waypoints null o sin camino completo hacia ellos. Con 2 o mas validos, patrulla
    // ciclica; si no, EstaDeambulando pasa a valer true y se usa ElegirPuntoDeambular().
    // Publico para las pruebas (CP-NAV-05): asi el self-test de Editor puede disparar la misma
    // correccion sin tener que simular un Start() completo (que necesitaria Play mode para que el
    // NavMeshAgent llegue a registrarse).
    public void ConstruirRutaValida()
    {
        rutaValida.Clear();
        List<string> descartados = null;
        List<string> corregidos = null;

        if (waypoints != null)
        {
            for (int i = 0; i < waypoints.Length; i++)
            {
                Transform wp = waypoints[i];
                if (wp == null)
                {
                    if (descartados == null) descartados = new List<string>();
                    descartados.Add($"[{i}] null");
                    continue;
                }

                // Mismo mecanismo que AsegurarSobreNavMesh usa consigo mismo: si el waypoint no
                // esta cerca del NavMesh, se intenta corregir su posicion (en memoria, nunca en la
                // escena guardada) antes de darlo por invalido.
                if (!NavMesh.SamplePosition(wp.position, out NavMeshHit hitCerca, RadioBusquedaNavMesh, NavMesh.AllAreas))
                {
                    if (NavMesh.SamplePosition(wp.position, out NavMeshHit hitLejos, RadioCorreccionWaypointLejano, NavMesh.AllAreas))
                    {
                        if (corregidos == null) corregidos = new List<string>();
                        corregidos.Add($"[{i}] {wp.name} ({wp.position} -> {hitLejos.position})");
                        wp.position = hitLejos.position;
                    }
                }

                NavMeshPath camino = CaminoTemporal();
                bool caminoCompleto = NavMesh.CalculatePath(transform.position, wp.position, NavMesh.AllAreas, camino)
                    && camino.status == NavMeshPathStatus.PathComplete;

                if (!caminoCompleto)
                {
                    if (descartados == null) descartados = new List<string>();
                    descartados.Add($"[{i}] {wp.name} (sin camino completo)");
                    continue;
                }

                rutaValida.Add(wp);
            }
        }

        if (!avisoRutaLogueado && (descartados != null || corregidos != null))
        {
            avisoRutaLogueado = true;
            if (corregidos != null) Debug.LogWarning($"{name}: waypoints corregidos por estar lejos del NavMesh: {string.Join(", ", corregidos)}. HU-08 #58");
            if (descartados != null) Debug.LogWarning($"{name}: waypoints descartados: {string.Join(", ", descartados)}. HU-08 #58");
        }

        indiceWaypoint = 0;
    }

    // ---------------------------------------------------------------
    // Modelo 3D: material, root motion y puente de Animation Events
    // ---------------------------------------------------------------

    // Pone enemyMaterial en todos los slots de todos los SkinnedMeshRenderer del modelo. Es la red
    // de seguridad del problema "el modelo se ve blanco": si el FBX se importo sin material (o su
    // busqueda de texturas fallo), igual entra en juego con el material correcto. Lo prolijo sigue
    // siendo dejarlo asignado en el propio SkinnedMeshRenderer; esto no reemplaza eso, lo cubre.
    void AplicarMaterialAlModelo()
    {
        if (enemyMaterial == null) return;

        SkinnedMeshRenderer[] mallas = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (mallas.Length == 0)
        {
            Debug.LogWarning($"{name}: hay enemyMaterial asignado pero el modelo no tiene ningun SkinnedMeshRenderer.", this);
            return;
        }

        for (int i = 0; i < mallas.Length; i++)
        {
            int slots = Mathf.Max(1, mallas[i].sharedMaterials.Length);
            Material[] materiales = new Material[slots];
            for (int s = 0; s < slots; s++) materiales[s] = enemyMaterial;
            mallas[i].sharedMaterials = materiales;
        }
    }

    // Apply Root Motion prendido + clips con desplazamiento (los de Mixamo que no son "in place")
    // = la malla se va caminando sola y deja atras al NavMeshAgent y a la hitbox. El desplazamiento
    // lo manda el agente, asi que la animacion tiene que quedarse en el lugar.
    void PrepararModelo()
    {
        if (!desactivarRootMotion || animatorModelo == null) return;
        animatorModelo.applyRootMotion = false;
    }

    // Unity busca el metodo de un Animation Event en los componentes del GameObject que tiene el
    // Animator, que aca es el hijo del modelo, no este objeto. EnemyAnimationEvents vive en ese
    // hijo y reenvia la llamada; se agrega solo para que no haga falta acordarse en el Editor.
    void AsegurarPuenteDeEventos()
    {
        if (animatorModelo == null) return;
        if (animatorModelo.GetComponent<EnemyAnimationEvents>() == null)
        {
            animatorModelo.gameObject.AddComponent<EnemyAnimationEvents>();
        }
    }

    // ---------------------------------------------------------------
    // Audio (SFX de ataque + rugidos en persecucion)
    // ---------------------------------------------------------------

    // Solo crea el AudioSource en juego: en modo Editor (los self-tests invocan Start() por
    // reflexion) no se le agregan componentes a la escena, y ReproducirSfx cae en PlayClipAtPoint.
    void AsegurarAudioSource()
    {
        if (audioSource != null) return;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            if (!Application.isPlaying) return;
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 1f; // 3D: el rugido tiene que sonar donde esta el bicho
        audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        audioSource.minDistance = 2f;
        audioSource.maxDistance = alcanceAudio;
        AudioPreferences.RutearASfx(audioSource); // asi el slider de SFX del menu lo afecta
    }

    void ReproducirSfx(AudioClip clip, float volumen)
    {
        if (clip == null) return;

        AsegurarAudioSource();
        if (audioSource == null)
        {
            // Fuera de Play Mode no se reproduce nada: Destroy con retardo (lo que usa
            // PlayClipAtPoint) no es valido en modo edicion y ensuciaria los self-tests.
            if (Application.isPlaying) AudioSource.PlayClipAtPoint(clip, transform.position, volumen);
            return;
        }

        audioSource.PlayOneShot(clip, volumen);
    }

    // Rugidos mientras persigue: un clip al azar cada minRoarInterval..maxRoarInterval. No pisa un
    // sonido dominante (el ataque): si el AudioSource esta ocupado, reintenta enseguida.
    void ActualizarRugidos()
    {
        if (roarSounds == null || roarSounds.Length == 0) return;

        tiempoHastaProximoRugido -= Time.deltaTime;
        if (tiempoHastaProximoRugido > 0f) return;

        if (audioSource != null && audioSource.isPlaying)
        {
            tiempoHastaProximoRugido = ReintentoRugidoOcupado;
            return;
        }

        AudioClip rugido = ElegirClipAleatorio(roarSounds);
        tiempoHastaProximoRugido = SiguienteIntervaloRugido(minRoarInterval, maxRoarInterval);
        if (rugido == null) return;

        ReproducirSfx(rugido, volumenRugido);
    }

    // Helpers puros (sin escena) para poder probar la seleccion y los intervalos: CP-ENE-11/12.
    // Saltea los huecos vacios que suele dejar el arreglo del Inspector al agrandarlo.
    public static AudioClip ElegirClipAleatorio(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return null;

        int validos = 0;
        for (int i = 0; i < clips.Length; i++) if (clips[i] != null) validos++;
        if (validos == 0) return null;

        int elegido = Random.Range(0, validos);
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] == null) continue;
            if (elegido == 0) return clips[i];
            elegido--;
        }
        return null;
    }

    // Tolera que min y max vengan invertidos o negativos desde el Inspector.
    public static float SiguienteIntervaloRugido(float min, float max)
    {
        float desde = Mathf.Max(0f, Mathf.Min(min, max));
        float hasta = Mathf.Max(desde, Mathf.Max(min, max));
        return Random.Range(desde, hasta);
    }

    // ---------------------------------------------------------------
    // Patrulla / Deambular
    // ---------------------------------------------------------------
    void Patrullar()
    {
        if (agent != null) agent.speed = patrolSpeed;

        if (EstaDeambulando)
        {
            Deambular();
            return;
        }

        Transform destino = rutaValida[indiceWaypoint];
        bool llego = MoverHacia(destino.position, patrolSpeed);
        if (!llego)
        {
            tiempoEsperando = 0f;
            return;
        }

        tiempoEsperando += Time.deltaTime;
        if (tiempoEsperando >= waitTimeAtWaypoint)
        {
            tiempoEsperando = 0f;
            indiceWaypoint = (indiceWaypoint + 1) % rutaValida.Count;
        }
    }

    void Deambular()
    {
        if (!tienePuntoDeambular && !ElegirPuntoDeambular(out puntoDeambular))
        {
            return; // no se encontro ningun punto valido este frame; se reintenta el proximo
        }
        tienePuntoDeambular = true;

        bool llego = MoverHacia(puntoDeambular, patrolSpeed);
        tiempoDeambulando += Time.deltaTime;

        if (llego || tiempoDeambulando >= TiempoMaximoDeambular)
        {
            tienePuntoDeambular = false;
            tiempoDeambulando = 0f;
        }
    }

    // Publico para las pruebas (CP-ENE-03): intenta varios puntos aleatorios dentro de
    // radioDeambular y solo acepta uno si esta sobre el NavMesh y tiene camino completo.
    public bool ElegirPuntoDeambular(out Vector3 punto)
    {
        for (int intento = 0; intento < IntentosElegirPuntoDeambular; intento++)
        {
            Vector2 circulo = Random.insideUnitCircle * radioDeambular;
            Vector3 candidato = transform.position + new Vector3(circulo.x, 0f, circulo.y);

            if (!NavMesh.SamplePosition(candidato, out NavMeshHit hit, radioDeambular, NavMesh.AllAreas))
                continue;

            NavMeshPath camino = CaminoTemporal();
            bool caminoCompleto = NavMesh.CalculatePath(transform.position, hit.position, NavMesh.AllAreas, camino)
                && camino.status == NavMeshPathStatus.PathComplete;

            if (caminoCompleto)
            {
                punto = hit.position;
                return true;
            }
        }

        punto = transform.position;
        return false;
    }

    // ---------------------------------------------------------------
    // Persecucion + ataque
    // ---------------------------------------------------------------
    void Perseguir(bool veAlJugador)
    {
        if (agent != null) agent.speed = chaseSpeed;

        ActualizarRugidos();

        // Distancia horizontal: el pivote del jugador esta a la altura de su cuerpo, asi que medir
        // en 3D inflaria el rango de ataque segun la diferencia de altura con el piso.
        float distancia = DistanciaHorizontalAlJugador();

        if (veAlJugador) tiempoSinVerJugador = 0f;
        else tiempoSinVerJugador += Time.deltaTime;

        bool perdido = distancia > loseTargetDistance || tiempoSinVerJugador >= tiempoSinVerParaAbandonar;
        if (perdido)
        {
            CambiarA(Estado.Investigar);
            return;
        }

        if (distancia <= DistanciaDeAtaque)
        {
            DetenerMovimiento();
            MirarHacia(jugador.position);
            Atacar();
            return;
        }

        // El destino se actualiza como maximo cada 0.2s (no hace falta recalcular el camino cada
        // frame), pero la rotacion se sigue actualizando siempre para que no se vea trabada.
        tiempoDesdeUltimoDestino += Time.deltaTime;
        if (tiempoDesdeUltimoDestino >= IntervaloActualizarDestinoPersecucion)
        {
            tiempoDesdeUltimoDestino = 0f;
            MoverHacia(jugador.position, chaseSpeed);
        }
        MirarHaciaElMovimiento();
    }

    // Arranca el ataque: dispara la animacion y el SFX, y deja un golpe pendiente. El dano se
    // aplica recien en OnAttackHit(), en el frame de impacto del clip (o, si ese clip todavia no
    // tiene su Animation Event, por el respaldo de ActualizarGolpePendiente()).
    void Atacar()
    {
        if (cooldownAtaqueRestante > 0f) return;
        cooldownAtaqueRestante = attackCooldown;

        golpePendiente = true;
        tiempoEsperandoGolpe = 0f;

        AlAtacar?.Invoke();               // EnemyAnimator -> animator.SetTrigger("Attack")
        ReproducirSfx(attackSound, volumenAtaque);
    }

    // Lo llama el Animation Event "OnAttackHit" del clip de ataque, via EnemyAnimationEvents del
    // hijo con el Animator. Publico a proposito: es la interfaz que ve la ventana de Animation.
    public void OnAttackHit()
    {
        if (!golpePendiente) return; // evento fuera de un ataque, o golpe ya resuelto: se ignora

        golpePendiente = false;
        AplicarDanoSiConecta();
    }

    // Respaldo: si el clip de ataque no tiene el Animation Event configurado, el golpe no se
    // resolveria nunca. Pasado tiempoMaximoEsperaGolpe se resuelve igual (avisando una vez por
    // enemigo), asi el enemigo no queda inofensivo por un paso de Editor pendiente.
    void ActualizarGolpePendiente()
    {
        if (!golpePendiente) return;

        tiempoEsperandoGolpe += Time.deltaTime;
        if (tiempoEsperandoGolpe < tiempoMaximoEsperaGolpe) return;

        golpePendiente = false;

        if (!avisoEventoGolpeLogueado)
        {
            avisoEventoGolpeLogueado = true;
            Debug.LogWarning($"{name}: el clip de ataque no llamo a OnAttackHit() en {tiempoMaximoEsperaGolpe}s. " +
                "Agregale el Animation Event en el frame del impacto, o subi tiempoMaximoEsperaGolpe si el " +
                "impacto del clip cae mas tarde que eso (ver EnemyAnimationEvents).", this);
        }

        if (golpeDeRespaldoSinEvento) AplicarDanoSiConecta();
    }

    bool AplicarDanoSiConecta()
    {
        if (statsJugador == null || !JugadorEnRangoDeGolpe()) return false;

        statsJugador.TakeDamage(attackDamage);
        return true;
    }

    // Se vuelve a validar rango y angulo en el frame del impacto: entre el inicio de la animacion y
    // el golpe el jugador pudo escapar, y en ese caso el golpe tiene que fallar.
    // Publico para las pruebas (CP-ENE-09/10).
    public bool JugadorEnRangoDeGolpe()
    {
        if (jugador == null) return false;

        Vector3 hacia = jugador.position - transform.position;
        hacia.y = 0f;
        float distancia = hacia.magnitude;

        if (distancia > DistanciaDeAtaque + margenRangoGolpe) return false;
        if (distancia < 0.0001f) return true; // encimado: no hay direccion que medir

        // El angulo se mide en horizontal (transform.forward ya es horizontal y 'hacia' tiene la Y
        // en cero unas lineas arriba): la diferencia de altura entre los pivotes no tiene que
        // contar como "esta de costado".
        return Vector3.Angle(transform.forward, hacia) <= anguloAtaque * 0.5f;
    }

    float DistanciaHorizontalAlJugador()
    {
        if (jugador == null) return float.MaxValue;

        Vector3 delta = jugador.position - transform.position;
        delta.y = 0f;
        return delta.magnitude;
    }

    // Radio horizontal del cuerpo del enemigo, en metros de mundo (con la escala del transform
    // aplicada). Soporta las dos formas que hay en la escena: la capsula que le deja EnemySetupFixer
    // al enemigo con modelo y la esfera placeholder de los enemigos extra (EnemigosExtraBuilder).
    //
    // Ojo con la capsula: si el radio pasa de la mitad del alto, Unity la trata como una ESFERA de
    // ese radio, y es exactamente el caso del enemigo de la escena (radio 0.78, alto 1.04). Por eso
    // se mide el radio y no se deduce del alto.
    float RadioPropio()
    {
        Vector3 escala = transform.lossyScale;
        float escalaXZ = Mathf.Max(Mathf.Abs(escala.x), Mathf.Abs(escala.z));

        CapsuleCollider capsula = GetComponent<CapsuleCollider>();
        if (capsula != null) return capsula.radius * escalaXZ;

        SphereCollider esfera = GetComponent<SphereCollider>();
        if (esfera != null) return esfera.radius * escalaXZ;

        // Sin collider propio, el agente es lo unico que sabe cuanto ocupa.
        return agent != null ? agent.radius : 0.5f;
    }

    // Radio del jugador, con su skinWidth incluido: el CharacterController mantiene esa piel de
    // separacion, asi que es parte de lo que impide acercarse mas.
    float RadioDelJugador()
    {
        if (controladorJugador == null) return RadioJugadorPorDefecto;

        Vector3 escala = controladorJugador.transform.lossyScale;
        float escalaXZ = Mathf.Max(Mathf.Abs(escala.x), Mathf.Abs(escala.z));
        return (controladorJugador.radius + controladorJugador.skinWidth) * escalaXZ;
    }

    // ---------------------------------------------------------------
    // Investigar (ultima posicion conocida, tras perder de vista o por oido)
    // ---------------------------------------------------------------
    void Investigar()
    {
        if (agent != null) agent.speed = patrolSpeed;

        bool llego = MoverHacia(ultimaPosicionConocida, patrolSpeed);
        if (!llego) return;

        tiempoInvestigando += Time.deltaTime;
        if (tiempoInvestigando >= tiempoInvestigacion)
        {
            CambiarA(Estado.Patrulla);
        }
    }

    // RF11: si el jugador corre (mas rapido que umbralVelocidadRuido) dentro de radioOido, no lo
    // ve pero hay un camino completo hasta el, el enemigo va a investigar el ruido.
    void RevisarOido()
    {
        float distancia = Vector3.Distance(transform.position, jugador.position);
        float velocidadJugador = VelocidadHorizontalJugador();

        if (!DebeInvestigarPorRuido(distancia, velocidadJugador, radioOido, umbralVelocidadRuido, false))
            return;

        NavMeshPath camino = CaminoTemporal();
        bool caminoCompleto = NavMesh.CalculatePath(transform.position, jugador.position, NavMesh.AllAreas, camino)
            && camino.status == NavMeshPathStatus.PathComplete;
        if (!caminoCompleto) return;

        ultimaPosicionConocida = jugador.position;
        CambiarA(Estado.Investigar);
    }

    float VelocidadHorizontalJugador()
    {
        if (controladorJugador == null) return 0f;
        Vector3 v = controladorJugador.velocity;
        v.y = 0f;
        return v.magnitude;
    }

    // Helper estatico puro (sin estado), para poder probar la tabla de verdad sin escena (CP-ENE-05).
    public static bool DebeInvestigarPorRuido(float distancia, float velocidadJugador, float radioOido, float umbralVelocidadRuido, bool loVe)
    {
        if (loVe) return false;
        if (distancia > radioOido) return false;
        return velocidadJugador > umbralVelocidadRuido;
    }

    // ---------------------------------------------------------------
    // Movimiento y vision
    // ---------------------------------------------------------------

    // Mueve el NavMeshAgent hacia el destino a la velocidad dada. Devuelve true cuando ya llego
    // (dentro de stoppingDistance y sin velocidad), para saber cuando esperar/reelegir destino.
    bool MoverHacia(Vector3 destino, float velocidad)
    {
        if (agent == null || !agent.isOnNavMesh) return false;

        agent.speed = velocidad;
        agent.isStopped = false;
        agent.SetDestination(destino);
        MirarHaciaElMovimiento();

        return !agent.pathPending
            && agent.remainingDistance <= agent.stoppingDistance
            && (!agent.hasPath || agent.velocity.sqrMagnitude < 0.01f);
    }

    void DetenerMovimiento()
    {
        if (agent != null) agent.isStopped = true;
    }

    // Gira hacia donde el agente va a dar el proximo paso (desiredVelocity), no hacia el destino
    // final: mirar el destino en linea recta a traves de una pared hacia que el modelo se viera
    // caminando de costado respecto de su propio movimiento.
    void MirarHaciaElMovimiento()
    {
        if (agent == null || !agent.isOnNavMesh) return;

        Vector3 direccion = agent.desiredVelocity;
        direccion.y = 0f;
        if (direccion.sqrMagnitude < 0.01f) return;

        MirarHacia(transform.position + direccion);
    }

    void MirarHacia(Vector3 objetivo)
    {
        Vector3 direccion = objetivo - transform.position;
        direccion.y = 0f;
        if (direccion.sqrMagnitude < 0.0001f) return;

        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direccion), velocidadGiro * Time.deltaTime);
    }

    bool PuedeVerAlJugador()
    {
        Vector3 origen = transform.position + Vector3.up * alturaOjos;
        Vector3 haciaJugador = jugador.position - origen;
        float distancia = haciaJugador.magnitude;

        if (distancia > detectionRadius) return false;

        // El cono de vision se mide en el plano horizontal, no en 3D.
        //
        // Medido en 3D, la diferencia de altura entre los ojos del enemigo (1.6 m) y el pivote del
        // jugador (el centro de su capsula, ~1 m) inflaba el angulo a medida que se acercaban: a
        // 1.4 m de distancia ya son 24 grados de los 50 disponibles, y mas cerca se pasa del cono
        // y el enemigo deja de "ver" al jugador que tiene encima. Eso lo mandaba a Investigar
        // justo cuando tenia que atacar: caminaba hacia la ultima posicion conocida -donde esta
        // el jugador- empujandolo, que es el "solo me sigue y me empuja" que se veia en el juego.
        Vector3 haciaJugadorPlano = haciaJugador;
        haciaJugadorPlano.y = 0f;

        // De frente justo encima (distancia horizontal ~0) no hay direccion que medir: se da por
        // visto, que es lo que corresponde cuando lo tiene pegado.
        if (haciaJugadorPlano.sqrMagnitude > 0.0001f
            && Vector3.Angle(transform.forward, haciaJugadorPlano) > fieldOfViewAngle * 0.5f)
        {
            return false;
        }

        // RaycastNonAlloc (no RaycastAll) para no reservar memoria cada frame: mismo buffer fijo
        // reutilizado, ordenado a mano por distancia para poder ignorar el propio collider del
        // enemigo (si el origen queda pegado a su propio cuerpo, el primer impacto seria consigo
        // mismo y nunca "veria" nada).
        Vector3 direccion = haciaJugador / distancia;
        int cantidad = Physics.RaycastNonAlloc(origen, direccion, impactosVision, distancia, capasBloqueoVision, QueryTriggerInteraction.Ignore);
        OrdenarPorDistancia(impactosVision, cantidad);

        for (int i = 0; i < cantidad; i++)
        {
            RaycastHit hit = impactosVision[i];
            if (EsPropio(hit.collider)) continue;         // su propio cuerpo: se ignora
            return hit.transform == jugador;              // lo primero relevante: jugador (hay vision) o muro (no hay)
        }

        return true; // no choco contra nada en el camino
    }

    static void OrdenarPorDistancia(RaycastHit[] impactos, int cantidad)
    {
        for (int i = 1; i < cantidad; i++)
        {
            RaycastHit actual = impactos[i];
            int j = i - 1;
            while (j >= 0 && impactos[j].distance > actual.distance)
            {
                impactos[j + 1] = impactos[j];
                j--;
            }
            impactos[j + 1] = actual;
        }
    }

    bool EsPropio(Collider col)
    {
        if (propiosColliders == null) return false;
        for (int i = 0; i < propiosColliders.Length; i++)
        {
            if (propiosColliders[i] == col) return true;
        }
        return false;
    }

    // Dibuja los radios de vision, de oido y de deambular, el rango de ataque y la ruta de
    // patrulla (la ya validada en juego, o los waypoints crudos en edicion, para quien arme el mapa).
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 1f, 0f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        Vector3 origen = transform.position + Vector3.up * alturaOjos;
        Gizmos.color = Color.yellow;
        Quaternion izquierda = Quaternion.AngleAxis(-fieldOfViewAngle * 0.5f, Vector3.up);
        Quaternion derecha = Quaternion.AngleAxis(fieldOfViewAngle * 0.5f, Vector3.up);
        Gizmos.DrawRay(origen, izquierda * transform.forward * detectionRadius);
        Gizmos.DrawRay(origen, derecha * transform.forward * detectionRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        Gizmos.color = new Color(0.3f, 0.3f, 1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, loseTargetDistance);

        // RF11: radio de oido
        Gizmos.color = new Color(1f, 0.4f, 1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, radioOido);

        // Radio de deambular (se usa cuando la ruta de patrulla no tiene 2+ waypoints validos)
        Gizmos.color = new Color(0.4f, 1f, 0.4f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, radioDeambular);

        IList<Transform> ruta = Application.isPlaying && rutaValida.Count > 0 ? (IList<Transform>)rutaValida : waypoints;
        if (ruta != null && ruta.Count > 0)
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < ruta.Count; i++)
            {
                if (ruta[i] == null) continue;
                Gizmos.DrawSphere(ruta[i].position, 0.25f);
                Transform siguiente = ruta[(i + 1) % ruta.Count];
                if (siguiente != null) Gizmos.DrawLine(ruta[i].position, siguiente.position);
            }
        }
    }
}
