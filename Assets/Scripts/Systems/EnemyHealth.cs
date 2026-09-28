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
    [SerializeField] private float maxHealth = 50f;

    private float? currentHealth;
    private bool yaMurio;

    public float MaxHealth => maxHealth;
    public float VidaActual => currentHealth ?? maxHealth;
    public bool EstaVivo => VidaActual > 0f;

    public event Action<float, float> AlRecibirDaño; // (actual, maximo)
    public event Action AlMorir;

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
