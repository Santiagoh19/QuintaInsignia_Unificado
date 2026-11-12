using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using Clock = System.Diagnostics.Stopwatch;

namespace RecorridoAR
{
    [Serializable] public class Finger { public string id; public float x, y; public bool isUp; }
    [Serializable] public class Packet { public double ts; public Finger[] fingers; public int countL, countR; }

    // No Unity API on worker threads. JSON and JPEG decoding occur on main thread.
    public sealed class RecorridoTransport : IDisposable
    {
        sealed class Datagram { public string json; public double arrival; }
        public sealed class VideoFrame { public byte[] jpeg; public double ts, arrival; }
        public sealed class Sample { public Packet packet; public double arrival; }
        readonly ConcurrentQueue<Datagram> incoming = new ConcurrentQueue<Datagram>();
        readonly Dictionary<double, Sample> samples = new Dictionary<double, Sample>();
        readonly Queue<double> keys = new Queue<double>();
        readonly object gate = new object();
        readonly ManualResetEvent stop = new ManualResetEvent(false);
        UdpClient udp;
        TcpClient tcp;
        Thread udpThread, videoThread;
        VideoFrame latest;
        volatile bool running;
        public string Error { get; private set; }
        public Sample LatestSample { get; private set; }
        public static double Now => (double)Clock.GetTimestamp() / Clock.Frequency;

        public void Start()
        {
            try { udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 9050)); }
            catch (SocketException e) { Error = "UDP 9050 ocupado. Deten SampleScene / otro Unity. " + e.SocketErrorCode; return; }
            running = true;
            udpThread = new Thread(ReadUdp) { IsBackground = true, Name = "AR UDP" };
            videoThread = new Thread(ReadVideo) { IsBackground = true, Name = "AR Video" };
            udpThread.Start(); videoThread.Start();
        }
        void ReadUdp()
        {
            var ep = new IPEndPoint(IPAddress.Loopback, 0);
            while (running)
            {
                try
                {
                    byte[] bytes = udp.Receive(ref ep);
                    if (!IPAddress.IsLoopback(ep.Address) || bytes.Length > 32000) continue;
                    incoming.Enqueue(new Datagram { json = Encoding.UTF8.GetString(bytes), arrival = Now });
                    while (incoming.Count > 120) incoming.TryDequeue(out _);
                }
                catch (SocketException) { if (running) stop.WaitOne(50); }
                catch (ObjectDisposedException) { break; }
            }
        }
        static byte[] ReadExactly(Stream stream, int count)
        {
            byte[] data = new byte[count];
            int offset = 0;
            while (offset < count)
            {
                int n = stream.Read(data, offset, count - offset);
                if (n == 0) throw new EndOfStreamException();
                offset += n;
            }
            return data;
        }
        void ReadVideo()
        {
            while (running)
            {
                TcpClient client = null;
                try
                {
                    client = new TcpClient { NoDelay = true, ReceiveTimeout = 1500 };
                    lock (gate) { if (!running) { client.Close(); break; } tcp = client; }
                    client.Connect(IPAddress.Loopback, 9051);
                    using (NetworkStream stream = client.GetStream())
                    {
                        while (running)
                        {
                            byte[] header = ReadExactly(stream, 12);
                            uint count = ((uint)header[0] << 24) | ((uint)header[1] << 16) | ((uint)header[2] << 8) | header[3];
                            if (count < 1 || count > 2000000) throw new IOException("JPEG length invalida");
                            byte[] timestamp = new byte[8]; Array.Copy(header, 4, timestamp, 0, 8);
                            if (BitConverter.IsLittleEndian) Array.Reverse(timestamp);
                            double ts = BitConverter.ToDouble(timestamp, 0);
                            var frame = new VideoFrame { jpeg = ReadExactly(stream, (int)count), ts = ts, arrival = Now };
                            lock (gate) latest = frame;
                        }
                    }
                }
                catch (Exception e) when (e is IOException || e is SocketException || e is ObjectDisposedException) { }
                finally { client?.Close(); lock (gate) { if (tcp == client) tcp = null; } }
                stop.WaitOne(400);
            }
        }
        public void Pump()
        {
            while (incoming.TryDequeue(out Datagram d))
            {
                try
                {
                    Packet p = JsonUtility.FromJson<Packet>(d.json);
                    if (p == null || p.fingers == null || double.IsNaN(p.ts) || double.IsInfinity(p.ts) || p.ts <= 0) continue;
                    var sample = new Sample { packet = p, arrival = d.arrival };
                    LatestSample = sample;
                    if (!samples.ContainsKey(p.ts)) keys.Enqueue(p.ts);
                    samples[p.ts] = sample;
                    while (keys.Count > 120) samples.Remove(keys.Dequeue());
                }
                catch (ArgumentException) { }
            }
        }
        public VideoFrame TakeVideo() { lock (gate) { var v = latest; latest = null; return v; } }
        public bool Match(double ts, out Sample sample) => samples.TryGetValue(ts, out sample);
        public void Dispose()
        {
            running = false; stop.Set(); udp?.Close();
            lock (gate) tcp?.Close();
            udpThread?.Join(2000); videoThread?.Join(2000);
            // Keep the wait handle valid if OS connect cancellation is slow.
            if ((udpThread == null || !udpThread.IsAlive) && (videoThread == null || !videoThread.IsAlive)) stop.Dispose();
        }
    }
}
