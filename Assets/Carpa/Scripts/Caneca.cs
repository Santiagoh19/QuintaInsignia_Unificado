using UnityEngine;

/// <summary>
/// Caneca de basura (escena Hojas). Tiene un Collider en modo Trigger.
/// Cuando la mano trae un objeto cerca, la tapa se abre; si se suelta encima, el objeto
/// cae adentro, desaparece y cuenta como tarea hecha.
/// </summary>
public class Caneca : MonoBehaviour, IZonaSoltar
{
    [Tooltip("Distancia en pantalla (fracción del alto) a la que se abre la tapa")]
    public float distanciaAbrir = 0.25f;
    [Tooltip("Punto de la boca de la caneca, donde caen los objetos")]
    public Transform boca;

    [Header("Dibujo 2D (cambia de cerrada a abierta)")]
    public SpriteRenderer dibujo;
    public Sprite cerrada, abierta;

    [Header("Modelo 3D (opcional): la tapa gira sobre su eje X")]
    public Transform tapa;
    public float anguloAbierta = -110f;

    public AudioClip sonido;

    public bool Abierta { get; private set; }

    float _apertura;
    float _salto;
    Vector3 _escalaBase;
    Quaternion _rotTapa;

    void Awake()
    {
        _escalaBase = transform.localScale;
        if (tapa) _rotTapa = tapa.localRotation;
        if (!boca) boca = transform;
    }

    void Update()
    {
        bool abrir = Agarrable.Actual != null && Mano.DistanciaPantalla(boca.position) < distanciaAbrir;
        if (abrir != Abierta)
        {
            Abierta = abrir;
            _salto = 1f;
            if (dibujo) dibujo.sprite = Abierta ? abierta : cerrada;
        }

        // Animación: la tapa gira y la caneca da un saltito al abrirse o cerrarse
        _apertura = Mathf.MoveTowards(_apertura, Abierta ? 1f : 0f, Time.deltaTime * 5f);
        if (tapa) tapa.localRotation = _rotTapa * Quaternion.Euler(anguloAbierta * Mathf.SmoothStep(0f, 1f, _apertura), 0f, 0f);
        _salto = Mathf.MoveTowards(_salto, 0f, Time.deltaTime * 4f);
        float s = Mathf.Sin(_salto * Mathf.PI) * 0.08f;
        transform.localScale = new Vector3(_escalaBase.x * (1f + s), _escalaBase.y * (1f - s * 0.6f), _escalaBase.z);
    }

    public bool Aceptar(Agarrable objeto)
    {
        objeto.Desaparecer(boca.position);
        Sonidos.Tocar(sonido ? sonido : Sonidos.Basura);
        _salto = 1f;
        if (Tareas.Instancia) Tareas.Instancia.Sumar();
        return true;
    }
}
