using UnityEngine;

namespace ArmSort
{
    // Builds simple sprites at runtime so the prototype needs no art files.
    public static class Shapes
    {
        static Sprite square;
        static Sprite circle;

        public static Sprite Square
        {
            get
            {
                if (square == null)
                {
                    var t = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                    var px = new Color[16];
                    for (int i = 0; i < px.Length; i++) px[i] = Color.white;
                    t.SetPixels(px);
                    t.filterMode = FilterMode.Point;
                    t.wrapMode = TextureWrapMode.Clamp;
                    t.Apply();
                    square = Sprite.Create(t, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
                }
                return square;
            }
        }

        public static Sprite Circle
        {
            get
            {
                if (circle == null)
                {
                    const int n = 128;
                    var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
                    var px = new Color[n * n];
                    float r = n / 2f;
                    for (int y = 0; y < n; y++)
                    {
                        for (int x = 0; x < n; x++)
                        {
                            float dx = x + 0.5f - r;
                            float dy = y + 0.5f - r;
                            float a = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy));
                            px[y * n + x] = new Color(1f, 1f, 1f, a);
                        }
                    }
                    t.SetPixels(px);
                    t.filterMode = FilterMode.Bilinear;
                    t.wrapMode = TextureWrapMode.Clamp;
                    t.Apply();
                    circle = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
                }
                return circle;
            }
        }

        public static SpriteRenderer Make(string name, Sprite sprite, Color color, Transform parent,
                                          Vector2 localPos, Vector2 size, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }
    }
}
