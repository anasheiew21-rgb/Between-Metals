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
    /// <summary>
    /// Como se acomoda la foto cuando su proporcion no coincide con la de la pantalla.
    ///   Cubrir   - se agranda hasta llenarla y lo que sobra se sale por los bordes (el "cover"
    ///              de CSS). Es el default: para una foto ambiente es lo que mejor queda.
    ///   Contener - entra entera y quedan franjas a los costados o arriba y abajo. Es lo que hace
    ///              falta cuando la imagen es un afiche con contenido pegado a los bordes (logo,
    ///              sellos, tira de iconos): recortarla se comeria justo eso.
    ///   Estirar  - se deforma hasta llenar la pantalla. Solo para fondos abstractos.
    /// </summary>
    public enum Encuadre { Cubrir, Contener, Estirar }

    [Header("Imagen")]
    [Tooltip("Foto de fondo del menu. Importala como Sprite (2D and UI). Vacio = solo el color de abajo")]
    [SerializeField] private Sprite imagenDeFondo;

    [Tooltip("Color que tapa la pantalla cuando no hay imagen (y que tinta la imagen cuando si hay)")]
    [SerializeField] private Color colorDeFondo = new Color(0.04f, 0.05f, 0.07f, 1f);

    [Header("Encuadre")]
    [Tooltip("Cubrir: recorta para llenar la pantalla. Contener: entra entera y deja franjas. Estirar: la deforma")]
    [SerializeField] private Encuadre modoEncuadre = Encuadre.Cubrir;

    [Tooltip("Orden de dibujado del Canvas. Negativo para quedar detras de cualquier otra UI")]
    [SerializeField] private int ordenCanvas = -100;

    Image imagen;
    Image fondoColor;
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

        // Capa de color a pantalla completa, debajo de la foto. Con Encuadre.Contener la foto no
        // llega a los bordes, y sin esto las franjas mostrarian lo que limpie la camara. Asi el
        // color de las franjas lo decide este componente y no la escena donde se use.
        GameObject fondoGO = new GameObject("Color", typeof(RectTransform));
        fondoGO.transform.SetParent(canvasGO.transform, false);

        RectTransform rtFondo = fondoGO.GetComponent<RectTransform>();
        rtFondo.anchorMin = Vector2.zero;
        rtFondo.anchorMax = Vector2.one;
        rtFondo.offsetMin = Vector2.zero;
        rtFondo.offsetMax = Vector2.zero;

        fondoColor = fondoGO.AddComponent<Image>();
        fondoColor.color = colorDeFondo;
        fondoColor.raycastTarget = false;

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

        if (fondoColor != null) fondoColor.color = colorDeFondo;

        // Sin foto, la capa de color de abajo ya pinta la pantalla entera: esta se apaga para no
        // dibujar dos veces lo mismo.
        imagen.enabled = imagenDeFondo != null;

        imagen.sprite = imagenDeFondo;
        imagen.color = Color.white;
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

        if (imagenDeFondo != null && modoEncuadre != Encuadre.Estirar) Encuadrar();
    }

    // Ajusta la Image a la proporcion de la foto. Se queda estirada a los 4 bordes y la diferencia
    // se mete en offsetMin/offsetMax en vez de pasar a un tamano fijo: asi sigue atada al Canvas y
    // un cambio de resolucion solo recalcula los offsets (lo hace Update).
    //
    // Cubrir desborda (offsets negativos de un lado y positivos del otro, la foto se sale de la
    // pantalla); Contener encoge (al reves, quedan franjas del colorDeFondo).
    void Encuadrar()
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

        // Con Cubrir, una foto mas ancha que la pantalla se ajusta al alto y desborda a los
        // costados; con Contener es al reves, una foto mas angosta se ajusta al alto y deja
        // franjas a los costados. La misma comparacion sirve para los dos, invertida.
        //
        // La cuenta tambien es la misma: da positiva cuando hay que desbordar y negativa cuando
        // hay que encoger, y los offsets salen bien en los dos casos sin un if extra.
        bool ajustaPorAlto = modoEncuadre == Encuadre.Cubrir
            ? proporcionSprite > proporcionPantalla
            : proporcionSprite < proporcionPantalla;

        if (ajustaPorAlto)
        {
            float diferencia = (tamanoCanvas.y * proporcionSprite - tamanoCanvas.x) * 0.5f;
            rt.offsetMin = new Vector2(-diferencia, 0f);
            rt.offsetMax = new Vector2(diferencia, 0f);
        }
        else
        {
            float diferencia = (tamanoCanvas.x / proporcionSprite - tamanoCanvas.y) * 0.5f;
            rt.offsetMin = new Vector2(0f, -diferencia);
            rt.offsetMax = new Vector2(0f, diferencia);
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
