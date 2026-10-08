using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Coloca las barreras dinamicas sobre huecos del laberinto que ya existe en la escena, sin regenerar
// el mapa. Las pone bajo su propia raiz ("Barreras_Dinamicas"), fuera de "Mapa", para que volver a
// generar el mapa no las borre. Solo elige huecos que alguna vez pueden cerrarse sin dejar al jugador
// sin ruta al spawn, la salida y el comerciante. Menu: Between Metals > Barreras
public static class BarrerasBuilder
{
    const string NombreRaiz = "Barreras_Dinamicas";
    const int Cantidad = 12;
    const int Semilla = 777;
    const int SeparacionMinima = 3;     // celdas (distancia Manhattan) entre dos barreras
    const int DistanciaMinimaSpawn = 3;  // celdas entre un hueco con barrera y el spawn: no se encierra al jugador recien nacido
    const int DistanciaMinimaSalida = 2; // idem con la celda de la salida
    const int GradoMaximoDePasillo = 2; // al menos una de las dos celdas tiene que ser de pasillo, no de sala

    [MenuItem("Between Metals/Barreras/Colocar barreras dinamicas", priority = 200)]
    static void Colocar()
    {
        Transform raiz = EscanerMapa.BuscarRaiz();
        if (raiz == null)
        {
            EditorUtility.DisplayDialog("Colocar barreras", "No hay un objeto '" + EscanerMapa.NombreRaiz + "' en la escena abierta.", "OK");
            return;
        }

        bool[,] paredes = EscanerMapa.LeerParedes(raiz);
        if (paredes == null)
        {
            EditorUtility.DisplayDialog("Colocar barreras", "No se encontraron muros dentro de '" + EscanerMapa.NombreRaiz + "'.", "OK");
            return;
        }

        var grafo = new GrafoLaberinto(paredes);
        int salida = EscanerMapa.CeldaDeSalida(grafo, raiz);
        if (salida < 0)
        {
            EditorUtility.DisplayDialog("Colocar barreras", "No hay 'Punto_Salida' ni una salida abierta en el perimetro del laberinto.", "OK");
            return;
        }

        PlayerStats jugador = Object.FindAnyObjectByType<PlayerStats>();
        int spawn = CeldaDelPunto(grafo, raiz, jugador != null ? jugador.transform : BuscarTransform("Punto_Inicio"));
        Transform puntoComerciante = BuscarTransform("Punto_Comerciante");
        var obligatorios = new List<int> { spawn, salida };
        if (puntoComerciante != null) obligatorios.Add(CeldaDelPunto(grafo, raiz, puntoComerciante));

        GameObject anterior = GameObject.Find(NombreRaiz);
        if (anterior != null && !EditorUtility.DisplayDialog("Colocar barreras",
                "Ya existe '" + NombreRaiz + "' en la escena. Se va a reemplazar.", "Reemplazar", "Cancelar"))
        {
            return;
        }

        var opciones = new SelectorDeHuecos.Opciones
        {
            cantidad = Cantidad,
            semilla = Semilla,
            separacionMinima = SeparacionMinima,
            distanciaMinimaSpawn = DistanciaMinimaSpawn,
            distanciaMinimaSalida = DistanciaMinimaSalida,
            gradoMaximoDePasillo = GradoMaximoDePasillo,
        };
        List<int> elegidos = SelectorDeHuecos.Elegir(grafo, spawn, salida, obligatorios, opciones, out int candidatos);
        Debug.Log("BarrerasBuilder: " + candidatos + " huecos candidatos; se eligen " + elegidos.Count + ".");
        if (elegidos.Count == 0)
        {
            EditorUtility.DisplayDialog("Colocar barreras", "No quedo ningun hueco valido para una barrera.", "OK");
            return;
        }

        Undo.IncrementCurrentGroup();
        int grupoUndo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Colocar barreras dinamicas");

        if (anterior != null) Undo.DestroyObjectImmediate(anterior);

        var contenedor = new GameObject(NombreRaiz);
        Undo.RegisterCreatedObjectUndo(contenedor, "Crear " + NombreRaiz);

        Material material = MaterialDeMuro(raiz);
        for (int i = 0; i < elegidos.Count; i++)
        {
            ConstruirBarrera(grafo, raiz, contenedor.transform, elegidos[i], i + 1, material);
        }

        Undo.CollapseUndoOperations(grupoUndo);
        Selection.activeGameObject = contenedor;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Debug.Log("BarrerasBuilder: " + elegidos.Count + " barreras dinamicas colocadas bajo '" + NombreRaiz + "'. Dale Play: el GestorBarreras se instala solo.");
    }

    [MenuItem("Between Metals/Barreras/Quitar barreras dinamicas", priority = 201)]
    static void Quitar()
    {
        GameObject raiz = GameObject.Find(NombreRaiz);
        if (raiz == null)
        {
            Debug.Log("BarrerasBuilder: no hay ningun objeto '" + NombreRaiz + "' en la escena.");
            return;
        }

        Undo.DestroyObjectImmediate(raiz);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    static void ConstruirBarrera(GrafoLaberinto grafo, Transform raiz, Transform padre, int hueco, int numero, Material material)
    {
        int tf = hueco / grafo.ColumnasGrilla;
        int tc = hueco % grafo.ColumnasGrilla;

        var barrera = new GameObject(string.Format("Barrera_{0:00}_T{1:00}-{2:00}", numero, tf, tc));
        Undo.RegisterCreatedObjectUndo(barrera, "Crear " + barrera.name);
        barrera.transform.SetParent(padre, false);
        barrera.transform.position = raiz.TransformPoint(MapaLayout.GrillaALocal(tf, tc));
        barrera.AddComponent<BarreraDinamica>();

        GameObject cuerpo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Undo.RegisterCreatedObjectUndo(cuerpo, "Crear Cuerpo");
        cuerpo.name = "Cuerpo";
        cuerpo.transform.SetParent(barrera.transform, false);
        cuerpo.transform.localScale = new Vector3(MapaLayout.AnchoCalle, MapaLayout.AltoMuro, MapaLayout.AnchoCalle);
        cuerpo.transform.localPosition = new Vector3(0f, -0.1f - MapaLayout.AltoMuro * 0.5f, 0f);
        if (material != null) cuerpo.GetComponent<Renderer>().sharedMaterial = material;
    }

    // Mismo material que los muros del laberinto, para que la barrera cerrada no se note distinta.
    static Material MaterialDeMuro(Transform raiz)
    {
        Transform grupo = raiz.Find("02_Muros_Laberinto");
        if (grupo == null || grupo.childCount == 0) return null;

        Renderer renderer = grupo.GetChild(0).GetComponent<Renderer>();
        return renderer != null ? renderer.sharedMaterial : null;
    }

    static int CeldaDelPunto(GrafoLaberinto grafo, Transform raiz, Transform punto)
    {
        if (punto == null) return grafo.IdCelda(0, 0);

        MapaLayout.CeldaMasCercana(raiz.InverseTransformPoint(punto.position), out int r, out int c);
        return grafo.IdCelda(r, c);
    }

    static Transform BuscarTransform(string nombre)
    {
        GameObject go = GameObject.Find(nombre);
        return go != null ? go.transform : null;
    }
}
