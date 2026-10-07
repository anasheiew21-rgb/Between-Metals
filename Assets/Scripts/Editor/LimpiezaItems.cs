using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Encuentra y limpia lo que quedo apuntando a un ItemData que se borro del proyecto. Borrar el
// asset no alcanza: Unity deja la referencia en null y eso se traduce en
//
//   - un ItemPickup en el mapa que no se puede recoger (su cartel sale vacio y al apretar 'E'
//     avisa por consola), y
//   - una fila fantasma en el panel del comerciante (ShopManager la saltea, asi que no se ve en
//     juego, pero queda ahi en el Inspector confundiendo a quien lo lea).
//
// Menu: Between Metals > Items > Limpiar referencias a items borrados
//
// Es una herramienta generica y no un parche para un item puntual: la proxima vez que se borre un
// ItemData, este comando ya sabe que hacer. Pide confirmacion con la lista de lo que va a tocar,
// porque borrar objetos de la escena no es algo que convenga hacer de callado.
public static class LimpiezaItems
{
    [MenuItem("Between Metals/Items/Limpiar referencias a items borrados", priority = 800)]
    static void Limpiar()
    {
        var pickupsHuerfanos = new List<ItemPickup>();
        foreach (ItemPickup pickup in Object.FindObjectsByType<ItemPickup>(FindObjectsInactive.Include))
        {
            if (ItemDe(pickup) == null) pickupsHuerfanos.Add(pickup);
        }

        var filasPorTienda = new Dictionary<ShopManager, List<int>>();
        int filasTotales = 0;
        foreach (ShopManager tienda in Object.FindObjectsByType<ShopManager>(FindObjectsInactive.Include))
        {
            List<int> vacias = FilasVacias(tienda);
            if (vacias.Count == 0) continue;

            filasPorTienda[tienda] = vacias;
            filasTotales += vacias.Count;
        }

        var casillasPorBarra = new Dictionary<BarraRapida, List<int>>();
        foreach (BarraRapida barra in Object.FindObjectsByType<BarraRapida>(FindObjectsInactive.Include))
        {
            // Una casilla vacia es un estado legitimo de la barra rapida (la UI la dibuja en gris),
            // asi que no se toca: solo se informa, para que no sorprenda.
            List<int> vacias = CasillasVacias(barra);
            if (vacias.Count > 0) casillasPorBarra[barra] = vacias;
        }

        if (pickupsHuerfanos.Count == 0 && filasTotales == 0)
        {
            Debug.Log("LimpiezaItems: no hay nada que limpiar en la escena abierta." +
                (casillasPorBarra.Count > 0 ? " (Hay casillas vacias en la barra rapida, que es un estado valido.)" : ""));
            return;
        }

        var detalle = new List<string>();
        foreach (ItemPickup pickup in pickupsHuerfanos) detalle.Add("  - Se BORRA el objeto '" + pickup.name + "' (ItemPickup sin item)");
        foreach (KeyValuePair<ShopManager, List<int>> par in filasPorTienda)
        {
            detalle.Add("  - Se quitan " + par.Value.Count + " fila(s) vacia(s) de la tienda '" + par.Key.name + "'");
        }

        string mensaje = "Se encontro lo siguiente:\n\n" + string.Join("\n", detalle) + "\n\nSe puede deshacer con Ctrl+Z.";
        if (!EditorUtility.DisplayDialog("Limpiar referencias a items borrados", mensaje, "Limpiar", "Cancelar")) return;

        int grupoUndo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Limpiar referencias a items borrados");

        foreach (ItemPickup pickup in pickupsHuerfanos)
        {
            Undo.DestroyObjectImmediate(pickup.gameObject);
        }

        foreach (KeyValuePair<ShopManager, List<int>> par in filasPorTienda)
        {
            QuitarFilas(par.Key, par.Value);
        }

        Undo.CollapseUndoOperations(grupoUndo);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        var resumen = new List<string>
        {
            "LimpiezaItems: listo.",
            "  Objetos borrados: " + pickupsHuerfanos.Count,
            "  Filas de tienda quitadas: " + filasTotales,
        };

        foreach (KeyValuePair<BarraRapida, List<int>> par in casillasPorBarra)
        {
            resumen.Add("  Barra rapida '" + par.Key.name + "': casillas sin item (" +
                string.Join(", ", par.Value.ConvertAll(i => (i + 1).ToString())) + "). " +
                "Es un estado valido: asignales un ItemData desde el Inspector si querés usarlas.");
        }

        resumen.Add("  Guardá la escena (Ctrl+S).");
        Debug.Log(string.Join("\n", resumen));
    }

    static ItemData ItemDe(ItemPickup pickup)
    {
        var so = new SerializedObject(pickup);
        SerializedProperty propiedad = so.FindProperty("item");
        return propiedad != null ? propiedad.objectReferenceValue as ItemData : null;
    }

    static List<int> FilasVacias(ShopManager tienda)
    {
        var vacias = new List<int>();
        var so = new SerializedObject(tienda);
        SerializedProperty items = so.FindProperty("items");
        if (items == null) return vacias;

        for (int i = 0; i < items.arraySize; i++)
        {
            if (items.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue == null) vacias.Add(i);
        }
        return vacias;
    }

    // De atras para adelante: borrar del array de adelante correria los indices que faltan tocar.
    static void QuitarFilas(ShopManager tienda, List<int> indices)
    {
        var so = new SerializedObject(tienda);
        SerializedProperty items = so.FindProperty("items");

        for (int i = indices.Count - 1; i >= 0; i--)
        {
            items.DeleteArrayElementAtIndex(indices[i]);
        }

        so.ApplyModifiedProperties();
    }

    static List<int> CasillasVacias(BarraRapida barra)
    {
        var vacias = new List<int>();
        for (int i = 0; i < BarraRapida.Casillas; i++)
        {
            if (barra.ItemDe(i) == null) vacias.Add(i);
        }
        return vacias;
    }
}
