using UnityEngine;
namespace RecorridoAR
{
    // Stateful level rules, independent of webcam, UI and Unity frame clock.
    public sealed class LevelOneRules
    {
        public enum Phase { Ready, Armed, Running, Captured, TimedOut, Won }
        public Phase State { get; private set; }
        public readonly RouteRules Path;
        public float Remaining { get; private set; }
        public float Progress { get; private set; }
        public Vector2 Battery { get; private set; }
        public bool Recovering { get; private set; }
        public float Limit=90, Tolerance=12, Confirmation=.25f;
        float outside, hold;
        // Default preserves the original rules and tests. The campaign supplies its route.
        public LevelOneRules(RouteRules path = null) { Path=path??new RouteRules(); Reset(); }
        public void Reset()
        {
            State=Phase.Ready; Remaining=Mathf.Max(1,Limit); Progress=outside=hold=0;
            Battery=Path.Points[0]; Recovering=false; Path.SetVisualProgress(0);
        }
        public void Arm() { if(State==Phase.Ready) State=Phase.Armed; }
        public void Tick(float seconds)
        {
            if(State!=Phase.Running) return;
            Remaining=Mathf.Max(0,Remaining-Mathf.Max(0,seconds));
            if(Remaining<=0) State=Phase.TimedOut;
        }
        public void Missing()
        {
            outside=hold=0;
            if(State==Phase.Running) Recovering=true;
        }
        public void Sample(Vector2 p,float seconds)
        {
            float dt=Mathf.Clamp(seconds,0,.1f);
            if(State==Phase.Armed)
            {
                if(Vector2.Distance(p,Path.Points[0])<=RouteRules.Radius)
                { hold+=dt; if(hold>=.45f) { State=Phase.Running; Battery=p; } }
                else hold=0;
                return;
            }
            if(State!=Phase.Running) return;
            Path.Project(p,out float arc,out float distance);
            float allowed=Path.LaneRadius+Mathf.Max(0,Tolerance);
            // After loss, reconnect at the battery; never bridge an unseen part of the route.
            if(Recovering)
            {
                if(distance<=allowed && Vector2.Distance(p,Battery)<=25 && Mathf.Abs(arc-Progress)<=25)
                { Recovering=false; outside=0; }
                return;
            }
            bool invalid=distance>allowed || arc>Progress+70 || arc<Progress-45 || Vector2.Distance(Battery,p)>70;
            int steps=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(Battery,p)/4));
            if(!invalid)
                for(int i=1;i<steps;i++)
                { Path.Project(Vector2.Lerp(Battery,p,i/(float)steps),out _,out float d); if(d>allowed) {invalid=true;break;} }
            if(invalid)
            {
                outside+=dt;
                if(outside>=Mathf.Max(.01f,Confirmation)) State=Phase.Captured;
                return;
            }
            outside=0; Battery=p; Progress=Mathf.Max(Progress,arc); Path.SetVisualProgress(Progress);
            if(Progress>=Path.Length-12 && Vector2.Distance(p,Path.Points[Path.Points.Count-1])<=RouteRules.Radius)
            { Progress=Path.Length; Path.SetVisualProgress(Progress); State=Phase.Won; }
        }
    }
}
