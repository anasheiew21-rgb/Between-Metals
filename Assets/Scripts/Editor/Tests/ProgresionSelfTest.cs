using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Autotest de editor de la progresion: llaves y puertas, muro secreto + boton, botin de los
// enemigos, equipamiento del arma y barra rapida. Casos CP-PROG-01..18.
//
// No abre ni guarda escenas: todo lo que crea vive en memoria con HideAndDontSave y se destruye al
// terminar cada caso. De disco solo LEE los ItemData que crea ProgresionBuilder, y solo para
// comprobarlos (nunca los escribe).
//
// Limitacion conocida: fuera de Play Mode no corre Update() ni avanza Time.deltaTime, asi que las
// aperturas (puerta girando, muro hundiendose) se verifican hasta el cambio de estado -que es
// donde esta la logica- y no hasta el final de la animacion.
//
// Batch: Unity.exe -batchmode -nographics -projectPath <ruta> -executeMethod ProgresionSelfTest.RunAllAndExit -logFile <log>
public static class ProgresionSelfTest
{
    const string Tag = "[ProgresionSelfTest]";
    const string CarpetaItems = "Assets/Items";

    static int passed;
    static int failed;

    [MenuItem("Between Metals/Tests/Progresion (llaves, puertas, botin)")]
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

        // ---------------- Puertas ----------------

        Run("CP-PROG-01", "Una puerta sin llave requerida se abre con solo interactuar", f =>
        {
            PuertaInteractuable puerta = f.NewPuerta(null);

            if (puerta.Abriendo) return "la puerta arranco abriendose";
            if (puerta.TextoPrompt != "Presiona E para abrir") return $"TextoPrompt = '{puerta.TextoPrompt}'";

            puerta.Interactuar();

            return puerta.Abriendo ? null : "la puerta no empezo a abrirse";
        });

        Run("CP-PROG-02", "Sin la llave en el inventario, la puerta no se abre y el cartel dice que falta", f =>
        {
            ItemData llave = f.NewItemData("llave_test", "Llave de prueba");
            PuertaInteractuable puerta = f.NewPuerta(llave);

            if (!puerta.TextoPrompt.Contains("Llave de prueba")) return $"TextoPrompt = '{puerta.TextoPrompt}'";
            if (puerta.TextoPrompt.Contains("Presiona E")) return $"el cartel invita a abrir sin tener la llave: '{puerta.TextoPrompt}'";

            puerta.Interactuar();

            return puerta.Abriendo ? "la puerta se abrio sin la llave" : null;
        });

        Run("CP-PROG-03", "Con la llave en el inventario, la puerta se abre y consume la llave", f =>
        {
            ItemData llave = f.NewItemData("llave_test", "Llave de prueba");
            PuertaInteractuable puerta = f.NewPuerta(llave);
            f.inventario.AddItem(llave);

            if (!puerta.TextoPrompt.Contains("Presiona E")) return $"con la llave encima, TextoPrompt = '{puerta.TextoPrompt}'";

            puerta.Interactuar();

            if (!puerta.Abriendo) return "la puerta no se abrio teniendo la llave";
            if (f.inventario.HasItem(llave)) return "la llave sigue en el inventario";
            if (f.inventario.Count != 0) return $"quedaron {f.inventario.Count} items en el inventario (se esperaba 0)";

            return null;
        });

        Run("CP-PROG-04", "Con consumirLlave apagado, la puerta se abre y la llave se conserva", f =>
        {
            ItemData llave = f.NewItemData("llave_test", "Llave de prueba");
            PuertaInteractuable puerta = f.NewPuerta(llave, consumirLlave: false);
            f.inventario.AddItem(llave);

            puerta.Interactuar();

            if (!puerta.Abriendo) return "la puerta no se abrio";
            return f.inventario.HasItem(llave) ? null : "la llave se consumio con consumirLlave en false";
        });

