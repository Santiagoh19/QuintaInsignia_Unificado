using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

// Sombras en la carpa — estacion 3 del campamento de exploradores, en Unity con MediaPipe.
// Etapa 1: el campamento estilo caricatura y la sombra en vivo de las dos manos.
//
// Como esta armada la escena (todo mira hacia +z):
//   linterna (en el suelo, detras de ti) -> tus manos -> la fogata (en medio) -> la carpa
// La linterna es la luz que hace las sombras: tus manos 3D se ven y su sombra cae en la tela.
// La fogata alumbra poco y NO hace sombras: es el centro del campamento.
//
// La escena se arma con el menu "Sombras" -> "Crear escena de sombras".
// Para volver a construir solo el campamento y las manos:
// clic derecho en el titulo de este componente -> "Construir campamento y manos".
public class SombrasJuego : MonoBehaviour
{
    // ---- lo que se ve en el Inspector (lo llena el boton de construir) ----
    [Header("Lo que construye el boton")]
    public Light luzLinterna;
    public Light luzFogata;
    public Transform[] llamas;
    public ParticleSystem chispas;
    public Transform[] estrellas;
    public Transform[] luciernagas;
    public ManoSombra manoIzquierda;
    public ManoSombra manoDerecha;

    [Header("Luces (puedes ajustarlas con el juego corriendo)")]
    public float intensidadLinterna = 90f;  // la que hace las sombras en la carpa
    public float intensidadFogata = 1.2f;   // la luz naranja alrededor del fuego
    [Range(0f, 1f)]
    public float parpadeo = 0.3f;           // cuanto tiembla la fogata (0 = nada)

    [Header("Manos")]
    [Tooltip("Cambia el color y vuelve a construir para que las manos se parezcan a las tuyas")]
    public Color colorPiel = new Color(0.94f, 0.74f, 0.58f);

    [Header("La partida")]
    public SombrasInterfaz interfaz;
    public int niveles = 3;                 // nivel 1 = 3 señas, nivel 2 = 4, nivel 3 = 5
    public float tiempoSostener = 0.6f;     // segundos que hay que sostener la seña para que cuente (contra el temblor)
    public float duracionFuego = 15f;       // segundos que tarda la fogata llena en apagarse
    public float tiempoMensaje = 3f;        // segundos que se muestran "nivel superado" y "se apago la fogata"

    [Header("Las palabras del explorador")]
    [Tooltip("Silabas separadas con guion y palabras con espacio. Cada silaba es una seña (CA palma, MI paz, NO cuernos, TA pulgar arriba, NA llamame). El nivel 1 toma las de 3 silabas, el 2 las de 4 y el 3 las de 5")]
    public string[] palabras = { "CA-MI-NO", "CA-MI-NA-TA", "MI CA-MI-NO", "MI CA-MI-NA-TA" };

    [Header("Dos manos")]
    [Tooltip("Segundos que se piden las dos manos en una seña x2. Despues una mano tambien vale (para no dejar fuera a quien solo puede usar una)")]
    public float tiempoAyudaDosManos = 8f;

    [Header("Sonidos (si esta vacio, se agrega solo)")]
    public SombrasSonidos sonidos;

    [Header("Pruebas")]
    public bool mostrarDatos = true;        // el texto de abajo con tus dedos


    // ---- los gestos que reconoce el juego ----
    // cada patron es la mano de pulgar a menique: '1' = arriba, '0' = abajo, '?' = no importa.
    // El pulgar no importa en la palma, la paz y los cuernos porque MediaPipe lo detecta mal
    // (lo dice el muro). En el pulgar arriba y en el llamame si importa: hay que probarlos
    // (son "static" para que la interfaz use los mismos nombres)
    public static readonly string[] NOMBRES = { "PALMA", "PAZ", "CUERNOS", "PULGAR ARRIBA", "LLÁMAME" };
    public static readonly string[] PATRONES = { "?1111", "?1100", "?1001", "10000", "10001" };

    // cada seña es una silaba del idioma de los exploradores (mismo orden que NOMBRES).
    // Las tres primeras (sin pulgar) bastan para CA-MI-NO; el pulgar suma TA y NA
    public static readonly string[] SILABAS = { "CA", "MI", "NO", "TA", "NA" };


    // ---- la escena: donde va cada cosa (en metros) ----
    Vector3 POS_CAMARA = new Vector3(0f, 1.6f, -3.2f);
    Vector3 POS_LINTERNA = new Vector3(0f, 0.05f, -9f);   // en el suelo, detras de la camara
    Vector3 POS_FOGATA = new Vector3(0f, 0f, 1.8f);
    float Z_CARPA = 5f;        // la tela donde caen las sombras
    float ALTO_CARPA = 4.2f;

