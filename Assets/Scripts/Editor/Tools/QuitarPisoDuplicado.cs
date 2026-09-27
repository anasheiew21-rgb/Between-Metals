using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Herramienta de un solo uso (issue #60): borra el "Suelo_Plano_Principal (1)" duplicado que dejo
// el commit 0f3f1c5 en Prototype.unity. Se deja commiteada para que quede trazabilidad de como se
// hizo el cambio, pero no esta pensada para correr de nuevo (una vez borrado el duplicado, ya no
// hay nada que hacer). Pensada para -batchmode -executeMethod desde la linea de comandos.
public static class QuitarPisoDuplicado
{
    const string RutaEscena = "Assets/Scenes/Prototype.unity";
    const string NombreOriginal = "Suelo_Plano_Principal";
    const string NombreDuplicado = "Suelo_Plano_Principal (1)";
    const float Tolerancia = 0.001f;

    public static void Ejecutar()
    {
        Scene escena;
        try
        {
            escena = EditorSceneManager.OpenScene(RutaEscena, OpenSceneMode.Single);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("QuitarPisoDuplicado: no se pudo abrir " + RutaEscena + ": " + ex.Message);
            Salir(1);
            return;
        }

        GameObject original = BuscarUnico(escena, NombreOriginal, out int cantidadOriginal);
        GameObject duplicado = BuscarUnico(escena, NombreDuplicado, out int cantidadDuplicado);

        if (cantidadOriginal != 1)
        {
            Debug.LogError(string.Format(
                "QuitarPisoDuplicado: se esperaba exactamente 1 objeto \"{0}\" y se encontraron {1}.",
                NombreOriginal, cantidadOriginal));
            Salir(1);
            return;
        }

        if (cantidadDuplicado != 1)
        {
            Debug.LogError(string.Format(
                "QuitarPisoDuplicado: se esperaba exactamente 1 objeto \"{0}\" y se encontraron {1}.",
                NombreDuplicado, cantidadDuplicado));
            Salir(1);
            return;
        }

        string motivo;
        if (!SonEquivalentes(original, duplicado, out motivo))
        {
            Debug.LogError("QuitarPisoDuplicado: " + motivo + " No se modifica ni se guarda la escena.");
            Salir(1);
            return;
        }

        Debug.Log("QuitarPisoDuplicado: verificaciones OK (mismo padre, transform y material dentro de la tolerancia). Borrando \"" + NombreDuplicado + "\".");

        Object.DestroyImmediate(duplicado);

        bool guardo = EditorSceneManager.SaveScene(escena);
        if (!guardo)
        {
            Debug.LogError("QuitarPisoDuplicado: EditorSceneManager.SaveScene devolvio false.");
            Salir(1);
            return;
        }

        Debug.Log("QuitarPisoDuplicado: RESULT OK — \"" + NombreDuplicado + "\" borrado y escena guardada.");
        Salir(0);
    }

    // Busca por nombre exacto entre TODOS los GameObject de la escena (incluidos inactivos), para
    // no depender de GameObject.Find (que ignora inactivos y no informa si hay mas de una coincidencia).
    static GameObject BuscarUnico(Scene escena, string nombre, out int cantidadEncontrada)
    {
        GameObject encontrado = null;
        int cantidad = 0;

        foreach (GameObject raiz in escena.GetRootGameObjects())
        {
            foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
            {
                if (t.gameObject.name == nombre)
                {
                    cantidad++;
                    if (encontrado == null) encontrado = t.gameObject;
                }
            }
        }

        cantidadEncontrada = cantidad;
        return encontrado;
    }

    static bool SonEquivalentes(GameObject original, GameObject duplicado, out string motivo)
    {
        if (original.transform.parent != duplicado.transform.parent)
        {
            motivo = "Los objetos no tienen el mismo padre.";
            return false;
        }

        if (!Cerca(original.transform.localPosition, duplicado.transform.localPosition))
        {
            motivo = string.Format("Las posiciones locales difieren mas de {0}: {1} vs {2}.",
                Tolerancia, original.transform.localPosition, duplicado.transform.localPosition);
            return false;
        }

        if (Quaternion.Angle(original.transform.localRotation, duplicado.transform.localRotation) > Tolerancia)
        {
            motivo = string.Format("Las rotaciones locales difieren mas de {0} grados: {1} vs {2}.",
                Tolerancia, original.transform.localRotation.eulerAngles, duplicado.transform.localRotation.eulerAngles);
            return false;
        }

        if (!Cerca(original.transform.localScale, duplicado.transform.localScale))
        {
            motivo = string.Format("Las escalas locales difieren mas de {0}: {1} vs {2}.",
                Tolerancia, original.transform.localScale, duplicado.transform.localScale);
            return false;
        }

        Renderer rendererOriginal = original.GetComponent<Renderer>();
        Renderer rendererDuplicado = duplicado.GetComponent<Renderer>();
        if (rendererOriginal == null || rendererDuplicado == null)
        {
            motivo = "Alguno de los dos objetos no tiene Renderer.";
            return false;
        }

        Material[] materialesOriginal = rendererOriginal.sharedMaterials;
        Material[] materialesDuplicado = rendererDuplicado.sharedMaterials;
        if (materialesOriginal.Length != materialesDuplicado.Length)
        {
            motivo = "La cantidad de materiales no coincide.";
            return false;
        }
        for (int i = 0; i < materialesOriginal.Length; i++)
        {
            if (materialesOriginal[i] != materialesDuplicado[i])
            {
                motivo = "El material en el indice " + i + " no coincide.";
                return false;
            }
        }

        motivo = null;
        return true;
    }

    static bool Cerca(Vector3 a, Vector3 b)
    {
        return Mathf.Abs(a.x - b.x) <= Tolerancia
            && Mathf.Abs(a.y - b.y) <= Tolerancia
            && Mathf.Abs(a.z - b.z) <= Tolerancia;
    }

    static void Salir(int codigo)
    {
        EditorApplication.Exit(codigo);
    }
}