        Run("CP-PROG-05", "Interactuar varias veces sobre la misma puerta dispara AlAbrirse una sola vez", f =>
        {
            ItemData llave = f.NewItemData("llave_test", "Llave de prueba");
            PuertaInteractuable puerta = f.NewPuerta(llave);
            f.inventario.AddItem(llave);
            f.inventario.AddItem(llave); // dos copias: no tiene que gastar las dos

            int avisos = 0;
            puerta.AlAbrirse += () => avisos++;

            puerta.Interactuar();
            puerta.Interactuar();
            puerta.Interactuar();

            if (avisos != 1) return $"AlAbrirse se disparo {avisos} veces (se esperaba 1)";
            if (f.inventario.CountOf(llave) != 1) return $"quedaron {f.inventario.CountOf(llave)} llaves (se esperaba 1: gasta una sola)";
            if (!string.IsNullOrEmpty(puerta.TextoPrompt)) return $"TextoPrompt sigue mostrando '{puerta.TextoPrompt}' con la puerta abriendose";

            return null;
        });

        // ---------------- Muro secreto y boton ----------------

        Run("CP-PROG-06", "El muro secreto arranca cerrado y solido, y Abrir() lo empieza a hundir", f =>
        {
            MuroSecreto muro = f.NewMuro();

            if (muro.Abierto) return "el muro arranco abierto";
            if (!f.CuerpoDelMuroEsSolido(muro)) return "el muro cerrado no tiene su Collider activo: los enemigos lo atravesarian";

            int avisos = 0;
            muro.AlAbrirse += () => avisos++;

            muro.Abrir();
            muro.Abrir();

            if (avisos != 1) return $"AlAbrirse se disparo {avisos} veces (se esperaba 1)";
            if (f.CuerpoDelMuroEsSolido(muro)) return "el muro sigue solido mientras se hunde: podria arrastrar al jugador";

            return null;
        });

        Run("CP-PROG-07", "El boton abre el muro que tiene asignado, y de un solo uso no repite el evento", f =>
        {
            MuroSecreto muro = f.NewMuro();
            BotonSecreto boton = f.NewBoton(muro);

            if (boton.Pulsado) return "el boton arranco pulsado";

            int avisos = 0;
            boton.AlPulsar += () => avisos++;

            boton.Interactuar();

            if (!boton.Pulsado) return "el boton no quedo marcado como pulsado";
            if (!muro.Abriendo) return "el muro no empezo a abrirse";

            boton.Interactuar();
            boton.Interactuar();

            return avisos == 1 ? null : $"AlPulsar se disparo {avisos} veces con unSoloUso activo (se esperaba 1)";
        });

        Run("CP-PROG-08", "El cartel del boton cambia despues de pulsarlo", f =>
        {
            BotonSecreto boton = f.NewBoton(f.NewMuro());

            string antes = boton.TextoPrompt;
            boton.Interactuar();
            string despues = boton.TextoPrompt;

            if (antes == despues) return $"el cartel no cambio: sigue en '{despues}'";
            return despues.Contains("Presiona E") ? $"el cartel pulsado sigue invitando a pulsar: '{despues}'" : null;
        });

        // ---------------- Botin de los enemigos ----------------

        Run("CP-PROG-09", "Al morir, el enemigo deja una moneda con el oro configurado", f =>
        {
            BotinEnemigo botin = f.NewEnemigoConBotin(20);

            if (botin.YaSolto) return "el botin se solto antes de que el enemigo muriera";

            EnemyHealth salud = botin.GetComponent<EnemyHealth>();
            salud.TakeDamage(salud.MaxHealth);

            if (!botin.YaSolto) return "el enemigo murio y no solto nada";

            MonedaPickup moneda = botin.MonedaSoltada;
            if (moneda == null) return "MonedaSoltada quedo en null";

            // Se adopta antes de los chequeos: asi un fallo no deja la moneda suelta en la escena.
            f.Adoptar(moneda.gameObject);

            if (moneda.Valor != 20) return $"la moneda vale {moneda.Valor} (se esperaba 20)";
            if (moneda.GetComponentInChildren<Collider>(true) == null) return "la moneda no tiene Collider: el raycast no la detectaria";

            return null;
        });

        Run("CP-PROG-10", "Recoger la moneda del enemigo suma su oro al jugador", f =>
        {
            BotinEnemigo botin = f.NewEnemigoConBotin(20);
            EnemyHealth salud = botin.GetComponent<EnemyHealth>();
            salud.TakeDamage(salud.MaxHealth);

            MonedaPickup moneda = botin.MonedaSoltada;
            if (moneda == null) return "el enemigo no solto moneda";
            f.Adoptar(moneda.gameObject);

            f.AsignarStatsA(moneda);
            moneda.Interactuar();

            return f.stats.Oro == 20 ? null : $"Oro = {f.stats.Oro} (se esperaba 20)";
        });