    // las formas que no trae Unity se hacen "en torno", como una vasija:
    // se gira un perfil (radio, altura) alrededor del eje vertical
    Vector2[] PERFIL_LLAMA =
    {
        new Vector2(0f, 0f), new Vector2(0.32f, 0.06f), new Vector2(0.5f, 0.25f), new Vector2(0.48f, 0.45f),
        new Vector2(0.34f, 0.68f), new Vector2(0.16f, 0.88f), new Vector2(0f, 1.1f),
    };
    Vector2[] PERFIL_CONO = { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f) };

    // tamanos y posiciones originales, para animar sin perderlos
    Vector3[] escalaLlamas;
    Vector3[] escalaEstrellas;
    Vector3[] baseLuciernagas;
    float tiempoRegistro = 0f;

    // ---- la partida ----
    // 'menu'     : se estan viendo el inicio o las instrucciones
    // 'jugando'  : esperando la seña que toca
    // 'apagada'  : se apago la fogata (no se pierde nada, solo se espera)
    // 'nivel'    : se completo la serie del nivel (se muestra la palabra)
    // 'ultima'   : se completo la ultima palabra (se muestra antes de la graduacion)
    // 'fin'      : se completo la estacion
    string estado = "menu";
    float tiempoEstado = 0f;
    int nivel = 1;
    string[] serie = new string[0];
    bool[] dobles = new bool[0];            // las señas que piden las dos manos (x2)
    string[] silabas = new string[0];       // la silaba de cada seña de la serie
    bool[] finDePalabra = new bool[0];      // true si despues de esa silaba hay un espacio ("MI CA-MI-NO")
    string palabra = "";                    // la palabra de este nivel, para leer: "MI CAMINO"
    List<string> dichas = new List<string>();   // las palabras que ya dijiste en esta partida
    int paso = 0;                           // cual seña de la serie toca (0 = la primera)
    float tiempoPaso = 0f;                  // segundos jugando en la seña que toca (para la ayuda de las dos manos)
    float sostenido = 0f;                   // segundos que llevas sosteniendo la seña que toca
    float fuego = 1f;                       // 1 = fogata llena, 0 = apagada
    float fuegoVisible = 1f;                // lo que se ve: sigue a "fuego" poco a poco


    void Start()
    {
        // que el juego siga corriendo aunque Unity no tenga el foco
        // (por ejemplo, cuando la ventana activa es la de la camara de Python)
        Application.runInBackground = true;

        // si todavia no se construyo la escena, se construye ahora
        if (luzLinterna == null || manoIzquierda == null || manoDerecha == null)
        {
            ConstruirTodo();
        }

        escalaLlamas = new Vector3[llamas.Length];
        for (int i = 0; i < llamas.Length; i++)
        {
            escalaLlamas[i] = llamas[i].localScale;
        }

        escalaEstrellas = new Vector3[estrellas.Length];
        for (int i = 0; i < estrellas.Length; i++)
        {
            escalaEstrellas[i] = estrellas[i].localScale;
        }

        baseLuciernagas = new Vector3[luciernagas.Length];
        for (int i = 0; i < luciernagas.Length; i++)
        {
            baseLuciernagas[i] = luciernagas[i].position;
        }

        if (interfaz == null)
        {
            interfaz = FindFirstObjectByType<SombrasInterfaz>();
        }

        // los sonidos se fabrican con codigo: si la escena no los tiene, se agregan aqui
        if (sonidos == null)
        {
            sonidos = GetComponent<SombrasSonidos>();
            if (sonidos == null)
            {
                sonidos = gameObject.AddComponent<SombrasSonidos>();
            }
        }

        RevisarPalabras();
    }


    void Update()
    {
        if (Pausa.Activa) return;   // JUEGO UNIFICADO: menú de pausa general abierto
        luzLinterna.intensity = intensidadLinterna;
        ActualizarPartida();
        MoverFogata();
        MoverNoche();
        RegistrarManos();
    }


    // ---- la fogata tiembla: la luz sube y baja y las llamas crecen, se encogen y se mecen ----
    // PerlinNoise da un numero de 0 a 1 que cambia suave, no a saltos como Random
    // el tamano depende de cuanto fuego queda: llena se ve completa, casi apagada se ve chiquita
    void MoverFogata()
    {
        // lo que se ve sigue al fuego poco a poco, asi al echarle lena crece en vez de saltar
        float meta = 0f;
        if (estado != "apagada")
        {
            meta = 0.3f + 0.7f * fuego;
        }
        fuegoVisible = Mathf.Lerp(fuegoVisible, meta, 4f * Time.deltaTime);

        float ruido = Mathf.PerlinNoise(Time.time * 3f, 0f);
        luzFogata.intensity = intensidadFogata * fuegoVisible * (1f - parpadeo / 2f + parpadeo * ruido);

        for (int i = 0; i < llamas.Length; i++)
        {
            float alto = 0.85f + 0.3f * Mathf.PerlinNoise(Time.time * 4f, i * 10f);
            float ancho = 0.95f + 0.1f * Mathf.PerlinNoise(Time.time * 3f, i * 10f + 5f);
            Vector3 e = escalaLlamas[i] * fuegoVisible;
            llamas[i].localScale = new Vector3(e.x * ancho, e.y * alto, e.z * ancho);
        }

        // sin fuego no salen chispas
        if (chispas != null)
        {
            var emision = chispas.emission;
            emision.enabled = estado != "apagada";
        }
    }


    // =====================================================================
    // LA PARTIDA
    // =====================================================================

    // la llama la interfaz al pasar las instrucciones (o al volver a jugar)
    public void EmpezarPartida()
    {
        nivel = 1;
        dichas.Clear();
        NuevaSerie();
    }

    // la llama la interfaz al volver al inicio
    public void DetenerPartida()
    {
        CambiarEstado("menu");
        fuego = 1f;
    }

    void CambiarEstado(string nuevo)
    {
        estado = nuevo;
        tiempoEstado = 0f;
    }


    // ---- una serie nueva para el nivel actual: la palabra que vas a decir con las señas ----
    // El nivel 1 tiene palabras de 3 silabas, el 2 de 4 y el 3 de 5 (salen de "palabras", en el Inspector).
    // Si para un nivel no hay ninguna palabra que sirva, la serie sale al azar como antes
    // y la palabra es inventada.
    // Las dos manos: el nivel 1 no las pide, el 2 las pide en UNA seña y el 3 en DOS.
    void NuevaSerie()
    {
        int largo = nivel + 2;

        List<string> candidatas = new List<string>();
        for (int i = 0; i < palabras.Length; i++)
        {
            if (Silabas(palabras[i]).Length == largo && PalabraValida(palabras[i]))
            {
                candidatas.Add(palabras[i]);
            }
        }

        if (candidatas.Count > 0)
        {
            ArmarPalabra(candidatas[Random.Range(0, candidatas.Count)]);
        }
        else
        {
            ArmarPalabra(PalabraAlAzar(largo));
        }

        ElegirDobles(nivel - 1);

        paso = 0;
        tiempoPaso = 0f;
        sostenido = 0f;
        fuego = 1f;
        CambiarEstado("jugando");

        if (interfaz != null)
        {
            interfaz.FijarNivel(nivel, niveles);
            interfaz.MostrarSerie(serie, dobles, silabas, paso);
            interfaz.FijarPalabra(PalabraEnCurso());
            interfaz.OcultarMensaje();
        }
    }

    // convierte "MI CA-MI-NO" en la serie de señas, sus silabas y donde van los espacios
    void ArmarPalabra(string texto)
    {
        string[] partes = Silabas(texto);
        serie = new string[partes.Length];
        silabas = new string[partes.Length];
        finDePalabra = new bool[partes.Length];

        for (int i = 0; i < partes.Length; i++)
        {
            int indice = System.Array.IndexOf(SILABAS, partes[i]);
            serie[i] = NOMBRES[indice];
            silabas[i] = partes[i];
        }

        // donde hay un espacio en el texto termina una palabra
        string[] palabrasSueltas = texto.Trim().ToUpper().Split(' ');
        int cuenta = 0;
        for (int p = 0; p < palabrasSueltas.Length; p++)
        {
            cuenta = cuenta + Silabas(palabrasSueltas[p]).Length;
            if (cuenta > 0 && cuenta < partes.Length)
            {
                finDePalabra[cuenta - 1] = true;
            }
        }

        // para leer: "MI CA-MI-NO" -> "MI CAMINO"
        palabra = texto.Trim().ToUpper().Replace("-", "");
    }

    // la serie al azar de antes (sin dos iguales seguidas), escrita como palabra inventada
    string PalabraAlAzar(int largo)
    {
        int cuantasSenas = 3;
        if (nivel >= 2)
        {
            cuantasSenas = NOMBRES.Length;
        }

        string texto = "";
        int anterior = -1;
        for (int i = 0; i < largo; i++)
        {
            int s = Random.Range(0, cuantasSenas);
            while (s == anterior)
            {
                s = Random.Range(0, cuantasSenas);
            }
            if (i > 0)
            {
                texto = texto + "-";
            }
            texto = texto + SILABAS[s];
            anterior = s;
        }
        return texto;
    }

    // las señas x2. Solo palma, paz y cuernos: con el pulgar MediaPipe falla mas, y con dos manos peor
    void ElegirDobles(int cuantas)
    {
        dobles = new bool[serie.Length];

        List<int> posibles = new List<int>();
        for (int i = 0; i < serie.Length; i++)
        {
            if (System.Array.IndexOf(NOMBRES, serie[i]) < 3)
            {
                posibles.Add(i);
            }
        }

        for (int k = 0; k < cuantas && posibles.Count > 0; k++)
        {
            int elegido = Random.Range(0, posibles.Count);
            dobles[posibles[elegido]] = true;
            posibles.RemoveAt(elegido);
        }
    }

    // "MI CA-MI-NO" -> { "MI", "CA", "MI", "NO" }
    string[] Silabas(string texto)
    {
        return texto.Trim().ToUpper().Split(new char[] { ' ', '-' }, System.StringSplitOptions.RemoveEmptyEntries);
    }

    // sirve si cada silaba es una seña y no hay dos iguales seguidas
    // (dos iguales seguidas no se pueden separar: sostener la seña contaria dos veces)
    bool PalabraValida(string texto)
    {
        string[] partes = Silabas(texto);
        for (int i = 0; i < partes.Length; i++)
        {
            if (System.Array.IndexOf(SILABAS, partes[i]) < 0)
            {
                return false;
            }
            if (i > 0 && partes[i] == partes[i - 1])
            {
                return false;
            }
        }
        return partes.Length > 0;
    }

    // avisa en la consola si una palabra del Inspector no sirve
    void RevisarPalabras()
    {
        for (int i = 0; i < palabras.Length; i++)
        {
            string[] partes = Silabas(palabras[i]);
            if (!PalabraValida(palabras[i]))
            {
                Debug.LogWarning("[Sombras] La palabra \"" + palabras[i] + "\" no sirve: cada silaba tiene que ser " +
                                 string.Join(", ", SILABAS) + " y no puede haber dos iguales seguidas.");
            }
            else if (partes.Length == 3 && (System.Array.IndexOf(partes, "TA") >= 0 || System.Array.IndexOf(partes, "NA") >= 0))
            {
                Debug.LogWarning("[Sombras] \"" + palabras[i] + "\" es del nivel 1 pero usa el pulgar (TA o NA), que MediaPipe detecta mal.");
            }
        }
    }

    // la palabra como va: "MI   CA · MI · _ · _"
    public string PalabraEnCurso()
    {
        string texto = "";
        for (int i = 0; i < silabas.Length; i++)
        {
            if (i < paso)
            {
                texto = texto + silabas[i];
            }
            else
            {
                texto = texto + "_";
            }

            if (i < silabas.Length - 1)
            {
                if (finDePalabra[i])
                {
                    texto = texto + "    ";
                }
                else
                {
                    texto = texto + " · ";
                }
            }
        }
        return texto;
    }

    // la silaba de una seña ("PAZ" -> "MI")
    public static string SilabaDe(string gesto)
    {
        int i = System.Array.IndexOf(NOMBRES, gesto);
        if (i < 0)
        {
            return "";
        }
        return SILABAS[i];
    }


    // ---- lo que pasa en cada cuadro ----
    void ActualizarPartida()
    {
        if (estado == "menu")
        {
            fuego = 1f;
            return;
        }

        tiempoEstado = tiempoEstado + Time.deltaTime;

        if (estado == "jugando")
        {
            // la fogata se va apagando
            fuego = fuego - Time.deltaTime / duracionFuego;
            tiempoPaso = tiempoPaso + Time.deltaTime;

            // ¿estas haciendo la seña que toca? hay que sostenerla un momento
            if (HaciendoLaQueToca())
            {
                sostenido = sostenido + Time.deltaTime;
            }
            else
            {
                sostenido = 0f;
            }

            if (sostenido >= tiempoSostener)
            {
                SenaLograda(serie[paso]);
            }
            else if (fuego <= 0f)
            {
                fuego = 0f;
                CambiarEstado("apagada");
                if (sonidos != null)
                {
                    sonidos.TocarApagada();
                }
                if (interfaz != null)
                {
                    interfaz.MostrarMensaje("¡Se apagó la fogata!", "Lucas la vuelve a prender...");
                }
            }
        }
        else if (estado == "apagada")
        {
            // se vuelve a prender y se sigue con la MISMA seña: aqui nadie pierde
            if (tiempoEstado > tiempoMensaje)
            {
                fuego = 1f;
                sostenido = 0f;
                CambiarEstado("jugando");
                if (interfaz != null)
                {
                    interfaz.OcultarMensaje();
                }
            }
        }
        else if (estado == "nivel")
        {
            if (tiempoEstado > tiempoMensaje)
            {
                nivel = nivel + 1;
                NuevaSerie();
            }
        }
        else if (estado == "ultima")
        {
            // se deja oir la ultima palabra y despues viene la graduacion
            if (tiempoEstado > tiempoMensaje)
            {
                CambiarEstado("fin");
                if (interfaz != null)
                {
                    interfaz.MostrarFinal(dichas.ToArray());
                }
            }
        }

        if (interfaz != null)
        {
            interfaz.FijarFogata(fuego);
            interfaz.FijarSostenido(sostenido / tiempoSostener);
        }
    }


    // ---- las dos manos ----
    // una seña x2 pide las dos manos. Si pasan "tiempoAyudaDosManos" segundos sin lograrla,
    // una mano tambien vale: asi nadie se queda trabado (por ejemplo, con hemiparesia)
    public bool PideDosManos()
    {
        return estado == "jugando" && paso < dobles.Length && dobles[paso] && tiempoPaso < tiempoAyudaDosManos;
    }

    public bool AyudaUnaMano()
    {
        return paso < dobles.Length && dobles[paso] && tiempoPaso >= tiempoAyudaDosManos;
    }

    // ¿la estas haciendo como la pide? (con las dos manos si es x2; si no, con cualquiera)
    public bool HaciendoLaQueToca()
    {
        if (paso >= serie.Length)
        {
            return false;
        }

        string objetivo = serie[paso];
        bool izquierda = NombreGesto(manoIzquierda) == objetivo;
        bool derecha = NombreGesto(manoDerecha) == objetivo;

        if (PideDosManos())
        {
            return izquierda && derecha;
        }
        return izquierda || derecha;
    }


    // ---- se sostuvo la seña que tocaba: suena su silaba ----
    void SenaLograda(string objetivo)
    {
        bool dosManos = NombreGesto(manoIzquierda) == objetivo && NombreGesto(manoDerecha) == objetivo;
        string silaba = silabas[paso];
        bool eraDoble = dobles[paso];

        paso = paso + 1;
        tiempoPaso = 0f;
        sostenido = 0f;
        fuego = 1f;   // cada seña lograda es lena nueva para la fogata

        if (sonidos != null)
        {
            sonidos.TocarSena(objetivo, dosManos);
        }

        if (interfaz != null)
        {
            interfaz.FijarPaso(paso);
            interfaz.FijarPalabra(PalabraEnCurso());
            if (eraDoble && dosManos)
            {
                interfaz.Celebrar("¡" + silaba + "! Con las dos manos");
            }
            else
            {
                interfaz.Celebrar("¡" + silaba + "!");
            }
        }

        if (paso >= serie.Length)
        {
            // la palabra completa: suena entera y se muestra
            dichas.Add(palabra);
            bool ultima = nivel >= niveles;

            if (sonidos != null)
            {
                if (ultima)
                {
                    sonidos.TocarGraduacion(serie);
                }
                else
                {
                    sonidos.TocarPalabra(serie);
                }
            }

            if (ultima)
            {
                CambiarEstado("ultima");
                if (interfaz != null)
                {
                    interfaz.MostrarMensaje("¡Dijiste " + palabra + "!", "Es la última seña secreta...");
                }
            }
            else
            {
                CambiarEstado("nivel");
                if (interfaz != null)
                {
                    interfaz.MostrarMensaje("¡Dijiste " + palabra + "!", "La próxima palabra tiene " + (nivel + 3) + " sílabas");
                }
            }
        }
    }


    // ---- las estrellas titilan y las luciernagas vuelan en circulos ----
    void MoverNoche()
    {
        for (int i = 0; i < estrellas.Length; i++)
        {
            float brillo = 0.6f + 0.6f * Mathf.PerlinNoise(Time.time * 1.5f, i * 3f);
            estrellas[i].localScale = escalaEstrellas[i] * brillo;
        }

        for (int i = 0; i < luciernagas.Length; i++)
        {
            float t = Time.time;
            Vector3 vuelo = new Vector3(
                Mathf.Sin(t * 0.7f + i) * 0.4f,
                Mathf.Sin(t * 1.1f + i * 2f) * 0.25f,
                Mathf.Cos(t * 0.5f + i) * 0.4f);
            luciernagas[i].position = baseLuciernagas[i] + vuelo;

            // se prenden y se apagan
            float luz = Mathf.Clamp01(Mathf.Sin(t * 2f + i * 1.7f) * 0.5f + 0.6f);
            luciernagas[i].localScale = Vector3.one * 0.06f * luz;
        }
    }


    // ---- para revisar desde la consola que llegan los dedos: una linea por segundo ----
    void RegistrarManos()
    {
        tiempoRegistro = tiempoRegistro + Time.deltaTime;
        if (tiempoRegistro < 1f)
        {
            return;
        }
        tiempoRegistro = 0f;

        Debug.Log("[Sombras] izq " + Dedos(manoIzquierda.arriba) + " " + NombreGesto(manoIzquierda) +
                  " | der " + Dedos(manoDerecha.arriba) + " " + NombreGesto(manoDerecha));
    }


    // ---- que gesto esta haciendo una mano ("—" si ninguno) ----
    public string NombreGesto(ManoSombra mano)
    {
        if (!mano.presente)
        {
            return "—";
        }

        for (int g = 0; g < PATRONES.Length; g++)
        {
            if (Coincide(mano.arriba, PATRONES[g]))
            {
                return NOMBRES[g];
            }
        }
        return "—";
    }

    // ---- el gesto que estas haciendo con cualquiera de las dos manos ----
    // primero mira la derecha; si no hace ningun gesto, la izquierda
    public string GestoActual()
    {
        string gesto = NombreGesto(manoDerecha);
        if (gesto == "—")
        {
            gesto = NombreGesto(manoIzquierda);
        }
        return gesto;
    }

    // revisa dedo por dedo si la mano coincide con el patron
    bool Coincide(bool[] arriba, string patron)
    {
        for (int i = 0; i < 5; i++)
        {
            if (patron[i] == '?')
            {
                continue;   // este dedo no importa
            }

            bool pide = patron[i] == '1';
            if (arriba[i] != pide)
            {
                return false;
            }
        }
        return true;
    }

    // los dedos como texto: "01100" (pulgar, indice, medio, anular, menique)
    string Dedos(bool[] arriba)
    {
        string texto = "";
        for (int i = 0; i < 5; i++)
        {
            if (arriba[i])
            {
                texto = texto + "1";
            }
            else
            {
                texto = texto + "0";
            }
        }
        return texto;
    }


    // =====================================================================
    // CONSTRUIR EN LA ESCENA
    // Esto corre desde el Inspector, sin dar Play: clic derecho en el titulo
    // del componente SombrasJuego -> "Construir campamento y manos".
    // Despues guarda la escena (Ctrl+S) y todo queda como si lo hubieras armado tu.
    // =====================================================================
    [ContextMenu("Construir campamento y manos")]
    public void ConstruirTodo()
    {
        // si ya habia algo construido, se borra para no duplicarlo
        string[] viejos = { "Campamento", "Manos", "Volumen" };
        for (int i = 0; i < viejos.Length; i++)
        {
            Transform viejo = transform.Find(viejos[i]);
            if (viejo != null)
            {
                Borrar(viejo.gameObject);
            }
        }

        PrepararCamaraYNoche();

        GameObject campamento = new GameObject("Campamento");
        campamento.transform.SetParent(transform);
        Transform c = campamento.transform;

        ConstruirCielo(c);
        ConstruirSuelo(c);
        ConstruirCarpa(c);
        ConstruirPinos(c);
        ConstruirFogata(c);
        ConstruirLinterna(c);
        ConstruirLuciernagas(c);
        ConstruirManos();
        ConstruirPostProceso();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            AssetDatabase.SaveAssets();
            // avisa a Unity que la escena cambio, para que se pueda guardar
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
        Debug.Log("[Sombras] Listo: campamento y manos construidos. Guarda la escena con Ctrl+S.");
    }


    // ---- la camara mira la carpa de frente, y la noche es azul ----
    void PrepararCamaraYNoche()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[Sombras] No hay camara con la etiqueta MainCamera: el campamento se construye igual, pero sin acomodar la camara.");
            return;
        }
        cam.orthographic = false;
        cam.fieldOfView = 55f;
        cam.transform.position = POS_CAMARA;
        cam.transform.LookAt(new Vector3(0f, 1.5f, Z_CARPA));
        cam.farClipPlane = 120f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.03f, 0.04f, 0.12f);

        // el brillo de la fogata y la luna (bloom) necesita el post-proceso prendido en la camara
        cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;

        // la luz de ambiente: un azul de noche, suficiente para ver todo sin linterna
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.22f, 0.24f, 0.4f);
    }


    // ---- el cielo: degradado, luna, estrellas y montanas al fondo ----
    void ConstruirCielo(Transform padre)
    {
        GameObject grupo = Grupo("Cielo", padre);

        // un cuadro gigante al fondo con un degradado: morado en el horizonte, azul arriba
        GameObject fondo = Primitiva(PrimitiveType.Quad, "Degradado", grupo.transform);
        fondo.transform.localPosition = new Vector3(0f, 20f, 58f);
        fondo.transform.localScale = new Vector3(220f, 80f, 1f);
        Pintar(fondo, ObtenerMaterial("Sombras_Cielo", Color.white, false, ObtenerDegradado()));
        SinSombra(fondo);

        // la luna, con tres crateres. Su color pasa de 1 para que brille con el bloom
        GameObject luna = Primitiva(PrimitiveType.Sphere, "Luna", grupo.transform);
        luna.transform.localPosition = new Vector3(-16f, 24f, 54f);
        luna.transform.localScale = Vector3.one * 6f;
        Pintar(luna, ObtenerMaterial("Sombras_Luna", new Color(1.3f, 1.22f, 0.95f), false));
        SinSombra(luna);

        Vector3[] crateres = { new Vector3(-1.1f, 0.8f, -2.4f), new Vector3(0.9f, -0.4f, -2.6f), new Vector3(-0.2f, -1.4f, -2.5f) };
        float[] tamanosCrater = { 1.3f, 0.9f, 0.7f };
        Material crater = ObtenerMaterial("Sombras_Crater", new Color(1.05f, 0.97f, 0.75f), false);
        for (int i = 0; i < crateres.Length; i++)
        {
            GameObject cr = Primitiva(PrimitiveType.Sphere, "Crater_" + (i + 1), grupo.transform);
            cr.transform.localPosition = luna.transform.localPosition + crateres[i];
            cr.transform.localScale = new Vector3(tamanosCrater[i], tamanosCrater[i], 0.4f);
            Pintar(cr, crater);
            SinSombra(cr);
        }

        // estrellas: brillan (bloom) y titilan en Update
        Material blanco = ObtenerMaterial("Sombras_Estrella", new Color(1.5f, 1.5f, 1.25f), false);
        Random.InitState(7);   // siempre las mismas estrellas
        estrellas = new Transform[60];
        for (int i = 0; i < estrellas.Length; i++)
        {
            GameObject e = Primitiva(PrimitiveType.Sphere, "Estrella_" + i, grupo.transform);
            e.transform.localPosition = new Vector3(Random.Range(-55f, 55f), Random.Range(9f, 42f), 55f);
            e.transform.localScale = Vector3.one * Random.Range(0.15f, 0.35f);
            Pintar(e, blanco);
            SinSombra(e);
            estrellas[i] = e.transform;
        }

        // montanas: esferas gigantes aplastadas, medio enterradas.
        // Las de atras son mas claras, como cuando el aire se ve azul a lo lejos
        Material lejos = ObtenerMaterial("Sombras_MontanaLejos", new Color(0.17f, 0.15f, 0.33f), false);
        Material cerca = ObtenerMaterial("Sombras_MontanaCerca", new Color(0.09f, 0.13f, 0.22f), false);
        Montana("Montana_1", new Vector3(-32f, -6f, 50f), new Vector3(75f, 26f, 20f), lejos, grupo.transform);
        Montana("Montana_2", new Vector3(30f, -9f, 52f), new Vector3(85f, 30f, 20f), lejos, grupo.transform);
        Montana("Montana_3", new Vector3(-8f, -10f, 44f), new Vector3(60f, 22f, 16f), cerca, grupo.transform);
        Montana("Montana_4", new Vector3(26f, -11f, 42f), new Vector3(50f, 20f, 16f), cerca, grupo.transform);
    }

    void Montana(string nombre, Vector3 posicion, Vector3 tamano, Material material, Transform padre)
    {
        GameObject m = Primitiva(PrimitiveType.Sphere, nombre, padre);
        m.transform.localPosition = posicion;
        m.transform.localScale = tamano;
        Pintar(m, material);
        SinSombra(m);
    }


    // ---- el pasto, la tierra alrededor del fuego, matas y troncos para sentarse ----
    void ConstruirSuelo(Transform padre)
    {
        GameObject grupo = Grupo("Suelo", padre);

        GameObject suelo = Primitiva(PrimitiveType.Plane, "Pasto", grupo.transform);
        suelo.transform.localPosition = Vector3.zero;
        suelo.transform.localScale = new Vector3(12f, 1f, 12f);   // un plano mide 10 x 10: queda de 120 x 120
        Pintar(suelo, ObtenerMaterial("Sombras_Pasto", new Color(0.22f, 0.45f, 0.3f), true));
        suelo.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

        GameObject tierra = Primitiva(PrimitiveType.Cylinder, "Tierra", grupo.transform);
        tierra.transform.localPosition = new Vector3(POS_FOGATA.x, 0.005f, POS_FOGATA.z);
        tierra.transform.localScale = new Vector3(2.6f, 0.005f, 2.6f);
        Pintar(tierra, ObtenerMaterial("Sombras_Tierra", new Color(0.4f, 0.28f, 0.19f), true));
        tierra.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

        // matas de pasto: conitos verdes regados, lejos del fuego y de la carpa
        Mesh cono = ObtenerMalla("Cono", PERFIL_CONO, 7);
        Material mata = ObtenerMaterial("Sombras_Mata", new Color(0.28f, 0.58f, 0.35f), true);
        Random.InitState(11);
        int puestas = 0;
        int intentos = 0;
        while (puestas < 40 && intentos < 400)
        {
            intentos = intentos + 1;
            Vector3 p = new Vector3(Random.Range(-7f, 7f), 0f, Random.Range(-1.5f, 9f));

            bool cercaDelFuego = Vector3.Distance(p, POS_FOGATA) < 1.5f;
            bool enLaCarpa = Mathf.Abs(p.x) < ALTO_CARPA + 0.5f && p.z > Z_CARPA - 0.5f;
            if (cercaDelFuego || enLaCarpa)
            {
                continue;
            }

            GameObject m = ConMalla(cono, "Mata_" + puestas, grupo.transform);
            m.transform.localPosition = p;
            m.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), Random.Range(-12f, 12f));
            m.transform.localScale = new Vector3(0.1f, Random.Range(0.18f, 0.32f), 0.1f);
            Pintar(m, mata);
            SinSombra(m);
            puestas = puestas + 1;
        }

        // dos troncos para sentarse, a los lados de la fogata
        Material madera = ObtenerMaterial("Sombras_Madera", new Color(0.5f, 0.32f, 0.2f), true);
        float[] lados = { -1f, 1f };
        for (int i = 0; i < 2; i++)
        {
            GameObject t = Primitiva(PrimitiveType.Cylinder, "Banca_" + (i + 1), grupo.transform);
            t.transform.localPosition = new Vector3(2.4f * lados[i], 0.25f, POS_FOGATA.z - 0.2f);
            t.transform.localRotation = Quaternion.Euler(0f, 25f * lados[i], 90f);
            t.transform.localScale = new Vector3(0.5f, 0.85f, 0.5f);
            Pintar(t, madera);
            Contorno(t, 0.06f, null);
        }
    }


    // ---- la carpa: un cubo girado 45 grados y medio enterrado ----
    // la mitad de arriba es un triangulo, como una carpa de dos aguas vista de frente.
    // La cara de adelante (en z = Z_CARPA) es la tela donde caen las sombras
    void ConstruirCarpa(Transform padre)
    {
        GameObject grupo = Grupo("Carpa", padre);

        Material lona = ObtenerMaterial("Sombras_Lona", new Color(0.97f, 0.9f, 0.76f), true);       // crema
        Material borde = ObtenerMaterial("Sombras_Borde", new Color(0.75f, 0.3f, 0.22f), true);     // rojo ladrillo
        Material madera = ObtenerMaterial("Sombras_Madera", new Color(0.5f, 0.32f, 0.2f), true);
        Material cuerda = ObtenerMaterial("Sombras_Cuerda", new Color(0.85f, 0.78f, 0.62f), true);
        Material mostaza = ObtenerMaterial("Sombras_Mostaza", new Color(0.95f, 0.79f, 0.3f), true);  // amarillo del grupo

        // el lado de un cubo que, girado 45 grados, llega hasta ALTO_CARPA
        float lado = ALTO_CARPA * Mathf.Sqrt(2f);
        float fondo = 5f;

        GameObject tela = Primitiva(PrimitiveType.Cube, "Lona", grupo.transform);
        tela.transform.localPosition = new Vector3(0f, 0f, Z_CARPA + fondo / 2f);
        tela.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        tela.transform.localScale = new Vector3(lado, lado, fondo);
        Pintar(tela, lona);
        tela.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;   // solo recibe sombras
        Contorno(tela, 0.012f, null);

        // los dos bordes de la tela, en rojo ladrillo
        float mitad = ALTO_CARPA / 2f;
        Borde("Borde_izquierdo", new Vector3(-mitad, mitad, Z_CARPA - 0.04f), 45f, lado, borde, grupo.transform);
        Borde("Borde_derecho", new Vector3(mitad, mitad, Z_CARPA - 0.04f), -45f, lado, borde, grupo.transform);

        // el palo de arriba con un banderin amarillo
        GameObject palo = Primitiva(PrimitiveType.Cylinder, "Palo", grupo.transform);
        palo.transform.localPosition = new Vector3(0f, ALTO_CARPA + 0.35f, Z_CARPA);
        palo.transform.localScale = new Vector3(0.08f, 0.45f, 0.08f);
        Pintar(palo, madera);
        Contorno(palo, 0.15f, null);

        GameObject banderin = ConMalla(ObtenerBanderin(), "Banderin", grupo.transform);
        banderin.transform.localPosition = new Vector3(0.04f, ALTO_CARPA + 0.45f, Z_CARPA);
        banderin.transform.localScale = Vector3.one * 1.2f;
        Pintar(banderin, mostaza);

        // las cuerdas que la sostienen, con sus estacas
        Vector3 punta = new Vector3(0f, ALTO_CARPA - 0.1f, Z_CARPA);
        Vector3[] estacas = { new Vector3(-ALTO_CARPA - 1.6f, 0f, Z_CARPA + 0.8f), new Vector3(ALTO_CARPA + 1.6f, 0f, Z_CARPA + 0.8f) };
        for (int i = 0; i < estacas.Length; i++)
        {
            Palito("Cuerda_" + (i + 1), punta, estacas[i], 0.025f, cuerda, grupo.transform);

            GameObject estaca = Primitiva(PrimitiveType.Cube, "Estaca_" + (i + 1), grupo.transform);
            estaca.transform.localPosition = estacas[i] + new Vector3(0f, 0.1f, 0f);
            estaca.transform.localScale = new Vector3(0.07f, 0.25f, 0.07f);
            Pintar(estaca, madera);
        }
    }

    void Borde(string nombre, Vector3 posicion, float giro, float largo, Material material, Transform padre)
    {
        GameObject b = Primitiva(PrimitiveType.Cube, nombre, padre);
        b.transform.localPosition = posicion;
        b.transform.localRotation = Quaternion.Euler(0f, 0f, giro);
        b.transform.localScale = new Vector3(largo, 0.16f, 0.06f);
        Pintar(b, material);
        b.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
    }

    // un cilindro delgado de un punto a otro (cuerdas)
    void Palito(string nombre, Vector3 desde, Vector3 hasta, float grosor, Material material, Transform padre)
    {
        GameObject p = Primitiva(PrimitiveType.Cylinder, nombre, padre);
        Vector3 direccion = hasta - desde;
        p.transform.localPosition = (desde + hasta) / 2f;
        p.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direccion);
        p.transform.localScale = new Vector3(grosor, direccion.magnitude / 2f, grosor);   // un cilindro mide 2 de alto
        Pintar(p, material);
        SinSombra(p);
    }


    // ---- pinos de caricatura: un tronco y tres conos ----
    void ConstruirPinos(Transform padre)
    {
        GameObject grupo = Grupo("Pinos", padre);

        Mesh cono = ObtenerMalla("Cono", PERFIL_CONO, 9);
        Material tronco = ObtenerMaterial("Sombras_Madera", new Color(0.5f, 0.32f, 0.2f), true);
        Material[] verdes =
        {
            ObtenerMaterial("Sombras_Pino1", new Color(0.12f, 0.33f, 0.24f), true),
            ObtenerMaterial("Sombras_Pino2", new Color(0.15f, 0.4f, 0.27f), true),
            ObtenerMaterial("Sombras_Pino3", new Color(0.19f, 0.47f, 0.3f), true),
        };

        Vector3[] lugares =
        {
            new Vector3(-6.2f, 0f, 6.5f), new Vector3(-8.4f, 0f, 9.5f), new Vector3(-5.2f, 0f, 11f),
            new Vector3(-11f, 0f, 13f), new Vector3(-3f, 0f, 14f), new Vector3(6.4f, 0f, 7f),
            new Vector3(8.6f, 0f, 10.5f), new Vector3(5.2f, 0f, 12.5f), new Vector3(11.5f, 0f, 14.5f),
            new Vector3(2.8f, 0f, 15.5f), new Vector3(-14f, 0f, 9f), new Vector3(14.5f, 0f, 8.5f),
        };

        Random.InitState(3);
        for (int i = 0; i < lugares.Length; i++)
        {
            GameObject pino = Grupo("Pino_" + (i + 1), grupo.transform);
            pino.transform.localPosition = lugares[i];
            pino.transform.localScale = Vector3.one * Random.Range(0.85f, 1.35f);

            GameObject t = Primitiva(PrimitiveType.Cylinder, "Tronco", pino.transform);
            t.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            t.transform.localScale = new Vector3(0.3f, 0.5f, 0.3f);
            Pintar(t, tronco);
            Contorno(t, 0.08f, null);

            // tres pisos de ramas, cada uno mas pequeno
            float[] alturas = { 0.8f, 1.7f, 2.5f };
            float[] radios = { 1.3f, 1.05f, 0.75f };
            float[] altos = { 1.5f, 1.3f, 1.1f };
            for (int k = 0; k < 3; k++)
            {
                GameObject piso = ConMalla(cono, "Ramas_" + (k + 1), pino.transform);
                piso.transform.localPosition = new Vector3(0f, alturas[k], 0f);
                piso.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                piso.transform.localScale = new Vector3(radios[k], altos[k], radios[k]);
                Pintar(piso, verdes[k]);
                Contorno(piso, 0.05f, null);
            }
        }
    }


    // ---- la fogata: piedras, troncos, llamas, chispas y su luz naranja ----
    void ConstruirFogata(Transform padre)
    {
        GameObject fogata = Grupo("Fogata", padre);
        fogata.transform.localPosition = POS_FOGATA;

        // un circulo de piedras
        Material[] grises =
        {
            ObtenerMaterial("Sombras_Piedra1", new Color(0.55f, 0.55f, 0.6f), true),
            ObtenerMaterial("Sombras_Piedra2", new Color(0.45f, 0.46f, 0.52f), true),
        };
        int piedras = 11;
        for (int i = 0; i < piedras; i++)
        {
            float angulo = i * Mathf.PI * 2f / piedras;
            GameObject p = Primitiva(PrimitiveType.Sphere, "Piedra_" + (i + 1), fogata.transform);
            p.transform.localPosition = new Vector3(Mathf.Cos(angulo) * 0.62f, 0.07f, Mathf.Sin(angulo) * 0.62f);
            p.transform.localRotation = Quaternion.Euler(0f, -angulo * Mathf.Rad2Deg, 0f);
            p.transform.localScale = new Vector3(0.24f, 0.17f, 0.3f);
            Pintar(p, grises[i % 2]);
            Contorno(p, 0.12f, null);
        }

        // tres troncos cruzados
        Material madera = ObtenerMaterial("Sombras_Madera", new Color(0.5f, 0.32f, 0.2f), true);
        float[] girosTronco = { 30f, -30f, 90f };
        for (int i = 0; i < 3; i++)
        {
            GameObject tronco = Primitiva(PrimitiveType.Cylinder, "Tronco_" + (i + 1), fogata.transform);
            tronco.transform.localPosition = new Vector3(0f, 0.09f + 0.05f * i, 0f);
            tronco.transform.localRotation = Quaternion.Euler(0f, girosTronco[i], 90f);
            tronco.transform.localScale = new Vector3(0.12f, 0.42f, 0.12f);
            Pintar(tronco, madera);
            Contorno(tronco, 0.12f, null);
        }

        // las llamas: gotas hechas en torno. Son Unlit (brillan solas) y su color pasa de 1
        // para que el bloom las haga resplandecer. Una grande, dos rojas a los lados y un nucleo amarillo
        Mesh gota = ObtenerMalla("Llama", PERFIL_LLAMA, 10);
        string[] nombres = { "Llama_grande", "Llama_izquierda", "Llama_derecha", "Llama_nucleo" };
        Color[] colores =
        {
            new Color(2.2f, 0.95f, 0.18f),   // naranja
            new Color(2.0f, 0.5f, 0.12f),    // roja
            new Color(2.0f, 0.5f, 0.12f),    // roja
            new Color(2.4f, 2.0f, 0.7f),     // amarilla
        };
        Vector3[] lugares =
        {
            new Vector3(0f, 0.12f, 0f),
            new Vector3(-0.17f, 0.1f, 0.03f),
            new Vector3(0.17f, 0.1f, 0.03f),
            new Vector3(0f, 0.12f, -0.14f),   // adelante, hacia la camara
        };
        Vector3[] tamanos =
        {
            new Vector3(0.5f, 0.75f, 0.5f),
            new Vector3(0.32f, 0.5f, 0.32f),
            new Vector3(0.3f, 0.45f, 0.3f),
            new Vector3(0.26f, 0.42f, 0.26f),
        };
        float[] inclinaciones = { 0f, 18f, -18f, 0f };

        llamas = new Transform[nombres.Length];
        for (int i = 0; i < nombres.Length; i++)
        {
            GameObject llama = ConMalla(gota, nombres[i], fogata.transform);
            llama.transform.localPosition = lugares[i];
            llama.transform.localRotation = Quaternion.Euler(0f, 0f, inclinaciones[i]);
            llama.transform.localScale = tamanos[i];
            Pintar(llama, ObtenerMaterial("Sombras_" + nombres[i], colores[i], false));
            SinSombra(llama);   // el fuego no hace sombra
            llamas[i] = llama.transform;
        }

        // chispas que suben
        ConstruirChispas(fogata.transform);

        // la luz naranja del fuego: poca y sin sombras (las sombras las hace la linterna)
        GameObject luz = new GameObject("Luz_fogata");
        luz.transform.SetParent(fogata.transform);
        luz.transform.localPosition = new Vector3(0f, 0.6f, 0f);

        luzFogata = luz.AddComponent<Light>();
        luzFogata.type = LightType.Point;
        luzFogata.range = 3f;
        luzFogata.intensity = intensidadFogata;
        luzFogata.color = new Color(1f, 0.6f, 0.25f);
        luzFogata.shadows = LightShadows.None;
    }


    // ---- chispas: un sistema de particulas que sale hacia arriba ----
    void ConstruirChispas(Transform padre)
    {
        GameObject chispas = new GameObject("Chispas");
        chispas.transform.SetParent(padre);
        chispas.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        chispas.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);   // el cono de particulas sale por +z: lo giramos hacia arriba

        ParticleSystem ps = chispas.AddComponent<ParticleSystem>();

        var principal = ps.main;
        principal.startLifetime = 1.4f;
        principal.startSpeed = 0.9f;
        principal.startSize = 0.045f;
        principal.startColor = new Color(2f, 1.2f, 0.4f);
        principal.gravityModifier = -0.05f;
        principal.maxParticles = 60;
        principal.simulationSpace = ParticleSystemSimulationSpace.World;

        var emision = ps.emission;
        emision.rateOverTime = 14f;

        var forma = ps.shape;
        forma.shapeType = ParticleSystemShapeType.Cone;
        forma.angle = 14f;
        forma.radius = 0.15f;

        // se van apagando al subir
        var color = ps.colorOverLifetime;
        color.enabled = true;
        Gradient degradado = new Gradient();
        degradado.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        color.color = degradado;

        ParticleSystemRenderer render = chispas.GetComponent<ParticleSystemRenderer>();
        render.sharedMaterial = ObtenerMaterialParticulas();

        this.chispas = ps;
    }


    // ---- la linterna del explorador: la luz que hace las sombras en la carpa ----
    // esta en el suelo detras de la camara. No se ve, pero alumbra tus manos por detras
    // y proyecta su sombra en la tela
    void ConstruirLinterna(Transform padre)
    {
        GameObject linterna = new GameObject("Linterna");
        linterna.transform.SetParent(padre);
        linterna.transform.position = POS_LINTERNA;
        linterna.transform.LookAt(new Vector3(0f, 2f, Z_CARPA));

        luzLinterna = linterna.AddComponent<Light>();
        luzLinterna.type = LightType.Spot;
        luzLinterna.spotAngle = 50f;
        luzLinterna.innerSpotAngle = 30f;
        luzLinterna.range = 25f;
        luzLinterna.intensity = intensidadLinterna;
        luzLinterna.color = new Color(1f, 0.92f, 0.78f);   // blanco calido
        luzLinterna.shadows = LightShadows.Soft;
        luzLinterna.shadowStrength = 1f;
        luzLinterna.shadowNearPlane = 0.1f;
    }


    // ---- luciernagas: puntitos verdes que brillan y vuelan (se mueven en Update) ----
    void ConstruirLuciernagas(Transform padre)
    {
        GameObject grupo = Grupo("Luciernagas", padre);
        Material verde = ObtenerMaterial("Sombras_Luciernaga", new Color(1.6f, 2.2f, 0.6f), false);

        Random.InitState(5);
        luciernagas = new Transform[12];
        for (int i = 0; i < luciernagas.Length; i++)
        {
            GameObject l = Primitiva(PrimitiveType.Sphere, "Luciernaga_" + (i + 1), grupo.transform);
            float x = Random.Range(3f, 6f);
            if (i % 2 == 0)
            {
                x = -x;   // la mitad a cada lado, para que no tapen la carpa
            }
            l.transform.localPosition = new Vector3(x, Random.Range(0.4f, 2.4f), Random.Range(1f, 7f));
            l.transform.localScale = Vector3.one * 0.06f;
            Pintar(l, verde);
            SinSombra(l);
            luciernagas[i] = l.transform;
        }
    }


    // ---- las dos manos ----
    void ConstruirManos()
    {
        GameObject manos = new GameObject("Manos");
        manos.transform.SetParent(transform);

        manoIzquierda = ConstruirMano("ManoIzquierda", true, new Vector3(-0.4f, 1.1f, 0.4f), manos.transform);
        manoDerecha = ConstruirMano("ManoDerecha", false, new Vector3(0.4f, 1.1f, 0.4f), manos.transform);
    }


    // una mano de caricatura con la manga del explorador. Se ve desde atras (como tus manos
    // cuando miras la carpa), por eso las unas quedan hacia la camara.
    // Se arma en "unidades de mano" y al final se encoge con la escala
    ManoSombra ConstruirMano(string nombre, bool izquierda, Vector3 posicion, Transform padre)
    {
        Material piel = ObtenerMaterial("Sombras_Piel", colorPiel, true);
        Material una = ObtenerMaterial("Sombras_Una", new Color(1f, 0.86f, 0.82f), true);
        Material manga = ObtenerMaterial("Sombras_Manga", new Color(0.24f, 0.48f, 0.31f), true);   // verde del explorador
        Material puno = ObtenerMaterial("Sombras_Puno", new Color(0.36f, 0.62f, 0.42f), true);

        GameObject mano = new GameObject(nombre);
        mano.transform.SetParent(padre);
        mano.transform.position = posicion;
        mano.transform.localScale = Vector3.one * 0.17f;   // la mano mide unos 60 cm (es de caricatura)

        ManoSombra control = mano.AddComponent<ManoSombra>();
        control.esIzquierda = izquierda;
        control.pivotes = new Transform[5];
        control.articulaciones = new Transform[5];
        List<Renderer> partes = new List<Renderer>();

        // la palma: una esfera aplastada y gordita
        GameObject palma = Primitiva(PrimitiveType.Sphere, "Palma", mano.transform);
        palma.transform.localScale = new Vector3(2f, 2.1f, 0.95f);
        Pintar(palma, piel);
        partes.Add(palma.GetComponent<Renderer>());
        Contorno(palma, 0.06f, partes);

        // la muneca, el puno de la camisa y la manga
        GameObject muneca = Primitiva(PrimitiveType.Capsule, "Muneca", mano.transform);
        muneca.transform.localPosition = new Vector3(0f, -1.15f, 0f);
        muneca.transform.localScale = new Vector3(1.25f, 0.6f, 0.8f);
        Pintar(muneca, piel);
        partes.Add(muneca.GetComponent<Renderer>());
        Contorno(muneca, 0.08f, partes);

        GameObject anillo = Primitiva(PrimitiveType.Cylinder, "Puno", mano.transform);
        anillo.transform.localPosition = new Vector3(0f, -1.65f, 0f);
        anillo.transform.localScale = new Vector3(1.7f, 0.22f, 1.2f);
        Pintar(anillo, puno);
        partes.Add(anillo.GetComponent<Renderer>());
        Contorno(anillo, 0.06f, partes);

        GameObject brazo = Primitiva(PrimitiveType.Cylinder, "Manga", mano.transform);
        brazo.transform.localPosition = new Vector3(0f, -3f, 0f);
        brazo.transform.localScale = new Vector3(1.55f, 1.2f, 1.1f);
        Pintar(brazo, manga);
        partes.Add(brazo.GetComponent<Renderer>());
        Contorno(brazo, 0.05f, partes);

        // los dedos: pulgar, indice, medio, anular, menique.
        // Cada dedo tiene dos falanges: el "pivote" en la base y la "articulacion" en la mitad.
        // Al girar las dos, el dedo se enrosca hacia la palma
        string[] nombres = { "Pulgar", "Indice", "Medio", "Anular", "Menique" };
        Vector3[] bases =
        {
            new Vector3(-0.95f, -0.3f, -0.05f),   // pulgar, al lado
            new Vector3(-0.7f, 0.8f, 0f),         // los otros cuatro, arriba de la palma
            new Vector3(-0.23f, 0.88f, 0f),
            new Vector3(0.24f, 0.82f, 0f),
            new Vector3(0.68f, 0.65f, 0f),
        };
        float[] largo1 = { 0.7f, 0.85f, 0.95f, 0.88f, 0.65f };
        float[] largo2 = { 0.6f, 0.7f, 0.75f, 0.7f, 0.55f };
        float[] grueso = { 0.55f, 0.5f, 0.52f, 0.5f, 0.44f };
        float[] giros = { 55f, 6f, 0f, -5f, -12f };   // el pulgar sale inclinado y los dedos un poco abiertos

        for (int i = 0; i < 5; i++)
        {
            GameObject pivote = new GameObject("Pivote_" + nombres[i]);
            pivote.transform.SetParent(mano.transform);
            pivote.transform.localPosition = bases[i];
            pivote.transform.localRotation = Quaternion.Euler(0f, 0f, giros[i]);
            pivote.transform.localScale = Vector3.one;

            // primera falange. Una capsula de escala 1 mide 2 de alto y 1 de ancho:
            // asi, con el grosor sumado, sus puntas redondas tapan las uniones
            GameObject f1 = Primitiva(PrimitiveType.Capsule, nombres[i] + "_1", pivote.transform);
            f1.transform.localPosition = new Vector3(0f, largo1[i] / 2f, 0f);
            f1.transform.localScale = new Vector3(grueso[i], (largo1[i] + grueso[i]) / 2f, grueso[i]);
            Pintar(f1, piel);
            partes.Add(f1.GetComponent<Renderer>());
            Contorno(f1, 0.14f, partes);

            GameObject articulacion = new GameObject("Articulacion_" + nombres[i]);
            articulacion.transform.SetParent(pivote.transform);
            articulacion.transform.localPosition = new Vector3(0f, largo1[i], 0f);
            articulacion.transform.localRotation = Quaternion.identity;
            articulacion.transform.localScale = Vector3.one;

            // segunda falange, con su una
            float g2 = grueso[i] * 0.95f;
            GameObject f2 = Primitiva(PrimitiveType.Capsule, nombres[i] + "_2", articulacion.transform);
            f2.transform.localPosition = new Vector3(0f, largo2[i] / 2f, 0f);
            f2.transform.localScale = new Vector3(g2, (largo2[i] + g2) / 2f, g2);
            Pintar(f2, piel);
            partes.Add(f2.GetComponent<Renderer>());
            Contorno(f2, 0.14f, partes);

            GameObject u = Primitiva(PrimitiveType.Sphere, "Una_" + nombres[i], articulacion.transform);
            u.transform.localPosition = new Vector3(0f, largo2[i] - 0.05f, -g2 * 0.42f);
            u.transform.localScale = new Vector3(g2 * 0.6f, g2 * 0.5f, g2 * 0.15f);
            Pintar(u, una);
            SinSombra(u);
            partes.Add(u.GetComponent<Renderer>());

            control.pivotes[i] = pivote.transform;
            control.articulaciones[i] = articulacion.transform;
        }

        control.partes = partes.ToArray();
        return control;
    }


    // ---- post-proceso: bloom (el resplandor del fuego y la luna) y vineta (bordes oscuros) ----
    void ConstruirPostProceso()
    {
        GameObject volumen = new GameObject("Volumen");
        volumen.transform.SetParent(transform);

        Volume v = volumen.AddComponent<Volume>();
        v.isGlobal = true;
        v.sharedProfile = ObtenerPerfil();
    }


    // =====================================================================
    // AYUDANTES
    // =====================================================================

    // ---- una figura basica sin fisica, hija de padre ----
    GameObject Primitiva(PrimitiveType tipo, string nombre, Transform padre)
    {
        GameObject g = GameObject.CreatePrimitive(tipo);
        g.name = nombre;
        Borrar(g.GetComponent<Collider>());   // no hace falta fisica
        g.transform.SetParent(padre);
        g.transform.localPosition = Vector3.zero;
        g.transform.localRotation = Quaternion.identity;
        return g;
    }

    // ---- un objeto con una malla hecha por nosotros ----
    GameObject ConMalla(Mesh malla, string nombre, Transform padre)
    {
        GameObject g = new GameObject(nombre);
        g.transform.SetParent(padre);
        g.transform.localPosition = Vector3.zero;
        g.transform.localRotation = Quaternion.identity;
        g.AddComponent<MeshFilter>().sharedMesh = malla;
        g.AddComponent<MeshRenderer>();
        return g;
    }

    // ---- un objeto vacio para agrupar ----
    GameObject Grupo(string nombre, Transform padre)
    {
        GameObject g = new GameObject(nombre);
        g.transform.SetParent(padre);
        g.transform.localPosition = Vector3.zero;
        g.transform.localRotation = Quaternion.identity;
        return g;
    }

    void Pintar(GameObject g, Material material)
    {
        g.GetComponent<Renderer>().sharedMaterial = material;
    }

    void SinSombra(GameObject g)
    {
        g.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
    }


    // ---- el contorno negro de caricatura ("casco invertido") ----
    // una copia un poquito mas grande de la pieza que solo pinta su cara de ATRAS:
    // por delante la tapa la pieza y solo asoma el borde, como una linea de dibujo
    void Contorno(GameObject pieza, float grosor, List<Renderer> lista)
    {
        GameObject linea = new GameObject("Contorno");
        linea.transform.SetParent(pieza.transform);
        linea.transform.localPosition = Vector3.zero;
        linea.transform.localRotation = Quaternion.identity;
        linea.transform.localScale = Vector3.one * (1f + grosor);

        linea.AddComponent<MeshFilter>().sharedMesh = pieza.GetComponent<MeshFilter>().sharedMesh;
        MeshRenderer r = linea.AddComponent<MeshRenderer>();
        r.sharedMaterial = ObtenerMaterialContorno();
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;

        if (lista != null)
        {
            lista.Add(r);
        }
    }


    // ---- busca un material en Assets/Materiales; si no existe, lo crea ----
    // conLuz = true: le afecta la luz y recibe sombras (Lit). false: brilla solo (Unlit).
    // Un color mayor que 1 brilla con el bloom
    Material ObtenerMaterial(string nombre, Color color, bool conLuz, Texture textura = null)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            string ruta = "Assets/Materiales/" + nombre + ".mat";
            Material existente = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (existente != null)
            {
                // si ya existia (de una construccion anterior), se le pone el color de ahora
                if (existente.color != color)
                {
                    existente.color = color;
                    MarcarCambio(existente);
                }
                return existente;
            }

            CrearCarpeta("Assets/Materiales");
            Material nuevo = CrearMaterial(color, conLuz, textura);
            AssetDatabase.CreateAsset(nuevo, ruta);
            return nuevo;
        }
