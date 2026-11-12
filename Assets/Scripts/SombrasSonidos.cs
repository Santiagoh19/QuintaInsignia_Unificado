using System.Collections;
using UnityEngine;

// Los sonidos de Sombras en la carpa.
// Cada seña tiene su nota, como una tecla de xilofono, y es una SILABA del idioma de los
// exploradores (SombrasJuego.SILABAS). Al completar la serie suenan todas las notas seguidas:
// esa es la palabra que dijiste.
//
// Los sonidos se fabrican con codigo al arrancar: no hace falta ningun archivo de audio.
// Si un dia quieres sonidos grabados (por ejemplo, tu voz diciendo cada silaba), arrastralos a
// "Sonidos grabados" en el mismo orden de las señas y el juego los usa en vez de los fabricados.
//
// SombrasJuego lo agrega solo si no esta en la escena: no hay que reconstruir nada.
[RequireComponent(typeof(AudioSource))]
public class SombrasSonidos : MonoBehaviour
{
    [Header("Volumen")]
    [Range(0f, 1f)]
    public float volumen = 0.8f;

    [Header("Sonidos grabados (opcional, en el orden de las señas)")]
    [Tooltip("Palma, Paz, Cuernos, Pulgar arriba, Llamame. Si una casilla esta vacia, se usa la nota fabricada")]
    public AudioClip[] sonidosGrabados = new AudioClip[5];

    [Header("La palabra")]
    public float pausaAntesDePalabra = 0.5f;   // silencio entre la ultima seña y la palabra completa
    public float pasoEntreSilabas = 0.32f;     // segundos entre una silaba y la siguiente


    // las notas de las señas: escala pentatonica (Do, Re, Mi, Sol, La).
    // Con esta escala cualquier orden suena bonito, asi toda palabra es una melodia
    static readonly float[] NOTAS = { 523.25f, 587.33f, 659.25f, 783.99f, 880f };

    const int MUESTRAS = 44100;   // muestras por segundo (calidad de CD)

    AudioSource fuente;
    AudioClip[] notas;            // una mano
    AudioClip[] notasDobles;      // dos manos: la nota con su quinta, suena mas llena
    AudioClip apagadaAlta;
    AudioClip apagadaBaja;
    AudioClip acorde;             // el "¡ta-da!" despues de la palabra
    AudioClip notaAguda;          // el Do de arriba, para la graduacion
    Coroutine melodia;


