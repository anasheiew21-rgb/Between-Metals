using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Autotest de editor de las pistas ambientales (RF12, HU-09): la señal (T09-F #29) y su
// activador (T09-T #30). Casos CP-SENAL-01..09.
// No abre ni guarda escenas y no crea assets en disco: todo vive en memoria y se destruye
// al terminar cada caso. La transición se avanza con SenalAmbiental.Avanzar(deltaTiempo),
// el mismo gancho que usa Update(), así que no depende del reloj del juego ni de Play Mode.
// Batch: Unity.exe -batchmode -nographics -projectPath <ruta> -executeMethod SenalAmbientalSelfTest.RunAllAndExit -logFile <log>
public static class SenalAmbientalSelfTest
{
    const string Tag = "[SenalAmbientalSelfTest]";

    static int passed;
    static int failed;

    [MenuItem("Between Metals/Tests/Senales Ambientales")]
    static void RunFromMenu()
    {
        RunAll();
    }

    public static void RunAllAndExit()
    {
        bool ok = false;
        try
        {
            ok = RunAll();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }

        if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
    }

    static bool RunAll()
    {
        passed = 0;
        failed = 0;

        // --- #29: la señal ---

        Run("CP-SENAL-01", "Recién creada está apagada: Activada false, Light deshabilitada y sin intensidad", f =>
        {
            SenalAmbiental senal = f.NewSenal();
            if (senal.Activada) return "Activada es true antes de encender";
            if (senal.Estable) return "Estable es true antes de encender";
            if (senal.Luz == null) return "no resolvió la Light del objeto";
            if (senal.Luz.enabled) return "la Light arranca habilitada (debería no costar nada hasta activarse)";
            if (senal.Luz.intensity != 0f) return $"intensity = {senal.Luz.intensity}";
            if (senal.Luz.shadows != LightShadows.None) return $"shadows = {senal.Luz.shadows}";
            return null;
        });

        Run("CP-SENAL-02", "Encender() activa la señal y suelta la Light, sin esperar a que termine la rampa", f =>
        {
            SenalAmbiental senal = f.NewSenal();
            senal.Encender();
            if (!senal.Activada) return "Activada sigue en false";
            if (senal.Estable) return "Estable es true ya en el primer frame: la rampa no ocurrió";
            return null;
        });

        Run("CP-SENAL-03", "La activación es permanente: tras estabilizarse sigue activa y la intensidad no se mueve", f =>
        {
            SenalAmbiental senal = f.NewSenal(intensidadFinal: 2.2f);
            senal.Encender();
            f.AvanzarHastaEstable(senal);

            if (!senal.Activada) return "Activada se apagó sola";
            if (!senal.Estable) return "no llegó a Estable";

            float intensidad = senal.Luz.intensity;
            for (int i = 0; i < 20; i++) senal.Avanzar(0.25f);

            if (!senal.Activada) return "Activada se apagó después de seguir avanzando";
            if (!senal.Luz.enabled) return "la Light se deshabilitó sola";
            if (!Mathf.Approximately(senal.Luz.intensity, intensidad))
                return $"la intensidad cambió: {intensidad} -> {senal.Luz.intensity} (no debería pulsar)";
            return null;
        });

        Run("CP-SENAL-04", "Encender() de nuevo no reinicia la secuencia ni vuelve a bajar la luz", f =>
        {
            SenalAmbiental senal = f.NewSenal(intensidadFinal: 2.2f);
            senal.Encender();
            f.AvanzarHastaEstable(senal);
            float intensidad = senal.Luz.intensity;

            senal.Encender();

            if (!senal.Estable) return "volvió a quedar en transición: reinició la rampa";
            if (!Mathf.Approximately(senal.Luz.intensity, intensidad))
                return $"la intensidad cambió: {intensidad} -> {senal.Luz.intensity}";

            senal.Avanzar(0.1f);
            if (!Mathf.Approximately(senal.Luz.intensity, intensidad))
                return $"tras avanzar un frame la intensidad cambió a {senal.Luz.intensity}";
            return null;
        });

        Run("CP-SENAL-05", "Avanzar(dt): el sonido va primero, la luz entra tras el retraso y la rampa termina en intensidadFinal", f =>
        {
            // fraccionInestable = 0 para medir la rampa sin el titileo, que es deliberadamente
            // irregular y se apaga solo (CP-SENAL-06 lo cubre aparte).
            SenalAmbiental senal = f.NewSenal(intensidadFinal: 3f, retrasoLuz: 0.5f, duracionSubida: 2f, fraccionInestable: 0f);
            senal.Encender();

            // Tramo 1: sólo se escuchó el mecanismo, la luz todavía no entra.
            senal.Avanzar(0.4f);
            if (senal.Luz.enabled) return "la Light se habilitó antes de que pasara el retraso";
            if (senal.Luz.intensity != 0f) return $"intensity = {senal.Luz.intensity} durante el retraso";

            // Tramo 2: sube. A mitad de la rampa tiene que estar encendida pero sin llegar al final.
            senal.Avanzar(0.6f);
            if (!senal.Luz.enabled) return "la Light sigue deshabilitada pasado el retraso";
            float media = senal.Luz.intensity;
            if (media <= 0f || media >= 3f) return $"a mitad de la rampa intensity = {media} (esperaba entre 0 y 3)";
            if (senal.Estable) return "quedó Estable a mitad de la rampa";

            // Tramo 3: termina y se queda fija en intensidadFinal.
            f.AvanzarHastaEstable(senal);
            if (!senal.Estable) return "no llegó a Estable";
            if (!Mathf.Approximately(senal.Luz.intensity, 3f))
                return $"intensity final = {senal.Luz.intensity}, esperaba 3 (intensidadFinal)";
            return null;
        });

        Run("CP-SENAL-06", "Con titileo, la rampa igual se asienta en intensidadFinal y nunca se va de rango", f =>
        {
            SenalAmbiental senal = f.NewSenal(intensidadFinal: 2.2f, retrasoLuz: 0.5f, duracionSubida: 2f, fraccionInestable: 0.35f);
            senal.Encender();

            float maxima = 0f;
            for (int i = 0; i < 200 && !senal.Estable; i++)
            {
                senal.Avanzar(0.02f);
                if (senal.Luz.intensity < 0f) return $"intensity negativa: {senal.Luz.intensity}";
                maxima = Mathf.Max(maxima, senal.Luz.intensity);
            }

            if (!senal.Estable) return "no se estabilizó en 4 s simulados";
            if (!Mathf.Approximately(senal.Luz.intensity, 2.2f))
                return $"intensity final = {senal.Luz.intensity}, esperaba 2.2";
            if (maxima > 2.2f + 0.001f)
                return $"el titileo se pasó de intensidadFinal: máxima {maxima}";
            return null;
        });

        // --- #30: el activador ---

        Run("CP-SENAL-07", "Con eventosRequeridos = 3 la secuencia 0->1->2->3 enciende sólo en la tercera", f =>
        {
            SenalAmbiental senal = f.NewSenal();
            ActivadorSenalAmbiental activador = f.NewActivador(3, senal);

            if (activador.EventosRequeridos != 3) return $"EventosRequeridos = {activador.EventosRequeridos}";

            // 0 eventos
            if (activador.Recibidas != 0) return $"Recibidas = {activador.Recibidas} sin notificar";
            if (activador.YaActivado || senal.Activada) return "activó con 0 notificaciones";

            // 1 evento
            activador.Notificar();
            if (activador.Recibidas != 1) return $"tras 1 notificación Recibidas = {activador.Recibidas}";
            if (activador.YaActivado || senal.Activada) return "activó con 1 notificación";

            // 2 eventos
            activador.Notificar();
            if (activador.Recibidas != 2) return $"tras 2 notificaciones Recibidas = {activador.Recibidas}";
            if (activador.YaActivado || senal.Activada) return "activó con 2 notificaciones";

            // 3 eventos: acá sí
            activador.Notificar();
            if (!activador.YaActivado) return "con 3 notificaciones YaActivado sigue en false";
            if (!senal.Activada) return "con 3 notificaciones la señal sigue apagada";
            return null;
        });

        Run("CP-SENAL-08", "Una notificación extra no vuelve a disparar la señal ni reinicia su rampa", f =>
        {
            SenalAmbiental senal = f.NewSenal(intensidadFinal: 2.2f);
            ActivadorSenalAmbiental activador = f.NewActivador(3, senal);

            activador.Notificar();
            activador.Notificar();
            activador.Notificar();
            f.AvanzarHastaEstable(senal);

            float intensidad = senal.Luz.intensity;
            int recibidas = activador.Recibidas;

            activador.Notificar(); // la cuarta

            if (activador.Recibidas != recibidas)
                return $"Recibidas siguió subiendo: {recibidas} -> {activador.Recibidas}";
            if (!senal.Estable) return "la cuarta notificación reinició la rampa de la señal";
            if (!Mathf.Approximately(senal.Luz.intensity, intensidad))
                return $"la intensidad cambió: {intensidad} -> {senal.Luz.intensity}";
            return null;
        });

        Run("CP-SENAL-09", "El umbral se respeta: 1 enciende a la primera, 5 recién a la quinta, y 0 se trata como 1", f =>
        {
            // Umbral 1: disparo único e inmediato.
            SenalAmbiental unaSenal = f.NewSenal();
            ActivadorSenalAmbiental uno = f.NewActivador(1, unaSenal);
            if (uno.EventosRequeridos != 1) return $"con 1, EventosRequeridos = {uno.EventosRequeridos}";
            uno.Notificar();
            if (!unaSenal.Activada) return "con eventosRequeridos = 1 no encendió en la primera notificación";

            // Umbral 5: no se adelanta.
            SenalAmbiental cincoSenal = f.NewSenal();
            ActivadorSenalAmbiental cinco = f.NewActivador(5, cincoSenal);
            if (cinco.EventosRequeridos != 5) return $"con 5, EventosRequeridos = {cinco.EventosRequeridos}";
            for (int i = 0; i < 4; i++) cinco.Notificar();
            if (cincoSenal.Activada) return "con eventosRequeridos = 5 encendió antes de la quinta";
            cinco.Notificar();
            if (!cincoSenal.Activada) return "con eventosRequeridos = 5 no encendió en la quinta";

            // Umbral 0 serializado: la propiedad lo sube a 1 y no se queda colgado.
            SenalAmbiental ceroSenal = f.NewSenal();
            ActivadorSenalAmbiental cero = f.NewActivador(0, ceroSenal);
            if (cero.EventosRequeridos != 1) return $"con 0, EventosRequeridos = {cero.EventosRequeridos} (esperaba 1)";
            cero.Notificar();
            if (!ceroSenal.Activada) return "con eventosRequeridos = 0 no encendió en la primera notificación";
            return null;
        });

        Debug.Log($"{Tag} RESULT: {passed} passed, {failed} failed");
        return failed == 0;
    }

    // Cada caso recibe objetos nuevos y devuelve null si pasa, o el detalle del fallo.
    static void Run(string id, string description, Func<Fixture, string> test)
    {
        Fixture fixture = null;
        string error;
        try
        {
            fixture = new Fixture();
            error = test(fixture);
        }
        catch (Exception e)
        {
            error = $"excepción {e.GetType().Name}: {e.Message}";
        }
        finally
        {
            fixture?.Destroy();
        }

        if (error == null)
        {
            passed++;
            Debug.Log($"{Tag} {id} PASS - {description}");
        }
        else
        {
            failed++;
            Debug.LogError($"{Tag} {id} FAIL - {description} - {error}");
        }
    }

    class Fixture
    {
        readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();

        // Señal con su Light hija, igual que el prefab SenalAmbiental. Los parámetros de la
        // transición se fijan por SerializedObject para que los casos no dependan de los
        // valores por defecto del componente.
        public SenalAmbiental NewSenal(float intensidadFinal = 2.2f, float retrasoLuz = 0.5f,
                                       float duracionSubida = 2f, float fraccionInestable = 0.35f)
        {
            GameObject go = NewGameObject("SelfTest_Senal");

            GameObject hijoLuz = new GameObject("Luz") { hideFlags = HideFlags.HideAndDontSave };
            hijoLuz.transform.SetParent(go.transform);
            Light luz = hijoLuz.AddComponent<Light>();

            SenalAmbiental senal = go.AddComponent<SenalAmbiental>();

            SerializedObject so = new SerializedObject(senal);
            RequireProperty(so, "luz").objectReferenceValue = luz;
            RequireProperty(so, "intensidadFinal").floatValue = intensidadFinal;
            RequireProperty(so, "retrasoLuz").floatValue = retrasoLuz;
            RequireProperty(so, "duracionSubida").floatValue = duracionSubida;
            RequireProperty(so, "fraccionInestable").floatValue = fraccionInestable;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Apagar() es parte de la API pública y está documentado para dejar la señal lista
            // sin dar Play: en modo edición Awake no corre de forma garantizada.
            senal.Apagar();
            return senal;
        }

        public ActivadorSenalAmbiental NewActivador(int eventosRequeridos, params SenalAmbiental[] senales)
        {
            GameObject go = NewGameObject("SelfTest_Activador");
            ActivadorSenalAmbiental activador = go.AddComponent<ActivadorSenalAmbiental>();

            SerializedObject so = new SerializedObject(activador);
            RequireProperty(so, "eventosRequeridos").intValue = eventosRequeridos;

            SerializedProperty array = RequireProperty(so, "senales");
            array.arraySize = senales.Length;
            for (int i = 0; i < senales.Length; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = senales[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return activador;
        }

        // Avanza la transición en pasos chicos hasta que se estabiliza, con un techo para no
        // colgarse si algo deja de converger.
        public void AvanzarHastaEstable(SenalAmbiental senal, float paso = 0.05f, int maximoPasos = 400)
        {
            for (int i = 0; i < maximoPasos && !senal.Estable; i++)
            {
                senal.Avanzar(paso);
            }
        }

        public void Destroy()
        {
            for (int i = created.Count - 1; i >= 0; i--)
            {
                if (created[i] != null) UnityEngine.Object.DestroyImmediate(created[i]);
            }
        }

        GameObject NewGameObject(string goName)
        {
            GameObject go = new GameObject(goName) { hideFlags = HideFlags.HideAndDontSave };
            created.Add(go);
            return go;
        }

        static SerializedProperty RequireProperty(SerializedObject so, string propertyName)
        {
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop == null) throw new InvalidOperationException($"no existe el campo serializado '{propertyName}'");
            return prop;
        }
    }
}
