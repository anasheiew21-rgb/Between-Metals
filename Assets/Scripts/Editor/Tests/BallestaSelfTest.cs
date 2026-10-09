using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Autotest de editor de la ballesta: el ItemData que crea BallestaBuilder, y lo que cambia en el
// equipo del jugador por tener DOS armas en una sola mano. Casos CP-BALL-01..04.
//
// Separado de ProgresionSelfTest por el mismo motivo que BallestaBuilder esta separado de
// ProgresionBuilder: la ballesta se instala con su propio comando, asi que su autotest no tiene que
// fallar en una copia del proyecto donde solo se corrio la progresion.
//
// No abre ni guarda escenas: todo lo que crea vive en memoria con HideAndDontSave y se destruye al
// terminar cada caso. De disco solo LEE el ItemData que crea BallestaBuilder, para comprobarlo.
//
// Lo que este autotest NO cubre, y hay que probar en Play Mode: el vuelo de la flecha y el impacto.
// Flecha avanza en Update() y resuelve el golpe con un Raycast contra la escena de fisica, y fuera de
// Play Mode no corre Update(), no avanza Time.deltaTime y no hay escena de fisica que castear. Lo
// mismo para Ballesta.Disparar(), que necesita la camara del jugador.
//
// Batch: Unity.exe -batchmode -nographics -projectPath <ruta> -executeMethod BallestaSelfTest.RunAllAndExit -logFile <log>
public static class BallestaSelfTest
{
    const string Tag = "[BallestaSelfTest]";
    const string RutaItem = "Assets/Items/Ballesta.asset";

    static int passed;
    static int failed;

    [MenuItem("Between Metals/Tests/Ballesta (dos armas, una mano)")]
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

        // ---------------- El asset que crea el builder ----------------