    void Awake()
    {
        fuente = GetComponent<AudioSource>();
        fuente.playOnAwake = false;
        fuente.spatialBlend = 0f;   // 2D: suena igual sin importar donde este la camara

        notas = new AudioClip[NOTAS.Length];
        notasDobles = new AudioClip[NOTAS.Length];
        for (int i = 0; i < NOTAS.Length; i++)
        {
            notas[i] = Fabricar("Nota_" + i, new float[] { NOTAS[i] }, 0.9f, 0.32f);
            notasDobles[i] = Fabricar("Nota_Doble_" + i, new float[] { NOTAS[i], NOTAS[i] * 1.5f, NOTAS[i] * 0.5f }, 1.3f, 0.5f);
        }

        // la fogata se apaga: dos notas graves que bajan (Sol y Do de abajo), suaves
        apagadaAlta = Fabricar("Apagada_1", new float[] { 392f }, 0.8f, 0.35f);
        apagadaBaja = Fabricar("Apagada_2", new float[] { 261.63f }, 1.2f, 0.5f);

        acorde = Fabricar("Acorde", new float[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 1.8f, 0.7f);
        notaAguda = Fabricar("Nota_Aguda", new float[] { 1046.5f }, 0.9f, 0.32f);
    }


    // =====================================================================
    // LO QUE PIDE EL JUEGO
    // =====================================================================

    // la seña que lograste (o que pruebas en las instrucciones)
    public void TocarSena(string gesto, bool dosManos)
    {
        int i = System.Array.IndexOf(SombrasJuego.NOMBRES, gesto);
        if (i < 0)
        {
            return;
        }

        fuente.PlayOneShot(Clip(i, dosManos), volumen);
    }

    // la palabra completa: todas las silabas seguidas y al final el acorde
    public void TocarPalabra(string[] serie)
    {
        Detener();
        melodia = StartCoroutine(Palabra(serie, false));
    }

    // la ultima palabra y despues la fanfarria de la graduacion
    public void TocarGraduacion(string[] serie)
    {
        Detener();
        melodia = StartCoroutine(Palabra(serie, true));
    }

    public void TocarApagada()
    {
        Detener();
        melodia = StartCoroutine(Apagada());
    }

    void Detener()
    {
        if (melodia != null)
        {
            StopCoroutine(melodia);
            melodia = null;
        }
    }


    // =====================================================================
    // LAS MELODIAS (corrutinas: esperan entre nota y nota sin congelar el juego)
    // =====================================================================

    IEnumerator Palabra(string[] serie, bool graduacion)
    {
        yield return new WaitForSeconds(pausaAntesDePalabra);

        for (int s = 0; s < serie.Length; s++)
        {
            int i = System.Array.IndexOf(SombrasJuego.NOMBRES, serie[s]);
            if (i >= 0)
            {
                fuente.PlayOneShot(Clip(i, false), volumen);
            }
            yield return new WaitForSeconds(pasoEntreSilabas);
        }

        yield return new WaitForSeconds(pasoEntreSilabas * 0.5f);
        fuente.PlayOneShot(acorde, volumen * 0.8f);

        if (graduacion)
        {
            // Do - Mi - Sol - Do agudo, y el acorde otra vez
            yield return new WaitForSeconds(1f);
            int[] subida = { 0, 2, 3 };
            for (int k = 0; k < subida.Length; k++)
            {
                fuente.PlayOneShot(notas[subida[k]], volumen);
                yield return new WaitForSeconds(0.18f);
            }
            fuente.PlayOneShot(notaAguda, volumen);
            yield return new WaitForSeconds(0.25f);
            fuente.PlayOneShot(acorde, volumen);
        }
        melodia = null;
    }

    IEnumerator Apagada()
    {
        fuente.PlayOneShot(apagadaAlta, volumen * 0.6f);
        yield return new WaitForSeconds(0.3f);
        fuente.PlayOneShot(apagadaBaja, volumen * 0.6f);
        melodia = null;
    }

    // el sonido grabado si lo hay; si no, la nota fabricada
    AudioClip Clip(int i, bool dosManos)
    {
        if (sonidosGrabados != null && i < sonidosGrabados.Length && sonidosGrabados[i] != null)
        {
            return sonidosGrabados[i];
        }
        if (dosManos)
        {
            return notasDobles[i];
        }
        return notas[i];
    }


    // =====================================================================
    // FABRICAR UN SONIDO
    // Un sonido es una lista de numeros entre -1 y 1 (la onda), 44100 por segundo.
    // Cada nota es una onda seno (el tono) mas un "golpe" agudo que dura muy poco,
    // como la baqueta contra la tecla de madera. Despues se va apagando sola (caida).
    // =====================================================================
    AudioClip Fabricar(string nombre, float[] frecuencias, float duracion, float caida)
    {
        int n = Mathf.RoundToInt(MUESTRAS * duracion);
        float[] datos = new float[n];
        float dosPi = 2f * Mathf.PI;

        for (int m = 0; m < n; m++)
        {
            float t = m / (float)MUESTRAS;

            // entra en 5 milisegundos (sin "click") y se apaga poco a poco
            float volumenNota = Mathf.Min(1f, t / 0.005f) * Mathf.Exp(-t / caida);

            float onda = 0f;
            for (int f = 0; f < frecuencias.Length; f++)
            {
                onda = onda + Mathf.Sin(dosPi * frecuencias[f] * t);
                onda = onda + 0.3f * Mathf.Sin(dosPi * frecuencias[f] * 4f * t) * Mathf.Exp(-t / 0.04f);
            }

            datos[m] = 0.5f * volumenNota * onda / frecuencias.Length;
        }

        // los ultimos 20 milisegundos bajan a cero, para que no truene al cortar
        int final = Mathf.Min(n, MUESTRAS / 50);
        for (int k = 0; k < final; k++)
        {
            datos[n - 1 - k] = datos[n - 1 - k] * k / (float)final;
        }

        AudioClip clip = AudioClip.Create(nombre, n, 1, MUESTRAS, false);
        clip.SetData(datos, 0);
        return clip;
    }
}
