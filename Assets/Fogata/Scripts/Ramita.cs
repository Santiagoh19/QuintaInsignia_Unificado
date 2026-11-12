using UnityEngine;

/// <summary>
/// Una ramita que se puede agarrar con la pinza. Se mueve suavemente hacia "objetivo".
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class Ramita : MonoBehaviour
{
    public enum Estado { EnSuelo, Agarrada, Colocada }

    public Estado estado = Estado.EnSuelo;
    [Tooltip("Qué tan rápido se mueve hacia su destino")]
    public float velocidad = 14f;

    [HideInInspector] public Vector2 objetivo;
    [HideInInspector] public float rotacionObjetivo;
    [HideInInspector] public float escalaObjetivo = 1f;

    public Vector2 PosicionInicial { get; private set; }
    public float RotacionInicial { get; private set; }
    public RectTransform Rect { get; private set; }

    void Awake()
    {
        Rect = GetComponent<RectTransform>();
        PosicionInicial = Rect.anchoredPosition;
        RotacionInicial = Rect.localEulerAngles.z;
        objetivo = PosicionInicial;
        rotacionObjetivo = RotacionInicial;
    }

    void Update()
    {
        float vel = estado == Estado.Agarrada ? velocidad * 2f : velocidad;
        float k = 1f - Mathf.Exp(-vel * Time.deltaTime);
        Rect.anchoredPosition = Vector2.Lerp(Rect.anchoredPosition, objetivo, k);
        float z = Mathf.LerpAngle(Rect.localEulerAngles.z, rotacionObjetivo, k);
        Rect.localEulerAngles = new Vector3(0f, 0f, z);
        float s = Mathf.Lerp(Rect.localScale.x, escalaObjetivo, k);
        Rect.localScale = new Vector3(s, s, 1f);
    }

    public void VolverAlInicio()
    {
        objetivo = PosicionInicial;
        rotacionObjetivo = RotacionInicial;
        escalaObjetivo = 1f;
    }
}
