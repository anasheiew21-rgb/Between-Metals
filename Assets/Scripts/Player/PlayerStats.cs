using System;
using UnityEngine;

// Vida y estamina del jugador. Otros scripts (dano, comerciante, UI) se enganchan via los
// eventos AlCambiarVida/AlCambiarEstamina/AlMorir en vez de leer estos campos en su propio
// Update(): asi PlayerUI (y quien quiera) se entera del cambio justo cuando pasa, no sondeando.
public class PlayerStats : MonoBehaviour
{
    [Header("Vida")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;

    [Header("Estamina")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float currentStamina;
    [SerializeField] private float staminaDrainPerSecond = 20f;
    [SerializeField] private float staminaRegenPerSecond = 15f;
    [Tooltip("Cuanto hay que esperar sin correr antes de que la estamina empiece a regenerarse")]
    [SerializeField] private float staminaRechargeDelay = 1.5f;

    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;
    public float MaxStamina => maxStamina;
    public float CurrentStamina => currentStamina;
    public bool EstaViva => currentHealth > 0f;

    // PlayerController marca esto en true/false cada frame segun si el jugador esta
    // efectivamente corriendo (tecla apretada + moviendose). Toda la logica de consumo y
    // regeneracion vive aca adentro, en un solo lugar.
    [HideInInspector] public bool estaCorriendo;
    public bool PuedeCorrer => currentStamina > 0f;

    public event Action<float, float> AlCambiarVida;     // (actual, maximo)
    public event Action<float, float> AlCambiarEstamina; // (actual, maximo)
    public event Action AlMorir;

    private float tiempoSinCorrer;
    private bool yaMurio;

    void Awake()
    {
        currentHealth = maxHealth;
        currentStamina = maxStamina;
    }

    void Start()
    {
        // Notifica el estado inicial para que la UI arranque ya con las barras correctas
        AlCambiarVida?.Invoke(currentHealth, maxHealth);
        AlCambiarEstamina?.Invoke(currentStamina, maxStamina);
    }

    void Update()
    {
        ActualizarEstamina();
    }

    public void TakeDamage(float damage)
    {
        if (!EstaViva || damage <= 0f) return;

        currentHealth = Mathf.Max(0f, currentHealth - damage);
        AlCambiarVida?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0f) Die();
    }

    public void Curar(float cantidad)
    {
        if (!EstaViva || cantidad <= 0f) return;

        currentHealth = Mathf.Min(maxHealth, currentHealth + cantidad);
        AlCambiarVida?.Invoke(currentHealth, maxHealth);
    }

    void Die()
    {
        if (yaMurio) return;
        yaMurio = true;
        AlMorir?.Invoke();
    }

    void ActualizarEstamina()
    {
        if (estaCorriendo && currentStamina > 0f)
        {
            tiempoSinCorrer = 0f;
            CambiarEstamina(-staminaDrainPerSecond * Time.deltaTime);
        }
        else
        {
            tiempoSinCorrer += Time.deltaTime;
            if (tiempoSinCorrer >= staminaRechargeDelay)
            {
                CambiarEstamina(staminaRegenPerSecond * Time.deltaTime);
            }
        }
    }

    void CambiarEstamina(float delta)
    {
        float anterior = currentStamina;
        currentStamina = Mathf.Clamp(currentStamina + delta, 0f, maxStamina);
        if (!Mathf.Approximately(currentStamina, anterior))
        {
            AlCambiarEstamina?.Invoke(currentStamina, maxStamina);
        }
    }
}
