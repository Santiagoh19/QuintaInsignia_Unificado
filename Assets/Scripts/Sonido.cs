using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Música, ambientes y efectos de "Quinta Insignia". Se crea solo y vive entre escenas.
///   · Música y ambiente cambian según la escena, con fundido (si la pista es la misma, sigue sonando sin cortes).
///   · Pasos de Lucas automáticos al caminar en pasto (en el camino de los troncos solo suenan los troncos).
///   · Efectos: Sonido.Efecto("Nombre") busca Resources/Audio/Efectos/Nombre (o Nombre_1, Nombre_2... al azar).
///   · Tecla M: silenciar / activar la música.
/// Sonidos CC0: ver Assets/Resources/Audio/CREDITOS.txt.
/// </summary>
public class Sonido : MonoBehaviour
{
    public static Sonido Instancia { get; private set; }

    [Header("Volumen")]
    [Range(0f, 1f)] public float volumenMusica = 0.32f;   // todas las músicas están a -16 LUFS: así quedan parejas en los 5 niveles
    [Range(0f, 1f)] public float volumenAmbiente = 0.5f;   // ambientes a -24 LUFS, por debajo de la música
    [Range(0f, 1f)] public float volumenEfectos = 0.9f;
    [Range(0f, 1f)] public float volumenPasos = 0.45f;

    AudioSource musicaA, musicaB, ambiente1, ambiente2, carrete;
    readonly List<AudioSource> canales = new List<AudioSource>();
    readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    readonly Dictionary<string, int> variantes = new Dictionary<string, int>();
    bool musicaSilenciada;
    float factorMusica = 1f;           // para bajar la música al final
    Coroutine fundidoMusica, fundidoAmbiente;

    // pasos
    RectTransform lucas;
    Vector3 posAnterior;
    float proximoPaso;
    string escena;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Crear()
    {
        if (Instancia != null) return;
        var go = new GameObject("Sonido");
        go.AddComponent<Sonido>();
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        if (Instancia != null && Instancia != this) { Destroy(gameObject); return; }
        Instancia = this;
        musicaA = Fuente(true); musicaB = Fuente(true);
        ambiente1 = Fuente(true); ambiente2 = Fuente(true);
        carrete = Fuente(true);
        carrete.ignoreListenerPause = false;   // el carrete sí se detiene en la pausa
        for (int i = 0; i < 12; i++) canales.Add(Fuente(false));
        SceneManager.sceneLoaded += AlCargar;
    }

    void OnDestroy() => SceneManager.sceneLoaded -= AlCargar;