        Run("CP-BALL-01", "El ItemData de la ballesta existe, con su itemId y sin consumirse al usarlo", f =>
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemData>(RutaItem);

            if (item == null) return $"falta '{RutaItem}'. Corré Between Metals > Items > Instalar la ballesta";
            if (item.itemId != EquipoJugador.IdBallesta) return $"{RutaItem} tiene itemId '{item.itemId}' (se esperaba '{EquipoJugador.IdBallesta}')";
            if (string.IsNullOrWhiteSpace(item.itemName)) return $"{RutaItem} no tiene itemName: en la tienda saldria sin nombre";
            if (item.consumeOnUse) return $"{RutaItem} tiene consumeOnUse activo: usarla desde el inventario la borraria";

            return null;
        });

        // ---------------- Dos armas, una sola mano ----------------

        Run("CP-BALL-02", "La casilla de la ballesta la saca a la mano y la vuelve a guardar", f =>
        {
            f.NewEquipo();
            ItemData ballesta = f.NewItemData(EquipoJugador.IdBallesta, "Ballesta");
            BarraRapida barra = f.NewBarraRapida(null, null, null, ballesta);

            if (f.equipo.HayAlgoEnMano) return "arranco con algo en la mano";

            // Sin tenerla en el inventario, la tecla no hace nada
            if (barra.UsarCasilla(3)) return "la casilla 4 funciono sin tener la ballesta";
            if (f.equipo.HayAlgoEnMano) return "equipo una ballesta que el jugador no tiene";

            f.inventario.AddItem(ballesta);
            if (f.equipo.BallestaEquipada) return "la ballesta se equipo sola al entrar al inventario";

            if (!barra.UsarCasilla(3)) return "la casilla 4 no saco la ballesta teniendola en el inventario";
            if (!f.equipo.BallestaEquipada) return "la ballesta no quedo en la mano";
            if (f.equipo.ArmaInstanciada == null) return "no hay objeto de ballesta en la mano";
            if (f.equipo.Ballesta == null) return "el objeto de la mano no tiene el componente Ballesta (el que dispara)";
            if (!barra.Equipado(3)) return "la casilla 4 no se reporta como equipada";

            // La misma tecla la guarda
            if (!barra.UsarCasilla(3)) return "la casilla 4 no guardo la ballesta";
            if (f.equipo.HayAlgoEnMano) return "quedo algo en la mano despues de guardarla";
            if (!f.inventario.HasItem(ballesta)) return "guardarla la saco del inventario";

            return null;
        });

        Run("CP-BALL-03", "La daga y la ballesta se turnan la mano, y cada casilla marca solo lo suyo", f =>
        {
            PlayerCombat combate = f.NewCombate();
            ItemData arma = f.NewItemData(EquipoJugador.IdArma, "Arma");
            ItemData ballesta = f.NewItemData(EquipoJugador.IdBallesta, "Ballesta");
            // Casilla 1 la daga y casilla 4 la ballesta, igual que las dejan los builders.
            BarraRapida barra = f.NewBarraRapida(arma, null, null, ballesta);

            f.inventario.AddItem(arma);
            f.inventario.AddItem(ballesta);

            // Sacar la daga
            if (!barra.UsarCasilla(0)) return "la casilla 1 no saco la daga";
            if (!f.equipo.ArmaEquipada) return "la daga no quedo en la mano";
            if (f.equipo.BallestaEquipada) return "la ballesta aparecio sola";
            if (!Mathf.Approximately(combate.DanoActual, 75f)) return $"con la daga, DanoActual = {combate.DanoActual} (se esperaba 75)";

            // Sacar la ballesta SIN guardar la daga primero: la tiene que cambiar, no sumar
            if (!barra.UsarCasilla(3)) return "la casilla 4 no saco la ballesta";
            if (!f.equipo.BallestaEquipada) return "la ballesta no quedo en la mano";
            if (f.equipo.ArmaEquipada) return "la daga siguio en la mano con la ballesta equipada";

            // Lo que arregla BarraRapida.Equipado(): antes preguntaba solo "hay algo en la mano", asi
            // que con mas de un arma la casilla de la daga se encendia con la ballesta empunada.
            if (barra.Equipado(0)) return "la casilla de la daga quedo marcada con la ballesta en la mano";
            if (!barra.Equipado(3)) return "la casilla de la ballesta no se reporta como equipada";

            // Con la ballesta en la mano el golpe cuerpo a cuerpo no vale el dano de la daga: el
            // ataque pasa a ser un tiro, que PlayerCombat rutea a Ballesta.
            if (!Mathf.Approximately(combate.DanoActual, 20f)) return $"con la ballesta, DanoActual = {combate.DanoActual} (se esperaba 20)";

            // Y al revés: volver a la daga guarda la ballesta
            if (!barra.UsarCasilla(0)) return "la casilla 1 no volvio a la daga";
            if (!f.equipo.ArmaEquipada) return "la daga no volvio a la mano";
            if (f.equipo.BallestaEquipada) return "la ballesta siguio en la mano";
            if (f.equipo.Ballesta != null) return "sigue habiendo un componente Ballesta con la daga en la mano";

            return null;
        });

        Run("CP-BALL-04", "Venderle la ballesta al comerciante la saca de la mano y no toca la daga", f =>
        {
            f.NewEquipo();
            ItemData arma = f.NewItemData(EquipoJugador.IdArma, "Arma");
            ItemData ballesta = f.NewItemData(EquipoJugador.IdBallesta, "Ballesta");
            BarraRapida barra = f.NewBarraRapida(arma, null, null, ballesta);

            f.inventario.AddItem(arma);
            f.inventario.AddItem(ballesta);

            barra.UsarCasilla(3);
            if (!f.equipo.BallestaEquipada) return "no se pudo equipar la ballesta para empezar";

            // Lo que hace ShopManager al vender: quitar el ItemData del inventario.
            f.inventario.RemoveItem(ballesta);

            if (f.equipo.HayAlgoEnMano) return "la ballesta sigue en la mano despues de venderla";
            if (!f.inventario.HasItem(arma)) return "vender la ballesta se llevo la daga del inventario";

            // Y la casilla de la daga tiene que seguir sirviendo
            if (!barra.UsarCasilla(0)) return "despues de vender la ballesta, la casilla de la daga no anda";
            if (!f.equipo.ArmaEquipada) return "la daga no se pudo equipar despues de vender la ballesta";

            return null;
        });

        Debug.Log($"{Tag} RESULT: {passed} passed, {failed} failed");
        return failed == 0;
    }

    // Cada caso recibe objetos nuevos y devuelve null si pasa, o el detalle del fallo. Mismo molde
    // que ProgresionSelfTest.Run.
    static void Run(string id, string description, Func<Fixture, string> test)
    {
        Fixture fixture = null;
        string error;

        try
        {
            fixture = new Fixture();
            error = test(fixture);
        }
        catch (TargetInvocationException e) when (e.InnerException != null)
        {
            error = $"excepción {e.InnerException.GetType().Name}: {e.InnerException.Message}";
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
            Debug.Log($"{Tag} PASS {id} — {description}");
        }
        else
        {
            failed++;
            Debug.LogError($"{Tag} FAIL {id} — {description}\n  {error}");
        }
    }

    // Jugador minimo en memoria: Inventory + PlayerStats + EquipoJugador + BarraRapida, sin escena.
    class Fixture
    {
        readonly System.Collections.Generic.List<UnityEngine.Object> created = new();

        public readonly Inventory inventario;
        public readonly PlayerStats stats;
        public EquipoJugador equipo;

        public Fixture()
        {
            GameObject jugador = NewGameObject("CP_BALL_Jugador");
            inventario = jugador.AddComponent<Inventory>();
            stats = jugador.AddComponent<PlayerStats>();
        }

        /// <summary>
        /// EquipoJugador con sus referencias pasadas a mano: ResolverAncla() busca la camara con
        /// MouseLook y en un test sin escena no hay ninguna, y FindAnyObjectByType no devuelve
        /// objetos HideAndDontSave.
        /// </summary>
        public EquipoJugador NewEquipo()
        {
            GameObject mano = NewGameObject("CP_BALL_Mano");

            equipo = inventario.gameObject.AddComponent<EquipoJugador>();
            var so = new SerializedObject(equipo);
            so.FindProperty("inventario").objectReferenceValue = inventario;
            so.FindProperty("ancla").objectReferenceValue = mano.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Start() es quien se suscribe a OnItemRemoved, y fuera de Play no corre solo.
            InvocarPrivado(equipo, "Start");

            return equipo;
        }

        /// <summary>El equipo mas un PlayerCombat apuntado a el, para poder leer DanoActual.</summary>
        public PlayerCombat NewCombate()
        {
            NewEquipo();

            PlayerCombat combate = stats.gameObject.AddComponent<PlayerCombat>();
            var so = new SerializedObject(combate);
            so.FindProperty("equipo").objectReferenceValue = equipo;
            so.ApplyModifiedPropertiesWithoutUndo();

            return combate;
        }

        // Las casillas se cargan por el campo serializado, igual que hace BallestaBuilder. Se puede
        // pasar null para dejar una casilla vacia.
        public BarraRapida NewBarraRapida(params ItemData[] items)
        {
            BarraRapida barra = inventario.gameObject.AddComponent<BarraRapida>();

            var so = new SerializedObject(barra);
            SerializedProperty casillas = so.FindProperty("items");
            casillas.arraySize = BarraRapida.Casillas;
            for (int i = 0; i < BarraRapida.Casillas; i++)
            {
                casillas.GetArrayElementAtIndex(i).objectReferenceValue = i < items.Length ? items[i] : null;
            }

            so.FindProperty("inventario").objectReferenceValue = inventario;
            so.FindProperty("equipo").objectReferenceValue = equipo;
            so.ApplyModifiedPropertiesWithoutUndo();

            return barra;
        }

        // ScriptableObject suelto, sin tocar disco: el asset de verdad se revisa en CP-BALL-01.
        public ItemData NewItemData(string id, string nombre)
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            item.hideFlags = HideFlags.HideAndDontSave;
            item.itemId = id;
            item.itemName = nombre;
            item.consumeOnUse = false;
            created.Add(item);
            return item;
        }

        public void Destroy()
        {
            // El arma que quede en la mano cuelga del ancla, que si esta en la lista: se va con ella.
            for (int i = created.Count - 1; i >= 0; i--)
            {
                if (created[i] != null) UnityEngine.Object.DestroyImmediate(created[i]);
            }
        }

        GameObject NewGameObject(string nombre)
        {
            var go = new GameObject(nombre) { hideFlags = HideFlags.HideAndDontSave };
            created.Add(go);
            return go;
        }

        static void InvocarPrivado(object objetivo, string metodo)
        {
            objetivo.GetType()
                .GetMethod(metodo, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                ?.Invoke(objetivo, null);
        }
    }
}
