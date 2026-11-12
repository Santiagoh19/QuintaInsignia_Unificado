using UnityEngine;

/// <summary>
/// La carpa se va levantando a medida que se completan las tareas (escena Ubicar):
/// escala en Y desde alturaInicial hasta 1.
/// </summary>
public class LevantarCarpa : MonoBehaviour
{
    [Range(0.05f, 1f)] public float alturaInicial = 0.15f;
    public float velocidad = 2.5f;

    Vector3 _escalaBase;
    float _altura;

    void Awake()
    {
        _escalaBase = transform.localScale;
        _altura = alturaInicial;
        Aplicar();
    }

    void Update()
    {
        float meta = Tareas.Instancia ? Mathf.Lerp(alturaInicial, 1f, Tareas.Instancia.Progreso) : 1f;
        _altura = Mathf.Lerp(_altura, meta, 1f - Mathf.Exp(-velocidad * Time.deltaTime));
        Aplicar();
    }

    void Aplicar()
    {
        transform.localScale = new Vector3(_escalaBase.x, _escalaBase.y * _altura, _escalaBase.z);
    }
}
