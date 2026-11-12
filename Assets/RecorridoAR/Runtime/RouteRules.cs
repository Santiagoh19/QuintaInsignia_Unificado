using System.Collections.Generic;
using UnityEngine;

namespace RecorridoAR
{
    // Pure game rules; no camera, network, clock or scene dependencies.
    public sealed class RouteRules
    {
        public readonly List<Vector2> Points = new List<Vector2>();
        readonly List<float> cumulative = new List<float>();
        public const float Radius = 22f;
        public float LaneRadius { get; private set; } = Radius;
        public float Length { get; private set; }
        public float Progress { get; private set; }
        public bool Active { get; private set; }
        public bool Won { get; private set; }
        public string Notice { get; private set; } = "Lleva el indice al circulo INICIO";
        float startHold;
        Vector2 previous;
        public RouteRules(int number = 2, bool compact = false)
        {
            if (number == 1)
            {
                LaneRadius = 31;
                Curve(new Vector2(125, 510), new Vector2(230, 510), new Vector2(240, 315), new Vector2(410, 315));
                Curve(new Vector2(410, 315), new Vector2(570, 315), new Vector2(600, 455), new Vector2(760, 455));
                Curve(new Vector2(760, 455), new Vector2(950, 455), new Vector2(980, 200), new Vector2(1150, 200));
            }
            else if (number == 3)
            {
                LaneRadius = 20;
                Curve(new Vector2(125, 510), new Vector2(95, 420), new Vector2(110, 200), new Vector2(235, 200));
                Curve(new Vector2(235, 200), new Vector2(360, 200), new Vector2(245, 520), new Vector2(385, 520));
                Curve(new Vector2(385, 520), new Vector2(525, 520), new Vector2(410, 200), new Vector2(545, 200));
                Curve(new Vector2(545, 200), new Vector2(680, 200), new Vector2(565, 520), new Vector2(700, 520));
                Curve(new Vector2(700, 520), new Vector2(835, 520), new Vector2(720, 200), new Vector2(855, 200));
                Curve(new Vector2(855, 200), new Vector2(990, 200), new Vector2(875, 520), new Vector2(1010, 520));
                Curve(new Vector2(1010, 520), new Vector2(1145, 520), new Vector2(1025, 200), new Vector2(1150, 200));
            }
            else
            {
            // Level 2 is the exact original route, including all control points.
            Curve(new Vector2(125, 510), new Vector2(90, 430), new Vector2(105, 180), new Vector2(270, 180));
            Curve(new Vector2(270, 180), new Vector2(440, 180), new Vector2(270, 530), new Vector2(460, 530));
            Curve(new Vector2(460, 530), new Vector2(645, 530), new Vector2(480, 180), new Vector2(680, 180));
            Curve(new Vector2(680, 180), new Vector2(870, 180), new Vector2(695, 530), new Vector2(890, 530));
            Curve(new Vector2(890, 530), new Vector2(1050, 530), new Vector2(1000, 200), new Vector2(1150, 200));
            }
            Finish(compact);
        }
        void Finish(bool compact)
        {
            if(compact)for(int i=0;i<Points.Count;i++)Points[i]=RouteLayout.Compact(Points[i]);
            cumulative.Add(0);
            for (int i = 1; i < Points.Count; i++) { Length += Vector2.Distance(Points[i-1], Points[i]); cumulative.Add(Length); }
        }
        RouteRules(float lane,Vector2[] controls)
        {
            LaneRadius=lane;
            for(int i=0;i+3<controls.Length;i+=4)Curve(controls[i],controls[i+1],controls[i+2],controls[i+3]);
            Finish(true);
        }

