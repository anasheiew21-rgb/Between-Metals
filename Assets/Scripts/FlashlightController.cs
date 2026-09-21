using UnityEngine;

public class FlashlightController : MonoBehaviour
{
    private Light flashlight;

    void Start()
    {
        flashlight = GetComponent<Light>();
    }

    void Update()
    {
        if (KeyBindings.Down(KeyBindings.Action.Flashlight) && Time.timeScale > 0f)
        {
            flashlight.enabled = !flashlight.enabled;
        }
    }
}