#endif
        return CrearMaterial(color, conLuz, textura);   // durante el juego, un material temporal
    }

    Material CrearMaterial(Color color, bool conLuz, Texture textura)
    {
        Shader shader;
        if (conLuz)
        {
            shader = Shader.Find("Universal Render Pipeline/Lit");
        }
        else
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        Material mat = new Material(shader);
        mat.color = color;
        if (textura != null)
        {
            mat.mainTexture = textura;
        }
        if (conLuz)
        {
            mat.SetFloat("_Smoothness", 0f);   // mate, sin brillos: se ve mas de caricatura
        }
        return mat;
    }

    // el material del contorno: oscuro, sin luz, y pinta solo la cara de atras (_Cull = 1)
    Material ObtenerMaterialContorno()
    {
        Material m = ObtenerMaterial("Sombras_Contorno", new Color(0.12f, 0.07f, 0.1f), false);
        if (m.GetFloat("_Cull") != 1f)
        {
            m.SetFloat("_Cull", 1f);   // 0 = las dos caras, 1 = solo atras, 2 = solo adelante
            MarcarCambio(m);
        }
        return m;
    }

    // el material de las chispas (las particulas necesitan su propio shader)
    Material ObtenerMaterialParticulas()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            string ruta = "Assets/Materiales/Sombras_Chispa.mat";
            Material existente = AssetDatabase.LoadAssetAtPath<Material>(ruta);
            if (existente != null)
            {
                return existente;
            }
            CrearCarpeta("Assets/Materiales");
            Material nuevo = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            AssetDatabase.CreateAsset(nuevo, ruta);
            return nuevo;
        }