        Run("CP-PROG-11", "Mas dano despues de muerto no vuelve a soltar botin", f =>
        {
            BotinEnemigo botin = f.NewEnemigoConBotin(20);
            EnemyHealth salud = botin.GetComponent<EnemyHealth>();

            salud.TakeDamage(salud.MaxHealth);
            GameObject primera = botin.MonedaSoltada != null ? botin.MonedaSoltada.gameObject : null;
            f.Adoptar(primera);

            salud.TakeDamage(salud.MaxHealth);
            botin.Soltar(); // tampoco a mano

            GameObject ultima = botin.MonedaSoltada != null ? botin.MonedaSoltada.gameObject : null;
            return ultima == primera ? null : "solto una segunda moneda";
        });

        // ---------------- Combate y arma ----------------

        Run("CP-PROG-12", "La vida por defecto del enemigo aguanta 10 golpes a mano limpia y 3 con arma", f =>
        {
            EnemyHealth salud = f.NewEnemyHealth();
            if (!Mathf.Approximately(salud.MaxHealth, 200f)) return $"MaxHealth = {salud.MaxHealth} (se esperaba 200)";

            // 9 golpes de 20 no alcanzan; el decimo si.
            for (int i = 0; i < 9; i++) salud.TakeDamage(20f);
            if (!salud.EstaVivo) return "murio con 9 golpes de 20";
            salud.TakeDamage(20f);
            if (salud.EstaVivo) return "sobrevivio a 10 golpes de 20";

            EnemyHealth conArma = f.NewEnemyHealth();
            conArma.TakeDamage(75f);
            conArma.TakeDamage(75f);
            if (!conArma.EstaVivo) return "murio con 2 golpes de 75";
            conArma.TakeDamage(75f);

            return conArma.EstaVivo ? "sobrevivio a 3 golpes de 75" : null;
        });

        Run("CP-PROG-13", "Comprar el arma la guarda SIN equiparla: el dano sigue siendo 20", f =>
        {
            PlayerCombat combate = f.NewCombate();

            if (f.equipo.ArmaEquipada) return "arranco con el arma equipada";
            if (!Mathf.Approximately(combate.DanoActual, 20f)) return $"sin arma, DanoActual = {combate.DanoActual} (se esperaba 20)";

            ItemData arma = f.NewItemData(EquipoJugador.IdArma, "Arma");
            f.inventario.AddItem(arma);

            if (f.equipo.ArmaEquipada) return "el arma se equipo sola al entrar al inventario";
            if (!f.inventario.HasItem(arma)) return "el arma no quedo guardada en el inventario";

            return Mathf.Approximately(combate.DanoActual, 20f)
                ? null
                : $"con el arma guardada (no en la mano), DanoActual = {combate.DanoActual} (se esperaba 20)";
        });

        Run("CP-PROG-14", "La casilla 1 saca el arma a la mano, la vuelve a guardar, y el dano acompana", f =>
        {
            PlayerCombat combate = f.NewCombate();
            ItemData arma = f.NewItemData(EquipoJugador.IdArma, "Arma");
            BarraRapida barra = f.NewBarraRapida(arma);
            f.inventario.AddItem(arma);

            // Sacarla
            if (!barra.UsarCasilla(0)) return "la casilla 1 no hizo nada teniendo el arma en el inventario";
            if (!f.equipo.ArmaEquipada) return "la casilla 1 no puso el arma en la mano";
            if (f.equipo.ArmaInstanciada == null) return "no hay objeto de arma en la mano";
            if (!Mathf.Approximately(combate.DanoActual, 75f)) return $"con el arma en la mano, DanoActual = {combate.DanoActual} (se esperaba 75)";
            if (!barra.Equipado(0)) return "la casilla 1 no se reporta como equipada";

            // Guardarla de vuelta con la misma tecla
            if (!barra.UsarCasilla(0)) return "la casilla 1 no guardo el arma";
            if (f.equipo.ArmaEquipada) return "el arma sigue en la mano";
            if (!f.inventario.HasItem(arma)) return "guardar el arma la saco del inventario";
            if (!Mathf.Approximately(combate.DanoActual, 20f)) return $"con el arma guardada, DanoActual = {combate.DanoActual} (se esperaba 20)";

            return null;
        });

