using UnityEngine;

/// <summary>
/// Tela de la carpa (escena Tela). La malla se estira entre las cuatro esquinas agarrables, así que
/// al llevar cada esquina a su círculo la tela se va desplegando. La pila doblada se encoge en tres
/// etapas: doblada (0 esquinas), medio abierta (1 a 3) y extendida (4).
/// Orden de las esquinas: 0 adelante-izquierda, 1 adelante-derecha, 2 atrás-derecha, 3 atrás-izquierda.
/// </summary>
[RequireComponent(typeof(MeshFilter))]
public class TelaDesdoblar : MonoBehaviour
{
    public Transform[] esquinas = new Transform[4];
    [Tooltip("Altura de las manijas de las esquinas sobre la tela")]
    public float alturaManija = 0.2f;
    public int divisiones = 10;

    [Tooltip("Pila de tela doblada en el centro, que se encoge al desplegar")]
    public Transform doblada;
    [Tooltip("Opcional: modelos de cada etapa (doblada, medio abierta, extendida)")]
    public GameObject[] etapas;

    Mesh _malla;
    Vector3[] _vertices;
    Vector3 _escalaDoblada;
    float _desplegado;

    void Awake()
    {
        if (doblada) _escalaDoblada = doblada.localScale;
        CrearMalla();
    }

    void CrearMalla()
    {
        int n = divisiones + 1;
        int caras = n * n;
        _vertices = new Vector3[caras * 2];
        var uv = new Vector2[caras * 2];
        var triangulos = new int[divisiones * divisiones * 12];
        int k = 0;
        for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                uv[j * n + i] = uv[caras + j * n + i] = new Vector2(i / (float)divisiones, j / (float)divisiones);
            }
        for (int j = 0; j < divisiones; j++)
            for (int i = 0; i < divisiones; i++)
            {
                int a = j * n + i, b = a + 1, c = a + n, d = c + 1;
                // Cara de arriba
                triangulos[k++] = a; triangulos[k++] = c; triangulos[k++] = b;
                triangulos[k++] = b; triangulos[k++] = c; triangulos[k++] = d;
                // Cara de abajo (la tela se ve por los dos lados)
                triangulos[k++] = caras + a; triangulos[k++] = caras + b; triangulos[k++] = caras + c;
                triangulos[k++] = caras + b; triangulos[k++] = caras + d; triangulos[k++] = caras + c;
            }
        _malla = new Mesh { name = "Tela" };
        _malla.MarkDynamic();
        _malla.vertices = _vertices;
        _malla.uv = uv;
        _malla.triangles = triangulos;
        GetComponent<MeshFilter>().sharedMesh = _malla;
    }

    void LateUpdate()
    {
        int colocadas = Tareas.Instancia ? Tareas.Instancia.Hechas : 0;
        _desplegado = Mathf.MoveTowards(_desplegado, colocadas / 4f, Time.deltaTime * 1.5f);

        int etapa = colocadas == 0 ? 0 : colocadas < 4 ? 1 : 2;
        for (int i = 0; i < etapas.Length; i++)
            if (etapas[i]) etapas[i].SetActive(i == etapa);
        if (doblada)
        {
            doblada.localScale = _escalaDoblada * (1f - _desplegado);
            doblada.gameObject.SetActive(_desplegado < 0.99f);
        }

        ActualizarMalla();
    }

    void ActualizarMalla()
    {
        if (esquinas.Length < 4 || !esquinas[0] || !esquinas[1] || !esquinas[2] || !esquinas[3]) return;
        Vector3 bajar = Vector3.down * alturaManija;
        Vector3 p0 = esquinas[0].position + bajar, p1 = esquinas[1].position + bajar;
        Vector3 p2 = esquinas[2].position + bajar, p3 = esquinas[3].position + bajar;

        int n = divisiones + 1;
        int caras = n * n;
        // Arrugas suaves mientras la tela no está extendida del todo
        float arruga = (1f - _desplegado) * 0.06f;
        for (int j = 0; j < n; j++)
        {
            float v = j / (float)divisiones;
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)divisiones;
                Vector3 adelante = Vector3.Lerp(p0, p1, u);
                Vector3 atras = Vector3.Lerp(p3, p2, u);
                Vector3 mundo = Vector3.Lerp(adelante, atras, v);
                mundo.y += Mathf.Sin(u * 9f + v * 4f) * Mathf.Sin(v * 7f) * arruga;
                Vector3 local = transform.InverseTransformPoint(mundo);
                _vertices[j * n + i] = local;
                _vertices[caras + j * n + i] = local + Vector3.down * 0.005f;
            }
        }
        _malla.vertices = _vertices;
        _malla.RecalculateNormals();
        _malla.RecalculateBounds();
    }
}
