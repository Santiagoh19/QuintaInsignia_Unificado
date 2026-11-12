using UnityEngine;

public class HandStateLogger : MonoBehaviour
{
    // Enumerador para los estados que vienen de tu proyecto original
    public enum MoveState { Nulo, Centro, Adelante, Atras, Izquierda, Derecha }
    public MoveState currentState = MoveState.Nulo;

    // Mantenemos los nombres legibles por si en el futuro necesitas debuggear
    static readonly string[] FingerNames =
    {
        "L0 (Pulgar Izq)",  "L1 (Índice Izq)",  "L2 (Medio Izq)",
        "L3 (Anular Izq)",  "L4 (Meñique Izq)",
        "R0 (Pulgar Der)",  "R1 (Índice Der)",   "R2 (Medio Der)",
        "R3 (Anular Der)",  "R4 (Meñique Der)"
    };

    void Update()
    {
        // Obtenemos los datos de la red (MediaPipe)
        var state = FingersMove.GetState();

        // Si no hay datos o no hay manos en pantalla, el estado es Nulo
        if (state == null || (state.countL == 0 && state.countR == 0))
        {
            SetState(MoveState.Nulo);
            return;
        }

        // Leemos la mano derecha basándonos en los índices de FingersMove
        // R0=5 (Pulgar), R1=6 (Índice), R2=7 (Medio), R3=8 (Anular), R4=9 (Meñique)
        bool thumbUp = state.up[5];
        bool indexUp = state.up[6];
        bool middleUp = state.up[7];
        bool ringUp = state.up[8];
        bool pinkyUp = state.up[9];

        DetermineGesture(thumbUp, indexUp, middleUp, ringUp, pinkyUp);
    }

    void DetermineGesture(bool thumb, bool index, bool middle, bool ring, bool pinky)
    {
        MoveState newState = MoveState.Nulo;

        // 1. Adelante: Índice, medio y anular extendidos (pulgar y meñique abajo)
        if (!thumb && index && middle && ring && !pinky)
        {
            newState = MoveState.Adelante;
        }
        // 2. Atrás: Índice y medio extendidos (Símbolo de la paz)
        else if (!thumb && index && middle && !ring && !pinky)
        {
            newState = MoveState.Atras;
        }
        // 3. Izquierda: Solo pulgar extendido apuntando
        else if (thumb && !index && !middle && !ring && !pinky)
        {
            newState = MoveState.Izquierda;
        }
        // 4. Derecha: Solo meñique extendido
        else if (!thumb && !index && !middle && !ring && pinky)
        {
            newState = MoveState.Derecha;
        }
        // 5. Centro: Solo índice extendido
        else if (!thumb && index && !middle && !ring && !pinky)
        {
            newState = MoveState.Centro;
        }
        // Nulo: Cualquier otra combinación (como la mano abierta por completo)
        else
        {
            newState = MoveState.Nulo;
        }

        SetState(newState);
    }

    void SetState(MoveState newState)
    {
        // Solo ejecutamos la acción si el gesto ha cambiado, para no llamar a las clases cada frame
        if (currentState != newState)
        {
            currentState = newState;
            ExecuteGameAction(newState);
        }
    }

    // --- INTEGRACIÓN CON TU JUEGO ---
    void ExecuteGameAction(MoveState state)
    {
        // Esto imprimirá en la consola de Unity qué gesto se acaba de activar
        Debug.Log(" Gesto detectado -> Acción activada: " + state);

        switch (state)
        {
            case MoveState.Adelante:
                // Reemplaza esto con la instancia o función de tu clase "Adelante"
                break;
            case MoveState.Atras:
                // Reemplaza esto con la instancia o función de tu clase "Atras"
                break;
            case MoveState.Izquierda:
                // Reemplaza esto con la instancia o función de tu clase "Izquierda"
                break;
            case MoveState.Derecha:
                // Reemplaza esto con la instancia o función de tu clase "Derecha"
                break;
            case MoveState.Centro:
                // Reemplaza esto con la instancia o función de tu clase "Centro"
                break;
            case MoveState.Nulo:
                // Reemplaza esto con la función que detiene el movimiento
                break;
        }
    }
}