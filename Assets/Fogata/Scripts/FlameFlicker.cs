using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hace "bailar" una llama (escala, rotación y brillo con ruido Perlin).
/// </summary>
public class FlameFlicker : MonoBehaviour
{
    public float amplitudEscala = 0.15f;
    public float amplitudRotacion = 6f;
    public float velocidad = 6f;
    public float amplitudAlfa = 0.15f;

    Vector3 _escalaBase;
    float _semilla;
    Graphic _grafico;
    float _alfaBase = 1f;

    void Awake()
    {
        _escalaBase = transform.localScale;
        _semilla = Random.value * 100f;
        _grafico = GetComponent<Graphic>();
        if (_grafico) _alfaBase = _grafico.color.a;
    }

    void Update()
    {
        float t = Time.time * velocidad + _semilla;
        float n1 = Mathf.PerlinNoise(t, 0.3f) * 2f - 1f;
        float n2 = Mathf.PerlinNoise(0.7f, t * 1.3f) * 2f - 1f;
        transform.localScale = new Vector3(
            _escalaBase.x * (1f + n2 * amplitudEscala * 0.5f),
            _escalaBase.y * (1f + n1 * amplitudEscala),
            1f);
        transform.localEulerAngles = new Vector3(0f, 0f, n2 * amplitudRotacion);
        if (_grafico)
        {
            var c = _grafico.color;
            c.a = Mathf.Clamp01(_alfaBase * (1f + n1 * amplitudAlfa));
            _grafico.color = c;
        }
    }
}
