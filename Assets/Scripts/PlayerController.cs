using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Movimiento")]
    public float walkSpeed = 5f;
    public float sprintSpeed = 8f;

    [Header("Gravedad")]
    public float gravity = -20f;

    private CharacterController controller;
    private float verticalVelocity;

    void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        // Movimiento
        float x = KeyBindings.Axis(KeyBindings.Action.Left, KeyBindings.Action.Right);
        float z = KeyBindings.Axis(KeyBindings.Action.Back, KeyBindings.Action.Forward);

        Vector3 movement = transform.right * x + transform.forward * z;

        // Correr
        float currentSpeed = KeyBindings.Held(KeyBindings.Action.Sprint)
            ? sprintSpeed
            : walkSpeed;

        // Gravedad
        if (controller.isGrounded && verticalVelocity < 0)
        {
            verticalVelocity = -2f;
        }

        verticalVelocity += gravity * Time.deltaTime;

        movement.y = verticalVelocity;

        // Aplicar movimiento
        controller.Move(movement * currentSpeed * Time.deltaTime);
    }
}