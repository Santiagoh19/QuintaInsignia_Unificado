using System.Collections.Generic;
using UnityEngine;

/// <summary>Lluvia de confeti para la celebración (escena Final). Copia la pieza de plantilla.</summary>
public class Confeti : MonoBehaviour
{
    [Tooltip("Pieza que se copia (un cubito o un quad). Se oculta al empezar")]
    public Transform plantilla;
    public int cantidad = 140;
    public Vector3 area = new Vector3(12f, 6f, 6f);
    public Color[] colores =
    {
        new Color(0.93f, 0.52f, 0.33f), new Color(0.96f, 0.80f, 0.36f), new Color(0.30f, 0.58f, 0.80f),
        new Color(0.45f, 0.75f, 0.40f), new Color(0.85f, 0.45f, 0.75f), Color.white,
    };

    struct Pieza { public Transform t; public Vector3 velocidad, giro; }
    readonly List<Pieza> _piezas = new List<Pieza>();

    void Start()
    {
        if (!plantilla) return;
        plantilla.gameObject.SetActive(false);
        for (int i = 0; i < cantidad; i++)
        {
            var t = Instantiate(plantilla, transform);
            t.gameObject.SetActive(true);
            Pintura.Pintar(t.GetComponent<Renderer>(), colores[i % colores.Length]);
            var p = new Pieza { t = t };
            Reiniciar(ref p, Random.Range(0f, area.y * 1.5f));
            _piezas.Add(p);
        }
        Sonidos.Tocar(Sonidos.Fanfarria);
    }

    void Reiniciar(ref Pieza p, float alturaExtra)
    {
        p.t.localPosition = new Vector3(Random.Range(-0.5f, 0.5f) * area.x, area.y + alturaExtra, Random.Range(-0.5f, 0.5f) * area.z);
        p.t.localRotation = Random.rotation;
        p.velocidad = new Vector3(Random.Range(-0.4f, 0.4f), -Random.Range(0.8f, 1.6f), Random.Range(-0.4f, 0.4f));
        p.giro = Random.insideUnitSphere * 360f;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        for (int i = 0; i < _piezas.Count; i++)
        {
            var p = _piezas[i];
            var pos = p.t.localPosition + p.velocidad * dt;
            pos.x += Mathf.Sin(Time.time * 2f + i) * 0.3f * dt;
            p.t.localPosition = pos;
            p.t.Rotate(p.giro * dt);
            if (pos.y < 0f) Reiniciar(ref p, 0f);
            _piezas[i] = p;
        }
    }
}
