using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using TMPro;

public class FingersMove : MonoBehaviour
{
    // ===== Inspector =====
    [Header("UDP")]
    [Tooltip("Debe coincidir con el puerto del emisor Python")]
    public int listenPort = 9050;

    [Header("Canvas")]
    public RectTransform canvasRect;  // MainCanvas (RectTransform)

    [Header("Finger UI (RectTransforms)")]
    public RectTransform L0, L1, L2, L3, L4;
    public RectTransform R0, R1, R2, R3, R4;

    [Header("Vista")]
    [Tooltip("Espejo horizontal/vertical para alinear con tu cámara")]
    public bool mirrorX = true;
    public bool mirrorY = true;

    [Header("Fluidez / Sensibilidad")]
    [Tooltip("Tiempo de suavizado hacia el objetivo (s). Menor = más rápido")]
    public float smoothTime = 0.06f;     // 0.03 - 0.12
    [Tooltip("Escala de sensibilidad (1 = normal)")]
    public float sensitivity = 1.0f;     // 0.8 - 1.2
    [Tooltip("Evita que los círculos salgan del área visible del Canvas")]
    public bool clampInsideCanvas = true;

    // ===== Tipos internos (coinciden con el JSON del emisor) =====
    [Serializable] class FingerSample { public string id; public float x; public float y; public bool isUp; }
    [Serializable] class Packet { public double ts; public FingerSample[] fingers; public int countL; public int countR; }

    // ===== Estado accesible por otros scripts =====
    public class HandState
    {
        // 0..4 = L0..L4 ; 5..9 = R0..R4
        public readonly bool[] up = new bool[10];
        public readonly Vector2[] canvas01 = new Vector2[10];
        public int countL, countR;

        public void Clear()
        {
            for (int i = 0; i < 10; i++) { up[i] = false; }
            countL = countR = 0;
        }
    }

    static FingersMove _instance;
    public static HandState GetState() => _instance != null ? _instance._state : null;

    readonly HandState _state = new HandState();

    // ===== Runtime =====
    UdpClient _client;
    Thread _thread;
    volatile bool _running;

    RectTransform[] _map = new RectTransform[10]; // índice 0..9 como arriba
    bool[] _show = new bool[10];
    Vector2[] _pos01 = new Vector2[10];
    Vector2[] _vel   = new Vector2[10];

    // ===== Unity lifecycle =====
    void Awake()
    {
        _instance = this;

        // Mapa manual
        _map[0]=L0; _map[1]=L1; _map[2]=L2; _map[3]=L3; _map[4]=L4;
        _map[5]=R0; _map[6]=R1; _map[7]=R2; _map[8]=R3; _map[9]=R4;
    }

    void OnEnable()
    {
        _running = true;
        _client = new UdpClient(new IPEndPoint(IPAddress.Loopback, listenPort));
        _thread = new Thread(ListenLoop) { IsBackground = true };
        _thread.Start();
    }

    void OnDisable()
    {
        _running = false;
        try { _client?.Close(); } catch {}
        try { _thread?.Join(200); } catch {}
    }

    void Update()
    {
        if (!canvasRect) return;

        var size = canvasRect.rect.size;
        Vector2 half = size * 0.5f;

        for (int i = 0; i < 10; i++)
        {
            var rt = _map[i]; if (!rt) continue;

            rt.gameObject.SetActive(_show[i]);
            if (!_show[i]) continue;

            var p = _pos01[i];
            if (mirrorX) p.x = 1f - p.x;
            if (mirrorY) p.y = 1f - p.y;

            // 0..1 -> coords locales del Canvas (anclado al centro)
            var target = new Vector2(
                (p.x - 0.5f) * size.x * sensitivity,
                (p.y - 0.5f) * size.y * sensitivity
            );

            var next = Vector2.SmoothDamp(rt.anchoredPosition, target, ref _vel[i], Mathf.Max(0.001f, smoothTime));

            if (clampInsideCanvas)
            {
                next.x = Mathf.Clamp(next.x, -half.x, half.x);
                next.y = Mathf.Clamp(next.y, -half.y, half.y);
            }

            rt.anchoredPosition = next;
        }

        
    }

    // ===== Networking =====
    void ListenLoop()
    {
        IPEndPoint ep = new IPEndPoint(IPAddress.Any, listenPort);
        while (_running)
        {
            try
            {
                var data = _client.Receive(ref ep);
                var json = Encoding.UTF8.GetString(data);
                var pkt = JsonUtility.FromJson<Packet>(json);

                // Reset visual de dedos no presentes en el paquete
                Array.Clear(_show, 0, _show.Length);

                if (pkt != null)
                {
                    lock (_state)
                    {
                        _state.Clear();
                        _state.countL = pkt.countL;
                        _state.countR = pkt.countR;

                        if (pkt.fingers != null)
                        {
                            foreach (var f in pkt.fingers)
                            {
                                int idx = ToIndex(f.id);
                                if (idx < 0) continue;

                                float x = Mathf.Clamp01(f.x);
                                float y = Mathf.Clamp01(f.y);

                                _show[idx] = f.isUp;
                                _pos01[idx] = new Vector2(x, y);

                                _state.up[idx] = f.isUp;
                                _state.canvas01[idx] = new Vector2(x, y);
                            }
                        }
                    }
                }

               
            }
            catch
            {
                // Ignorar paquetes malformados / cierres
            }
        }
    }

    // ===== Util =====
    void TryAutoAssign(string childName, int idx)
    {
        if (_map[idx]) return;
        var tr = transform.Find(childName);
        if (tr && tr is RectTransform rt) _map[idx] = rt;
    }

    int ToIndex(string id)
    {
        // "L0..L4" => 0..4 ; "R0..R4" => 5..9
        if (string.IsNullOrEmpty(id)) return -1;
        char hand = char.ToUpperInvariant(id[0]);
        if (id.Length < 2 || id[1] < '0' || id[1] > '4') return -1;
        int f = id[1] - '0';
        if (hand == 'L') return f;
        if (hand == 'R') return 5 + f;
        return -1;
    }
}
