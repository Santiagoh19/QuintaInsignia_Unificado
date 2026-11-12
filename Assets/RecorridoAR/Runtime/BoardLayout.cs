using UnityEngine;

namespace RecorridoAR
{
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public sealed class BoardLayout : MonoBehaviour
    {
        public RectTransform canvasRect;
        RectTransform board;
        void OnEnable() { board = GetComponent<RectTransform>(); Resize(); }
        void Update() { Resize(); }
        void Resize()
        {
            if (!board || !canvasRect) return;
            float scale = Mathf.Min(canvasRect.rect.width / 1280f, canvasRect.rect.height / 720f);
            if (scale <= 0) return;
            var wanted = new Vector3(scale, scale, 1);
            if (board.localScale != wanted) board.localScale = wanted;
        }
    }
}
