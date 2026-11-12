using UnityEngine;

/// <summary>
/// Atajos para leer HandTracker.Instance y saber qué objeto 3D hay debajo de la mano
/// (Physics.Raycast desde Camera.main a través de HandScreenPos).
/// </summary>
public static class Mano
{
    static readonly RaycastHit[] _hits = new RaycastHit[32];

    public static HandTracker Tracker => HandTracker.Instance;
    public static bool Activa => Tracker != null && Tracker.HayCursor;
    public static Vector2 Pos => Tracker != null ? Tracker.HandScreenPos : Vector2.zero;
    public static bool Puno => Activa && Tracker.IsFist;
    /// <summary>La mano se acaba de cerrar (agarrar / pulsar).</summary>
    public static bool PunoEmpezo => Activa && Tracker.FistDown;
    /// <summary>La mano se acaba de abrir (soltar).</summary>
    public static bool PunoTermino => Tracker != null && Tracker.FistUp;

    public static Ray Rayo()
    {
        var cam = Camera.main;
        return cam ? cam.ScreenPointToRay(Pos) : new Ray(Vector3.zero, Vector3.forward);
    }

    /// <summary>Primer collider (que no sea trigger) que toca el rayo de la mano.</summary>
    public static bool Bajo(out RaycastHit hit)
    {
        hit = default;
        if (!Activa || !Camera.main) return false;
        return Physics.Raycast(Rayo(), out hit, 500f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
    }

    /// <summary>Componente del primer objeto que tiene la mano debajo (o null).</summary>
    public static T ObjetoBajo<T>() where T : class
    {
        return Bajo(out var hit) ? hit.collider.GetComponentInParent<T>() : null;
    }

    /// <summary>¿El primer collider bajo la mano pertenece a este objeto (o a sus hijos)?</summary>
    public static bool Sobre(Transform t)
    {
        return t && Bajo(out var hit) && hit.collider.transform.IsChildOf(t);
    }

    /// <summary>La zona de soltar más cercana que atraviesa el rayo de la mano (incluye triggers).</summary>
    public static IZonaSoltar ZonaBajo(Transform ignorar)
    {
        if (!Camera.main) return null;
        int n = Physics.RaycastNonAlloc(Rayo(), _hits, 500f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        IZonaSoltar mejor = null;
        float dMin = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            var col = _hits[i].collider;
            if (ignorar && col.transform.IsChildOf(ignorar)) continue;
            var zona = col.GetComponentInParent<IZonaSoltar>();
            if (zona != null && _hits[i].distance < dMin) { mejor = zona; dMin = _hits[i].distance; }
        }
        return mejor;
    }

    /// <summary>Punto donde el rayo de la mano corta un plano.</summary>
    public static bool PuntoEnPlano(Plane plano, out Vector3 punto)
    {
        var rayo = Rayo();
        if (plano.Raycast(rayo, out float d)) { punto = rayo.GetPoint(d); return true; }
        punto = Vector3.zero;
        return false;
    }

    /// <summary>Plano paralelo a la cámara que pasa por un punto.</summary>
    public static Plane PlanoCamara(Vector3 punto)
    {
        var cam = Camera.main;
        return new Plane(cam ? -cam.transform.forward : Vector3.back, punto);
    }

    /// <summary>Distancia en pantalla entre la mano y un punto del mundo, en fracción del alto de la pantalla.</summary>
    public static float DistanciaPantalla(Vector3 mundo)
    {
        var cam = Camera.main;
        if (!cam) return float.MaxValue;
        Vector3 p = cam.WorldToScreenPoint(mundo);
        if (p.z < 0f) return float.MaxValue;
        return Vector2.Distance(Pos, p) / Screen.height;
    }
}

/// <summary>Algo donde se puede soltar un Agarrable (la caneca, los puntos, la DropZone...).</summary>
public interface IZonaSoltar
{
    /// <summary>Devuelve true si se queda con el objeto; false para que vuelva a su lugar.</summary>
    bool Aceptar(Agarrable objeto);
}
