using System;
using System.Collections.Generic;

public enum EstadoBarrera { Abierta, Aviso, Cerrando, Cerrada, Abriendo }

// Libre: se llega desde la salida cruzando solo barreras que dejan pasar. Aislado: queda cortado.
public enum EstadoSector { Libre, Aislado }

// Mapa logico de sectores: el laberinto partido por los huecos que llevan barrera. Guarda el estado
// de cada barrera y de cada sector en tiempo real, y es quien decide si un cierre es seguro (los
// puntos obligatorios, como spawn, comerciante, salida y mas adelante las llaves, tienen que seguir
// siendo alcanzables desde el jugador). Sin dependencias de Unity: se prueba sin escena.
public sealed class MapaSectores
{
    readonly GrafoLaberinto grafo;
    readonly int celdaSalida;
    readonly Dictionary<int, EstadoBarrera> estados = new Dictionary<int, EstadoBarrera>();
    readonly HashSet<int> bloqueantes = new HashSet<int>();
    readonly List<int> huecosBarrera = new List<int>();
    readonly bool[] visitado;

    int[] sectorDeCelda;
    EstadoSector[] estadoSector;

    public event Action AlCambiar;

    public GrafoLaberinto Grafo => grafo;
    public int CeldaSalida => celdaSalida;
    public IReadOnlyList<int> HuecosConBarrera => huecosBarrera;
    public int CantidadSectores => estadoSector.Length;

    public MapaSectores(GrafoLaberinto grafo, IEnumerable<int> huecosConBarrera, int celdaSalida)
    {
        this.grafo = grafo ?? throw new ArgumentNullException(nameof(grafo));
        if (celdaSalida < 0 || celdaSalida >= grafo.Celdas) throw new ArgumentOutOfRangeException(nameof(celdaSalida));
        this.celdaSalida = celdaSalida;
        visitado = new bool[grafo.Celdas];

        foreach (int hueco in huecosConBarrera ?? throw new ArgumentNullException(nameof(huecosConBarrera)))
        {
            if (!grafo.EsHuecoAbierto(hueco)) throw new ArgumentException("El hueco " + hueco + " no es un paso abierto entre dos celdas.");
            if (estados.ContainsKey(hueco)) throw new ArgumentException("El hueco " + hueco + " esta repetido.");

            estados[hueco] = EstadoBarrera.Abierta;
            huecosBarrera.Add(hueco);
        }

        ParticionarEnSectores();
        RecalcularSectores();
    }

    // Aviso, Cerrando y Cerrada ya cuentan como pared a la hora de validar rutas: reservar el hueco
    // desde el aviso evita que dos barreras decidan por separado y entre las dos corten el camino.
    public static bool Bloquea(EstadoBarrera estado)
    {
        return estado == EstadoBarrera.Aviso || estado == EstadoBarrera.Cerrando || estado == EstadoBarrera.Cerrada;
    }

    public bool TieneBarrera(int idHueco) => estados.ContainsKey(idHueco);

    public EstadoBarrera EstadoDeBarrera(int idHueco)
    {
        if (!estados.TryGetValue(idHueco, out EstadoBarrera estado)) throw new ArgumentException("No hay barrera en el hueco " + idHueco + ".");
        return estado;
    }

    public int SectorDeCelda(int celda) => sectorDeCelda[celda];
    public EstadoSector EstadoDeSector(int sector) => estadoSector[sector];
    public EstadoSector EstadoDeCelda(int celda) => estadoSector[sectorDeCelda[celda]];

    public void CambiarEstado(int idHueco, EstadoBarrera nuevo)
    {
        EstadoBarrera anterior = EstadoDeBarrera(idHueco);
        if (anterior == nuevo) return;

        estados[idHueco] = nuevo;
        if (Bloquea(nuevo)) bloqueantes.Add(idHueco);
        else bloqueantes.Remove(idHueco);

        RecalcularSectores();
        AlCambiar?.Invoke();
    }

    // Desde su celda, el jugador llega a todos los puntos obligatorios con las barreras tal como estan ahora.
    public bool RutaGarantizada(int celdaJugador, IReadOnlyList<int> obligatorios)
    {
        return grafo.TodosAlcanzables(celdaJugador, obligatorios, bloqueantes);
    }

    // Cerrar esta barrera (ademas de las ya cerradas o avisadas) sigue dejando todos los obligatorios a mano.
    public bool PuedeCerrar(int idHueco, int celdaJugador, IReadOnlyList<int> obligatorios)
    {
        if (!estados.TryGetValue(idHueco, out EstadoBarrera estado) || estado != EstadoBarrera.Abierta) return false;

        bloqueantes.Add(idHueco);
        try
        {
            return grafo.TodosAlcanzables(celdaJugador, obligatorios, bloqueantes);
        }
        finally
        {
            bloqueantes.Remove(idHueco);
        }
    }

    // Los sectores no cambian mientras no cambie la geometria: son las regiones que quedan al cortar
    // todos los huecos con barrera, esten abiertos o cerrados.
    void ParticionarEnSectores()
    {
        var todas = new HashSet<int>(huecosBarrera);
        sectorDeCelda = new int[grafo.Celdas];
        for (int i = 0; i < sectorDeCelda.Length; i++) sectorDeCelda[i] = -1;

        int cantidad = 0;
        for (int celda = 0; celda < grafo.Celdas; celda++)
        {
            if (sectorDeCelda[celda] != -1) continue;

            grafo.CalcularAlcanzables(celda, todas, visitado);
            for (int i = 0; i < visitado.Length; i++)
            {
                if (visitado[i]) sectorDeCelda[i] = cantidad;
            }
            cantidad++;
        }

        estadoSector = new EstadoSector[cantidad];
    }

    void RecalcularSectores()
    {
        grafo.CalcularAlcanzables(celdaSalida, bloqueantes, visitado);

        for (int i = 0; i < estadoSector.Length; i++) estadoSector[i] = EstadoSector.Aislado;
        for (int celda = 0; celda < visitado.Length; celda++)
        {
            if (visitado[celda]) estadoSector[sectorDeCelda[celda]] = EstadoSector.Libre;
        }
    }
}
