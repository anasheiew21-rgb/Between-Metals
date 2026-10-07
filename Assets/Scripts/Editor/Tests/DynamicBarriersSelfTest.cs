using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Autotest de editor de la logica de barreras dinamicas (GrafoLaberinto, MapaSectores, MapaLayout).
// Casos CP-BAR-01..15. No necesita escena ni crea assets: todo es logica pura en memoria.
// Batch: Unity.exe -batchmode -nographics -projectPath <ruta> -executeMethod DynamicBarriersSelfTest.RunAllAndExit -logFile <log>
public static class DynamicBarriersSelfTest
{
    const string Tag = "[DynamicBarriersSelfTest]";

    // '#' pared, '.' paso. Celdas A..I (3x3) en los tiles (impar, impar); la salida es el tile de abajo de I.
    // Ciclo A-B-E-D. Bolsones: C (por B-C), G (por D-G) y H (por H-I). E-F y F-I llevan a la salida.
    static readonly string[] Plano =
    {
        "#######",
        "#.....#",
        "#.#.###",
        "#.....#",
        "#.###.#",
        "#.#...#",
        "#####.#",
    };

    const int A = 0, B = 1, C = 2, D = 3, E = 4, F = 5, G = 6, H = 7, I = 8;

    static int passed;
    static int failed;

    [MenuItem("Between Metals/Tests/Barreras Dinamicas (logica)")]
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

        Run("CP-BAR-01", "El grafo mide 3x3 celdas y tiene 9 huecos abiertos", () =>
        {
            GrafoLaberinto g = CrearGrafo();
            if (g.Filas != 3 || g.Columnas != 3) return $"{g.Filas}x{g.Columnas} celdas (se esperaba 3x3)";
            int abiertos = 0;
            foreach (int _ in g.HuecosAbiertos()) abiertos++;
            return abiertos == 9 ? null : $"{abiertos} huecos abiertos (se esperaban 9)";
        });

        Run("CP-BAR-02", "Extremos de un hueco y celda de salida", () =>
        {
            GrafoLaberinto g = CrearGrafo();
            int hueco = Hueco(g, B, C);
            if (!g.Extremos(hueco, out int a, out int b) || a != B || b != C) return $"Extremos = ({a},{b}) (se esperaba B,C)";
            int salida = g.BuscarCeldaSalida();
            return salida == I ? null : $"salida = {salida} (se esperaba {I})";
        });

        Run("CP-BAR-03", "Bloquear un puente aisla celdas; bloquear un hueco de ciclo no", () =>
        {
            GrafoLaberinto g = CrearGrafo();
            var visitado = new bool[g.Celdas];

            g.CalcularAlcanzables(A, new HashSet<int> { Hueco(g, E, F) }, visitado);
            if (visitado[I]) return "con E-F bloqueado todavia se llega a I";

            g.CalcularAlcanzables(A, new HashSet<int> { Hueco(g, A, B) }, visitado);
            return visitado[B] ? null : "con A-B bloqueado (hay ciclo) no se llega a B";
        });

        Run("CP-BAR-04", "Veta cerrar lo que corta la ruta a la salida o al comerciante; permite ciclo y bolsones vacios", () =>
        {
            GrafoLaberinto g = CrearGrafo();
            MapaSectores m = CrearMapa(g, Hueco(g,A, B), Hueco(g,B, C), Hueco(g,E, F), Hueco(g,F, I), Hueco(g,D, G), Hueco(g,H, I));
            int[] obligatorios = { A, G, I };

            if (m.PuedeCerrar(Hueco(g, E, F), A, obligatorios)) return "dejo cerrar E-F (corta la salida)";
            if (m.PuedeCerrar(Hueco(g, F, I), A, obligatorios)) return "dejo cerrar F-I (corta la salida)";
            if (m.PuedeCerrar(Hueco(g, D, G), A, obligatorios)) return "dejo cerrar D-G (corta al comerciante)";
            if (!m.PuedeCerrar(Hueco(g, A, B), A, obligatorios)) return "no dejo cerrar A-B (esta en un ciclo)";
            if (!m.PuedeCerrar(Hueco(g, B, C), A, obligatorios)) return "no dejo cerrar B-C (bolson vacio)";
            return m.PuedeCerrar(Hueco(g, H, I), A, obligatorios) ? null : "no dejo cerrar H-I (bolson vacio)";
        });

