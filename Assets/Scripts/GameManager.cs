using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    [Header("Referencias del Jugador y Puntos")]
    public RectTransform playerCat;
    public RectTransform puntoCentro;
    public RectTransform puntoAdelante;
    public RectTransform puntoAtras;
    public RectTransform puntoIzquierda;
    public RectTransform puntoDerecha;
    public float moveSpeed = 5f;

    [Header("Referencias de UI")]
    public GameObject startButton;
    public TextMeshProUGUI textMisionActual;
    public TextMeshProUGUI textInstruccion;
    public Image progressBarFill;
    public TextMeshProUGUI textPorcentaje;
    public TextMeshProUGUI textDeteccionCam;

    [Header("Configuración de Progreso")]
    public float tiempoRequerido = 3f;
    public float tiempoEnCentro = 2f; // Tiempo que debe esperar en el centro
    private float tiempoActual = 0f;

    private HandStateLogger logger;
    private Vector2 targetPosition;
    private bool juegoIniciado = false;
    private bool esperandoRegresoAlCentro = false; // Nueva variable de control

    // Misiones
    private List<HandStateLogger.MoveState> misiones = new List<HandStateLogger.MoveState>()
    {
        HandStateLogger.MoveState.Adelante,
        HandStateLogger.MoveState.Izquierda,
        HandStateLogger.MoveState.Atras,
        HandStateLogger.MoveState.Derecha
    };
    private int misionActualIndex = 0;

    void Start()
    {
        logger = FindObjectOfType<HandStateLogger>();
        // Si tienes el puntoCentro asignado, el target inicial será ese
        if (puntoCentro != null) targetPosition = puntoCentro.anchoredPosition;

        progressBarFill.fillAmount = 0;
        textPorcentaje.text = "0%";
        textMisionActual.text = "Presiona Comenzar para iniciar la terapia.";
        textInstruccion.text = "Esperando...";
        startButton.SetActive(true);
    }

    public void IniciarJuego()
    {
        juegoIniciado = true;
        esperandoRegresoAlCentro = false;
        startButton.SetActive(false);
        misionActualIndex = 0;
        ActualizarTextosMision();
    }

    void Update()
    {
        // --- NUEVO: Iniciar con la barra espaciadora ---
        if (!juegoIniciado && Input.GetKeyDown(KeyCode.Space))
        {
            IniciarJuego();
        }
        // ----------------------------------------------

        if (logger == null) return;

        HandStateLogger.MoveState estadoManos = logger.currentState;
        if (textDeteccionCam != null)
            textDeteccionCam.text = "La cámara detecta: " + estadoManos.ToString();

        MoverGatoVisual(estadoManos);

        if (juegoIniciado)
        {
            if (esperandoRegresoAlCentro)
            {
                ComprobarRegresoAlCentro(estadoManos);
            }
            else if (misionActualIndex < misiones.Count)
            {
                ComprobarProgresoMision(estadoManos);
            }
        }
    }

    void ComprobarProgresoMision(HandStateLogger.MoveState estadoManos)
    {
        HandStateLogger.MoveState misionObjetivo = misiones[misionActualIndex];

        if (estadoManos == misionObjetivo)
        {
            AumentarProgreso(tiempoRequerido);
            if (tiempoActual >= tiempoRequerido)
            {
                CompletarMision();
            }
        }
        else
        {
            DisminuirProgreso(tiempoRequerido);
        }
    }

    void ComprobarRegresoAlCentro(HandStateLogger.MoveState estadoManos)
    {
        // El estado Nulo o Centro cuenta como estar en el centro
        if (estadoManos == HandStateLogger.MoveState.Centro || estadoManos == HandStateLogger.MoveState.Nulo)
        {
            AumentarProgreso(tiempoEnCentro);
            if (tiempoActual >= tiempoEnCentro)
            {
                // Termina la espera en el centro, pasa a la siguiente misión
                esperandoRegresoAlCentro = false;
                tiempoActual = 0f;
                progressBarFill.fillAmount = 0f;
                textPorcentaje.text = "0%";
                ActualizarTextosMision();
            }
        }
        else
        {
            DisminuirProgreso(tiempoEnCentro);
        }
    }

    void AumentarProgreso(float metaTiempo)
    {
        tiempoActual += Time.deltaTime;
        float progreso = Mathf.Clamp01(tiempoActual / metaTiempo);
        progressBarFill.fillAmount = progreso;
        textPorcentaje.text = Mathf.RoundToInt(progreso * 100) + "%";
    }

    void DisminuirProgreso(float metaTiempo)
    {
        if (tiempoActual > 0)
        {
            tiempoActual -= Time.deltaTime * 2f;
            tiempoActual = Mathf.Max(0, tiempoActual);
            float progreso = Mathf.Clamp01(tiempoActual / metaTiempo);
            progressBarFill.fillAmount = progreso;
            textPorcentaje.text = Mathf.RoundToInt(progreso * 100) + "%";
        }
    }

    void CompletarMision()
    {
        tiempoActual = 0f;
        progressBarFill.fillAmount = 0f;
        misionActualIndex++;

        if (misionActualIndex < misiones.Count)
        {
            // Completó una dirección, ahora debe volver al centro
            esperandoRegresoAlCentro = true;
            textMisionActual.text = "¡Muy bien! Objetivo completado.";
            textInstruccion.text = "Vuelve al Centro y mantén la posición para continuar.";
        }
        else
        {
            // Terminó el juego
            juegoIniciado = false;
            textMisionActual.text = "¡Lo lograste!\nHas completado todas las direcciones.";
            textInstruccion.text = "¡Excelente trabajo!";
            progressBarFill.fillAmount = 1f;
            textPorcentaje.text = "100%";
        }
    }

    void ActualizarTextosMision()
    {
        HandStateLogger.MoveState siguiente = misiones[misionActualIndex];

        switch (siguiente)
        {
            case HandStateLogger.MoveState.Adelante:
                textMisionActual.text = "Objetivo: Adelante.";
                textInstruccion.text = "Muévete hacia adelante manteniendo el gesto de los 3 dedos.";
                break;
            case HandStateLogger.MoveState.Izquierda:
                textMisionActual.text = "Objetivo: Izquierda.";
                textInstruccion.text = "Muévete hacia la izquierda levantando solo tu pulgar.";
                break;
            case HandStateLogger.MoveState.Atras:
                textMisionActual.text = "Objetivo: Atrás.";
                textInstruccion.text = "Muévete hacia atrás haciendo el símbolo de paz.";
                break;
            case HandStateLogger.MoveState.Derecha:
                textMisionActual.text = "Objetivo: Derecha.";
                textInstruccion.text = "Muévete hacia la derecha levantando tu meñique.";
                break;
        }
    }

    void MoverGatoVisual(HandStateLogger.MoveState estado)
    {
        // Usa los RectTransforms que asignes en el inspector en lugar de calcular distancias matemáticas
        switch (estado)
        {
            case HandStateLogger.MoveState.Adelante:
                if (puntoAdelante != null) targetPosition = puntoAdelante.anchoredPosition;
                break;
            case HandStateLogger.MoveState.Atras:
                if (puntoAtras != null) targetPosition = puntoAtras.anchoredPosition;
                break;
            case HandStateLogger.MoveState.Izquierda:
                if (puntoIzquierda != null) targetPosition = puntoIzquierda.anchoredPosition;
                break;
            case HandStateLogger.MoveState.Derecha:
                if (puntoDerecha != null) targetPosition = puntoDerecha.anchoredPosition;
                break;
            case HandStateLogger.MoveState.Centro:
            case HandStateLogger.MoveState.Nulo:
                if (puntoCentro != null) targetPosition = puntoCentro.anchoredPosition;
                break;
        }

        playerCat.anchoredPosition = Vector2.Lerp(playerCat.anchoredPosition, targetPosition, Time.deltaTime * moveSpeed);
    }
}