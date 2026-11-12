using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Escena ElegirCarpa. Tres botones (BotonCarpa1..3): la carpa elegida se agranda, gira despacio
/// y aparece un aro brillante; su número se guarda en DatosJuego.carpaElegida.
/// Después de elegir aparece BotonConfirmar ("Listo").
/// </summary>
public class SeleccionCarpa : MonoBehaviour
{
    public BotonMano[] botones = new BotonMano[3];
    [Tooltip("Lo que gira de cada carpa (hijo del botón)")]
    public Transform[] modelos = new Transform[3];
    public GameObject[] aros = new GameObject[3];
    public BotonMano botonConfirmar;
    public Text textoElegida;

    public float escalaElegida = 1.25f;
    public float velocidadGiro = 35f;

    int _elegida;
    Quaternion[] _rotIniciales;

    void Start()
    {
        _rotIniciales = new Quaternion[modelos.Length];
        for (int i = 0; i < modelos.Length; i++) _rotIniciales[i] = modelos[i].localRotation;
        for (int i = 0; i < botones.Length; i++)
        {
            int numero = i + 1;
            botones[i].alPulsar.AddListener(() => Elegir(numero));
        }
        foreach (var a in aros) if (a) a.SetActive(false);
        if (botonConfirmar) botonConfirmar.gameObject.SetActive(false);
        if (textoElegida) textoElegida.text = "";
    }

    public void Elegir(int numero)
    {
        _elegida = numero;
        DatosJuego.carpaElegida = numero;
        for (int i = 0; i < botones.Length; i++)
        {
            bool esta = i == numero - 1;
            botones[i].multiplicador = esta ? escalaElegida : 1f;
            if (aros[i]) aros[i].SetActive(esta);
        }
        if (botonConfirmar) botonConfirmar.gameObject.SetActive(true);
        if (textoElegida) textoElegida.text = "Elegiste la carpa " + DatosJuego.NOMBRES[numero - 1];
    }

    void Update()
    {
        for (int i = 0; i < modelos.Length; i++)
        {
            if (i == _elegida - 1) modelos[i].Rotate(0f, velocidadGiro * Time.deltaTime, 0f, Space.World);
            else modelos[i].localRotation = Quaternion.Slerp(modelos[i].localRotation, _rotIniciales[i], 1f - Mathf.Exp(-4f * Time.deltaTime));
        }
    }
}
