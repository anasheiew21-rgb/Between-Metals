using System;
using UnityEngine;

// Vida de los enemigos (HU-14, combate). Implementa IDamageable para que PlayerCombat (o
// cualquier otra fuente de daño futura) le pegue sin depender de EnemyAI; EnemyAI, a su vez, solo
// se entera de la muerte via el evento AlMorir, sin que EnemyHealth sepa nada de estados ni de
// NavMesh (mismo desacople que usa PlayerStats con el jugador).
//
// VidaActual devuelve maxHealth hasta el primer cambio real (mismo truco que Inventory.Items con
// ??=): asi no hace falta Awake, que en modo Editor sin Play (AddComponent desde un self-test)
// puede no llegar a correr.
public class EnemyHealth : MonoBehaviour, IDamageable
{
    [Header("Vida")]
    [Tooltip("500 contra los 20 de dano a mano limpia y los 75 con el arma del comerciante (PlayerCombat): 25 golpes sin arma, 7 con arma.")]
    [SerializeField] private float maxHealth = 500f;

    private float? currentHealth;
    private bool yaMurio;

    public float MaxHealth => maxHealth;
    public float VidaActual => currentHealth ?? maxHealth;
    public bool EstaVivo => VidaActual > 0f;

    public event Action<float, float> AlRecibirDaño; // (actual, maximo)
    public event Action AlMorir;

    // Autoagrega el feedback visual de daño (T14-F), el botin (economia) y el despawn del cuerpo
    // (para que un enemigo muerto no se quede bloqueando el pasillo para siempre) para que ningun
    // enemigo con EnemyHealth necesite que alguien le agregue estos componentes a mano en el
    // Editor. Esto acopla puntualmente EnemyHealth a EnemyHitFeedback, BotinEnemigo y EnemyDespawn;
    // a cambio, "tener vida" alcanza para "flashear al recibir daño", "soltar una moneda al morir"
    // y "desaparecer un rato despues de morir" sin pasos manuales.
    // Solo corre en Play (Awake no llega a correr en Editor sin Play, ver el comentario de
    // VidaActual mas arriba), asi que no afecta a los self-tests.
    void Awake()
    {
        if (GetComponent<EnemyHitFeedback>() == null) gameObject.AddComponent<EnemyHitFeedback>();

        // Despues de EnemyHitFeedback a proposito: BotinEnemigo y EnemyDespawn se enganchan a
        // AlMorir en su propio Awake, que corre recien al terminar cada AddComponent, asi que no se
        // pierde el evento.
        if (GetComponent<BotinEnemigo>() == null) gameObject.AddComponent<BotinEnemigo>();
        if (GetComponent<EnemyDespawn>() == null) gameObject.AddComponent<EnemyDespawn>();
    }

    public void TakeDamage(float cantidad)
    {
        if (!EstaVivo || cantidad <= 0f) return;

        currentHealth = Mathf.Max(0f, VidaActual - cantidad);
        AlRecibirDaño?.Invoke(currentHealth.Value, maxHealth);

        if (currentHealth.Value <= 0f) Morir();
    }

    void Morir()
    {
        if (yaMurio) return;
        yaMurio = true;

        AlMorir?.Invoke();
    }
}
