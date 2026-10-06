using System;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

// Pared que sube y baja en un hueco entre dos celdas del laberinto. Solo ejecuta la maquina de
// estados (Abierta, Aviso, Cerrando, Cerrada, Abriendo): quien decide cuando cerrar, y si es seguro,
// es GestorBarreras. Necesita un hijo "Cuerpo" (cubo de 6x8x6 m con su BoxCollider) y se pone en el
// centro del hueco, a nivel de piso.
public class BarreraDinamica : MonoBehaviour
{
    const float AlturaOculta = -0.1f; // la cara de arriba queda justo bajo el piso
    const float VelocidadAviso = 3f;
    const float TembloAviso = 0.05f;

    [SerializeField] float duracionMovimiento = 1.5f;
    [Tooltip("Cuanto asoma del piso durante el aviso, en metros")]
    [SerializeField] float alturaAviso = 0.6f;

    Transform cuerpo;
    Collider[] colliders;
    NavMeshObstacle obstaculo;
    float alturaActual = AlturaOculta;

    public EstadoBarrera Estado { get; private set; } = EstadoBarrera.Abierta;
    public int IdHueco { get; set; } = -1;
    public float TiempoEnEstado { get; private set; }

    // Se consulta justo antes de sellar: si devuelve false (hay alguien en el hueco) la barrera vuelve a abrirse.
    public Func<bool> ZonaLibreParaSellar { get; set; }

    public event Action<BarreraDinamica, EstadoBarrera> AlCambiarEstado;

    float VelocidadMovimiento => (MapaLayout.AltoMuro - AlturaOculta) / Mathf.Max(0.1f, duracionMovimiento);

    void Awake()
    {
        cuerpo = transform.Find("Cuerpo");
        if (cuerpo == null)
        {
            Debug.LogError("BarreraDinamica '" + name + "': falta el hijo 'Cuerpo' (cubo de 6x8x6 m).", this);
            enabled = false;
            return;
        }

        colliders = cuerpo.GetComponentsInChildren<Collider>(true);

        // Excluida del bake del NavMesh (que se arma una sola vez al cargar): el corte se hace con carving.
        NavMeshModifier modificador = GetComponent<NavMeshModifier>();
        if (modificador == null) modificador = gameObject.AddComponent<NavMeshModifier>();
        modificador.ignoreFromBuild = true;

        obstaculo = GetComponent<NavMeshObstacle>();
        if (obstaculo == null) obstaculo = gameObject.AddComponent<NavMeshObstacle>();
        obstaculo.shape = NavMeshObstacleShape.Box;
        obstaculo.size = new Vector3(MapaLayout.AnchoCalle, MapaLayout.AltoMuro, MapaLayout.AnchoCalle);
        obstaculo.center = new Vector3(0f, MapaLayout.AltoMuro * 0.5f, 0f);
        obstaculo.carving = true;
        obstaculo.carveOnlyStationary = true;

        AplicarFisica();
        AplicarAltura(alturaActual, false);
    }

    void Update()
    {
        TiempoEnEstado += Time.deltaTime;
        float paso = VelocidadMovimiento * Time.deltaTime;

        switch (Estado)
        {
            case EstadoBarrera.Abierta:
                alturaActual = Mathf.MoveTowards(alturaActual, AlturaOculta, paso);
                AplicarAltura(alturaActual, false);
                break;

            case EstadoBarrera.Aviso:
                alturaActual = Mathf.MoveTowards(alturaActual, alturaAviso, VelocidadAviso * Time.deltaTime);
                AplicarAltura(alturaActual, true);
                break;

            case EstadoBarrera.Cerrando:
                alturaActual = Mathf.MoveTowards(alturaActual, MapaLayout.AltoMuro, paso);
                AplicarAltura(alturaActual, false);
                if (alturaActual >= MapaLayout.AltoMuro) Sellar();
                break;

            case EstadoBarrera.Cerrada:
                AplicarAltura(MapaLayout.AltoMuro, false);
                break;

            case EstadoBarrera.Abriendo:
                alturaActual = Mathf.MoveTowards(alturaActual, AlturaOculta, paso);
                AplicarAltura(alturaActual, false);
                if (alturaActual <= AlturaOculta) CambiarEstado(EstadoBarrera.Abierta);
                break;
        }
    }

    public void EmpezarAviso()
    {
        if (Estado == EstadoBarrera.Abierta) CambiarEstado(EstadoBarrera.Aviso);
    }

    public void EmpezarCierre()
    {
        if (Estado == EstadoBarrera.Aviso) CambiarEstado(EstadoBarrera.Cerrando);
    }

    public void EmpezarApertura()
    {
        if (Estado == EstadoBarrera.Cerrada || Estado == EstadoBarrera.Cerrando) CambiarEstado(EstadoBarrera.Abriendo);
    }

    // Cancela un cierre en curso: en aviso vuelve a Abierta, ya subiendo empieza a bajar.
    public void Abortar()
    {
        if (Estado == EstadoBarrera.Aviso) CambiarEstado(EstadoBarrera.Abierta);
        else EmpezarApertura();
    }

    void Sellar()
    {
        bool libre = ZonaLibreParaSellar == null || ZonaLibreParaSellar();
        CambiarEstado(libre ? EstadoBarrera.Cerrada : EstadoBarrera.Abriendo);
    }

    void CambiarEstado(EstadoBarrera nuevo)
    {
        if (Estado == nuevo) return;

        Estado = nuevo;
        TiempoEnEstado = 0f;
        AplicarFisica();
        AlCambiarEstado?.Invoke(this, nuevo);
    }

    // El collider solo existe con la barrera sellada: mientras se mueve no puede empujar ni aplastar a nadie.
    // El carving arranca al empezar a cerrar y sigue hasta que termina de abrir.
    void AplicarFisica()
    {
        bool solida = Estado == EstadoBarrera.Cerrada;
        for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = solida;

        obstaculo.enabled = Estado == EstadoBarrera.Cerrando || Estado == EstadoBarrera.Cerrada || Estado == EstadoBarrera.Abriendo;
    }

    // "altura" es hasta donde llega la cara de arriba del cuerpo, medida desde el piso.
    void AplicarAltura(float altura, bool temblar)
    {
        Vector3 temblor = temblar ? new Vector3(UnityEngine.Random.Range(-TembloAviso, TembloAviso), 0f, UnityEngine.Random.Range(-TembloAviso, TembloAviso)) : Vector3.zero;
        cuerpo.localPosition = new Vector3(temblor.x, altura - MapaLayout.AltoMuro * 0.5f, temblor.z);
    }

    void OnDrawGizmos()
    {
        switch (Estado)
        {
            case EstadoBarrera.Abierta: Gizmos.color = new Color(0.2f, 0.9f, 0.3f, 0.8f); break;
            case EstadoBarrera.Aviso: Gizmos.color = new Color(1f, 0.9f, 0.1f, 0.9f); break;
            case EstadoBarrera.Cerrada: Gizmos.color = new Color(1f, 0.15f, 0.1f, 0.9f); break;
            default: Gizmos.color = new Color(1f, 0.55f, 0.1f, 0.9f); break;
        }

        Gizmos.DrawWireCube(
            transform.position + Vector3.up * MapaLayout.AltoMuro * 0.5f,
            new Vector3(MapaLayout.AnchoCalle, MapaLayout.AltoMuro, MapaLayout.AnchoCalle));
    }
}
