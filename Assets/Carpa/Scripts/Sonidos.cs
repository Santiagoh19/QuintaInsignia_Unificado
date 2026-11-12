using System;
using UnityEngine;

/// <summary>
/// Sonidos cortos generados por código (no hace falta importar audios).
/// Para usar audios propios, asigna clips en los campos "sonido..." de cada script.
/// </summary>
public static class Sonidos
{
    const int FRECUENCIA = 44100;
    static AudioSource _fuente;
    static AudioClip _click, _agarrar, _soltar, _exito, _error, _golpe, _basura, _tic, _fanfarria;

    public static AudioClip Click => _click ? _click : _click = Crear("click", 0.12f, t => Seno(880f, t) * Mathf.Exp(-t * 40f) * 0.5f);
    public static AudioClip Agarrar => _agarrar ? _agarrar : _agarrar = Crear("agarrar", 0.15f, t => Mathf.Sin(2f * Mathf.PI * (400f * t + 1500f * t * t)) * Mathf.Exp(-t * 25f) * 0.45f);
    public static AudioClip Soltar => _soltar ? _soltar : _soltar = Crear("soltar", 0.15f, t => Mathf.Sin(2f * Mathf.PI * (600f * t - 900f * t * t)) * Mathf.Exp(-t * 25f) * 0.4f);
    public static AudioClip Error => _error ? _error : _error = Crear("error", 0.3f, t => Mathf.Sign(Seno(150f, t)) * Mathf.Exp(-t * 9f) * 0.18f);
    public static AudioClip Tic => _tic ? _tic : _tic = Crear("tic", 0.04f, Ruido(7, 0.35f, 140f));

    public static AudioClip Exito => _exito ? _exito : _exito = Crear("exito", 0.45f, t =>
        (t < 0.12f ? Seno(660f, t) * Mathf.Exp(-t * 15f) : Seno(990f, t) * Mathf.Exp(-(t - 0.12f) * 8f)) * 0.4f);

    public static AudioClip Golpe => _golpe ? _golpe : _golpe = CrearGolpe();

    static AudioClip CrearGolpe()
    {
        var ruido = Ruido(3, 0.5f, 70f);
        return Crear("golpe", 0.3f, t => Seno(95f * Mathf.Exp(-t * 4f), t) * Mathf.Exp(-t * 22f) * 0.7f + ruido(t));
    }

    public static AudioClip Basura => _basura ? _basura : _basura = Crear("basura", 0.55f, t =>
        (Seno(523f, t) + 0.6f * Seno(1371f, t) + 0.4f * Seno(2211f, t)) * Mathf.Exp(-t * 10f) * 0.22f);

    public static AudioClip Fanfarria => _fanfarria ? _fanfarria : _fanfarria = Crear("fanfarria", 1.1f, t =>
    {
        float[] notas = { 523f, 659f, 784f, 1046f };
        int i = Mathf.Min((int)(t / 0.16f), 3);
        float local = t - i * 0.16f;
        return Seno(notas[i], t) * Mathf.Exp(-local * (i == 3 ? 3f : 10f)) * 0.35f;
    });

    public static void Tocar(AudioClip clip, float volumen = 1f, float tono = 1f)
    {
        if (!clip) return;
        if (!_fuente)
        {
            var go = new GameObject("Sonidos");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _fuente = go.AddComponent<AudioSource>();
            _fuente.playOnAwake = false;
            _fuente.spatialBlend = 0f;
        }
        _fuente.pitch = tono;
        _fuente.PlayOneShot(clip, volumen);
    }

    static float Seno(float frecuencia, float t) => Mathf.Sin(2f * Mathf.PI * frecuencia * t);

    static Func<float, float> Ruido(int semilla, float volumen, float caida)
    {
        var azar = new System.Random(semilla);
        return t => ((float)azar.NextDouble() * 2f - 1f) * Mathf.Exp(-t * caida) * volumen;
    }

    static AudioClip Crear(string nombre, float duracion, Func<float, float> onda)
    {
        int n = Mathf.CeilToInt(duracion * FRECUENCIA);
        var datos = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)FRECUENCIA;
            float suave = Mathf.Clamp01(t / 0.004f) * Mathf.Clamp01((duracion - t) / 0.01f);
            datos[i] = onda(t) * suave;
        }
        var clip = AudioClip.Create(nombre, n, 1, FRECUENCIA, false);
        clip.SetData(datos, 0);
        return clip;
    }
}