        Run("CP-PROG-15", "Venderle el arma al comerciante la saca de la mano", f =>
        {
            PlayerCombat combate = f.NewCombate();
            ItemData arma = f.NewItemData(EquipoJugador.IdArma, "Arma");
            BarraRapida barra = f.NewBarraRapida(arma);

            f.inventario.AddItem(arma);
            barra.UsarCasilla(0);
            if (!f.equipo.ArmaEquipada) return "no se pudo equipar el arma para empezar";

            // Lo que hace ShopManager.Vender: quitar el ItemData del inventario.
            f.inventario.RemoveItem(arma);

            if (f.equipo.ArmaEquipada) return "el arma sigue en la mano despues de venderla";
            return Mathf.Approximately(combate.DanoActual, 20f) ? null : $"DanoActual = {combate.DanoActual} (se esperaba 20)";
        });

        Run("CP-PROG-16", "Una casilla cuyo item el jugador no tiene no hace nada", f =>
        {
            f.NewCombate();
            ItemData arma = f.NewItemData(EquipoJugador.IdArma, "Arma");
            BarraRapida barra = f.NewBarraRapida(arma);

            int fallos = 0;
            barra.AlFallarCasilla += _ => fallos++;

            // Sin el arma en el inventario
            if (barra.UsarCasilla(0)) return "la casilla 1 funciono sin tener el arma";
            if (f.equipo.ArmaEquipada) return "equipo un arma que el jugador no tiene";
            if (barra.Disponible(0)) return "Disponible(0) dio true sin tener el item";

            // Casilla vacia
            if (barra.UsarCasilla(3)) return "una casilla vacia hizo algo";

            // Indices fuera de rango: no tienen que tirar excepcion
            if (barra.UsarCasilla(-1) || barra.UsarCasilla(99)) return "un indice invalido hizo algo";

            return fallos == 4 ? null : $"AlFallarCasilla se disparo {fallos} veces (se esperaba 4)";
        });

        Run("CP-PROG-17", "Una casilla con un consumible lo usa por el inventario y lo gasta", f =>
        {
            f.NewCombate();
            ItemData pocion = f.NewItemData("pocion_vida", "Pocion", consumeOnUse: true);
            BarraRapida barra = f.NewBarraRapida(null, pocion);

            f.inventario.AddItem(pocion);
            f.inventario.AddItem(pocion);

            if (barra.Cantidad(1) != 2) return $"Cantidad(1) = {barra.Cantidad(1)} (se esperaba 2)";

            int usados = 0;
            ItemData ultimoUsado = null;
            f.inventario.OnItemUsed += item => { usados++; ultimoUsado = item; };

            if (!barra.UsarCasilla(1)) return "la casilla 2 no uso la pocion";
            if (usados != 1) return $"OnItemUsed se disparo {usados} veces (se esperaba 1)";
            if (ultimoUsado != pocion) return "OnItemUsed informo otro item";
            if (barra.Cantidad(1) != 1) return $"despues de usar una, Cantidad(1) = {barra.Cantidad(1)} (se esperaba 1)";

            // Una pocion no se empuna: la casilla no tiene que quedar marcada como equipada.
            return barra.Equipado(1) ? "la pocion quedo marcada como equipada" : null;
        });

        // ---------------- Assets que crea el builder ----------------

