using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movimiento")]
    public float walkSpeed = 5f;
    public float sprintSpeed = 8f;

    [Header("Salto")]
    [SerializeField] private float jumpForce = 7f;
    [SerializeField] private float groundCheckDistance = 0.2f;
    [SerializeField] private LayerMask groundMask = ~0;

    [Header("Gravedad")]
    public float gravity = -20f;

    private CharacterController controller;
    private PlayerStats stats;
    private float verticalVelocity;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        stats = GetComponent<PlayerStats>();
    }

    void Update()
    {
        // Muerto no se mueve mas (redundante con Time.timeScale = 0 que pone GameOverUI, pero
        // explicito: si algun dia esto corre sin esa pantalla, igual deja de andar).
        if (stats != null && !stats.EstaViva) return;

        // Movimiento
        float x = KeyBindings.Axis(KeyBindings.Action.Left, KeyBindings.Action.Right);
        float z = KeyBindings.Axis(KeyBindings.Action.Back, KeyBindings.Action.Forward);

        Vector3 movement = transform.right * x + transform.forward * z;
        movement = Vector3.ClampMagnitude(movement, 1f);

        // Correr: solo si hay estamina. PlayerStats es quien la consume/regenera; aca solo se
        // le avisa si este frame se esta corriendo de verdad (tecla apretada y moviendose).
        bool intentaCorrer = KeyBindings.Held(KeyBindings.Action.Sprint) && movement.sqrMagnitude > 0.01f;
        bool corriendo = intentaCorrer && (stats == null || stats.PuedeCorrer);
        if (stats != null) stats.estaCorriendo = corriendo;

        float currentSpeed = corriendo ? sprintSpeed : walkSpeed;

        // La velocidad solo afecta al plano horizontal
        Vector3 velocity = movement * currentSpeed;

        // Suelo: se comprueba con un SphereCast ademas de controller.isGrounded porque
        // isGrounded solo se actualiza tras el ultimo Move() y puede llegar un frame tarde.
        bool grounded = IsGrounded();

        if (grounded && verticalVelocity < 0)
        {
            verticalVelocity = -2f;
        }

        if (grounded && KeyBindings.Down(KeyBindings.Action.Jump))
        {
            verticalVelocity = jumpForce;
        }

        verticalVelocity += gravity * Time.deltaTime;

        velocity.y = verticalVelocity;

        // Aplicar movimiento
        controller.Move(velocity * Time.deltaTime);
    }

    private bool IsGrounded()
    {
        if (controller.isGrounded) return true;

        Vector3 origin = transform.position + Vector3.up * controller.radius;
        float castDistance = controller.radius + groundCheckDistance;

        return Physics.SphereCast(origin, controller.radius * 0.9f, Vector3.down, out _, castDistance, groundMask, QueryTriggerInteraction.Ignore);
    }
}
