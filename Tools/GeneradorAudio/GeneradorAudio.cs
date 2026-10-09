// Generador de los .wav de Between Metals: sintetiza TODOS los sonidos del juego (menu, musica,
// pasos, linterna, items) por codigo, sin assets externos ni librerias de terceros.
//
// Por que por codigo y no clips descargados: el repo no puede traer audio con licencia ajena, y
// asi cada sonido queda documentado, versionado y regenerable byte a byte (el ruido sale de un
// System.Random con semilla fija por clip, no de Random.Shared). Son placeholders de produccion:
// suenan y encajan con el tono del juego, pero estan pensados para que se puedan reemplazar por
// grabaciones reales dejando el MISMO nombre de archivo, sin tocar una linea de C# del juego
// (BibliotecaDeSonidos resuelve todo por nombre, ver Docs/audio-y-modelo-enemigos.md).
//
// No es un script de Unity a proposito: es .NET puro y se corre desde Tools/GeneradorAudio/generar.ps1
// con el Editor abierto o cerrado. Unity despues importa los .wav como cualquier otro asset.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace BetweenMetals.Tools
{
    public static class GeneradorAudio
    {
        // 44.1kHz para los efectos (los transitorios de un click se oyen); 22.05kHz alcanza y
        // sobra para la musica, que es todo graves y pads, y parte al medio el peso del archivo
        // (el juego apunta a PCs de gama baja).
        const int SrEfectos = 44100;
        const int SrMusica = 22050;

        // Largo del loop de musica. 48s con todas las frecuencias y LFOs en multiplos de 1/48 Hz
        // para que el ciclo cierre solo; el crossfade de abajo tapa lo unico que no es periodico
        // (la cama de ruido).
        const double DuracionMusica = 48.0;
        const double CrossfadeMusica = 2.0;

        public static void Generar(string carpetaDestino)
        {
            var escritos = new List<string>();

            escritos.AddRange(GenerarUI(Path.Combine(carpetaDestino, "UI")));
            escritos.AddRange(GenerarMusica(Path.Combine(carpetaDestino, "Music")));
            escritos.AddRange(GenerarPasos(Path.Combine(carpetaDestino, "Player")));
            escritos.AddRange(GenerarLinterna(Path.Combine(carpetaDestino, "Flashlight")));
            escritos.AddRange(GenerarItems(Path.Combine(carpetaDestino, "Items")));
            escritos.AddRange(GenerarLaberinto(Path.Combine(carpetaDestino, "Maze")));

            Console.WriteLine();
            Console.WriteLine("GeneradorAudio: " + escritos.Count + " archivo(s) escritos en " + carpetaDestino);
        }

        // ------------------------------------------------------------------ menu / UI

        // Tres sonidos cortos y secos, deliberadamente discretos: el menu se abre y se cierra
        // muchas veces por partida y un "ding" brillante cansa y rompe el clima de terror.
        static List<string> GenerarUI(string carpeta)
        {
            var escritos = new List<string>();

            // Hover: lo mas tenue de todo el juego. Solo marca que el cursor paso por encima.
            float[] hover = Buffer(0.08, SrEfectos);
            Seno(hover, SrEfectos, 0, 523.25, 0.08, Decaimiento(0.022), 0.5);
            Seno(hover, SrEfectos, 0, 1046.5, 0.05, Decaimiento(0.012), 0.12);
            Normalizar(hover, 0.22);
            escritos.Add(Escribir(carpeta, "boton_hover.wav", hover, SrEfectos));

            // Click: transitorio de ruido (el "golpe" del boton) + cuerpo con dos parciales que
            // bajan de tono. Es el sonido que el jugador va a oir mas seguido en todo el juego.
            float[] click = Buffer(0.14, SrEfectos);
            float[] golpe = Ruido(0.012, SrEfectos, 9001);
            Filtrar(golpe, SrEfectos, Biquad.PasaBanda(SrEfectos, 2200, 1.1));
            Envolver(golpe, SrEfectos, Decaimiento(0.005));
            Mezclar(click, golpe, 0.55, 0);
            Barrido(click, SrEfectos, 0.004, 440, 300, 0.13, Decaimiento(0.035), 0.75);
            Seno(click, SrEfectos, 0.004, 880, 0.06, Decaimiento(0.014), 0.18);
            Normalizar(click, 0.62);
            escritos.Add(Escribir(carpeta, "boton_click.wav", click, SrEfectos));

            // Atras / cerrar: el mismo click pero bajando, para que "volver" se distinga de
            // "entrar" sin leer la pantalla.
            float[] atras = Buffer(0.18, SrEfectos);
            float[] golpeAtras = Ruido(0.012, SrEfectos, 9002);
            Filtrar(golpeAtras, SrEfectos, Biquad.PasaBanda(SrEfectos, 1500, 1.1));
            Envolver(golpeAtras, SrEfectos, Decaimiento(0.006));
            Mezclar(atras, golpeAtras, 0.45, 0);
            Barrido(atras, SrEfectos, 0.004, 330, 180, 0.17, Decaimiento(0.05), 0.8);
            Normalizar(atras, 0.58);
            escritos.Add(Escribir(carpeta, "boton_atras.wav", atras, SrEfectos));

            return escritos;
        }

        // ------------------------------------------------------------------ musica

        // Drone de suspenso en La: subgrave constante que da el peso, un pad con el tritono
        // (La-Re#, el intervalo "inestable" de toda la musica de terror) que entra y sale muy
        // lento, un latido grave cada 3.2s y pings metalicos lejanos puestos a mano.
        //
        // No tiene melodia ni ritmo marcado a proposito: la musica tiene que poder sonar 20
        // minutos seguidos mientras el jugador se pierde en el laberinto sin volverse pegadiza
        // ni avisar "aca pasa algo".
        static List<string> GenerarMusica(string carpeta)
        {
            int sr = SrMusica;
            double total = DuracionMusica + CrossfadeMusica;
            float[] mezcla = Buffer(total, sr);

            // 1) Subgrave: dos osciladores apenas desafinados (55 y 55.25 Hz) laten entre si muy
            //    despacio. Es la base que hace que la escena "pese" sin tapar los efectos.
            Drone(mezcla, sr, 55.0, total, 0.50, 1.0 / 16.0, 0.35);
            Drone(mezcla, sr, 55.25, total, 0.38, 1.0 / 24.0, 0.40);
            Drone(mezcla, sr, 82.5, total, 0.22, 1.0 / 32.0, 0.50);

            // 2) Pad con tritono (220 / 311.25 Hz): la disonancia que pone incomodo. Entra y sale
            //    en ciclos largos y desfasados, asi nunca suena igual dos veces en el loop.
            Drone(mezcla, sr, 220.0, total, 0.085, 1.0 / 48.0, 0.85);
            Drone(mezcla, sr, 311.25, total, 0.075, 1.0 / 36.0, 0.90);
            Drone(mezcla, sr, 440.0, total, 0.030, 1.0 / 24.0, 0.95);

            // 3) Cama de ruido filtrada: el "aire" del lugar. Es lo unico no periodico del loop,
            //    y por eso existe el crossfade del final.
            float[] aire = Ruido(total, sr, 4711);
            Filtrar(aire, sr, Biquad.PasaBajos(sr, 420, 0.7));
            Filtrar(aire, sr, Biquad.PasaAltos(sr, 90, 0.7));
            ModularAmplitud(aire, sr, 1.0 / 16.0, 0.45); // 1/16 Hz = 3 ciclos justos en 48s
            Mezclar(mezcla, aire, 0.30, 0);

            // 4) Latido: cada 3.2s (15 golpes exactos en 48s, asi el pulso no se corta en el loop).
            //    Se sintetiza una sola vez y se mezcla 15 veces: es el mismo golpe siempre, como
            //    un corazon, y cuesta una fraccion de lo que costaria regenerarlo por latido.
            float[] latido = Buffer(0.75, sr);
            Barrido(latido, sr, 0.00, 62, 44, 0.40, Decaimiento(0.10), 1.00);
            Barrido(latido, sr, 0.30, 58, 42, 0.35, Decaimiento(0.09), 0.58); // "tum-tum"
            for (double t = 0.0; t < DuracionMusica; t += 3.2)
            {
                Mezclar(mezcla, latido, 0.45, (int)(t * sr));
            }

            // 5) Pings metalicos lejanos: el metal del laberinto crujiendo. Tiempos y notas
            //    elegidos a mano (no random) para que queden desparejos pero siempre iguales, y
            //    todos antes del final del loop para no pisar el crossfade.
            double[,] pings =
            {
                { 5.4, 1174.0 }, { 11.9, 932.0 }, { 17.2, 1396.0 },
                { 23.8, 1046.0 }, { 29.1, 1567.0 }, { 36.6, 880.0 }, { 42.3, 1244.0 },
            };
            for (int i = 0; i < pings.GetLength(0); i++)
            {
                float[] ping = Buffer(2.4, sr);
                Seno(ping, sr, 0, pings[i, 1], 2.4, Decaimiento(0.55), 1.0);
                Seno(ping, sr, 0, pings[i, 1] * 2.76, 1.2, Decaimiento(0.22), 0.35); // parcial inarmonico = metal
                Filtrar(ping, sr, Biquad.PasaBajos(sr, 3500, 0.7));
                Mezclar(mezcla, ping, 0.085, (int)(pings[i, 0] * sr));
            }

            // El pasaaltos va ANTES del crossfade: un biquad arranca con el estado en cero y deja
            // un transitorio en las primeras muestras, y si se filtrara despues ese transitorio
            // caeria justo sobre la costura del loop (medido: un salto de 0.34 entre la ultima
            // muestra y la primera, o sea un click en cada vuelta).
            Filtrar(mezcla, sr, Biquad.PasaAltos(sr, 28, 0.7)); // saca el DC que deja la suma de drones

            // Loop: el ultimo CrossfadeMusica se desvanece sobre el principio, asi el salto del
            // final al inicio no tiene costura audible.
            float[] loop = Crossfade(mezcla, sr, DuracionMusica, CrossfadeMusica);
            Normalizar(loop, 0.70);

            var escritos = new List<string>();
            escritos.Add(Escribir(carpeta, "suspenso_loop.wav", loop, sr));
            return escritos;
        }

        // ------------------------------------------------------------------ pasos

        // 4 variantes del mismo paso sobre metal/piedra. PasosJugador las alterna y les mueve el
        // pitch, asi caminar 10 minutos no suena a un sample repetido.
        //
        // Cada paso es: golpe grave (el peso del cuerpo) + ruido filtrado (la suela raspando).
        static List<string> GenerarPasos(string carpeta)
        {
            var escritos = new List<string>();
            double[] centros = { 1250, 1050, 1500, 900 }; // el "color" de cada variante
            double[] graves = { 72, 66, 78, 62 };

            for (int i = 0; i < 4; i++)
            {
                float[] paso = Buffer(0.26, SrEfectos);

                // Suela: ruido corto con pasabanda. El decaimiento rapido es lo que lo hace
                // sonar a golpe seco y no a soplido.
                float[] suela = Ruido(0.18, SrEfectos, 2100 + i);
                Filtrar(suela, SrEfectos, Biquad.PasaBanda(SrEfectos, centros[i], 0.9));
                Envolver(suela, SrEfectos, Ataque(0.002, 0.055));
                Mezclar(paso, suela, 0.65, 0);

                // Cola de grava, apenas corrida: el eco del pasillo.
                float[] grava = Ruido(0.12, SrEfectos, 2200 + i);
                Filtrar(grava, SrEfectos, Biquad.PasaBanda(SrEfectos, centros[i] * 2.1, 1.4));
                Envolver(grava, SrEfectos, Decaimiento(0.030));
                Mezclar(paso, grava, 0.18, (int)(0.012 * SrEfectos));

                // Peso del cuerpo.
                Barrido(paso, SrEfectos, 0, graves[i], graves[i] * 0.72, 0.14, Decaimiento(0.035), 0.5);

                Normalizar(paso, 0.55);
                escritos.Add(Escribir(carpeta, "paso_" + (i + 1) + ".wav", paso, SrEfectos));
            }

            return escritos;
        }

        // ------------------------------------------------------------------ linterna

        // Dos clicks de interruptor mecanico. Encender es mas agudo y brillante que apagar: el
        // jugador distingue el estado de la linterna sin mirar la pantalla, que es justo lo que
        // hace falta cuando la esta prendiendo porque no ve nada.
        static List<string> GenerarLinterna(string carpeta)
        {
            var escritos = new List<string>();

            float[] on = Buffer(0.12, SrEfectos);
            ClickMecanico(on, SrEfectos, 3300, 1.6, 0.0035, 7301);
            ClickMecanico(on, SrEfectos, 1700, 2.4, 0.0120, 7302, 0.45, 0.004);
            Normalizar(on, 0.70);
            escritos.Add(Escribir(carpeta, "linterna_on.wav", on, SrEfectos));

            float[] off = Buffer(0.12, SrEfectos);
            ClickMecanico(off, SrEfectos, 2100, 1.6, 0.0040, 7303);
            ClickMecanico(off, SrEfectos, 1050, 2.4, 0.0150, 7304, 0.45, 0.005);
            Normalizar(off, 0.62);
            escritos.Add(Escribir(carpeta, "linterna_off.wav", off, SrEfectos));

            return escritos;
        }

        // ------------------------------------------------------------------ laberinto

        // Muros de metal corriendose: lo que se oye cuando el boton secreto abre un muro.
        //
        // Dura 3.2s, un poco mas que los 2.5s de MuroSecreto.duracionApertura, para que el sonido
        // no se corte antes de que el muro termine de hundirse.
        //
        // Se arma en cinco capas, que es como suena de verdad una masa de metal moviendose:
        //   1. El tiron del arranque: el golpe seco de algo pesado que se despega.
        //   2. El roce: ruido pasabanda con la amplitud modulada despacio. Es el "grrrr" continuo,
        //      y la modulacion es lo que lo hace sonar a metal que agarra y suelta en vez de a
        //      ruido blanco con un filtro encima.
        //   3. Las resonancias: tres parciales desafinados entre si. Esto es lo que distingue metal
        //      de piedra; afinados darian un acorde y sonaria a campana.
        //   4. El retumbe grave: la cama de 45 Hz que hace sentir que lo que se movio es grande.
        //   5. El golpe final: el muro llegando al fondo, a los 2.5s.
        static List<string> GenerarLaberinto(string carpeta)
        {
            var escritos = new List<string>();

            const double duracion = 3.2;
            const double finDelRoce = 2.6;   // acompaña al muro y afloja justo antes de que pare
            const double golpeFinal = 2.45;  // un pelo antes de que el muro llegue, no despues

            float[] muro = Buffer(duracion, SrEfectos);

            // 1. Tiron del arranque.
            float[] tiron = Ruido(0.30, SrEfectos, 5101);
            Filtrar(tiron, SrEfectos, Biquad.PasaBanda(SrEfectos, 240, 0.9));
            Envolver(tiron, SrEfectos, Ataque(0.004, 0.075));
            Mezclar(muro, tiron, 0.95, 0);
            Barrido(muro, SrEfectos, 0.0, 165, 52, 0.40, Decaimiento(0.11), 0.55);

            // 2. Roce continuo. La envolvente entra rapido, se sostiene y afloja sobre el final;
            // encima va una modulacion lenta e irregular (dos senos que no son multiplos entre si,
            // asi el patron no se repite de forma audible en los 2.6s).
            float[] roce = Ruido(finDelRoce, SrEfectos, 5102);
            Filtrar(roce, SrEfectos, Biquad.PasaBanda(SrEfectos, 650, 0.7));

            for (int i = 0; i < roce.Length; i++)
            {
                double t = (double)i / SrEfectos;

                double entrada = Math.Min(1.0, t / 0.12);
                double salida = t > finDelRoce - 0.45 ? Math.Max(0.0, (finDelRoce - t) / 0.45) : 1.0;
                double agarre = 0.70 + 0.30 * Math.Sin(2.0 * Math.PI * 7.3 * t) * Math.Sin(2.0 * Math.PI * 2.1 * t);

                roce[i] = (float)(roce[i] * entrada * salida * agarre);
            }

            Mezclar(muro, roce, 0.55, (int)(0.04 * SrEfectos));

            // 3. Resonancias metalicas. Las tres entran escalonadas y se van apagando: el metal
            // suena mientras se mueve, no solo cuando lo golpean.
            double[,] parciales =
            {
                // frecuencia, inicio, duracion, ganancia
                { 196.0, 0.02, 2.40, 0.16 },
                { 293.7, 0.18, 2.10, 0.11 },
                { 437.0, 0.35, 1.80, 0.07 }
            };

            for (int i = 0; i < parciales.GetLength(0); i++)
            {
                Seno(muro, SrEfectos, parciales[i, 1], parciales[i, 0], parciales[i, 2],
                    Ataque(0.05, 0.85), parciales[i, 3]);
            }

            // 4. Retumbe grave debajo de todo.
            Seno(muro, SrEfectos, 0.0, 45.0, finDelRoce, Ataque(0.20, 1.40), 0.38);

            // 5. Golpe final: el muro tocando fondo.
            float[] fondo = Ruido(0.40, SrEfectos, 5103);
            Filtrar(fondo, SrEfectos, Biquad.PasaBanda(SrEfectos, 180, 0.8));
            Envolver(fondo, SrEfectos, Ataque(0.003, 0.11));
            Mezclar(muro, fondo, 0.85, (int)(golpeFinal * SrEfectos));
            Barrido(muro, SrEfectos, golpeFinal, 120, 42, 0.50, Decaimiento(0.14), 0.60);

            // Mas alto que el resto de los efectos a proposito: es el aviso de que el laberinto
            // cambio, y tiene que llegar aunque el muro este lejos del jugador.
            Normalizar(muro, 0.88);
            escritos.Add(Escribir(carpeta, "muro_deslizando.wav", muro, SrEfectos));

            return escritos;
        }

        // Ruido muy corto con un pasabanda angosto: suena a resonancia de plastico/metal golpeado.
        static void ClickMecanico(float[] destino, int sr, double centro, double q, double tau,
            int semilla, double ganancia = 1.0, double retardoSegundos = 0.0)
        {
            float[] click = Ruido(Math.Max(tau * 8.0, 0.01), sr, semilla);
            Filtrar(click, sr, Biquad.PasaBanda(sr, centro, q));
            Envolver(click, sr, Decaimiento(tau));
            Mezclar(destino, click, ganancia, (int)(retardoSegundos * sr));
        }

        // ------------------------------------------------------------------ items

        // Un sonido por item, no uno generico: recoger la llave de la salida y recoger una racion
        // de comida tienen que sentirse distinto. El nombre del archivo es "pickup_" + itemId
        // del ItemData, que es lo que BibliotecaDeSonidos busca (convencion, cero cableado en el
        // Inspector): agregar un item nuevo con sonido propio es dejar un .wav mas en esta carpeta.
        static List<string> GenerarItems(string carpeta)
        {
            var escritos = new List<string>();

            // Generico: el fallback cuando un item no tiene su propio .wav. Dos notas que suben.
            float[] generico = Buffer(0.45, SrEfectos);
            Seno(generico, SrEfectos, 0.00, 659.25, 0.22, Decaimiento(0.070), 0.55);
            Seno(generico, SrEfectos, 0.09, 987.77, 0.34, Decaimiento(0.110), 0.45);
            Normalizar(generico, 0.55);
            escritos.Add(Escribir(carpeta, "pickup_generico.wav", generico, SrEfectos));

            // Arma: chapa golpeada. Parciales inarmonicos y nada de nota clara.
            float[] arma = Buffer(0.70, SrEfectos);
            float[] chapa = Ruido(0.05, SrEfectos, 3301);
            Filtrar(chapa, SrEfectos, Biquad.PasaBanda(SrEfectos, 2600, 0.8));
            Envolver(chapa, SrEfectos, Decaimiento(0.012));
            Mezclar(arma, chapa, 0.70, 0);
            Seno(arma, SrEfectos, 0.002, 523.0, 0.60, Decaimiento(0.150), 0.40);
            Seno(arma, SrEfectos, 0.002, 1488.0, 0.45, Decaimiento(0.100), 0.30); // 2.84x: metalico
            Seno(arma, SrEfectos, 0.002, 2790.0, 0.30, Decaimiento(0.060), 0.18);
            Barrido(arma, SrEfectos, 0, 120, 90, 0.18, Decaimiento(0.045), 0.35);
            Normalizar(arma, 0.65);
            escritos.Add(Escribir(carpeta, "pickup_arma.wav", arma, SrEfectos));

            // Llaves: manojo tintineando. Varios pings cortos y desparejos.
            escritos.Add(Escribir(carpeta, "pickup_llave_interior.wav",
                Manojo(new double[] { 0.000, 0.045, 0.085, 0.150 }, new double[] { 1860, 2340, 1560, 2790 }, 5501), SrEfectos));
            // La llave de la salida suena mas grande y con mas cola: es el item que abre el final.
            escritos.Add(Escribir(carpeta, "pickup_llave_salida.wav",
                Manojo(new double[] { 0.000, 0.055, 0.110, 0.175, 0.240 }, new double[] { 1240, 1660, 2480, 1860, 3310 }, 5502), SrEfectos));

            // Pocion de vida: vidrio y liquido. Arpegio ascendente limpio = "te hizo bien".
            float[] pocion = Buffer(0.80, SrEfectos);
            double[] notas = { 587.33, 783.99, 1174.66 };
            for (int i = 0; i < notas.Length; i++)
            {
                Seno(pocion, SrEfectos, 0.07 * i, notas[i], 0.55, Decaimiento(0.160), 0.40);
                Seno(pocion, SrEfectos, 0.07 * i, notas[i] * 3.0, 0.20, Decaimiento(0.050), 0.10); // brillo de vidrio
            }
            float[] liquido = Ruido(0.35, SrEfectos, 6601);
            Filtrar(liquido, SrEfectos, Biquad.PasaBanda(SrEfectos, 3800, 0.6));
            Envolver(liquido, SrEfectos, Ataque(0.02, 0.120));
            Mezclar(pocion, liquido, 0.12, (int)(0.05 * SrEfectos));
            Normalizar(pocion, 0.58);
            escritos.Add(Escribir(carpeta, "pickup_pocion_vida.wav", pocion, SrEfectos));

            // Racion de comida: golpe sordo de bolsa + papel. Sin nota: no es un item "brillante".
            float[] racion = Buffer(0.40, SrEfectos);
            float[] papel = Ruido(0.30, SrEfectos, 7701);
            Filtrar(papel, SrEfectos, Biquad.PasaBanda(SrEfectos, 5200, 0.5));
            Envolver(papel, SrEfectos, Ataque(0.004, 0.090));
            Mezclar(racion, papel, 0.45, 0);
            float[] bolsa = Ruido(0.18, SrEfectos, 7702);
            Filtrar(bolsa, SrEfectos, Biquad.PasaBajos(SrEfectos, 700, 0.8));
            Envolver(bolsa, SrEfectos, Decaimiento(0.040));
            Mezclar(racion, bolsa, 0.55, 0);
            Barrido(racion, SrEfectos, 0, 150, 110, 0.12, Decaimiento(0.030), 0.30);
            Normalizar(racion, 0.50);
            escritos.Add(Escribir(carpeta, "pickup_racion_comida.wav", racion, SrEfectos));

            // Moneda: ping brillante con dos parciales apenas desafinados (el "wobble" del metal
            // girando). Es el unico sonido del juego que suena a recompensa pura.
            float[] moneda = Buffer(0.85, SrEfectos);
            Seno(moneda, SrEfectos, 0, 2093.0, 0.70, Decaimiento(0.200), 0.45);
            Seno(moneda, SrEfectos, 0, 2110.0, 0.70, Decaimiento(0.200), 0.35);
            Seno(moneda, SrEfectos, 0, 3136.0, 0.45, Decaimiento(0.110), 0.22);
            Seno(moneda, SrEfectos, 0.004, 1046.0, 0.30, Decaimiento(0.060), 0.18);
            Normalizar(moneda, 0.52);
            escritos.Add(Escribir(carpeta, "pickup_moneda.wav", moneda, SrEfectos));

            // Item de prueba: a proposito un bip plano y feo, para que se note en el acto que se
            // recogio el item de debug y no uno del juego.
            float[] prueba = Buffer(0.20, SrEfectos);
            Seno(prueba, SrEfectos, 0, 880.0, 0.18, Ataque(0.003, 0.120), 0.6);
            Normalizar(prueba, 0.45);
            escritos.Add(Escribir(carpeta, "pickup_item_prueba.wav", prueba, SrEfectos));

            return escritos;
        }

        static float[] Manojo(double[] tiempos, double[] frecuencias, int semilla)
        {
            double largo = tiempos[tiempos.Length - 1] + 0.75;
            float[] manojo = Buffer(largo, SrEfectos);

            for (int i = 0; i < tiempos.Length; i++)
            {
                double f = frecuencias[i];
                Seno(manojo, SrEfectos, tiempos[i], f, 0.50, Decaimiento(0.090), 0.42);
                Seno(manojo, SrEfectos, tiempos[i], f * 2.41, 0.28, Decaimiento(0.045), 0.20);
                ClickMecanico(manojo, SrEfectos, f * 1.5, 1.0, 0.004, semilla + i, 0.30, tiempos[i]);
            }

            Normalizar(manojo, 0.55);
            return manojo;
        }

        // ------------------------------------------------------------------ sintesis

        static float[] Buffer(double segundos, int sr)
        {
            return new float[Math.Max(1, (int)(segundos * sr))];
        }

        // Seno con envolvente, sumado al buffer desde 'inicioSegundos'.
        static void Seno(float[] destino, int sr, double inicioSegundos, double frecuencia,
            double duracion, Func<double, double> envolvente, double ganancia)
        {
            int inicio = (int)(inicioSegundos * sr);
            int muestras = (int)(duracion * sr);
            double paso = 2.0 * Math.PI * frecuencia / sr;

            for (int i = 0; i < muestras; i++)
            {
                int indice = inicio + i;
                if (indice < 0 || indice >= destino.Length) continue;
                destino[indice] += (float)(Math.Sin(paso * i) * envolvente((double)i / sr) * ganancia);
            }
        }

        // Seno que baja (o sube) de frecuencia: lo que hace que un golpe suene a golpe y no a nota.
        // La fase se integra muestra a muestra, no se calcula como f(t)*t (eso produce un barrido
        // del doble de pendiente y un salto de fase al empezar).
        static void Barrido(float[] destino, int sr, double inicioSegundos, double desde, double hasta,
            double duracion, Func<double, double> envolvente, double ganancia)
        {
            int inicio = (int)(inicioSegundos * sr);
            int muestras = (int)(duracion * sr);
            double fase = 0.0;

            for (int i = 0; i < muestras; i++)
            {
                double t = (double)i / muestras;
                double f = desde + (hasta - desde) * t;
                fase += 2.0 * Math.PI * f / sr;

                int indice = inicio + i;
                if (indice < 0 || indice >= destino.Length) continue;
                destino[indice] += (float)(Math.Sin(fase) * envolvente((double)i / sr) * ganancia);
            }
        }

        // Oscilador largo con dos armonicos y la amplitud respirando por un LFO. 'profundidad' 0
        // deja el volumen fijo, 1 lo lleva a silencio en el valle.
        static void Drone(float[] destino, int sr, double frecuencia, double duracion,
            double ganancia, double lfoHz, double profundidad)
        {
            int muestras = Math.Min(destino.Length, (int)(duracion * sr));
            double paso = 2.0 * Math.PI * frecuencia / sr;
            double pasoLfo = 2.0 * Math.PI * lfoHz / sr;

            for (int i = 0; i < muestras; i++)
            {
                double onda = Math.Sin(paso * i) + 0.30 * Math.Sin(paso * 2 * i) + 0.12 * Math.Sin(paso * 3 * i);
                double lfo = 1.0 - profundidad * 0.5 * (1.0 - Math.Cos(pasoLfo * i)); // 1 -> 1-profundidad
                destino[i] += (float)(onda * lfo * ganancia);
            }
        }

        static void ModularAmplitud(float[] señal, int sr, double lfoHz, double profundidad)
        {
            double pasoLfo = 2.0 * Math.PI * lfoHz / sr;
            for (int i = 0; i < señal.Length; i++)
            {
                double lfo = 1.0 - profundidad * 0.5 * (1.0 - Math.Cos(pasoLfo * i));
                señal[i] = (float)(señal[i] * lfo);
            }
        }

        // Ruido blanco con semilla fija: el mismo .wav byte a byte en cada corrida.
        static float[] Ruido(double segundos, int sr, int semilla)
        {
            var rnd = new Random(semilla);
            float[] ruido = Buffer(segundos, sr);
            for (int i = 0; i < ruido.Length; i++) ruido[i] = (float)(rnd.NextDouble() * 2.0 - 1.0);
            return ruido;
        }

        static void Envolver(float[] señal, int sr, Func<double, double> envolvente)
        {
            // Las envolventes trabajan en segundos, asi que hace falta el sr con el que se creo el
            // buffer: con el valor equivocado el mismo Decaimiento(0.03) duraria el doble o la mitad.
            for (int i = 0; i < señal.Length; i++)
            {
                señal[i] = (float)(señal[i] * envolvente((double)i / sr));
            }
        }

        static void Mezclar(float[] destino, float[] fuente, double ganancia, int offsetMuestras)
        {
            for (int i = 0; i < fuente.Length; i++)
            {
                int indice = offsetMuestras + i;
                if (indice < 0 || indice >= destino.Length) continue;
                destino[indice] += (float)(fuente[i] * ganancia);
            }
        }

        static void Filtrar(float[] señal, int sr, Biquad filtro)
        {
            filtro.Procesar(señal);
        }

        // Escala la señal para que su pico quede exactamente en 'pico'. Normalizar cada clip por
        // separado y despues fijar la ganancia a mano (el 'pico' de cada llamada) es lo que
        // mantiene el balance entre sonidos: el hover queda tenue y el click del boton presente,
        // sin tener que medir dB.
        static void Normalizar(float[] señal, double pico)
        {
            float maximo = 0f;
            for (int i = 0; i < señal.Length; i++) maximo = Math.Max(maximo, Math.Abs(señal[i]));
            if (maximo <= 0.0001f) return;

            double factor = pico / maximo;
            for (int i = 0; i < señal.Length; i++) señal[i] = (float)(señal[i] * factor);
        }

        // Devuelve los primeros 'duracion' segundos con la cola desvanecida encima del arranque,
        // para que el clip se pueda poner en loop sin costura.
        static float[] Crossfade(float[] señal, int sr, double duracion, double cruce)
        {
            int largo = (int)(duracion * sr);
            int muestrasCruce = (int)(cruce * sr);
            float[] salida = new float[largo];

            Array.Copy(señal, salida, Math.Min(largo, señal.Length));

            for (int i = 0; i < muestrasCruce; i++)
            {
                int origen = largo + i;
                if (origen >= señal.Length) break;

                // La cola entra fuerte al principio del clip y se va yendo; el arranque hace lo
                // contrario. Asi, al saltar del final al inicio, lo que se oye es continuo.
                double peso = 1.0 - (double)i / muestrasCruce;
                salida[i] = (float)(salida[i] * (1.0 - peso) + señal[origen] * peso);
            }

            return salida;
        }

        // Caida exponencial desde 1. 'tau' es el tiempo en el que baja a ~37%.
        static Func<double, double> Decaimiento(double tau)
        {
            return t => Math.Exp(-t / tau);
        }

        // Ataque lineal y despues caida exponencial: para golpes que no arrancan de cero duro
        // (un ataque instantaneo en un ruido ancho suena a chasquido digital).
        static Func<double, double> Ataque(double ataque, double tau)
        {
            return t => t < ataque ? t / ataque : Math.Exp(-(t - ataque) / tau);
        }

        // ------------------------------------------------------------------ WAV

        // WAV PCM 16 bits mono. Mono y no estereo a proposito: todos estos sonidos se reproducen
        // por un AudioSource 3D (pasos, items, linterna) o por el mixer (UI, musica), y Unity
        // igual colapsa a mono los clips espacializados; un .wav estereo seria el doble de peso
        // por nada.
        static string Escribir(string carpeta, string nombre, float[] muestras, int sr)
        {
            Directory.CreateDirectory(carpeta);
            string ruta = Path.Combine(carpeta, nombre);

            using (var archivo = new FileStream(ruta, FileMode.Create, FileAccess.Write))
            using (var w = new BinaryWriter(archivo))
            {
                int bytesDeDatos = muestras.Length * 2;

                w.Write(new[] { 'R', 'I', 'F', 'F' });
                w.Write(36 + bytesDeDatos);
                w.Write(new[] { 'W', 'A', 'V', 'E' });

                w.Write(new[] { 'f', 'm', 't', ' ' });
                w.Write(16);              // tamaño del bloque fmt
                w.Write((short)1);        // PCM sin comprimir
                w.Write((short)1);        // 1 canal
                w.Write(sr);
                w.Write(sr * 2);          // bytes por segundo
                w.Write((short)2);        // bytes por bloque
                w.Write((short)16);       // bits por muestra

                w.Write(new[] { 'd', 'a', 't', 'a' });
                w.Write(bytesDeDatos);

                for (int i = 0; i < muestras.Length; i++)
                {
                    // Clamp antes de convertir: un pico por encima de 1 daria la vuelta y sonaria
                    // a distorsion rota en vez de recortada.
                    double v = Math.Max(-1.0, Math.Min(1.0, muestras[i]));
                    w.Write((short)Math.Round(v * 32767.0));
                }
            }

            var info = new FileInfo(ruta);
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "  {0,-34} {1,6:0.00}s  {2,3}kHz  {3,7:0.0} KB",
                nombre, (double)muestras.Length / sr, sr / 1000, info.Length / 1024.0));
            return ruta;
        }

        // ------------------------------------------------------------------ biquad

        // Filtro biquad clasico (RBJ cookbook). Alcanza y sobra para darle color a ruido blanco,
        // que es de lo que estan hechos los pasos, los clicks y la cama de la musica.
        public sealed class Biquad
        {
            double a1, a2, b0, b1, b2;

            Biquad(double b0, double b1, double b2, double a0, double a1, double a2)
            {
                this.b0 = b0 / a0;
                this.b1 = b1 / a0;
                this.b2 = b2 / a0;
                this.a1 = a1 / a0;
                this.a2 = a2 / a0;
            }

            public static Biquad PasaBajos(int sr, double corte, double q)
            {
                double w = 2.0 * Math.PI * corte / sr;
                double cos = Math.Cos(w), alpha = Math.Sin(w) / (2.0 * q);
                return new Biquad((1 - cos) / 2, 1 - cos, (1 - cos) / 2, 1 + alpha, -2 * cos, 1 - alpha);
            }

            public static Biquad PasaAltos(int sr, double corte, double q)
            {
                double w = 2.0 * Math.PI * corte / sr;
                double cos = Math.Cos(w), alpha = Math.Sin(w) / (2.0 * q);
                return new Biquad((1 + cos) / 2, -(1 + cos), (1 + cos) / 2, 1 + alpha, -2 * cos, 1 - alpha);
            }

            public static Biquad PasaBanda(int sr, double centro, double q)
            {
                double w = 2.0 * Math.PI * centro / sr;
                double sin = Math.Sin(w), cos = Math.Cos(w), alpha = sin / (2.0 * q);
                return new Biquad(alpha, 0, -alpha, 1 + alpha, -2 * cos, 1 - alpha);
            }

            public void Procesar(float[] señal)
            {
                double x1 = 0, x2 = 0, y1 = 0, y2 = 0;
                for (int i = 0; i < señal.Length; i++)
                {
                    double x = señal[i];
                    double y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                    x2 = x1; x1 = x;
                    y2 = y1; y1 = y;
                    señal[i] = (float)y;
                }
            }
        }
    }
}
