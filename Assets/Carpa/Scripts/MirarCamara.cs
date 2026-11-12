using UnityEngine;

/// <summary>Hace que un dibujo plano (SpriteRenderer) mire siempre a la cámara, girando solo de lado.</summary>
public class MirarCamara : MonoBehaviour
{
    [Tooltip("Si está activo también se inclina hacia la cámara (no solo gira de lado)")]
    public bool completo;

    void LateUpdate()
    {
        var cam = Camera.main;
        if (!cam) return;
        Vector3 adelante = cam.transform.forward;
        if (!completo) { adelante.y = 0f; if (adelante.sqrMagnitude < 0.001f) return; }
        transform.rotation = Quaternion.LookRotation(adelante, Vector3.up);
    }
}