#endif
        return new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
    }


    // ---- una malla "en torno": gira un perfil (radio, altura) alrededor del eje vertical ----
    // Cada cara lleva sus propios vertices, asi la luz la pinta plana (estilo low poly)
    Mesh ObtenerMalla(string nombre, Vector2[] perfil, int lados)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            string ruta = "Assets/Modelos/Sombras_" + nombre + ".asset";
            Mesh existente = AssetDatabase.LoadAssetAtPath<Mesh>(ruta);
            if (existente != null)
            {
                return existente;
            }
            CrearCarpeta("Assets/Modelos");
            Mesh nueva = Tornear(nombre, perfil, lados);
            AssetDatabase.CreateAsset(nueva, ruta);
            return nueva;
        }
#endif
        return Tornear(nombre, perfil, lados);
    }

    Mesh Tornear(string nombre, Vector2[] perfil, int lados)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangulos = new List<int>();

        for (int j = 0; j < lados; j++)
        {
            float a0 = j * Mathf.PI * 2f / lados;
            float a1 = (j + 1) * Mathf.PI * 2f / lados;

            for (int k = 0; k < perfil.Length - 1; k++)
            {
                Vector2 p0 = perfil[k];
                Vector2 p1 = perfil[k + 1];

                int n = vertices.Count;
                vertices.Add(new Vector3(p0.x * Mathf.Cos(a0), p0.y, p0.x * Mathf.Sin(a0)));   // abajo, este lado
                vertices.Add(new Vector3(p0.x * Mathf.Cos(a1), p0.y, p0.x * Mathf.Sin(a1)));   // abajo, el siguiente
                vertices.Add(new Vector3(p1.x * Mathf.Cos(a0), p1.y, p1.x * Mathf.Sin(a0)));   // arriba, este lado
                vertices.Add(new Vector3(p1.x * Mathf.Cos(a1), p1.y, p1.x * Mathf.Sin(a1)));   // arriba, el siguiente

                // dos triangulos por cuadro, en el orden que Unity ve "de frente" desde afuera
                triangulos.Add(n);
                triangulos.Add(n + 2);
                triangulos.Add(n + 3);
                triangulos.Add(n);
                triangulos.Add(n + 3);
                triangulos.Add(n + 1);
            }
        }

        Mesh malla = new Mesh();
        malla.name = "Sombras_" + nombre;
        malla.SetVertices(vertices);
        malla.SetTriangles(triangulos, 0);
        malla.RecalculateNormals();
        malla.RecalculateBounds();
        return malla;
    }

    // el banderin: un triangulo que se ve por las dos caras
    Mesh ObtenerBanderin()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            string ruta = "Assets/Modelos/Sombras_Banderin.asset";
            Mesh existente = AssetDatabase.LoadAssetAtPath<Mesh>(ruta);
            if (existente != null)
            {
                return existente;
            }
            CrearCarpeta("Assets/Modelos");
            Mesh nueva = CrearBanderin();
            AssetDatabase.CreateAsset(nueva, ruta);
            return nueva;
        }
