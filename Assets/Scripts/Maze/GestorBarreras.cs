using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Arbitro unico de las barreras dinamicas: agenda los cierres y valida cada uno antes de dejarlo
// avanzar. Una barrera solo empieza a cerrar si (1) no hay jugador ni enemigos cerca del hueco y
// (2) el jugador sigue llegando al spawn, a la salida y a todos los PuntoObligatorio activos
// (el comerciante, y mas adelante las llaves sin recoger). Ambas condiciones se vuelven a revisar
// durante el aviso y el cierre; si dejan de cumplirse, la barrera aborta y se reabre.
// Se instala sola cuando la escena tiene BarreraDinamica (mismo patron que ExitTrigger).
public class GestorBarreras : MonoBehaviour
{
    const string NombreObjeto = "_GestorBarreras";
    const float IntervaloRevision = 0.1f;
    const float IntervaloRefrescoEnemigos = 1f;
    const float RadioCuerpo = 0.75f;

    [Header("Ciclo")]
    [SerializeField] float intervaloMinimo = 8f;
    [SerializeField] float intervaloMaximo = 20f;
    [SerializeField] int maximoCerradasSimultaneas = 3;
    [SerializeField] float duracionAviso = 3f;
    [SerializeField] float duracionCerradaMinima = 20f;
    [SerializeField] float duracionCerradaMaxima = 45f;
    [Tooltip("Una barrera recien abierta (o al arrancar la partida) espera esto antes de poder volver a cerrarse")]
    [SerializeField] float tiempoMinimoAbierta = 10f;

    [Header("Seguridad")]
    [Tooltip("Metros alrededor del hueco donde no puede haber jugador ni enemigos para empezar o seguir un cierre")]
    [SerializeField] float margenSeguridad = 4f;
    [Tooltip("0 = distinta en cada partida")]
    [SerializeField] int semilla = 0;

    class Registro
    {
        public BarreraDinamica barrera;
        public float duracionCerrada;
    }

    readonly List<Registro> registros = new List<Registro>();
    readonly List<int> obligatorios = new List<int>();
    readonly List<Registro> candidatos = new List<Registro>();

    Transform raiz;
    PlayerStats jugador;
    EnemyAI[] enemigos = new EnemyAI[0];
    System.Random azar;
    int celdaSpawn;
    int celdaJugador;
    float tiempoRevision;
    float tiempoRefrescoEnemigos;
    float tiempoHastaCierre;