        Run("CP-BAR-05", "No deja cerrar un bolson con el jugador adentro", () =>
        {
            GrafoLaberinto g = CrearGrafo();
            MapaSectores m = CrearMapa(g, Hueco(g,B, C));
            int[] obligatorios = { A, G, I };

            if (m.PuedeCerrar(Hueco(g, B, C), C, obligatorios)) return "dejo cerrar B-C con el jugador en C";
            return m.PuedeCerrar(Hueco(g, B, C), A, obligatorios) ? null : "no dejo cerrar B-C con el jugador en A";
        });

        Run("CP-BAR-06", "Reservar una barrera (Aviso) impide el segundo corte del mismo ciclo", () =>
        {
            GrafoLaberinto g = CrearGrafo();
            MapaSectores m = CrearMapa(g, Hueco(g,A, B), Hueco(g,D, E));
            int[] obligatorios = { A, G, I };

            if (!m.PuedeCerrar(Hueco(g, A, B), A, obligatorios)) return "no dejo cerrar A-B";
            m.CambiarEstado(Hueco(g, A, B), EstadoBarrera.Aviso);
            if (m.PuedeCerrar(Hueco(g, D, E), A, obligatorios)) return "dejo cerrar D-E con A-B ya reservada: entre las dos cortan a A del resto";

            m.CambiarEstado(Hueco(g, A, B), EstadoBarrera.Abierta);
            return m.PuedeCerrar(Hueco(g, D, E), A, obligatorios) ? null : "no dejo cerrar D-E una vez liberada A-B";
        });

        Run("CP-BAR-07", "Una llave sin recoger dentro del bolsón veta el cierre; recogida, lo permite", () =>
        {
            GrafoLaberinto g = CrearGrafo();
            MapaSectores m = CrearMapa(g, Hueco(g,B, C));

            if (m.PuedeCerrar(Hueco(g, B, C), A, new[] { A, G, I, C })) return "dejo cerrar B-C con una llave sin recoger en C";
            return m.PuedeCerrar(Hueco(g, B, C), A, new[] { A, G, I }) ? null : "no dejo cerrar B-C con la llave ya recogida";
        });

        Run("CP-BAR-08", "RutaGarantizada detecta una barrera reservada que dejo al jugador sin ruta", () =>
        {
            GrafoLaberinto g = CrearGrafo();
            MapaSectores m = CrearMapa(g, Hueco(g,B, C));
            int[] obligatorios = { A, G, I };

            m.CambiarEstado(Hueco(g, B, C), EstadoBarrera.Aviso);
            if (m.RutaGarantizada(C, obligatorios)) return "con el jugador en C y B-C reservada se dio la ruta por garantizada";
            return m.RutaGarantizada(A, obligatorios) ? null : "con el jugador en A la ruta deberia seguir garantizada";
        });

        Run("CP-BAR-09", "Los sectores pasan a Aislado al cerrarse y vuelven a Libre al abrirse", () =>
        {
            GrafoLaberinto g = CrearGrafo();
            MapaSectores m = CrearMapa(g, Hueco(g,A, B), Hueco(g,D, E), Hueco(g,B, C), Hueco(g,H, I));

            if (m.CantidadSectores != 4) return $"{m.CantidadSectores} sectores (se esperaban 4: {{A,D,G}}, {{B,E,F,I}}, {{C}}, {{H}})";
            for (int s = 0; s < m.CantidadSectores; s++)
            {
                if (m.EstadoDeSector(s) != EstadoSector.Libre) return $"el sector {s} no arranca Libre";
            }

            m.CambiarEstado(Hueco(g, B, C), EstadoBarrera.Cerrada);
            if (m.EstadoDeCelda(C) != EstadoSector.Aislado) return "C no quedo Aislado con B-C cerrada";
            if (m.EstadoDeCelda(E) != EstadoSector.Libre) return "E dejo de estar Libre por cerrar B-C";

            m.CambiarEstado(Hueco(g, A, B), EstadoBarrera.Cerrada);
            m.CambiarEstado(Hueco(g, D, E), EstadoBarrera.Cerrada);
            if (m.EstadoDeCelda(G) != EstadoSector.Aislado) return "{A,D,G} no quedo Aislado con A-B y D-E cerradas";

            m.CambiarEstado(Hueco(g, D, E), EstadoBarrera.Abriendo);
            return m.EstadoDeCelda(G) == EstadoSector.Libre ? null : "{A,D,G} no volvio a Libre al empezar a abrirse D-E";
        });

