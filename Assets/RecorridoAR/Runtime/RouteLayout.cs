using UnityEngine;
namespace RecorridoAR
{
    // Keep webcam coordinates mapped to the whole board, independently of the preview box.
    public static class RouteLayout
    {
        public static Vector2 Compact(Vector2 p)
        {return new Vector2(980-(p.x-125)*(680f/1025f),215+(p.y-180)*.65f);}
        public static Vector2 IndexToBoard(float x,float y,bool mirror)
        {return new Vector2((mirror?1-x:x)*1280,y*720);}
    }
}
