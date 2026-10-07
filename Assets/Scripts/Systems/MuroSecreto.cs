using System;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

// Seccion de muro del laberinto que arranca CERRADA y se hunde en el piso cuando algo la abre
// (hoy el BotonSecreto escondido en la habitacion del comerciante). Es la contracara de
// BarreraDinamica: esa se cierra sola y la maneja GestorBarreras, esta no tiene ciclo propio ni
// arbitro, se abre una sola vez y para siempre.
//
// Reutiliza la misma jerarquia y las mismas medidas que BarreraDinamica (un hijo "Cuerpo", cubo de
// AnchoCalle x AltoMuro x AnchoCalle), porque ProgresionBuilder convierte una barrera dinamica ya
// colocada en un muro secreto: la geometria se conserva tal cual y solo se cambia el componente.
//
// NavMesh: igual que BarreraDinamica, el cuerpo se excluye del horneado
// (NavMeshModifier.ignoreFromBuild) y el corte real lo hace un NavMeshObstacle con carving. Con el
// muro cerrado los enemigos no pueden atravesarlo; cuando termina de abrirse el obstaculo se apaga
// y el paso queda libre para ellos tambien.
public class MuroSecreto : MonoBehaviour
{
    // La cara de arriba del cuerpo queda justo bajo el piso: el mismo valor que usa
    // BarreraDinamica, para que el muro abierto se vea igual que una barrera abierta.
    const float AlturaOculta = -0.1f;

    [Header("Apertura")]
    [Tooltip("Segundos que tarda el muro en hundirse del todo.")]
    [Min(0.1f)] [SerializeField] private float duracionApertura = 2.5f;

    [Tooltip("Si es verdadero, el muro arranca abierto (para probar la zona sin pasar por el boton).")]
    [SerializeField] private bool abiertoAlArrancar;

    [Header("Audio")]
    [Tooltip("Opcional. Suena en la posicion del muro mientras se abre (piedra arrastrandose).")]
    [SerializeField] private AudioClip sonidoApertura;

    [Header("Eventos")]
    [Tooltip("Se invoca una sola vez, en el momento en que el muro empieza a abrirse.")]
    [SerializeField] private UnityEvent alAbrirse = new UnityEvent();

    Transform cuerpo;
    Collider[] colliders = Array.Empty<Collider>();
    NavMeshObstacle obstaculo;
    float altura;            // hasta donde llega la cara de arriba del cuerpo, medida desde el piso
    bool abriendo;

    /// <summary>Verdadero cuando el muro ya termino de hundirse.</summary>
    public bool Abierto => altura <= AlturaOculta;

    /// <summary>Verdadero mientras el muro se esta hundiendo.</summary>
    public bool Abriendo => abriendo;

    /// <summary>Se dispara una vez, al empezar la apertura.</summary>
    public event Action AlAbrirse;

    float VelocidadMovimiento => (MapaLayout.AltoMuro - AlturaOculta) / Mathf.Max(0.1f, duracionApertura);

    void Awake()
    {
        cuerpo = transform.Find("Cuerpo");
        if (cuerpo == null)
        {
            Debug.LogError($"MuroSecreto '{name}': falta el hijo 'Cuerpo' (el cubo del muro).", this);
            enabled = false;
            return;
        }

        colliders = cuerpo.GetComponentsInChildren<Collider>(true);

        PrepararNavMesh();

        altura = abiertoAlArrancar ? AlturaOculta : MapaLayout.AltoMuro;
        AplicarAltura();
        AplicarFisica();
    }

    void Update()
    {
        if (!abriendo) return;

        altura = Mathf.MoveTowards(altura, AlturaOculta, VelocidadMovimiento * Time.deltaTime);
        AplicarAltura();

        if (Abierto)
        {
            abriendo = false;
            AplicarFisica();
        }
    }

    /// <summary>
    /// Empieza a hundir el muro. Llamarlo dos veces no hace nada: un muro ya abierto, o a medio
    /// abrir, ignora la orden (no vuelve a sonar ni a disparar los eventos).
    /// </summary>
    public void Abrir()
    {
        if (Abierto || abriendo) return;

        abriendo = true;
        AplicarFisica();

        if (sonidoApertura != null && Application.isPlaying)
        {
            AudioSource.PlayClipAtPoint(sonidoApertura, transform.position);
        }

        alAbrirse?.Invoke();
        AlAbrirse?.Invoke();
    }

    // El cuerpo deja de ser solido en cuanto empieza a bajar: asi no arrastra ni aplasta al
    // jugador que este pegado al muro (mismo criterio que BarreraDinamica.AplicarFisica).
    void AplicarFisica()
    {
        bool solido = !abriendo && !Abierto;
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null) colliders[i].enabled = solido;
        }

        // El carving se mantiene mientras baja: recien con el muro del todo abajo el NavMesh
        // vuelve a tener paso, que es cuando el hueco esta libre de verdad.
        if (obstaculo != null) obstaculo.enabled = !Abierto;
    }

    void AplicarAltura()
    {
        cuerpo.localPosition = new Vector3(0f, altura - MapaLayout.AltoMuro * 0.5f, 0f);
    }

    void PrepararNavMesh()
    {
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
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (transform.Find("Cuerpo") == null)
        {
            Debug.LogWarning($"MuroSecreto '{name}': no tiene un hijo 'Cuerpo'; sin el no hay nada que mover.", this);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Abierto
            ? new Color(0.2f, 0.9f, 0.3f, 0.8f)
            : new Color(0.6f, 0.2f, 0.9f, 0.9f);

        Gizmos.DrawWireCube(
            transform.position + Vector3.up * MapaLayout.AltoMuro * 0.5f,
            new Vector3(MapaLayout.AnchoCalle, MapaLayout.AltoMuro, MapaLayout.AnchoCalle));
    }
#endif
}
