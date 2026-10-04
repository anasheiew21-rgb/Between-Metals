using System;
using UnityEngine;

// Vida y estamina del jugador. Otros scripts (dano, comerciante, UI) se enganchan via los
// eventos AlCambiarVida/AlCambiarEstamina/AlMorir en vez de leer estos campos en su propio
// Update(): asi PlayerUI (y quien quiera) se entera del cambio justo cuando pasa, no sondeando.
//
// Audio: el SFX de impacto se reproduce aca adentro, en TakeDamage(), no del lado del que pega.
// Asi suena una sola vez por golpe recibido venga de donde venga (EnemyAI, una trampa, lo que sea)
// y queda sincronizado con el frame exacto en que la vida baja: el ataque del enemigo aplica el
// dano desde el Animation Event del clip (EnemyAI.OnAttackHit), asi que el quejido del jugador cae
// justo en el frame del golpe visual.
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
    [Tooltip("Estamina que consume cada golpe de ataque cuerpo a cuerpo (HU-14)")]
    [SerializeField] private float costoAtaqueEstamina = 15f;

    [Header("Economia")]
    [SerializeField] private int oro = 0;

    [Header("Audio")]
    [Tooltip("Si se deja vacio se busca/crea un AudioSource en este objeto al entrar en juego")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("Impacto: suena cada vez que el jugador recibe dano y sigue vivo")]
    [SerializeField] private AudioClip hurtSound;
    [Tooltip("Golpe final: suena en lugar de hurtSound cuando el dano lo mata")]
    [SerializeField] private AudioClip deathSound;
    [Range(0f, 1f)][SerializeField] private float volumenDano = 1f;
    [Tooltip("Variacion de tono (+/-) de cada impacto, para que repetir el mismo clip no se vuelva monotono")]
    [Range(0f, 0.5f)][SerializeField] private float variacionTonoDano = 0.1f;

    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;
    public float MaxStamina => maxStamina;
    public float CurrentStamina => currentStamina;
    // Mientras esta en false, PlayerController deja de procesar movimiento (ver su Update)
    public bool EstaViva => currentHealth > 0f;

    // PlayerController marca esto en true/false cada frame segun si el jugador esta
    // efectivamente corriendo (tecla apretada + moviendose). Toda la logica de consumo y
    // regeneracion vive aca adentro, en un solo lugar.
    [HideInInspector] public bool estaCorriendo;
    public bool PuedeCorrer => currentStamina > 0f;
    // PlayerCombat lo consulta antes de dejar conectar un golpe (mismo criterio que PuedeCorrer)
    public bool PuedeAtacar => currentStamina >= costoAtaqueEstamina;

    public int Oro => oro;

    public event Action<float, float> AlCambiarVida;     // (actual, maximo)
    public event Action<float, float> AlCambiarEstamina; // (actual, maximo)
    public event Action<int> AlCambiarOro;                // (actual)
    public event Action AlMorir;

    private float tiempoSinCorrer;
    private bool yaMurio;

    void Awake()
    {
        currentHealth = maxHealth;
        currentStamina = maxStamina;

        AsegurarAudioSource();
    }

    void Start()
    {
        // Notifica el estado inicial para que la UI arranque ya con las barras correctas
        AlCambiarVida?.Invoke(currentHealth, maxHealth);
        AlCambiarEstamina?.Invoke(currentStamina, maxStamina);
        AlCambiarOro?.Invoke(oro);
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

        bool muere = currentHealth <= 0f;
        // Si el golpe mata, suena el clip de muerte en vez del de dolor (y no los dos encimados).
        ReproducirSfx(muere && deathSound != null ? deathSound : hurtSound);

        if (muere) Die();
    }

    public void Curar(float cantidad)
    {
        if (!EstaViva || cantidad <= 0f) return;

        currentHealth = Mathf.Min(maxHealth, currentHealth + cantidad);
        AlCambiarVida?.Invoke(currentHealth, maxHealth);
    }

    // La tienda consulta esto antes de intentar cobrar una compra
    public bool PuedePagar(int cantidad) => cantidad >= 0 && oro >= cantidad;

    // Devuelve false sin tocar nada si no alcanza el oro; el llamador (ShopManager) decide que
    // hacer en ese caso.
    public bool GastarOro(int cantidad)
    {
        if (!PuedePagar(cantidad)) return false;

        oro -= cantidad;
        AlCambiarOro?.Invoke(oro);
        return true;
    }

    public void AgregarOro(int cantidad)
    {
        if (cantidad <= 0) return;

        oro += cantidad;
        AlCambiarOro?.Invoke(oro);
    }

    // PlayerCombat llama esto al intentar un ataque, ya validado PuedeAtacar antes
    public void ConsumirEstaminaAtaque()
    {
        CambiarEstamina(-costoAtaqueEstamina);
    }

    void Die()
    {
        if (yaMurio) return;
        yaMurio = true;

        // Se libera el cursor aca mismo (no solo en GameOverUI) para que el jugador pueda
        // interactuar con la pantalla de Game Over aunque, por lo que sea, esta no exista.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        AlMorir?.Invoke();
    }

    // ---------------------------------------------------------------
    // Audio
    // ---------------------------------------------------------------

    // El jugador se escucha a si mismo: AudioSource 2D (spatialBlend 0), sin atenuacion por
    // distancia. Solo se crea el componente en juego, para no ensuciar la escena desde los
    // self-tests de Editor (que invocan Awake por reflexion).
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
        audioSource.spatialBlend = 0f;
        AudioPreferences.RutearASfx(audioSource); // asi el slider de SFX del menu lo afecta
    }

    void ReproducirSfx(AudioClip clip)
    {
        if (clip == null) return;

        AsegurarAudioSource();
        if (audioSource == null)
        {
            // Mismo motivo que en EnemyAI.ReproducirSfx: nada de audio en modo edicion.
            if (Application.isPlaying) AudioSource.PlayClipAtPoint(clip, transform.position, volumenDano);
            return;
        }

        // El tono se fija (no se acumula) en cada golpe, asi las variaciones no se van sumando
        // hasta quedar en un chillido.
        audioSource.pitch = 1f + UnityEngine.Random.Range(-variacionTonoDano, variacionTonoDano);
        audioSource.PlayOneShot(clip, volumenDano);
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