#endif
        return CrearBanderin();
    }

    Mesh CrearBanderin()
    {
        Mesh malla = new Mesh();
        malla.name = "Sombras_Banderin";
        malla.vertices = new Vector3[]
        {
            new Vector3(0f, 0f, 0f), new Vector3(0f, 0.32f, 0f), new Vector3(0.55f, 0.16f, 0f),   // cara de adelante
            new Vector3(0f, 0f, 0f), new Vector3(0f, 0.32f, 0f), new Vector3(0.55f, 0.16f, 0f),   // cara de atras
        };
        malla.triangles = new int[] { 0, 1, 2, 3, 5, 4 };
        malla.RecalculateNormals();
        malla.RecalculateBounds();
        return malla;
    }


    // ---- la textura del cielo: un degradado vertical ----
    Texture2D ObtenerDegradado()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            string ruta = "Assets/Materiales/Sombras_Degradado.asset";
            Texture2D existente = AssetDatabase.LoadAssetAtPath<Texture2D>(ruta);
            if (existente != null)
            {
                return existente;
            }
            CrearCarpeta("Assets/Materiales");
            Texture2D nueva = CrearDegradado();
            AssetDatabase.CreateAsset(nueva, ruta);
            return nueva;
        }
#endif
        return CrearDegradado();
    }

    Texture2D CrearDegradado()
    {
        Color horizonte = new Color(0.36f, 0.22f, 0.48f);   // morado
        Color medio = new Color(0.1f, 0.12f, 0.32f);        // azul
        Color arriba = new Color(0.02f, 0.03f, 0.1f);       // casi negro

        int alto = 128;
        Texture2D tex = new Texture2D(2, alto);
        tex.name = "Sombras_Degradado";
        tex.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < alto; y++)
        {
            // el cuadro del cielo empieza bajo el suelo: el horizonte queda en el 25 % de abajo
            float v = y / (float)(alto - 1);
            Color c;
            if (v < 0.25f)
            {
                c = horizonte;
            }
            else
            {
                float t = (v - 0.25f) / 0.75f;
                if (t < 0.4f)
                {
                    c = Color.Lerp(horizonte, medio, t / 0.4f);
                }
                else
                {
                    c = Color.Lerp(medio, arriba, (t - 0.4f) / 0.6f);
                }
            }
            tex.SetPixel(0, y, c);
            tex.SetPixel(1, y, c);
        }
        tex.Apply();
        return tex;
    }


    // ---- el perfil del post-proceso (bloom y vineta) ----
    VolumeProfile ObtenerPerfil()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            string ruta = "Assets/Settings/Sombras_Volumen.asset";
            VolumeProfile existente = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ruta);
            if (existente != null)
            {
                return existente;
            }

            VolumeProfile nuevo = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(nuevo, ruta);
            LlenarPerfil(nuevo, true);
            return nuevo;
        }
