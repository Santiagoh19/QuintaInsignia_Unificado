using UnityEngine;

// Una mano 3D estilo caricatura, con la manga del explorador: SE VE y ademas hace sombra en la carpa.
// Lee los dedos de UNA mano (izquierda o derecha) que manda MediaPipe por FingersMove,
// dobla los dedos que estan abajo (cada dedo tiene dos falanges) y sigue la posicion de tu mano.
// La construye SombrasJuego con "Construir campamento y manos"; tambien la puedes editar a mano.
public class ManoSombra : MonoBehaviour
{
    [Header("Que mano es")]
    public bool esIzquierda = false;
    [Tooltip("Si el pulgar sale del lado equivocado, marca esto")]
    public bool voltear = false;

    [Header("Partes (las pone el boton de construir)")]
    public Transform[] pivotes;        // la base de cada dedo: pulgar, indice, medio, anular, menique
    public Transform[] articulaciones; // la mitad de cada dedo (la segunda falange)
    public Renderer[] partes;          // todo lo que se ve: se apaga cuando no esta la mano

    [Header("Dedos")]
    public float anguloDoblado = 85f;  // cuanto gira CADA falange al doblarse (dos falanges = casi 170)
    public float rapidez = 12f;        // que tan rapido se dobla o se levanta

    [Header("Donde se mueve la mano (en el mundo)")]
    // la camara de MediaPipe va de 0 a 1; aqui se dice a que zona del mundo corresponde
    public Vector2 rangoX = new Vector2(-0.8f, 0.8f);
    public Vector2 rangoY = new Vector2(0.8f, 1.55f);
    public float profundidad = 0.4f;   // z del plano de las manos (entre la linterna y la carpa)
    public float suavizado = 0.08f;    // segundos que tarda en alcanzar la mano real (contra el temblor)
    public bool espejoX = true;        // igual que en FingersMove
    public bool espejoY = true;

    [Header("Caricatura")]
    public float inclinacionMax = 15f; // cuanto se ladea la mano al moverse rapido (grados)
    public float rapidezAparecer = 10f; // que tan rapido aparece o desaparece (efecto "pop")

    // ---- lo que leen los demas scripts ----
    [HideInInspector] public bool[] arriba = new bool[5];   // que dedos estan levantados
    [HideInInspector] public bool presente = false;          // si la mano esta en la camara

    float[] angulos = new float[5];       // el angulo actual de cada dedo (para moverlo poco a poco)
    Quaternion[] giroBase = new Quaternion[5];
    Vector3 objetivo;                      // a donde va la mano
    Vector3 velocidad;                     // la usa SmoothDamp
    float tiempoSinMano = 1f;             // segundos sin dedos levantados
    float tamanoBase;                      // la escala que le puso el constructor
    float aparicion = 0f;                  // 0 = escondida, 1 = completa
    float inclinacion = 0f;


    void Start()
    {
        // el giro de cada dedo cuando esta derecho (el pulgar sale inclinado)
        for (int i = 0; i < pivotes.Length; i++)
        {
            giroBase[i] = pivotes[i].localRotation;
        }

        tamanoBase = Mathf.Abs(transform.localScale.y);
        objetivo = transform.position;

        // empieza escondida hasta que llegue la mano
        Mostrar(false);
    }


    void Update()
    {
        LeerMano();
        Mover();
        DoblarDedos();
    }


    // ---- lee los dedos de esta mano y calcula a donde va ----
    void LeerMano()
    {
        var mano = FingersMove.GetState();
        if (mano == null)
        {
            return;   // todavia no hay datos de MediaPipe
        }

        // en mano.up y mano.canvas01: 0..4 = izquierda, 5..9 = derecha (el primero es el pulgar)
        int inicio = 5;
        if (esIzquierda)
        {
            inicio = 0;
        }

        int levantados = 0;
        Vector2 suma = Vector2.zero;

        lock (mano)   // el mismo candado que usa FingersMove al escribir
        {
            for (int i = 0; i < 5; i++)
            {
                arriba[i] = mano.up[inicio + i];

                // el emisor de Python SOLO manda los dedos levantados:
                // la posicion de un dedo doblado es vieja, por eso no se suma
                if (arriba[i])
                {
                    levantados = levantados + 1;
                    suma = suma + mano.canvas01[inicio + i];
                }
            }
        }

        // el puño se ve igual que "no hay mano" (0 dedos), asi que la mano esta si hay algun dedo arriba.
        // Se espera un momento antes de esconderla, para que no parpadee
        if (levantados > 0)
        {
            tiempoSinMano = 0f;

            // la posicion: el promedio de las puntas de los dedos levantados
            Vector2 p = suma / levantados;
            if (espejoX)
            {
                p.x = 1f - p.x;
            }
            if (espejoY)
            {
                p.y = 1f - p.y;
            }

            objetivo = new Vector3(
                Mathf.Lerp(rangoX.x, rangoX.y, p.x),
                Mathf.Lerp(rangoY.x, rangoY.y, p.y),
                profundidad);
        }
        else
        {
            tiempoSinMano = tiempoSinMano + Time.deltaTime;
        }

        presente = tiempoSinMano < 0.3f;
    }


    // ---- mover, ladear y hacer aparecer la mano ----
    void Mover()
    {
        // va hacia donde esta la real, suave
        transform.position = Vector3.SmoothDamp(transform.position, objetivo, ref velocidad, suavizado);

        // se ladea un poco segun la velocidad de lado, como en los dibujos animados
        float meta = Mathf.Clamp(-velocidad.x * 6f, -inclinacionMax, inclinacionMax);
        inclinacion = Mathf.Lerp(inclinacion, meta, 8f * Time.deltaTime);
        transform.rotation = Quaternion.Euler(0f, 0f, inclinacion);

        // aparece creciendo y desaparece encogiendose ("pop")
        float metaAparicion = 0f;
        if (presente)
        {
            metaAparicion = 1f;
        }
        aparicion = Mathf.Lerp(aparicion, metaAparicion, rapidezAparecer * Time.deltaTime);
        Mostrar(aparicion > 0.02f);

        // la izquierda es la derecha en espejo (el pulgar queda al otro lado)
        float lado = 1f;
        if (esIzquierda != voltear)
        {
            lado = -1f;
        }
        float t = tamanoBase * aparicion;
        transform.localScale = new Vector3(t * lado, t, t);
    }


    // ---- doblar o levantar cada dedo, poco a poco ----
    void DoblarDedos()
    {
        for (int i = 0; i < pivotes.Length; i++)
        {
            float meta = anguloDoblado;
            if (arriba[i])
            {
                meta = 0f;
            }

            angulos[i] = Mathf.Lerp(angulos[i], meta, rapidez * Time.deltaTime);

            // las dos falanges giran hacia la palma: asi el dedo se enrosca en vez de quedar tieso
            pivotes[i].localRotation = giroBase[i] * Quaternion.Euler(angulos[i], 0f, 0f);
            articulaciones[i].localRotation = Quaternion.Euler(angulos[i], 0f, 0f);
        }
    }


    // prende o apaga lo que se ve (sin renderers tampoco hay sombra)
    void Mostrar(bool mostrar)
    {
        for (int i = 0; i < partes.Length; i++)
        {
            partes[i].enabled = mostrar;
        }
    }
}