    public static GestorBarreras Instancia { get; private set; }
    public MapaSectores Sectores { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInstalar()
    {
        Instalar();
        SceneManager.sceneLoaded -= AlCargarEscena;
        SceneManager.sceneLoaded += AlCargarEscena;
    }

    static void AlCargarEscena(Scene escena, LoadSceneMode modo) => Instalar();

    static void Instalar()
    {
        if (FindAnyObjectByType<BarreraDinamica>() == null) return;
        if (FindAnyObjectByType<GestorBarreras>() != null) return;

        new GameObject(NombreObjeto).AddComponent<GestorBarreras>();
    }

    void Awake()
    {
        Instancia = this;
    }

    void OnDestroy()
    {
        if (Instancia == this) Instancia = null;
        foreach (Registro r in registros)
        {
            if (r.barrera != null) r.barrera.AlCambiarEstado -= AlCambiarEstadoBarrera;
        }
    }

    void Start()
    {
        if (!Iniciar()) enabled = false;
    }

    bool Iniciar()
    {
        raiz = EscanerMapa.BuscarRaiz();
        if (raiz == null) return Fallo("no hay un objeto '" + EscanerMapa.NombreRaiz + "' en la escena");

        bool[,] paredes = EscanerMapa.LeerParedes(raiz);
        if (paredes == null) return Fallo("no se encontraron muros en '" + EscanerMapa.NombreRaiz + "'");

        var grafo = new GrafoLaberinto(paredes);
        int salida = EscanerMapa.CeldaDeSalida(grafo, raiz);
        if (salida < 0) return Fallo("no hay 'Punto_Salida' ni una salida abierta en el perimetro del laberinto");

        jugador = FindAnyObjectByType<PlayerStats>();
        if (jugador == null) return Fallo("no hay un PlayerStats en la escena");

        BarreraDinamica[] encontradas = FindObjectsByType<BarreraDinamica>(FindObjectsInactive.Exclude);
        Array.Sort(encontradas, (a, b) => string.CompareOrdinal(a.name, b.name));

        var huecos = new List<int>();
        foreach (BarreraDinamica barrera in encontradas)
        {
            if (!barrera.isActiveAndEnabled) continue;

            EscanerMapa.TileDe(raiz, barrera.transform.position, out int tf, out int tc);
            int id = grafo.IdHueco(tf, tc);
            if (!grafo.EsHuecoAbierto(id) || huecos.Contains(id))
            {
                Debug.LogError("[Barreras] '" + barrera.name + "' no esta sobre un hueco abierto entre dos celdas (o esta repetida): se ignora.", barrera);
                continue;
            }

            barrera.IdHueco = id;
            huecos.Add(id);
            registros.Add(new Registro { barrera = barrera });
        }

        if (registros.Count == 0) return Fallo("ninguna barrera quedo sobre un hueco valido");

        Sectores = new MapaSectores(grafo, huecos, salida);
        celdaSpawn = CeldaDe(jugador.transform.position);

        GameObject comerciante = GameObject.Find("Punto_Comerciante");
        if (comerciante != null && comerciante.GetComponent<PuntoObligatorio>() == null) comerciante.AddComponent<PuntoObligatorio>();

        azar = semilla != 0 ? new System.Random(semilla) : new System.Random();
        tiempoHastaCierre = Sortear(intervaloMinimo, intervaloMaximo);

        foreach (Registro r in registros)
        {
            BarreraDinamica barrera = r.barrera;
            barrera.AlCambiarEstado += AlCambiarEstadoBarrera;
            barrera.ZonaLibreParaSellar = () => !ZonaOcupada(barrera, 0f);
        }

        RefrescarEnemigos();
        Debug.Log("[Barreras] " + registros.Count + " barreras, " + Sectores.CantidadSectores + " sectores, spawn en la celda " + celdaSpawn + ".");
        return true;
    }

    bool Fallo(string motivo)
    {
        Debug.LogError("[Barreras] Barreras dinamicas desactivadas: " + motivo + ".");
        return false;
    }

    void Update()
    {
        tiempoRevision += Time.deltaTime;
        if (tiempoRevision < IntervaloRevision) return;

        float dt = tiempoRevision;
        tiempoRevision = 0f;

        tiempoRefrescoEnemigos += dt;
        if (tiempoRefrescoEnemigos >= IntervaloRefrescoEnemigos) RefrescarEnemigos();

        if (jugador == null) return;
        celdaJugador = CeldaDe(jugador.transform.position);
        ReconstruirObligatorios();

        SupervisarBarreras();

        if (GameManager.EstadoActual != GameManager.Estado.Jugando) return;

        tiempoHastaCierre -= dt;
        if (tiempoHastaCierre > 0f) return;

        IntentarNuevoCierre();
        tiempoHastaCierre = Sortear(intervaloMinimo, intervaloMaximo);
    }

    void SupervisarBarreras()
    {
        foreach (Registro r in registros)
        {
            BarreraDinamica barrera = r.barrera;

            switch (barrera.Estado)
            {
                case EstadoBarrera.Aviso:
                case EstadoBarrera.Cerrando:
                    if (ZonaOcupada(barrera, margenSeguridad) || !Sectores.RutaGarantizada(celdaJugador, obligatorios))
                    {
                        barrera.Abortar();
                    }
                    else if (barrera.Estado == EstadoBarrera.Aviso && barrera.TiempoEnEstado >= duracionAviso)
                    {
                        barrera.EmpezarCierre();
                        r.duracionCerrada = Sortear(duracionCerradaMinima, duracionCerradaMaxima);
                    }
                    break;

                case EstadoBarrera.Cerrada:
                    if (barrera.TiempoEnEstado >= r.duracionCerrada) barrera.EmpezarApertura();
                    break;
            }
        }
    }

    void IntentarNuevoCierre()
    {
        int cerradas = 0;
        candidatos.Clear();
        foreach (Registro r in registros)
        {
            if (MapaSectores.Bloquea(r.barrera.Estado)) cerradas++;
            else if (r.barrera.Estado == EstadoBarrera.Abierta && r.barrera.TiempoEnEstado >= tiempoMinimoAbierta) candidatos.Add(r);
        }
        if (cerradas >= maximoCerradasSimultaneas) return;

        for (int i = candidatos.Count - 1; i > 0; i--)
        {
            int j = azar.Next(i + 1);
            Registro tmp = candidatos[i];
            candidatos[i] = candidatos[j];
            candidatos[j] = tmp;
        }

        foreach (Registro r in candidatos)
        {
            if (ZonaOcupada(r.barrera, margenSeguridad)) continue;
            if (!Sectores.PuedeCerrar(r.barrera.IdHueco, celdaJugador, obligatorios)) continue;

            r.barrera.EmpezarAviso();
            return;
        }
    }

    void AlCambiarEstadoBarrera(BarreraDinamica barrera, EstadoBarrera nuevo)
    {
        Sectores.CambiarEstado(barrera.IdHueco, nuevo);
    }

    void ReconstruirObligatorios()
    {
        obligatorios.Clear();
        obligatorios.Add(celdaSpawn);
        obligatorios.Add(Sectores.CeldaSalida);

        foreach (PuntoObligatorio punto in PuntoObligatorio.Activos)
        {
            int celda = CeldaDe(punto.transform.position);
            if (!obligatorios.Contains(celda)) obligatorios.Add(celda);
        }
    }

    // Jugador o enemigo dentro del cuadrado del hueco ensanchado en "margen" metros por cada lado.
    bool ZonaOcupada(BarreraDinamica barrera, float margen)
    {
        float limite = MapaLayout.AnchoCalle * 0.5f + margen + RadioCuerpo;

        if (jugador != null && EstaDentro(barrera, jugador.transform.position, limite)) return true;
        foreach (EnemyAI enemigo in enemigos)
        {
            if (enemigo != null && EstaDentro(barrera, enemigo.transform.position, limite)) return true;
        }
        return false;
    }

    bool EstaDentro(BarreraDinamica barrera, Vector3 posicion, float limite)
    {
        Vector3 d = raiz.InverseTransformVector(posicion - barrera.transform.position);
        return Mathf.Abs(d.x) <= limite && Mathf.Abs(d.z) <= limite && d.y > -1f && d.y < MapaLayout.AltoMuro + 1f;
    }

    void RefrescarEnemigos()
    {
        tiempoRefrescoEnemigos = 0f;
        enemigos = FindObjectsByType<EnemyAI>(FindObjectsInactive.Exclude);
    }

    int CeldaDe(Vector3 posicionMundo)
    {
        MapaLayout.CeldaMasCercana(raiz.InverseTransformPoint(posicionMundo), out int r, out int c);
        return Sectores != null ? Sectores.Grafo.IdCelda(r, c) : r * MapaLayout.Columnas + c;
    }

    float Sortear(float minimo, float maximo)
    {
        return minimo + (float)azar.NextDouble() * Mathf.Max(0f, maximo - minimo);
    }

    public EstadoSector EstadoDeSectorEn(Vector3 posicionMundo)
    {
        return Sectores.EstadoDeCelda(CeldaDe(posicionMundo));
    }

    // Seleccionando el objeto en Play: azul = sector libre, rojo = aislado.
    void OnDrawGizmosSelected()
    {
        if (Sectores == null || raiz == null) return;

        GrafoLaberinto g = Sectores.Grafo;
        for (int celda = 0; celda < g.Celdas; celda++)
        {
            Vector3 centro = raiz.TransformPoint(MapaLayout.GrillaALocal(2 * g.FilaDe(celda) + 1, 2 * g.ColumnaDe(celda) + 1));
            Gizmos.color = Sectores.EstadoDeCelda(celda) == EstadoSector.Libre ? new Color(0.2f, 0.5f, 1f, 0.35f) : new Color(1f, 0.1f, 0.1f, 0.5f);
            Gizmos.DrawCube(centro + Vector3.up * 0.15f, new Vector3(MapaLayout.AnchoCalle * 0.8f, 0.1f, MapaLayout.AnchoCalle * 0.8f));
        }
    }
}
