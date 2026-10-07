using UnityEngine;
using UnityEngine.UI;

// Foto de fondo del menu principal. Es un componente aparte de Menu a proposito: Menu se dibuja
// con IMGUI (OnGUI), que no sabe escalar una imagen a pantalla completa sin deformarla, asi que el
// fondo se arma con un Canvas uGUI y una Image de verdad. IMGUI se dibuja siempre encima de un
// Canvas en ScreenSpaceOverlay, asi que los botones del menu quedan sobre la foto sin ningun ajuste.
//
// Es el unico lugar del proyecto, junto con PlayerUI, que usa uGUI, y por el mismo motivo: hace
// falta un Canvas real para que Image haga su trabajo.
//
// Para poner la foto: arrastra el Sprite al campo "Imagen de fondo" en el Inspector. La textura
// tiene que estar importada como Sprite (2D and UI); si se deja vacio queda el color plano de
// "Color de fondo", que es lo que se ve ahora.
[DisallowMultipleComponent]
public class FondoMenu : MonoBehaviour
{
    [Header("Imagen")]
    [Tooltip("Foto de fondo del menu. Importala como Sprite (2D and UI). Vacio = solo el color de abajo")]
    [SerializeField] private Sprite imagenDeFondo;

    [Tooltip("Color que tapa la pantalla cuando no hay imagen (y que tinta la imagen cuando si hay)")]
    [SerializeField] private Color colorDeFondo = new Color(0.04f, 0.05f, 0.07f, 1f);

    [Header("Encuadre")]
    [Tooltip("Recorta la foto para llenar la pantalla sin deformarla (como el 'cover' de CSS). Apagado la estira")]
    [SerializeField] private bool mantenerProporcion = true;

    [Tooltip("Orden de dibujado del Canvas. Negativo para quedar detras de cualquier otra UI")]
    [SerializeField] private int ordenCanvas = -100;

    Image imagen;
    Vector2Int ultimaPantalla;

    /// <summary>La Image del fondo, ya creada. Null antes del primer Start.</summary>
    public Image Imagen => imagen;

    void Start()
    {
        ConstruirCanvas();
    }

    // El encuadre depende de la proporcion de la pantalla, y el menu de Graficos puede cambiar la
    // resolucion en cualquier momento. Se recalcula solo cuando cambia (dos comparaciones de int
    // por frame), no todos los frames: ultimaPantalla arranca en cero, asi que el primer Update
    // siempre encuadra, ya con el Canvas medido de verdad.
    void Update()
    {
        var pantalla = new Vector2Int(Screen.width, Screen.height);
        if (pantalla == ultimaPantalla) return;

        ultimaPantalla = pantalla;
        AplicarImagen();
    }

    /// <summary>
    /// Cambia la foto en tiempo de ejecucion (para una transicion, o para probar varias sin
    /// reiniciar). Si todavia no se construyo el Canvas, se guarda y se aplica en Start.
    /// </summary>
    public void CambiarImagen(Sprite nueva)
    {
        imagenDeFondo = nueva;
        AplicarImagen();
    }

    void ConstruirCanvas()
    {
        GameObject canvasGO = new GameObject("Canvas_FondoMenu");
        canvasGO.transform.SetParent(transform, false);

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Detras de todo: el HUD del jugador (PlayerUI) usa el orden 0 por defecto.
        canvas.sortingOrder = ordenCanvas;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // Sin GraphicRaycaster: el fondo no recibe clics, y uno de mas se comeria los clics que
        // van a los botones IMGUI del menu.

        GameObject imagenGO = new GameObject("Imagen", typeof(RectTransform));
        imagenGO.transform.SetParent(canvasGO.transform, false);

        RectTransform rt = imagenGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        imagen = imagenGO.AddComponent<Image>();
        imagen.raycastTarget = false;

        AplicarImagen();
    }

    void AplicarImagen()
    {
        if (imagen == null) return;

        imagen.sprite = imagenDeFondo;
        imagen.color = imagenDeFondo != null ? Color.white : colorDeFondo;
        imagen.type = Image.Type.Simple;
        // Sin sprite, preserveAspect dejaria el rectangulo vacio en vez de pintarlo del color.
        imagen.preserveAspect = false;

        // Se vuelve a estirar a los 4 bordes antes de encuadrar: si antes habia una foto con
        // desborde y ahora no hay ninguna, los offsets viejos dejarian el color corrido.
        RectTransform rt = imagen.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        if (imagenDeFondo != null && mantenerProporcion) EncuadrarCubriendo();
    }

    // "Cover": el rectangulo se desborda lo justo para tapar la pantalla manteniendo la proporcion
    // de la foto, y lo que sobra se sale por los costados. preserveAspect hace lo contrario
    // (deja franjas vacias), que en un fondo se ve mal.
    //
    // Se queda estirado a los 4 bordes y el desborde se mete en offsetMin/offsetMax (negativos de
    // un lado, positivos del otro) en vez de pasar a un tamano fijo: asi la Image sigue atada al
    // Canvas y un cambio de resolucion solo recalcula el desborde (lo hace Update).
    void EncuadrarCubriendo()
    {
        Vector2 sprite = imagenDeFondo.rect.size;
        if (sprite.x <= 0f || sprite.y <= 0f || Screen.height <= 0) return;

        RectTransform rt = imagen.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);

        float proporcionPantalla = (float)Screen.width / Screen.height;
        float proporcionSprite = sprite.x / sprite.y;

        Vector2 tamanoCanvas = ((RectTransform)rt.parent).rect.size;

        if (proporcionSprite > proporcionPantalla)
        {
            // Foto mas ancha que la pantalla: se ajusta al alto y sobra a los costados.
            float desborde = (tamanoCanvas.y * proporcionSprite - tamanoCanvas.x) * 0.5f;
            rt.offsetMin = new Vector2(-desborde, 0f);
            rt.offsetMax = new Vector2(desborde, 0f);
        }
        else
        {
            // Foto mas alta: se ajusta al ancho y sobra arriba y abajo.
            float desborde = (tamanoCanvas.x / proporcionSprite - tamanoCanvas.y) * 0.5f;
            rt.offsetMin = new Vector2(0f, -desborde);
            rt.offsetMax = new Vector2(0f, desborde);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Solo en juego: en modo edicion el Canvas todavia no existe.
        if (Application.isPlaying) AplicarImagen();
    }
#endif
}
