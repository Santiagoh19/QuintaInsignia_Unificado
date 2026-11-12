using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Círculo de destino en el suelo (esquinas de la tela, Punto1..Punto4 de Ubicar).
/// Necesita un Collider en modo Trigger. Si aceptaId es 0 o más, solo acepta el Agarrable con ese id;
/// con otro, el objeto vuelve a su lugar. Al aceptarlo lo deja fijo y se pone verde.
/// </summary>
public class ZonaPunto : MonoBehaviour, IZonaSoltar
{
    [Tooltip("-1 = acepta cualquiera")]
    public int aceptaId = -1;
    [Tooltip("Dónde y con qué rotación queda el objeto. Vacío = este mismo punto")]
    public Transform puntoFijar;
    public Renderer marca;
    public Color colorNormal = new Color(1f, 1f, 1f, 0.45f);
    public Color colorCorrecto = new Color(0.35f, 0.85f, 0.35f, 0.85f);
    public bool sumarTarea = true;
    public UnityEvent alAceptar;

    public bool Ocupado { get; private set; }

    Vector3 _escalaMarca;

    void Awake()
    {
        if (marca)
        {
            _escalaMarca = marca.transform.localScale;
            Pintura.Pintar(marca, colorNormal);
        }
    }

    void Update()
    {
        if (!marca || Ocupado) return;
        // Late cuando la mano trae el objeto que va aquí
        var actual = Agarrable.Actual;
        bool esperando = actual && (aceptaId < 0 || actual.id == aceptaId);
        float s = esperando ? 1f + 0.12f * (0.5f + 0.5f * Mathf.Sin(Time.time * 8f)) : 1f;
        marca.transform.localScale = new Vector3(_escalaMarca.x * s, _escalaMarca.y, _escalaMarca.z * s);
    }

    public bool Aceptar(Agarrable objeto)
    {
        if (Ocupado) return false;
        if (aceptaId >= 0 && objeto.id != aceptaId)
        {
            Sonidos.Tocar(Sonidos.Error);
            return false;
        }

        Ocupado = true;
        var destino = puntoFijar ? puntoFijar : transform;
        objeto.FijarEn(destino.position, destino.rotation);
        if (marca)
        {
            marca.transform.localScale = _escalaMarca;
            Pintura.Pintar(marca, colorCorrecto);
        }
        Sonidos.Tocar(Sonidos.Exito);
        alAceptar.Invoke();
        if (sumarTarea && Tareas.Instancia) Tareas.Instancia.Sumar();
        return true;
    }
}