        Run("CP-BAR-10", "AlCambiar se dispara por cada cambio real de estado y no por uno repetido", () =>
        {
            GrafoLaberinto g = CrearGrafo();
            MapaSectores m = CrearMapa(g, Hueco(g,B, C));
            int veces = 0;
            m.AlCambiar += () => veces++;

            m.CambiarEstado(Hueco(g, B, C), EstadoBarrera.Cerrando);
            m.CambiarEstado(Hueco(g, B, C), EstadoBarrera.Cerrando);
            m.CambiarEstado(Hueco(g, B, C), EstadoBarrera.Cerrada);
            return veces == 2 ? null : $"AlCambiar se disparo {veces} veces (se esperaban 2)";
        });

        Run("CP-BAR-11", "El constructor rechaza huecos invalidos, repetidos o una grilla de tamano par", () =>
        {
            GrafoLaberinto g = CrearGrafo();
            if (!Lanza<ArgumentException>(() => CrearMapa(g, g.IdHueco(1, 3)))) return "acepto un tile de celda como hueco";
            if (!Lanza<ArgumentException>(() => CrearMapa(g, g.IdHueco(2, 5)))) return "acepto una pared como hueco";
            if (!Lanza<ArgumentException>(() => CrearMapa(g, Hueco(g,A, B), Hueco(g,A, B)))) return "acepto un hueco repetido";
            return Lanza<ArgumentException>(() => new GrafoLaberinto(new bool[4, 5])) ? null : "acepto una grilla de tamano par";
        });

        Run("CP-BAR-12", "Solo se puede cerrar una barrera que esta Abierta", () =>
        {
            GrafoLaberinto g = CrearGrafo();
            MapaSectores m = CrearMapa(g, Hueco(g,B, C));
            m.CambiarEstado(Hueco(g, B, C), EstadoBarrera.Cerrada);
            return m.PuedeCerrar(Hueco(g, B, C), A, new[] { A }) ? "dejo cerrar una barrera que ya esta Cerrada" : null;
        });

        Run("CP-BAR-13", "MapaLayout: conversiones grilla <-> local y celda mas cercana", () =>
        {
            Vector3 centro = MapaLayout.GrillaALocal(13f, 11f);
            if (!Cerca(centro.x, 0f) || !Cerca(centro.z, 0f)) return $"el centro de la grilla cae en {centro} (se esperaba el origen)";

            Vector3 esquina = MapaLayout.GrillaALocal(1f, 1f);
            if (!Cerca(esquina.x, -60f) || !Cerca(esquina.z, 72f)) return $"la celda (0,0) cae en {esquina} (se esperaba (-60, 0, 72))";

            MapaLayout.LocalAGrilla(esquina, out float fila, out float columna);
            if (!Cerca(fila, 1f) || !Cerca(columna, 1f)) return $"LocalAGrilla devolvio ({fila},{columna}) (se esperaba (1,1))";

            MapaLayout.CeldaMasCercana(MapaLayout.GrillaALocal(2 * 4 + 1, 2 * 7 + 1), out int r, out int c);
            if (r != 4 || c != 7) return $"CeldaMasCercana = ({r},{c}) (se esperaba (4,7))";

            MapaLayout.CeldaMasCercana(new Vector3(1000f, 0f, -1000f), out r, out c);
            return r == MapaLayout.Filas - 1 && c == MapaLayout.Columnas - 1 ? null : $"fuera del mapa dio ({r},{c}) (se esperaba la esquina sureste)";
        });