        Run("CP-PROG-18", "Los ItemData de las llaves y del arma existen con su itemId y no se consumen al usarlos", f =>
        {
            var esperados = new Dictionary<string, string>
            {
                { "Llave_Interior", "llave_interior" },
                { "Llave_Salida", "llave_salida" },
                { "Arma", EquipoJugador.IdArma },
            };

            foreach (KeyValuePair<string, string> par in esperados)
            {
                string ruta = CarpetaItems + "/" + par.Key + ".asset";
                var item = AssetDatabase.LoadAssetAtPath<ItemData>(ruta);

                if (item == null)
                {
                    return $"falta '{ruta}'. Corré Between Metals > Progresion > Instalar progresion completa";
                }

                if (item.itemId != par.Value) return $"{ruta} tiene itemId '{item.itemId}' (se esperaba '{par.Value}')";
                if (string.IsNullOrWhiteSpace(item.itemName)) return $"{ruta} no tiene itemName: el cartel de la puerta saldria vacio";
                if (item.consumeOnUse) return $"{ruta} tiene consumeOnUse activo: usarlo desde el inventario lo borraria";
            }

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
            Debug.Log($"{Tag} {id} PASS - {description}");
        }
        else
        {
            failed++;
            Debug.LogError($"{Tag} {id} FAIL - {description} - {error}");
        }
    }

    // Todo en memoria (HideAndDontSave). AddComponent no dispara Awake() fuera de Play Mode, asi
    // que se invoca por reflexion, igual que hacen GameManagerSelfTest y MonedaSelfTest.
    class Fixture
    {
        readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();

        public Inventory inventario;
        public PlayerStats stats;
        public EquipoJugador equipo;

        public Fixture()
        {
            GameObject jugador = NewGameObject("CP_PROG_Player");
            inventario = jugador.AddComponent<Inventory>();

            GameObject cuerpo = new GameObject("CP_PROG_PlayerController") { hideFlags = HideFlags.HideAndDontSave };
            cuerpo.transform.SetParent(jugador.transform);
            stats = cuerpo.AddComponent<PlayerStats>();
            InvocarPrivado(stats, "Awake");
        }

        // ---- Puertas ----

        public PuertaInteractuable NewPuerta(ItemData llave, bool consumirLlave = true)
        {
            GameObject raiz = NewGameObject("CP_PROG_Puerta");

            GameObject hoja = new GameObject("Hoja") { hideFlags = HideFlags.HideAndDontSave };
            hoja.transform.SetParent(raiz.transform, false);
            hoja.AddComponent<BoxCollider>();

            PuertaInteractuable puerta = raiz.AddComponent<PuertaInteractuable>();

            var so = new SerializedObject(puerta);
            RequireProperty(so, "llaveRequerida").objectReferenceValue = llave;
            RequireProperty(so, "consumirLlave").boolValue = consumirLlave;
            RequireProperty(so, "inventarioJugador").objectReferenceValue = inventario;
            so.ApplyModifiedPropertiesWithoutUndo();

            InvocarPrivado(puerta, "Awake");
            return puerta;
        }

        // ---- Muro secreto ----

        public MuroSecreto NewMuro()
        {
            GameObject raiz = NewGameObject("CP_PROG_Muro");

            GameObject cuerpo = new GameObject("Cuerpo") { hideFlags = HideFlags.HideAndDontSave };
            cuerpo.transform.SetParent(raiz.transform, false);
            cuerpo.AddComponent<BoxCollider>();

            MuroSecreto muro = raiz.AddComponent<MuroSecreto>();
            InvocarPrivado(muro, "Awake");
            return muro;
        }

        public bool CuerpoDelMuroEsSolido(MuroSecreto muro)
        {
            Transform cuerpo = muro.transform.Find("Cuerpo");
            if (cuerpo == null) return false;

            Collider collider = cuerpo.GetComponent<Collider>();
            return collider != null && collider.enabled;
        }

        // ---- Boton ----

        public BotonSecreto NewBoton(MuroSecreto muro)
        {
            GameObject go = NewGameObject("CP_PROG_Boton");
            go.AddComponent<BoxCollider>();

            BotonSecreto boton = go.AddComponent<BotonSecreto>();

            var so = new SerializedObject(boton);
            SerializedProperty muros = RequireProperty(so, "muros");
            muros.arraySize = 1;
            muros.GetArrayElementAtIndex(0).objectReferenceValue = muro;
            so.ApplyModifiedPropertiesWithoutUndo();

            return boton;
        }

        // ---- Enemigos ----

        public EnemyHealth NewEnemyHealth()
        {
            // Sin invocar Awake a proposito: el Awake de EnemyHealth autoagrega EnemyHitFeedback y
            // BotinEnemigo, y este caso mide solo la vida.
            return NewGameObject("CP_PROG_Enemigo").AddComponent<EnemyHealth>();
        }

