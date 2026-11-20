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

        static Sprite roundSquare;
        static Sprite roundSliced;

        // Rounded square of 1x1 unit, for objects that scale (parts).
        public static Sprite RoundSquare
        {
            get
            {
                if (roundSquare == null) roundSquare = RoundedSprite(128, 28, false);
                return roundSquare;
            }
        }

        // Rounded rectangle for 9-slice drawing, so corners keep their size at any width.
        static Sprite RoundSliced
        {
            get
            {
                if (roundSliced == null) roundSliced = RoundedSprite(64, 12, true);
                return roundSliced;
            }
        }

        static Sprite RoundedSprite(int n, int radius, bool sliced)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, radius, n - radius);
                    float cy = Mathf.Clamp(y + 0.5f, radius, n - radius);
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    float a = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                    px[y * n + x] = new Color(1f, 1f, 1f, a);
                }
            }
            t.SetPixels(px);
            t.filterMode = FilterMode.Bilinear;
            t.wrapMode = TextureWrapMode.Clamp;
            t.Apply();
            if (!sliced) return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n, 0,
                                 SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }

        // Rounded rectangle of the given world size; corners stay round however long it is.
        public static SpriteRenderer MakeRound(string name, Color color, Transform parent,
                                               Vector2 localPos, Vector2 size, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = RoundSliced;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = size;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
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