    AudioSource Fuente(bool bucle)
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.loop = bucle; s.playOnAwake = false; s.spatialBlend = 0f;
        s.ignoreListenerPause = true;   // música, ambiente y efectos de menú siguen sonando en la pausa (ver Pausa.cs)
        return s;
    }

    // ------------------------------------------------------------------ Escenas

    void AlCargar(Scene s, LoadSceneMode modo)
    {
        escena = s.name;
        factorMusica = 1f;
        Carrete(false);
        switch (escena)
        {
            case "Historia":   // inicio con la historia de Lucas
            case "Inicio":
                Musica("MusicaInicio"); Ambiente("NocheBosque", null); break;
            case "Pesca":
                Musica("MusicaJuego"); Ambiente("NocheBosque", "AguaCalma"); break;   // agua tranquila, sin olas
            case "CarpaInicio": case "ElegirCarpa": case "Hojas": case "AbrirMaleta":   // nivel 1: música de día (sus efectos los trae él)
            case "Tela": case "Tubos": case "Ubicar": case "Estacas": case "CarpaFinal":
                Musica("MusicaCarpa"); Ambiente(null, null); break;
            case "Fogata":      // nivel 4: música de fogata; el ambiente y el fuego los trae el nivel
                Musica("MusicaFogata"); Ambiente(null, null); break;
            case "Sombras":     // nivel 5: música suave en Do pentatónico (las mismas notas de las señas)
                Musica("MusicaSombras"); Ambiente(null, null); break;
            case "Recorrido":   // nivel 2: trae su propio AudioDirector
                if (fundidoMusica != null) StopCoroutine(fundidoMusica);
                musicaA.Stop(); musicaB.Stop(); Ambiente(null, null); break;
            default: // SampleScene (bifurcación), CaminoLena, Lena
                Musica("MusicaJuego"); Ambiente("NocheBosque", null); break;
        }
        var go = GameObject.Find("Lucas");
        lucas = go != null ? go.GetComponent<RectTransform>() : null;
        if (lucas != null) posAnterior = lucas.position;
        proximoPaso = Time.time + 0.5f;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.M)) musicaSilenciada = !musicaSilenciada;
        float vm = musicaSilenciada ? 0f : volumenMusica * factorMusica;
        if (fundidoMusica == null)
        {
            if (musicaA.isPlaying) musicaA.volume = vm;
        }
        Pasos();
    }

    void Pasos()
    {
        if (lucas == null || escena == "Inicio") return;
        Vector3 p = lucas.position;
        float vel = (p - posAnterior).magnitude / Mathf.Max(Time.deltaTime, 0.0001f) / Mathf.Max(1f, Screen.height / 1080f);
        posAnterior = p;
        if (Time.time < proximoPaso) return;

        var grupo = lucas.GetComponent<CanvasGroup>();
        if (grupo != null && grupo.alpha < 0.05f) return;   // ya desapareció (final)

        if (escena == "CaminoLena") return;   // en el camino solo se oyen los troncos (cada uno con su sonido)
        if (vel > 100f)   // (el balanceo de Lucas quieto no cuenta como caminar)
        {
            Efecto("Paso", volumenPasos, 0.08f);
            proximoPaso = Time.time + 0.30f;
        }
    }

    // ------------------------------------------------------------------ API

    /// <summary>Reproduce un efecto. Si no existe "Nombre", usa una variante "Nombre_1", "Nombre_2"... al azar.</summary>
    public static void Efecto(string nombre, float volumen = 1f, float variacionTono = 0.04f)
    {
        if (Instancia == null) return;
        Instancia.Reproducir(nombre, volumen, variacionTono);
    }

    /// <summary>Sonido de carrete en bucle mientras se recoge el pez.</summary>
    public static void Carrete(bool sonando)
    {
        if (Instancia == null) return;
        var c = Instancia.carrete;
        if (sonando && !c.isPlaying)
        {
            c.clip = Instancia.Cargar("Efectos/Carrete");
            c.volume = Instancia.volumenEfectos * 0.55f;
            c.pitch = 1.1f;
            if (c.clip != null) c.Play();
        }
        else if (!sonando && c.isPlaying) c.Stop();
    }

    /// <summary>Baja (o sube) la música y el ambiente poco a poco; 0 = silencio. Se reinicia al cambiar de escena.</summary>
    public static void Bajar(float objetivo, float segundos)
    {
        if (Instancia == null) return;
        Instancia.StartCoroutine(Instancia.BajarRutina(objetivo, segundos));
    }

    IEnumerator BajarRutina(float objetivo, float d)
    {
        float desde = factorMusica, amb1 = ambiente1.volume, amb2 = ambiente2.volume;
        for (float t = 0; t < 1f; t += Time.deltaTime / d)
        {
            factorMusica = Mathf.Lerp(desde, objetivo, t);
            ambiente1.volume = amb1 * Mathf.Lerp(1f, objetivo, t);
            ambiente2.volume = amb2 * Mathf.Lerp(1f, objetivo, t);
            yield return null;
        }
        factorMusica = objetivo;
    }

    public static void Musica(string nombre) { if (Instancia != null) Instancia.CambiarMusica(nombre); }

    void CambiarMusica(string nombre)
    {
        var clip = Cargar("Musica/" + nombre);
        if (clip == null || (musicaA.clip == clip && musicaA.isPlaying)) return;   // misma pista: sigue sin cortar
        if (fundidoMusica != null) StopCoroutine(fundidoMusica);
        // la que suena pasa a B y se apaga; la nueva entra en A
        var vieja = musicaA; musicaA = musicaB; musicaB = vieja;
        musicaA.clip = clip; musicaA.volume = 0f; musicaA.Play();
        fundidoMusica = StartCoroutine(FundirMusica(1.5f));
    }

    IEnumerator FundirMusica(float d)
    {
        float inicioB = musicaB.volume;
        for (float t = 0; t < 1f; t += Time.deltaTime / d)
        {
            float vm = musicaSilenciada ? 0f : volumenMusica * factorMusica;
            musicaA.volume = vm * t;
            musicaB.volume = inicioB * (1f - t);
            yield return null;
        }
        musicaB.Stop();
        fundidoMusica = null;
    }

    void Ambiente(string a, string b)
    {
        if (fundidoAmbiente != null) StopCoroutine(fundidoAmbiente);
        fundidoAmbiente = StartCoroutine(FundirAmbiente(Cargar(a == null ? null : "Ambientes/" + a), Cargar(b == null ? null : "Ambientes/" + b)));
    }

    IEnumerator FundirAmbiente(AudioClip a, AudioClip b)
    {
        bool mismoA = ambiente1.clip == a && ambiente1.isPlaying;
        bool mismoB = ambiente2.clip == b && (b == null || ambiente2.isPlaying);
        float v1 = ambiente1.volume, v2 = ambiente2.volume;
        if (!(mismoA && mismoB))
        {
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.8f)
            {
                if (!mismoA) ambiente1.volume = v1 * (1f - t);
                if (!mismoB) ambiente2.volume = v2 * (1f - t);
                yield return null;
            }
            if (!mismoA) { ambiente1.clip = a; if (a != null) ambiente1.Play(); else ambiente1.Stop(); }
            if (!mismoB) { ambiente2.clip = b; if (b != null) ambiente2.Play(); else ambiente2.Stop(); }
        }
        float obj1 = volumenAmbiente, obj2 = volumenAmbiente * 0.85f;
        float i1 = ambiente1.volume, i2 = ambiente2.volume;
        for (float t = 0; t < 1f; t += Time.deltaTime / 1.2f)
        {
            ambiente1.volume = Mathf.Lerp(i1, obj1, t);
            ambiente2.volume = Mathf.Lerp(i2, obj2, t);
            yield return null;
        }
        fundidoAmbiente = null;
    }

    void Reproducir(string nombre, float volumen, float variacion)
    {
        var clip = Cargar("Efectos/" + nombre);
        if (clip == null)
        {
            if (!variantes.TryGetValue(nombre, out int n))
            {
                n = 0;
                while (Cargar("Efectos/" + nombre + "_" + (n + 1)) != null) n++;
                variantes[nombre] = n;
            }
            if (n > 0) clip = Cargar("Efectos/" + nombre + "_" + Random.Range(1, n + 1));
        }
        if (clip == null) return;

        AudioSource libre = null;
        foreach (var c in canales) if (!c.isPlaying) { libre = c; break; }
        if (libre == null) libre = canales[0];
        libre.pitch = 1f + Random.Range(-variacion, variacion);
        libre.PlayOneShot(clip, volumen * volumenEfectos);
    }

    AudioClip Cargar(string ruta)
    {
        if (string.IsNullOrEmpty(ruta)) return null;
        if (!clips.TryGetValue(ruta, out var c))
        {
            c = Resources.Load<AudioClip>("Audio/" + ruta);
            clips[ruta] = c;
        }
        return c;
    }
}
