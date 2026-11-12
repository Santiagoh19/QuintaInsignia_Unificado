using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Claro del bosque: Lucas camina hacia los troncos; al llegar a uno, lo corta con el hacha,
/// un trozo de leña vuela hasta él y sube el contador. Con la meta completa, vuelve a la bifurcación.
/// </summary>
public class RecolectarLena : MonoBehaviour
{
    [Header("Escenas")]
    public string escenaBifurcacion = "SampleScene";

    [Header("Referencias")]
    public RectTransform mundo;
    public RectTransform lucas;
    public RectTransform hacha;
    public RectTransform pilaLucas;       // donde se apilan los trozos sobre Lucas
    public RectTransform[] tocones;
    public RectTransform iconoContador;
    public TextMeshProUGUI textoContador;
    public TextMeshProUGUI textoObjetivo;
    public CanvasGroup mensaje;
    public TextMeshProUGUI textoMensaje;
    public Sprite spriteLena;

    [Header("Ajustes")]
    public float velocidad = 520f;
    public float limiteX = 840f;
    public float distanciaCorte = 170f;
    public int golpesPorTronco = 3;

    bool[] cortado;
    bool ocupado, terminado;
    float baseY;

    void Start()
    {
        cortado = new bool[tocones.Length];
        baseY = lucas.anchoredPosition.y;
        hacha.gameObject.SetActive(false);
        mensaje.alpha = 0f;
        EstadoJuego.Lena = 0; // cada visita empieza en 0
        ActualizarContador(false);
        textoObjetivo.text = "Acércate a los troncos para cortar leña";
    }

    void Update()
    {
        if (terminado)
        {
            if (EntradaJugador.Accion) Volver();
            return;
        }
        if (ocupado) return;

        float h = EntradaJugador.Horizontal;
        var p = lucas.anchoredPosition;
        p.x = Mathf.Clamp(p.x + h * velocidad * Time.deltaTime, -limiteX, limiteX);
        p.y = baseY + (h != 0f ? Mathf.Abs(Mathf.Sin(Time.time * 12f)) * 10f : 0f);
        lucas.anchoredPosition = p;
        lucas.localRotation = Quaternion.Euler(0, 0, h != 0f ? Mathf.Sin(Time.time * 12f) * 3f : 0f);

        for (int i = 0; i < tocones.Length; i++)
        {
            if (cortado[i]) continue;
            if (Mathf.Abs(tocones[i].anchoredPosition.x - p.x) < distanciaCorte)
            {
                StartCoroutine(Cortar(i));
                break;
            }
        }
    }

    IEnumerator Cortar(int i)
    {
        ocupado = true;
        lucas.localRotation = Quaternion.identity;
        var p = lucas.anchoredPosition; p.y = baseY; lucas.anchoredPosition = p;

        var tocon = tocones[i];
        float dir = Mathf.Sign(tocon.anchoredPosition.x - p.x);
        if (dir == 0) dir = 1;

        hacha.gameObject.SetActive(true);
        hacha.anchoredPosition = new Vector2(dir * 120f, 10f);
        hacha.localScale = new Vector3(dir, 1f, 1f);

        Vector2 posTocon = tocon.anchoredPosition;
        for (int g = 0; g < golpesPorTronco; g++)
        {
            // levantar
            yield return Girar(hacha, 0f, 50f, 0.18f);
            // golpe
            yield return Girar(hacha, 50f, -70f, 0.1f);
            Sonido.Efecto("Hachazo", 0.9f, 0.08f);
            StartCoroutine(Temblar(tocon, posTocon));
            yield return new WaitForSeconds(0.15f);
        }
        yield return Girar(hacha, -70f, 0f, 0.12f);
        hacha.gameObject.SetActive(false);

        cortado[i] = true;
        tocon.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.45f);

        yield return VolarLena(posTocon + new Vector2(0, 80f));

        EstadoJuego.Lena++;
        ActualizarContador(true);

        if (EstadoJuego.Lena >= EstadoJuego.MetaLena)
        {
            EstadoJuego.LenaCompleta = true;
            terminado = true;
            textoObjetivo.text = "¡Leña completa!";
            Sonido.Efecto("MisionCompleta");
            textoMensaje.text = "¡Conseguiste toda la leña!\nLevanta la mano derecha para volver al camino.";
            yield return Bifurcacion.Fundido(mensaje, 0f, 1f, 0.3f);
            yield return new WaitForSeconds(6f);
            if (terminado) Volver();
        }
        ocupado = false;
    }

    IEnumerator VolarLena(Vector2 desde)
    {
        Sonido.Efecto("Lanzar", 0.5f);
        var go = new GameObject("LenaVolando", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(mundo, false);
        var img = go.GetComponent<Image>();
        img.sprite = spriteLena;
        img.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(150, 75);

        Vector2 hasta = lucas.anchoredPosition + pilaLucas.anchoredPosition;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.6f;
            Vector2 pos = Vector2.Lerp(desde, hasta, t);
            pos.y += Mathf.Sin(t * Mathf.PI) * 220f;
            rt.anchoredPosition = pos;
            rt.localRotation = Quaternion.Euler(0, 0, t * 360f);
            rt.localScale = Vector3.one * Mathf.Lerp(1f, 0.6f, t);
            yield return null;
        }
        Destroy(go);
        Sonido.Efecto("LenaCae", 0.8f, 0.06f);

        // trozo que queda apilado sobre Lucas
        var pieza = new GameObject("Lena" + EstadoJuego.Lena, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        pieza.transform.SetParent(pilaLucas, false);
        var pi = pieza.GetComponent<Image>();
        pi.sprite = spriteLena;
        pi.raycastTarget = false;
        var prt = pieza.GetComponent<RectTransform>();
        prt.sizeDelta = new Vector2(110, 55);
        prt.anchoredPosition = new Vector2(0, pilaLucas.childCount * 38f - 38f);
    }

    void ActualizarContador(bool animar)
    {
        textoContador.text = EstadoJuego.Lena + "/" + EstadoJuego.MetaLena;
        if (animar) { StartCoroutine(Pop(iconoContador)); Sonido.Efecto("Punto", 0.8f); }
    }

    IEnumerator Pop(RectTransform rt)
    {
        for (float t = 0; t < 1f; t += Time.deltaTime / 0.35f)
        {
            rt.localScale = Vector3.one * (1f + Mathf.Sin(t * Mathf.PI) * 0.35f);
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    IEnumerator Girar(RectTransform rt, float a, float b, float d)
    {
        for (float t = 0; t < 1f; t += Time.deltaTime / d)
        {
            rt.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(a, b, t) * Mathf.Sign(rt.localScale.x) * -1f);
            yield return null;
        }
        rt.localRotation = Quaternion.Euler(0, 0, b * Mathf.Sign(rt.localScale.x) * -1f);
    }

    IEnumerator Temblar(RectTransform rt, Vector2 basePos)
    {
        for (float t = 0; t < 0.2f; t += Time.deltaTime)
        {
            rt.anchoredPosition = basePos + new Vector2(Mathf.Sin(t * 90f) * 8f, 0);
            yield return null;
        }
        rt.anchoredPosition = basePos;
    }

    void Volver()
    {
        Sonido.Efecto("Boton");
        terminado = false;
        Transicion.Cargar(escenaBifurcacion);
    }
}