        // Ruta de Energía: 3 difficulties x 3 routes. Route 1 of each difficulty is the original stage route
        // (1 = Primeros pasos, 2 = the professor's route, 3 = El gran recorrido); routes 2 and 3 keep the same
        // lane width and number of curves. All start at Inicio (right) and end at the flashlight (left).
        public static RouteRules Crear(int dificultad,int etapa)
        {
            dificultad=Mathf.Clamp(dificultad,1,3); etapa=Mathf.Clamp(etapa,1,3);
            if(etapa==1)return new RouteRules(dificultad,true);
            float lane=dificultad==1?31:dificultad==3?20:Radius;
            return new RouteRules(lane,Diseno(dificultad,etapa));
        }
        static Vector2 V(float x,float y)=>new Vector2(x,y);
        static Vector2[] Diseno(int dificultad,int etapa)
        {
            if(dificultad==1 && etapa==2) return new[]{
                V(125,510),V(260,520),V(300,250),V(470,250), V(470,250),V(640,250),V(660,480),V(820,480),
                V(820,480),V(980,480),V(1010,200),V(1150,200)};
            if(dificultad==1) return new[]{
                V(125,510),V(300,510),V(330,230),V(560,230), V(560,230),V(780,230),V(800,430),V(960,430),
                V(960,430),V(1080,430),V(1100,200),V(1150,200)};
            if(dificultad==2 && etapa==2) return new[]{
                V(125,510),V(260,510),V(190,220),V(330,220), V(330,220),V(480,220),V(410,510),V(560,510),
                V(560,510),V(710,510),V(640,210),V(790,210), V(790,210),V(940,210),V(870,500),V(1010,500),
                V(1010,500),V(1110,500),V(1090,200),V(1150,200)};
            if(dificultad==2) return new[]{
                V(125,510),V(100,390),V(190,300),V(300,320), V(300,320),V(430,345),V(390,530),V(520,510),
                V(520,510),V(660,490),V(560,185),V(700,190), V(700,190),V(840,195),V(770,480),V(900,480),
                V(900,480),V(1040,480),V(1000,200),V(1150,200)};
            if(etapa==2) return new[]{
                V(125,510),V(250,510),V(150,250),V(260,250), V(260,250),V(380,250),V(280,520),V(400,520),
                V(400,520),V(520,520),V(420,210),V(540,210), V(540,210),V(660,210),V(560,500),V(680,500),
                V(680,500),V(800,500),V(700,230),V(820,230), V(820,230),V(940,230),V(840,470),V(960,470),
                V(960,470),V(1090,470),V(1060,200),V(1150,200)};
            return new[]{
                V(125,510),V(188,510),V(167,290),V(230,290), V(230,290),V(314,290),V(286,505),V(370,505),
                V(370,505),V(454,505),V(426,205),V(510,205), V(510,205),V(594,205),V(566,445),V(650,445),
                V(650,445),V(734,445),V(706,220),V(790,220), V(790,220),V(874,220),V(846,505),V(930,505),
                V(930,505),V(1062,505),V(1018,200),V(1150,200)};
        }
        void Curve(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            for (int i = Points.Count == 0 ? 0 : 1; i <= 60; i++)
            {
                float t = i / 60f, u = 1-t;
                Points.Add(u*u*u*a + 3*u*u*t*b + 3*u*t*t*c + t*t*t*d);
            }
        }
        public void SetVisualProgress(float value) { Progress = Mathf.Clamp(value,0,Length); }
        public void Reset(string notice = "Lleva el indice al circulo INICIO")
        { Progress = 0; Active = Won = false; startHold = 0; Notice = notice; }
        public void Lost()
        {
            if (!Won) Reset("Indice no detectado: vuelve a INICIO");
        }
        public void Project(Vector2 p, out float arc, out float distance)
        {
            arc = 0; float best = float.MaxValue;
            for (int i = 1; i < Points.Count; i++)
            {
                Vector2 delta = Points[i] - Points[i-1];
                float t = Mathf.Clamp01(Vector2.Dot(p-Points[i-1], delta) / delta.sqrMagnitude);
                float d = (p - (Points[i-1] + t*delta)).sqrMagnitude;
                if (d < best) { best = d; arc = cumulative[i-1] + t*delta.magnitude; }
            }
            distance = Mathf.Sqrt(best);
        }
        public void Step(Vector2 p, float seconds)
        {
            if (Won) return;
            if (!Active)
            {
                if (Vector2.Distance(p, Points[0]) <= Radius)
                {
                    startHold += Mathf.Clamp(seconds, 0, 0.1f);
                    Notice = "Manten el indice en INICIO...";
                    if (startHold >= 0.45f) { Active = true; previous = p; Notice = "Sigue la linea blanca en orden"; }
                }
                else startHold = 0;
                return;
            }
            Project(p, out float arc, out float distance);
            if (distance > Radius) { Reset("Saliste del camino. Vuelve a INICIO"); return; }
            if (arc > Progress + 70 || arc < Progress - 45 || Vector2.Distance(previous, p) > 70)
            { Reset("Movimiento demasiado rapido / salto. Vuelve a INICIO"); return; }
            // Check the entire motion between samples, not just its endpoint.
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(previous, p) / 4));
            for (int i = 1; i < steps; i++)
            {
                Project(Vector2.Lerp(previous, p, i/(float)steps), out _, out float d);
                if (d > Radius) { Reset("Saliste del camino. Vuelve a INICIO"); return; }
            }
            previous = p; Progress = Mathf.Max(Progress, arc);
            if (Progress >= Length-12 && Vector2.Distance(p, Points[Points.Count-1]) <= Radius)
            { Progress = Length; Won = true; Active = false; Notice = "¡Recorrido completado!"; }
        }
    }
}
