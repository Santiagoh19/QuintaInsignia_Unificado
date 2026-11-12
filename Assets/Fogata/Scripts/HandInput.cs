using UnityEngine;

public enum LadoMano { Cualquiera, Derecha, Izquierda }

/// <summary>
/// Convierte los datos que llegan de MediaPipe (FingersMove) en un "cursor" con pinza.
/// - Cursor  = entre el pulgar y el dedo de la pinza (o la punta de ese dedo).
/// - Pinza   = pulgar y `dedoPinza` juntos (índice por defecto; con histéresis para que no parpadee).
/// - Si no hay mano y "permitirMouse" está activo, usa el mouse (clic = pinza) para probar sin cámara.
/// No requiere cambiar el script de Python.
/// </summary>
[DefaultExecutionOrder(-50)]
public class HandInput : MonoBehaviour
{
    public static HandInput Instance { get; private set; }

    [Header("Referencias")]
    [Tooltip("RectTransform del Canvas (el mismo que usa FingersMove)")]
    public RectTransform area;

    [Header("Mano")]
    [Tooltip("Con qué mano juega el paciente. Si se elige Derecha o Izquierda, el dibujo queda fijo y, si hay dos manos " +
             "en cámara, se sigue esa. Si hay una sola mano se sigue igual aunque MediaPipe la etiquete mal")]
    public LadoMano mano = LadoMano.Cualquiera;
    [Tooltip("Modo Cualquiera: segundos que MediaPipe tiene que insistir en el otro lado para voltear el dibujo de la mano")]
    public float tiempoCambioLado = 0.8f;
    [Tooltip("El Python ya voltea la imagen como espejo. Si el cursor se mueve al revés, marca esta casilla (o presiona M en Play)")]
    public bool mirrorX = false;
    [Tooltip("Debe quedar activo: la Y de la cámara crece hacia abajo")]
    public bool mirrorY = true;
    [Tooltip("Ignora este borde de la cámara (0.1 = 10%) para que no haya que estirar tanto el brazo para llegar a las esquinas")]
    [Range(0f, 0.3f)] public float margenCamara = 0.12f;
    [Tooltip("El cursor va entre la punta del pulgar y la del índice (donde se cierra la pinza). Si está apagado, va en la punta del índice")]
    public bool cursorEnPinza = true;

    [Header("Suavizado")]
    [Tooltip("Qué tan rápido sigue el cursor a la mano. Menor = más suave (útil con temblor)")]
    [Range(2f, 40f)] public float seguimiento = 16f;
    [Tooltip("La cámara manda la mano 15-30 veces por segundo y el juego dibuja a 60: entre dato y dato se predice " +
             "hacia dónde va la mano para que el cursor se deslice en vez de avanzar a saltos")]
    public bool prediccion = true;

    [Header("Pinza (distancia pulgar-dedo en coordenadas de cámara)")]
    [Tooltip("Dedo que se junta con el pulgar: 1 = índice, 2 = medio, 3 = anular, 4 = meñique (el juego lo cambia en el nivel Difícil)")]
    [Range(1, 4)] public int dedoPinza = 1;
    [Tooltip("Ancho/alto de la cámara. 640x480 = 1.333")]
    public float aspectoCamara = 4f / 3f;
    [Tooltip("Por debajo de esta distancia se considera pinza cerrada")]
    public float umbralCerrar = 0.045f;
    [Tooltip("Por encima de esta distancia se considera pinza abierta")]
    public float umbralAbrir = 0.07f;
    [Tooltip("Tiempo que se tolera perder la mano antes de soltar la pinza (s)")]
    public float toleranciaPerdida = 0.5f;
    [Tooltip("La pinza tiene que estar abierta este tiempo para contar como soltar (evita soltar por un parpadeo de la cámara)")]
    public float tiempoParaSoltar = 0.12f;

    [Header("Pruebas sin cámara")]
    public bool permitirMouse = true;

