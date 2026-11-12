using UnityEngine;
namespace RecorridoAR
{
    public static class CaptureMotion
    {
        // Source PNG points right. Its nose lies near x=98%, y=53.5% of the image.
        // Retreat without flipping the image: the wolf keeps holding Lucas while backing out.
        public static bool Evaluate(float seconds, Vector2 home, Vector2 wolfSize, float speed,
            out Vector2 wolf, out Vector2 child, out bool dragging)
        {
            speed = Mathf.Max(40, speed);
            Vector2 mouth = new Vector2(.48f * wolfSize.x, -.035f * wolfSize.y);
            Vector2 contact = home + new Vector2(-12, 0) - mouth;
            Vector2 start = new Vector2(-640 - wolfSize.x * .55f, contact.y);
            float approach = Vector2.Distance(start, contact) / speed;
            child = home; dragging = false;
            if (seconds < approach)
            { wolf = Vector2.Lerp(start, contact, Mathf.Clamp01(seconds / approach)); return false; }
            float travel = Mathf.Max(0, seconds - approach - .18f) * speed * 1.15f;
            float exit = Mathf.Max(0, home.x + 640 + 180);
            Vector2 offset = new Vector2(Mathf.Min(travel, exit), 0);
            wolf = contact - offset; child = home - offset; dragging = travel > 0;
            return travel >= exit;
        }
    }
}
