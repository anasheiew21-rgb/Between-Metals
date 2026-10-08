using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Self-test manual para el audio del juego: BibliotecaDeSonidos (la cadena de fallbacks y la
// convencion de nombres), los grupos del mixer y los ajustes de importacion de los .wav.
// No hay asmdef de test en el proyecto, asi que corre como item de menu, mismo patron que
// AudioPreferencesSelfTest / EnemyAISelfTest.
//
// Lo que se puede probar sin dar Play es justamente la parte fragil de este sistema: que los
// clips esten donde el codigo los busca. Un .wav mal ubicado o un itemId que no coincide con el
// nombre de archivo no rompe nada visible -el juego sigue andando, solo que en silencio-, y ese
// es el tipo de fallo que se descubre tarde. Aca falla fuerte y temprano.
//
// Lo que NO se cubre: la cadencia de PasosJugador y el fade de MusicaAmbiente, que viven en
// Update y necesitan Play.
//
// Batch: Unity.exe -batchmode -nographics -projectPath <ruta> -executeMethod AudioJuegoSelfTest.RunAllAndExit -logFile <log>
public static class AudioJuegoSelfTest
{
    const string Tag = "[AudioJuegoSelfTest]";
    const string CarpetaItems = "Assets/Items";
    const string CarpetaAudio = "Assets/Audio/Resources";

    static int passed;
    static int failed;

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

    [MenuItem("Between Metals/Tests/Audio del juego Self-Test")]
    public static void Run()
    {
        RunAll();
    }

    static bool RunAll()
    {
        passed = 0;
        failed = 0;

        Run("CP-AUDJ-01", "Todos los clips declarados como constantes en BibliotecaDeSonidos existen", () =>
        {
            // Si alguien renombra un .wav sin tocar la constante (o al revés), este es el caso que
            // lo caza: el juego no avisaría, solo se quedaría sin ese sonido.
            var rutas = new[]
            {
                BibliotecaDeSonidos.BotonClick, BibliotecaDeSonidos.BotonHover, BibliotecaDeSonidos.BotonAtras,
                BibliotecaDeSonidos.MusicaSuspenso,
                BibliotecaDeSonidos.LinternaEncender, BibliotecaDeSonidos.LinternaApagar,
                BibliotecaDeSonidos.ItemGenerico, BibliotecaDeSonidos.Moneda,
            };

            var faltantes = new List<string>();
            foreach (string ruta in rutas)
            {
                if (BibliotecaDeSonidos.Clip(ruta) == null) faltantes.Add(ruta);
            }

            return faltantes.Count == 0
                ? null
                : "no se encontraron: " + string.Join(", ", faltantes);
        });

        Run("CP-AUDJ-02", "Una ruta inexistente devuelve null sin lanzar excepcion", () =>
        {
            // El juego tiene que poder correr con un sonido faltante: avisa una vez y sigue.
            AudioClip clip = BibliotecaDeSonidos.Clip("Ruta/Que/No/Existe");
            return clip == null ? null : "devolvio un clip (" + clip.name + ") para una ruta inventada";
        });

        Run("CP-AUDJ-03", "Clip(null) y Clip(\"\") devuelven null sin lanzar excepcion", () =>
        {
            if (BibliotecaDeSonidos.Clip(null) != null) return "Clip(null) devolvio algo";
            if (BibliotecaDeSonidos.Clip("") != null) return "Clip(\"\") devolvio algo";
            return null;
        });

        Run("CP-AUDJ-04", "Hay al menos un paso y ninguno es null", () =>
        {
            AudioClip[] pasos = BibliotecaDeSonidos.Pasos;
            if (pasos.Length == 0) return "no se encontro ningun Player/paso_N.wav";

            for (int i = 0; i < pasos.Length; i++)
            {
                if (pasos[i] == null) return "el paso " + (i + 1) + " es null";
            }
            return null;
        });

        Run("CP-AUDJ-05", "La numeracion de los pasos es contigua (no quedo un hueco)", () =>
        {
            // Pasos corta en el primer hueco, asi que un paso_1/paso_2/paso_4 dejaria paso_4 sin
            // usar en silencio. Se cuentan los .wav de la carpeta y se comparan con los cargados.
            int enDisco = AssetDatabase.FindAssets("paso_ t:AudioClip", new[] { CarpetaAudio + "/Player" }).Length;
            int cargados = BibliotecaDeSonidos.Pasos.Length;

            return enDisco == cargados
                ? null
                : "hay " + enDisco + " paso(s) en disco pero la biblioteca carga " + cargados +
                  "; revisa que la numeracion arranque en 1 y no se saltee ninguno";
        });

        Run("CP-AUDJ-06", "SonidoDeItem resuelve el sonido propio de un itemId conocido", () =>
        {
            AudioClip clip = BibliotecaDeSonidos.SonidoDeItem("llave_salida");
            if (clip == null) return "devolvio null";

            return clip.name == "pickup_llave_salida"
                ? null
                : "devolvio '" + clip.name + "' en vez de 'pickup_llave_salida'";
        });

        Run("CP-AUDJ-07", "SonidoDeItem ignora mayusculas y espacios en el itemId", () =>
        {
            // ItemData.OnValidate solo avisa cuando itemId esta vacio, asi que un id con otra
            // capitalizacion pasa el filtro; mismo criterio que EfectosDeItem.CuracionDe.
            AudioClip clip = BibliotecaDeSonidos.SonidoDeItem("  LLAVE_Salida ");
            if (clip == null) return "devolvio null";

            return clip.name == "pickup_llave_salida"
                ? null
                : "devolvio '" + clip.name + "' en vez de 'pickup_llave_salida'";
        });

        Run("CP-AUDJ-08", "SonidoDeItem cae al generico con un itemId sin sonido propio", () =>
        {
            AudioClip clip = BibliotecaDeSonidos.SonidoDeItem("item_que_no_tiene_sonido");
            if (clip == null) return "devolvio null: siempre tendria que haber sonido";

            return clip.name == "pickup_generico"
                ? null
                : "devolvio '" + clip.name + "' en vez del generico";
        });

        Run("CP-AUDJ-09", "SonidoDeItem cae al generico con un itemId vacio o null", () =>
        {
            AudioClip conNull = BibliotecaDeSonidos.SonidoDeItem(null);
            AudioClip conVacio = BibliotecaDeSonidos.SonidoDeItem("   ");

            if (conNull == null || conNull.name != "pickup_generico") return "SonidoDeItem(null) no dio el generico";
            if (conVacio == null || conVacio.name != "pickup_generico") return "SonidoDeItem(\"   \") no dio el generico";
            return null;
        });

        Run("CP-AUDJ-10", "Todos los ItemData de Assets/Items tienen un sonido propio (no el generico)", () =>
        {
            // El pedido era que CADA item suene distinto. Este caso es el que avisa cuando se
            // agrega un item nuevo y nadie le dejo su .wav: no es un error del codigo, es trabajo
            // de contenido que falta, y sin este aviso pasaria desapercibido.
            string[] guids = AssetDatabase.FindAssets("t:ItemData", new[] { CarpetaItems });
            if (guids.Length == 0) return "no se encontro ningun ItemData en " + CarpetaItems;

            var sinSonidoPropio = new List<string>();
            foreach (string guid in guids)
            {
                var item = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(guid));
                if (item == null) continue;

                // El campo del Inspector gana: si lo tiene asignado a mano, ya tiene sonido propio.
                if (item.sonido != null) continue;

                AudioClip porConvencion = BibliotecaDeSonidos.SonidoDeItem(item.itemId);
                if (porConvencion == null || porConvencion.name == "pickup_generico")
                {
                    sinSonidoPropio.Add(item.name + " (itemId '" + item.itemId + "')");
                }
            }

            return sinSonidoPropio.Count == 0
                ? null
                : "falta Items/pickup_<itemId>.wav para: " + string.Join(", ", sinSonidoPropio);
        });

        Run("CP-AUDJ-11", "El mixer tiene los grupos sfx y music", () =>
        {
            // Sin estos grupos el audio igual suena, pero sale directo al Master y los sliders del
            // menu no lo afectan: un fallo silencioso que solo se nota moviendo el slider.
            if (AudioPreferences.SfxGroup == null) return "no se encontro el grupo 'sfx' en MainMixer";
            if (AudioPreferences.MusicGroup == null) return "no se encontro el grupo 'music' en MainMixer";
            return null;
        });

        Run("CP-AUDJ-12", "La musica se importa como Streaming y los efectos como DecompressOnLoad", () =>
        {
            // Verifica que los .meta escritos a mano realmente quedaron aplicados tras la
            // importacion. Si la musica se importara con DecompressOnLoad, 48s de audio se
            // descomprimirian enteros en RAM al cargar la escena, justo lo que el proyecto quiere
            // evitar en una PC de gama baja.
            string error = RevisarImportacion(CarpetaAudio + "/Music/suspenso_loop.wav", AudioClipLoadType.Streaming);
            if (error != null) return error;

            return RevisarImportacion(CarpetaAudio + "/UI/boton_click.wav", AudioClipLoadType.DecompressOnLoad);
        });

        Run("CP-AUDJ-13", "El loop de musica dura lo que espera el diseño del loop (48s)", () =>
        {
            AudioClip musica = BibliotecaDeSonidos.Clip(BibliotecaDeSonidos.MusicaSuspenso);
            if (musica == null) return "no se encontro el clip de musica";

            // El largo no es cosmetico: todas las frecuencias y LFOs del generador son multiplos
            // de 1/48 Hz y el latido cae 15 veces justas. Si el clip dejara de medir 48s, el loop
            // se escucharia con un salto en la costura.
            return Mathf.Abs(musica.length - 48f) < 0.1f
                ? null
                : "mide " + musica.length.ToString("0.00") + "s en vez de 48s";
        });

        Run("CP-AUDJ-14", "Ningun .wav del juego se importa con normalize activado", () =>
        {
            // El balance entre sonidos ya esta hecho en el generador (el hover se genera mucho mas
            // tenue que el click a proposito). Si Unity normalizara al importar, los igualaria a
            // todos y romperia ese balance.
            // AudioImporter no expone 'normalize' en su API publica (solo vive en el .meta), asi
            // que se lee el .meta como texto. Es exactamente el archivo que se versiona, con lo
            // cual el caso prueba lo que se commitea y no un estado en memoria.
            var conNormalize = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { CarpetaAudio }))
            {
                string ruta = AssetDatabase.GUIDToAssetPath(guid) + ".meta";
                if (!System.IO.File.Exists(ruta)) continue;

                foreach (string linea in System.IO.File.ReadAllLines(ruta))
                {
                    if (linea.Trim() != "normalize: 1") continue;

                    conNormalize.Add(System.IO.Path.GetFileNameWithoutExtension(ruta));
                    break;
                }
            }

            return conNormalize.Count == 0
                ? null
                : "tienen normalize activado: " + string.Join(", ", conNormalize);
        });

        Debug.Log($"{Tag} RESULT: {passed} passed, {failed} failed");
        return failed == 0;
    }

    static string RevisarImportacion(string ruta, AudioClipLoadType esperado)
    {
        var importer = AssetImporter.GetAtPath(ruta) as AudioImporter;
        if (importer == null) return "no se encontro el AudioImporter de " + ruta;

        AudioClipLoadType actual = importer.defaultSampleSettings.loadType;
        return actual == esperado
            ? null
            : ruta + " se importa como " + actual + " y se esperaba " + esperado;
    }

    static void Run(string id, string description, Func<string> test)
    {
        string error;
        try
        {
            error = test();
        }
        catch (Exception e)
        {
            error = $"excepcion {e.GetType().Name}: {e.Message}";
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
}