    // ===== Estado público =====
    public bool ManoDetectada { get; private set; }
    public bool UsandoMouse { get; private set; }
    public Vector2 Cursor { get; private set; }        // coordenadas locales del área (centro = 0,0)
    public Vector2 Pulgar { get; private set; }
    public float DistanciaPinza { get; private set; }  // cruda, para calibrar
    public bool Pinza { get; private set; }
    public bool PinzaEmpezo { get; private set; }
    public bool PinzaTermino { get; private set; }
    public float SegundosEnPinza { get; private set; }  // cuánto lleva cerrada (0 si está abierta)
    public float SegundosSinPinza { get; private set; } // cuánto lleva abierta (0 si está cerrada)
    public bool EsIzquierda { get; private set; }       // la mano que se está siguiendo
    /// <summary>Dedo (1-4) más cerca del pulgar si están juntos, 0 si ninguno. Sirve para avisar si se usó otro dedo.</summary>
    public int DedoTocandoPulgar { get; private set; }
    /// <summary>Cuántas veces por segundo llegan datos nuevos de la mano (depende de la cámara y de la luz).</summary>
    public float DatosPorSegundo => 1f / Mathf.Max(0.001f, _intervaloDatos);
    public Vector2 Velocidad { get; private set; }     // unidades del área por segundo
    /// <summary>Velocidad en "anchos de pantalla por segundo" (independiente de la resolución)</summary>
    public float VelocidadNormalizada => area ? Velocidad.magnitude / Mathf.Max(1f, area.rect.width) : 0f;
    public float VelocidadXNormalizada => area ? Mathf.Abs(Velocidad.x) / Mathf.Max(1f, area.rect.width) : 0f;

    float _tiempoSinMano, _tiempoAbierta, _tiempoOtroLado;
    int _ladoSeguido = -1; // 0 = slots L, 5 = slots R
    bool _hayDatos;
    Vector2 _cursorPrevio;
    readonly Vector2[] _puntas = new Vector2[5]; // 0 = pulgar ... 4 = meñique (coordenadas de cámara)

    // predicción entre datos de la cámara
    Vector2 _ultimoDato, _velDato;
    float _tUltimoDato = -1f, _intervaloDatos = 0.05f, _tConMano;
    bool _avisoDatos;

    void Awake()
    {
        Instance = this;
        if (!area)
        {
            var c = FindObjectOfTypeCompat<Canvas>();
            if (c) area = c.GetComponent<RectTransform>();
        }
    }

    /// <param name="exigencia">Fracción del rango abierta→cerrada en la que se considera pinza cerrada (menor = hay que cerrar más)</param>
    public void AplicarCalibracion(float abierta, float cerrada, float exigencia = 0.30f)
    {
        float rango = abierta - cerrada;
        if (rango <= 0.01f) return; // calibración inválida: se dejan los valores actuales
        exigencia = Mathf.Clamp(exigencia, 0.05f, 0.6f);
        umbralCerrar = cerrada + rango * exigencia;
        umbralAbrir = cerrada + rango * (exigencia + 0.20f);
    }

