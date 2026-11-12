using UnityEngine;

/// <summary>Movimiento suave de adorno: sube y baja, se mece y puede girar (campista, aro brillante).</summary>
public class Balanceo : MonoBehaviour
{
    public float altura = 0.05f;
    public float velocidad = 2f;
    public float mecer = 4f;
    [Tooltip("Grados por segundo alrededor de Y")]
    public float girar;
    [Tooltip("Late cambiando de tamaño (0 = no)")]
    public float pulso;

    Vector3 _pos, _escala;
    Quaternion _rot;
    float _giro;

    void Awake()
    {
        _pos = transform.localPosition;
        _rot = transform.localRotation;
        _escala = transform.localScale;
    }

    void Update()
    {
        float t = Time.time * velocidad;
        _giro += girar * Time.deltaTime;
        transform.localPosition = _pos + Vector3.up * Mathf.Sin(t) * altura;
        transform.localRotation = _rot * Quaternion.Euler(0f, _giro, Mathf.Sin(t * 0.5f) * mecer);
        transform.localScale = _escala * (1f + Mathf.Sin(t * 1.7f) * pulso);
    }
}
