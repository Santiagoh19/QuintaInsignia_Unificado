namespace RecorridoAR
{
    // Ruta de Energia is level 2 of Quinta Insignia. The player chooses a difficulty (Fácil, Media, Difícil);
    // each difficulty has its own three routes, and each route awards one battery.
    public sealed class CampaignRules
    {
        public const int LevelCount=3;
        public int Number { get; private set; }=1;          // route (battery) 1..3 inside the chosen difficulty
        public int Dificultad { get; private set; }=1;      // 1 Fácil, 2 Media, 3 Difícil
        public int Batteries { get; private set; }
        public LevelOneRules Current { get; private set; }
        public bool Complete => Batteries==LevelCount;
        bool awarded;
        public CampaignRules(int dificultad=1) { Dificultad=Clamp(dificultad); Load(); }
        static int Clamp(int d)=>d<1?1:d>3?3:d;
        // Time per route: Fácil 1:30, Media 1:00, Difícil 0:40.
        public static float Seconds(int dificultad) => dificultad==1?90:dificultad==2?60:40;
        public static float Tolerance(int dificultad) => dificultad==3?10:12;
        public static string Name(int dificultad) => dificultad==1?"Fácil":dificultad==2?"Media":"Difícil";
        void Load()
        {
            Current=new LevelOneRules(RouteRules.Crear(Dificultad,Number));
            Current.Limit=Seconds(Dificultad);Current.Tolerance=Tolerance(Dificultad);
            Current.Reset();awarded=false;
        }
        // Choosing a difficulty starts its three routes from the first one, with no batteries.
        public void Elegir(int dificultad) { Dificultad=Clamp(dificultad); Restart(); }
        public bool Collect()
        {
            if(awarded || Current.State!=LevelOneRules.Phase.Won)return false;
            awarded=true;Batteries=Number;return true;
        }
        public bool Advance()
        {
            Collect();
            if(!awarded || Complete)return false;
            Number++;Load();return true;
        }
        public void Retry() { if(Current.State!=LevelOneRules.Phase.Won)Load(); }
        public void Restart() { Number=1;Batteries=0;Load(); }
    }
}