    void Update()
    {
        PinzaEmpezo = false;
        PinzaTermino = false;
        if (!area) return;
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.M)) { mirrorX = !mirrorX; Debug.Log("[Fogata] mirrorX = " + mirrorX); }
#endif

        float dt = Mathf.Max(Time.deltaTime, 1e-4f);
        Vector2 dedoRaw = Vector2.zero, pulgarRaw = Vector2.zero;
        bool hayMano = LeerMediaPipe(ref dedoRaw, ref pulgarRaw);
        DedoTocandoPulgar = 0;

        bool pinzaDeseada = Pinza;
        Vector2 objetivoCursor = Cursor;
        Vector2 objetivoPulgar = Pulgar;

        if (hayMano)
        {
            _tiempoSinMano = 0f;
            ManoDetectada = true;
            UsandoMouse = false;

            objetivoCursor = A_Area(Predecir(cursorEnPinza ? (dedoRaw + pulgarRaw) * 0.5f : dedoRaw));
            objetivoPulgar = A_Area(pulgarRaw);

            float d = DistanciaAlPulgar(dedoRaw);
            DistanciaPinza = Mathf.Lerp(DistanciaPinza, d, 1f - Mathf.Exp(-25f * dt));

            float mejor = umbralCerrar;
            for (int i = 1; i <= 4; i++)
            {
                float di = DistanciaAlPulgar(_puntas[i]);
                if (di < mejor) { mejor = di; DedoTocandoPulgar = i; }
            }

            if (!Pinza && DistanciaPinza < umbralCerrar) pinzaDeseada = true;
            else if (Pinza && DistanciaPinza > umbralAbrir)
            {
                // soltar solo si se mantiene abierta un momento
                _tiempoAbierta += dt;
                if (_tiempoAbierta >= tiempoParaSoltar) pinzaDeseada = false;
            }
            if (!Pinza || DistanciaPinza <= umbralAbrir) _tiempoAbierta = 0f;
        }
        else
        {
            _tiempoSinMano += dt;
            _tUltimoDato = -1f; // al volver la mano, se empieza a predecir de cero
            if (_tiempoSinMano > toleranciaPerdida)
            {
                ManoDetectada = false;
                pinzaDeseada = false;

                if (permitirMouse && LeerMouse(out Vector2 m, out bool clic))
                {
                    UsandoMouse = true;
                    objetivoCursor = m;
                    objetivoPulgar = m;
                    pinzaDeseada = clic;
                }
                else UsandoMouse = false;
            }
        }

        // Suavizado del cursor (independiente de los FPS)
        float k = UsandoMouse ? 1f : 1f - Mathf.Exp(-seguimiento * dt);
        if (!_hayDatos) { k = 1f; _hayDatos = true; }
        _cursorPrevio = Cursor;
        Cursor = Vector2.Lerp(Cursor, objetivoCursor, k);
        Pulgar = Vector2.Lerp(Pulgar, objetivoPulgar, k);

        Vector2 v = (Cursor - _cursorPrevio) / dt;
        Velocidad = Vector2.Lerp(Velocidad, v, 1f - Mathf.Exp(-12f * dt));

        if (pinzaDeseada != Pinza)
        {
            Pinza = pinzaDeseada;
            if (Pinza) PinzaEmpezo = true; else PinzaTermino = true;
            _tiempoAbierta = 0f;
        }
        if (Pinza) { SegundosEnPinza += dt; SegundosSinPinza = 0f; }
        else { SegundosSinPinza += dt; SegundosEnPinza = 0f; }
    }

    /// <summary>
    /// Entre un dato de la cámara y el siguiente, sigue moviendo el punto con la última velocidad medida
    /// (como mucho un intervalo, para no pasarse y rebotar al frenar), así el cursor no frena y salta con cada dato.
    /// </summary>
    Vector2 Predecir(Vector2 p)
    {
        float ahora = Time.unscaledTime;
        if (_tUltimoDato < 0f) { _ultimoDato = p; _velDato = Vector2.zero; _tUltimoDato = ahora; return p; }

        if ((p - _ultimoDato).sqrMagnitude > 1e-12f) // llegó un dato nuevo
        {
            float dtd = Mathf.Clamp(ahora - _tUltimoDato, 0.008f, 0.25f);
            _velDato = Vector2.Lerp(_velDato, (p - _ultimoDato) / dtd, 0.6f);
            _intervaloDatos = Mathf.Lerp(_intervaloDatos, dtd, 0.1f);
            _ultimoDato = p;
            _tUltimoDato = ahora;
        }
        else if (ahora - _tUltimoDato > _intervaloDatos * 2f) _velDato = Vector2.zero; // la mano se detuvo

        // aviso una vez en la consola: sirve para saber si la cámara va lenta (poca luz baja los cuadros por segundo)
        _tConMano += Time.unscaledDeltaTime;
        if (!_avisoDatos && _tConMano > 4f)
        {
            _avisoDatos = true;
            Debug.Log($"[Fogata] La cámara manda la mano ~{DatosPorSegundo:F0} veces por segundo" +
                      (DatosPorSegundo < 20f ? " (pocos: más luz en el cuarto ayuda a que la cámara vaya más rápido)" : ""));
        }

        if (!prediccion) return p;
        float adelanto = Mathf.Min(ahora - _tUltimoDato, _intervaloDatos);
        return _ultimoDato + _velDato * adelanto;
    }

    /// <summary>Distancia de un dedo al pulgar, corregida por el aspecto de la cámara.</summary>
    float DistanciaAlPulgar(Vector2 punta)
    {
        float dx = (_puntas[0].x - punta.x) * aspectoCamara;
        float dy = _puntas[0].y - punta.y;
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    bool LeerMediaPipe(ref Vector2 dedo, ref Vector2 pulgar)
    {
        var st = FingersMove.GetState();
        if (st == null) return false;

        int baseIdx;
        lock (st)
        {
            bool der = st.countR > 0, izq = st.countL > 0;
            if (!der && !izq) return false;

            // MediaPipe a veces etiqueta mal la mano (la izquierda llega como "Right" unos cuadros).
            // Con una sola mano en cámara se sigue esa, diga lo que diga la etiqueta.
            // Con dos manos: la elegida, o en modo Cualquiera la que ya se estaba siguiendo.
            if (der && izq)
            {
                if (mano == LadoMano.Derecha) baseIdx = 5;
                else if (mano == LadoMano.Izquierda) baseIdx = 0;
                else baseIdx = _ladoSeguido >= 0 ? _ladoSeguido : 5;
            }
            else baseIdx = der ? 5 : 0;

            for (int i = 0; i < 5; i++) _puntas[i] = st.canvas01[baseIdx + i];
        }
        pulgar = _puntas[0];
        dedo = _puntas[Mathf.Clamp(dedoPinza, 1, 4)];
        _ladoSeguido = baseIdx;

        // Lado del dibujo: fijo si el terapeuta eligió la mano; si no, cambia solo si la etiqueta se mantiene
        if (mano != LadoMano.Cualquiera) EsIzquierda = mano == LadoMano.Izquierda;
        else if (!ManoDetectada) { EsIzquierda = baseIdx == 0; _tiempoOtroLado = 0f; } // recién apareció
        else if ((baseIdx == 0) != EsIzquierda)
        {
            _tiempoOtroLado += Time.deltaTime;
            if (_tiempoOtroLado >= tiempoCambioLado) { EsIzquierda = baseIdx == 0; _tiempoOtroLado = 0f; }
        }
        else _tiempoOtroLado = 0f;
        return true;
    }

    Vector2 A_Area(Vector2 p01)
    {
        float m = Mathf.Clamp(margenCamara, 0f, 0.45f);
        p01.x = (p01.x - m) / (1f - 2f * m);
        p01.y = (p01.y - m) / (1f - 2f * m);
        if (mirrorX) p01.x = 1f - p01.x;
        if (mirrorY) p01.y = 1f - p01.y;
        Vector2 size = area.rect.size;
        var p = new Vector2((p01.x - 0.5f) * size.x, (p01.y - 0.5f) * size.y);
        Vector2 half = size * 0.5f;
        p.x = Mathf.Clamp(p.x, -half.x, half.x);
        p.y = Mathf.Clamp(p.y, -half.y, half.y);
        return p;
    }

    bool LeerMouse(out Vector2 pos, out bool clic)
    {
        pos = Vector2.zero; clic = false;
#if ENABLE_LEGACY_INPUT_MANAGER
        var canvas = area.GetComponentInParent<Canvas>();
        Camera cam = (canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(area, Input.mousePosition, cam, out Vector2 local))
            return false;
        pos = local - area.rect.center;
        clic = Input.GetMouseButton(0);
        return true;
#else
        return false;
#endif
    }

    static T FindObjectOfTypeCompat<T>() where T : Object
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindFirstObjectByType<T>();
#else
        return Object.FindObjectOfType<T>();
#endif
    }
}
