using UnityEngine;

/// <summary>
/// Maleta o bolsa de la carpa que se abre según el progreso de la cremallera (0 = cerrada, 1 = abierta).
/// Sirve cualquiera de estas formas (se usan las que estén asignadas):
///  - una bisagra que gira (la tapa),
///  - una animación controlada por el progreso (Animator + nombre del estado),
///  - un blend shape,
///  - cambiar del modelo cerrado al abierto al terminar.
/// </summary>
public class Maleta : MonoBehaviour
{
    [Header("Bisagra")]
    public Transform bisagra;
    [Tooltip("Grados que gira la bisagra en X al abrir del todo (positivo = la tapa sube hacia atrás)")]
    public float anguloAbierta = 105f;

    [Header("Animación (opcional)")]
    public Animator animador;
    public string estado = "Abrir";

    [Header("Blend shape (opcional)")]
    public SkinnedMeshRenderer malla;
    public int indiceBlendShape;

    [Header("Cambio de modelo (opcional)")]
    public GameObject modeloCerrado;
    public GameObject modeloAbierto;

    [Tooltip("Lo que hay adentro (la tela doblada); aparece al abrirse")]
    public GameObject contenido;

    Quaternion _rotBisagra;

    void Awake()
    {
        if (bisagra) _rotBisagra = bisagra.localRotation;
        if (animador) animador.speed = 0f;
    }

    public void Abrir(float t)
    {
        t = Mathf.Clamp01(t);
        if (bisagra) bisagra.localRotation = _rotBisagra * Quaternion.Euler(anguloAbierta * Mathf.SmoothStep(0f, 1f, t), 0f, 0f);
        if (animador && animador.isActiveAndEnabled) animador.Play(estado, 0, t);
        if (malla) malla.SetBlendShapeWeight(indiceBlendShape, t * 100f);
        if (modeloCerrado) modeloCerrado.SetActive(t < 1f || !modeloAbierto);
        if (modeloAbierto) modeloAbierto.SetActive(t >= 1f);
        if (contenido) contenido.SetActive(t > 0.25f);
    }
}
