using UnityEngine;

public class MouseLook : MonoBehaviour
{
    [SerializeField] private float mouseSensitivity = 100f;

    // Mouse X/Y ya es un delta por frame, no se multiplica por Time.deltaTime.
    // 1/60 conserva la sensación que tenía mouseSensitivity = 100 a 60 fps.
    private const float SensitivityScale = 1f / 60f;

    private float xRotation = 0f;
    private Transform body;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        body = transform.parent;

        if (body == null)
        {
            Debug.LogWarning("MouseLook: la cámara no tiene un objeto padre (cuerpo). No se aplicará la rotación horizontal.");
        }
    }

    void Update()
    {
        // Con el menu abierto el juego esta en pausa: la camara no debe girar
        if (Menu.IsOpen) return;

        float sensitivity = PlayerPrefs.GetFloat("Sensitivity", 1.0f);

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * sensitivity * SensitivityScale;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * sensitivity * SensitivityScale;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        if (body != null)
        {
            body.Rotate(Vector3.up * mouseX);
        }
    }
}