        Run("CP-BAR-14", "SelectorDeHuecos solo elige huecos que se pueden cerrar sin cortar spawn, salida ni comerciante", () =>
        {
            GrafoLaberinto g = CrearGrafo();
            var opciones = new SelectorDeHuecos.Opciones { cantidad = 9, semilla = 1, separacionMinima = 0, distanciaMinimaSpawn = 0, distanciaMinimaSalida = 0, gradoMaximoDePasillo = 4 };
            List<int> elegidos = SelectorDeHuecos.Elegir(g, A, I, new[] { A, G, I }, opciones, out int candidatos);

            if (candidatos != 6) return $"{candidatos} candidatos (se esperaban 6: el ciclo A-B-E-D y los bolsones B-C y H-I)";
            foreach (int prohibido in new[] { Hueco(g, E, F), Hueco(g, F, I), Hueco(g, D, G) })
            {
                if (elegidos.Contains(prohibido)) return "eligio un hueco que corta la ruta obligatoria";
            }
            return elegidos.Count == 6 ? null : $"eligio {elegidos.Count} huecos (se esperaban los 6 cerrables)";
        });

        Run("CP-BAR-15", "SelectorDeHuecos respeta la cantidad y la separacion minima, y es determinista con la misma semilla", () =>
        {
            GrafoLaberinto g = CrearGrafo();
            var opciones = new SelectorDeHuecos.Opciones { cantidad = 3, semilla = 5, separacionMinima = 2, distanciaMinimaSpawn = 0, distanciaMinimaSalida = 0, gradoMaximoDePasillo = 4 };
            List<int> primera = SelectorDeHuecos.Elegir(g, A, I, new[] { A, G, I }, opciones, out _);
            List<int> segunda = SelectorDeHuecos.Elegir(g, A, I, new[] { A, G, I }, opciones, out _);

            if (primera.Count == 0 || primera.Count > 3) return $"eligio {primera.Count} huecos (se esperaba entre 1 y 3)";
            if (primera.Count != segunda.Count) return "con la misma semilla eligio cantidades distintas";
            for (int i = 0; i < primera.Count; i++)
            {
                if (primera[i] != segunda[i]) return "con la misma semilla eligio huecos distintos";
                for (int j = i + 1; j < primera.Count; j++)
                {
                    g.Extremos(primera[i], out int a, out _);
                    g.Extremos(primera[j], out int b, out _);
                    int distancia = Math.Abs(g.FilaDe(a) - g.FilaDe(b)) + Math.Abs(g.ColumnaDe(a) - g.ColumnaDe(b));
                    if (distancia < 2) return $"dos barreras a distancia {distancia} (minimo 2)";
                }
            }
            return null;
        });

        Debug.Log($"{Tag} RESULT: {passed} passed, {failed} failed");
        return failed == 0;
    }

    static GrafoLaberinto CrearGrafo()
    {
        var pared = new bool[Plano.Length, Plano[0].Length];
        for (int f = 0; f < Plano.Length; f++)
            for (int c = 0; c < Plano[f].Length; c++)
                pared[f, c] = Plano[f][c] == '#';

        return new GrafoLaberinto(pared);
    }

    static MapaSectores CrearMapa(GrafoLaberinto g, params int[] huecos)
    {
        return new MapaSectores(g, huecos, g.BuscarCeldaSalida());
    }

    // Id del hueco entre dos celdas vecinas: es el tile a mitad de camino entre sus centros.
    static int Hueco(GrafoLaberinto g, int celdaA, int celdaB)
    {
        int tf = (2 * g.FilaDe(celdaA) + 1 + 2 * g.FilaDe(celdaB) + 1) / 2;
        int tc = (2 * g.ColumnaDe(celdaA) + 1 + 2 * g.ColumnaDe(celdaB) + 1) / 2;
        return g.IdHueco(tf, tc);
    }

    static bool Lanza<T>(Action accion) where T : Exception
    {
        try
        {
            accion();
            return false;
        }
        catch (T)
        {
            return true;
        }
    }

    static bool Cerca(float a, float b) => Mathf.Abs(a - b) < 1e-4f;

    static void Run(string id, string description, Func<string> test)
    {
        string error;
        try
        {
            error = test();
        }
        catch (Exception e)
        {
            error = $"excepción {e.GetType().Name}: {e.Message}";
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
