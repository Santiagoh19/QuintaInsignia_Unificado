using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Corazones del nivel. Perder() quita uno con una pequeña animación y devuelve los que quedan.
/// </summary>
public class Vidas : MonoBehaviour
{
    public Image[] corazones;
    public Sprite lleno;
    public Sprite vacio;

    int actuales;
    public int Actuales => actuales;

    void Awake()
    {
        actuales = corazones.Length;
        Refrescar();
    }

    public int Perder()
    {
        if (actuales <= 0) return 0;
        actuales--;
        Sonido.Efecto("CorazonMenos");
        StartCoroutine(Latido(corazones[actuales].rectTransform));
        Refrescar();
        return actuales;
    }

    void Refrescar()
    {
        for (int i = 0; i < corazones.Length; i++)
            corazones[i].sprite = i < actuales ? lleno : vacio;
    }

    IEnumerator Latido(RectTransform rt)
    {
        var basePos = rt.anchoredPosition;
        for (float t = 0; t < 1f; t += Time.deltaTime / 0.45f)
        {
            rt.localScale = Vector3.one * (1f + Mathf.Sin(t * Mathf.PI) * 0.5f);
            rt.anchoredPosition = basePos + new Vector2(Mathf.Sin(t * 50f) * 6f * (1f - t), 0);
            yield return null;
        }
        rt.localScale = Vector3.one;
        rt.anchoredPosition = basePos;
    }
}
