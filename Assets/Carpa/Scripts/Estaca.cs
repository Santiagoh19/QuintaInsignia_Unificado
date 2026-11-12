using UnityEngine;

/// <summary>
/// Estaca (puntilla) a medio clavar en una punta de la carpa (escena Estacas).
/// Cada golpe del martillo la hunde un poco, suena y tiembla. Con golpesNecesarios queda clavada.
/// </summary>
public class Estaca : MonoBehaviour
{
    public int golpesNecesarios = 3;
    [Tooltip("Cuánto se hunde en total (unidades)")]
    public float profundidad = 0.5f;
    [Tooltip("Punto de arriba de la estaca, donde se golpea")]
    public Transform punta;
    [Tooltip("Se pinta de colorClavada al terminar")]
    public Renderer cabeza;
    public Color colorClavada = new Color(0.35f, 0.8f, 0.35f);
    [Tooltip("Aro que se muestra cuando el martillo está encima")]
    public GameObject resaltado;
    public AudioClip sonidoGolpe;

    public int Golpes { get; private set; }
    public bool Clavada => Golpes >= golpesNecesarios;
    public Vector3 Punta => punta ? punta.position : transform.position;

    Vector3 _posBase, _destino;
    float _temblor;

    void Awake()
    {
        _posBase = _destino = transform.localPosition;
        Resaltar(false);
    }

    public void Resaltar(bool si)
    {
        if (resaltado) resaltado.SetActive(si && !Clavada);
    }

    public bool Golpear()
    {
        if (Clavada) return false;
        Golpes++;
        _destino = _posBase + Vector3.down * (profundidad * Golpes / golpesNecesarios);
        _temblor = 1f;
        Sonidos.Tocar(sonidoGolpe ? sonidoGolpe : Sonidos.Golpe, 1f, Random.Range(0.92f, 1.08f));
        if (Clavada)
        {
            Pintura.Pintar(cabeza, colorClavada);
            Resaltar(false);
            Sonidos.Tocar(Sonidos.Exito, 0.8f);
            if (Tareas.Instancia) Tareas.Instancia.Sumar();
        }
        return true;
    }

    void Update()
    {
        _temblor = Mathf.MoveTowards(_temblor, 0f, Time.deltaTime * 4f);
        Vector3 sacudida = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)) * 0.035f * _temblor;
        transform.localPosition = Vector3.Lerp(transform.localPosition, _destino, 1f - Mathf.Exp(-30f * Time.deltaTime)) + sacudida;
    }
}