        public BotinEnemigo NewEnemigoConBotin(int oro)
        {
            GameObject go = NewGameObject("CP_PROG_EnemigoConBotin");
            go.AddComponent<EnemyHealth>();

            BotinEnemigo botin = go.AddComponent<BotinEnemigo>();

            var so = new SerializedObject(botin);
            RequireProperty(so, "oro").intValue = oro;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Awake es el que lo engancha a EnemyHealth.AlMorir.
            InvocarPrivado(botin, "Awake");
            return botin;
        }

        // La moneda que suelta el enemigo no es HideAndDontSave (la crea el juego, no el test):
        // se adopta para que el fixture la limpie al terminar.
        public void Adoptar(GameObject go)
        {
            if (go == null) return;

            go.hideFlags = HideFlags.HideAndDontSave;
            created.Add(go);
        }

        // FindAnyObjectByType no devuelve objetos HideAndDontSave, asi que la moneda recibe el
        // PlayerStats por su campo serializado (mismo truco que MonedaSelfTest).
        public void AsignarStatsA(MonedaPickup moneda)
        {
            var so = new SerializedObject(moneda);
            RequireProperty(so, "stats").objectReferenceValue = stats;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---- Combate y equipo ----

        public PlayerCombat NewCombate()
        {
            // El ancla se pasa a mano: ResolverAncla() busca la camara con MouseLook y en un test
            // sin escena no hay ninguna.
            GameObject mano = NewGameObject("CP_PROG_Mano");

            equipo = inventario.gameObject.AddComponent<EquipoJugador>();
            var soEquipo = new SerializedObject(equipo);
            RequireProperty(soEquipo, "inventario").objectReferenceValue = inventario;
            RequireProperty(soEquipo, "ancla").objectReferenceValue = mano.transform;
            soEquipo.ApplyModifiedPropertiesWithoutUndo();
            InvocarPrivado(equipo, "Start");

            PlayerCombat combate = stats.gameObject.AddComponent<PlayerCombat>();
            var soCombate = new SerializedObject(combate);
            RequireProperty(soCombate, "equipo").objectReferenceValue = equipo;
            soCombate.ApplyModifiedPropertiesWithoutUndo();

            return combate;
        }

        // ---- Barra rapida ----

        // Las casillas se cargan por el campo serializado, igual que hace ProgresionBuilder. Se
        // puede pasar null para dejar una casilla vacia (lo usa CP-PROG-17).
        public BarraRapida NewBarraRapida(params ItemData[] items)
        {
            BarraRapida barra = inventario.gameObject.AddComponent<BarraRapida>();

            var so = new SerializedObject(barra);
            SerializedProperty casillas = RequireProperty(so, "items");
            casillas.arraySize = BarraRapida.Casillas;
            for (int i = 0; i < BarraRapida.Casillas; i++)
            {
                casillas.GetArrayElementAtIndex(i).objectReferenceValue = i < items.Length ? items[i] : null;
            }

            // Las referencias se pasan a mano: FindAnyObjectByType no devuelve objetos HideAndDontSave.
            RequireProperty(so, "inventario").objectReferenceValue = inventario;
            RequireProperty(so, "equipo").objectReferenceValue = equipo;
            so.ApplyModifiedPropertiesWithoutUndo();

            return barra;
        }

        // ---- Items en memoria ----

        // ScriptableObject suelto, sin tocar disco: los assets de verdad se revisan en CP-PROG-18.
        public ItemData NewItemData(string id, string nombre, bool consumeOnUse = false)
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            item.hideFlags = HideFlags.HideAndDontSave;
            item.itemId = id;
            item.itemName = nombre;
            item.consumeOnUse = consumeOnUse;
            created.Add(item);
            return item;
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

        static void InvocarPrivado(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (method == null) throw new InvalidOperationException($"no se encontró {target.GetType().Name}.{methodName} por reflexión");
            method.Invoke(target, null);
        }

        static SerializedProperty RequireProperty(SerializedObject so, string propertyName)
        {
            SerializedProperty property = so.FindProperty(propertyName);
            if (property == null) throw new InvalidOperationException($"{so.targetObject.GetType().Name} no tiene el campo serializado '{propertyName}'");
            return property;
        }
    }
}