#endif
        VolumeProfile temporal = ScriptableObject.CreateInstance<VolumeProfile>();
        LlenarPerfil(temporal, false);
        return temporal;
    }

    void LlenarPerfil(VolumeProfile perfil, bool guardar)
    {
        // solo brilla lo que pasa de 1.3: el fuego, la luna, las estrellas y las luciernagas
        Bloom bloom = perfil.Add<Bloom>(true);
        bloom.threshold.Override(1.3f);
        bloom.intensity.Override(1.2f);
        bloom.scatter.Override(0.7f);

        Vignette vineta = perfil.Add<Vignette>(true);
        vineta.intensity.Override(0.32f);

#if UNITY_EDITOR
        if (guardar)
        {
            // los efectos se guardan DENTRO del archivo del perfil
            AssetDatabase.AddObjectToAsset(bloom, perfil);
            AssetDatabase.AddObjectToAsset(vineta, perfil);
            EditorUtility.SetDirty(perfil);
        }
#endif
    }


    // ---- avisa a Unity que un archivo cambio, para que lo guarde ----
    void MarcarCambio(Object cosa)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(cosa);
        }
#endif
    }

    void CrearCarpeta(string ruta)
    {
#if UNITY_EDITOR
        if (!AssetDatabase.IsValidFolder(ruta))
        {
            AssetDatabase.CreateFolder("Assets", ruta.Substring("Assets/".Length));
        }
#endif
    }


    // ---- borrar un objeto: en el editor y en el juego se hace distinto ----
    void Borrar(Object cosa)
    {
        if (Application.isPlaying)
        {
            Destroy(cosa);
        }
        else
        {
            DestroyImmediate(cosa);
        }
    }


    // ---- lo que el juego lee de tus manos, abajo al centro y pequeno ----
    // es para probar (sobre todo si MediaPipe ve bien el pulgar). Se apaga con "mostrarDatos"
    void OnGUI()
    {
        // JUEGO UNIFICADO: el texto de los dedos es solo para probar en el editor (en el juego tapaba las instrucciones)
        if (!mostrarDatos || !Application.isEditor)
        {
            return;
        }

        int alto = Screen.height;
        float ancho = alto * 0.7f;
        float x = (Screen.width - ancho) / 2f;
        float y = alto - alto * 0.075f;

        GUIStyle normal = new GUIStyle(GUI.skin.label);
        normal.fontSize = Mathf.RoundToInt(alto * 0.02f);
        normal.normal.textColor = Color.white;
        normal.alignment = TextAnchor.MiddleCenter;

        GUI.Box(new Rect(x, y, ancho, alto * 0.065f), "");
        GUI.Label(new Rect(x, y, ancho, alto * 0.033f),
                  "Izq: " + NombreGesto(manoIzquierda) + " (" + Dedos(manoIzquierda.arriba) + ")   ·   Der: " +
                  NombreGesto(manoDerecha) + " (" + Dedos(manoDerecha.arriba) + ")", normal);

        normal.normal.textColor = new Color(1f, 1f, 1f, 0.7f);
        GUI.Label(new Rect(x, y + alto * 0.03f, ancho, alto * 0.033f),
                  "Dedos de pulgar a meñique: 1 = arriba, 0 = abajo", normal);
    }
}
