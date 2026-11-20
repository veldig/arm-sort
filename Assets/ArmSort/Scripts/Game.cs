using System.Collections.Generic;
using UnityEngine;

namespace ArmSort
{
    // Core loop: parts ride a conveyor, the player drags the arm to grab each part
    // and drops it in the bin of the same color before it falls off the belt.
    public class Game : MonoBehaviour
    {
        enum State { Menu, Levels, Playing, Won, Lost }

        static readonly Color[] Palette =
        {
            new Color(0.96f, 0.36f, 0.36f),
            new Color(0.27f, 0.62f, 0.98f),
            new Color(0.99f, 0.78f, 0.22f),
            new Color(0.32f, 0.83f, 0.52f),
        };
        static readonly Color Background = new Color(0.09f, 0.11f, 0.16f);
        static readonly Color BeltColor = new Color(0.20f, 0.23f, 0.30f);
        static readonly Color StripeColor = new Color(0.28f, 0.31f, 0.40f);
        static readonly Color ChuteColor = new Color(0.30f, 0.33f, 0.42f);
        static readonly Color ArmBody = new Color(0.82f, 0.85f, 0.90f);
        static readonly Color ArmJoint = new Color(0.98f, 0.55f, 0.20f);
        static readonly Color WrongFlash = new Color(0.90f, 0.20f, 0.20f);
        static readonly Color Gold = new Color(1f, 0.80f, 0.25f);
        static readonly Color Dim = new Color(1f, 1f, 1f, 0.18f);

        const float BeltHalf = 3.2f;
        const float SpawnX = -BeltHalf - 0.3f;
        const float ComboWindow = 2.5f; // seconds between correct drops to keep a combo

        // Layout. Phones in portrait get a taller layout with a longer arm, so the game
        // fills the screen instead of sitting in a band across the middle.
        float BaseY, BeltY, BinY, PartSize, GrabRadius, BinW, BinH, L1, L2;
        float ObstacleX, ObstacleY, ObstacleW, ObstacleH;
        bool tall;
        const float StripeGap = 0.8f;
        const int StripeCount = 9;
        const int MaxMisses = 3;
        const int FirstObstacleLevel = 4;   // level 5 for the player
        const int MovingObstacleLevel = 10; // level 11
        const string UnlockedKey = "armsort.unlocked";
        const string OldLevelKey = "armsort.level";
        static string StarsKey(int i) => "armsort.stars." + i;

        class Part
        {
            public Transform t;
            public SpriteRenderer sr;
            public Transform shadow;
            public int color;
            public bool onBelt = true;
        }

        class Bin
        {
            public Transform t;
            public SpriteRenderer inside;
            public int color;
            public Rect area;
            public float flash;
        }

        class Obstacle
        {
            public Transform t;
            public SpriteRenderer body;
            public Rect rect;
            public float baseY;
            public bool moves;
            public float flash;
        }

        class Spark
        {
            public Transform t;
            public SpriteRenderer sr;
            public Vector2 v;
            public float life, maxLife, size;
        }

        class Floater
        {
            public Vector3 pos;
            public string text;
            public Color color;
            public float life;
        }

        readonly List<Part> parts = new List<Part>();
        readonly List<Bin> bins = new List<Bin>();
        readonly List<Transform> stripes = new List<Transform>();
        readonly List<Spark> sparks = new List<Spark>();
        readonly List<Floater> floaters = new List<Floater>();
        readonly List<Obstacle> obstacles = new List<Obstacle>();
        Vector3 camHome = new Vector3(0f, -0.4f, -10f);

        Camera cam;
        Transform world;
        RobotArm arm;
        Part held;
        State state;
        int level, unlocked, colorCount, toSpawn, total, sorted, misses, stars, combo, bestCombo, score;
        float lastGoodTime = -10f;
        float beltSpeed, spawnEvery, spawnTimer, shake, blockSoundCooldown;
        Rect flipRect;
        bool armWasDown, blockPress, wasPressed;
        Vector2 levelScroll;

        AudioSource audioSrc, comboSrc;
        AudioClip sGrab, sGood, sBad, sWin;

        GUIStyle big, small, button, smallButton;
        Texture2D white, btnTex, btnHover, btnGray;

        void Awake()
        {
            Application.targetFrameRate = 60;
            cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
            }
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Background;
            cam.transform.SetPositionAndRotation(camHome, Quaternion.identity);
            if (FindAnyObjectByType<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();

            audioSrc = gameObject.AddComponent<AudioSource>();
            comboSrc = gameObject.AddComponent<AudioSource>();
            sGrab = Sfx.Notes("grab", new[] { 660f }, 0.05f);
            sGood = Sfx.Notes("good", new[] { 660f, 990f }, 0.07f);
            sBad = Sfx.Notes("bad", new[] { 150f, 110f }, 0.10f, true, 0.25f);
            sWin = Sfx.Notes("win", new[] { 523f, 659f, 784f, 1047f }, 0.09f);

            unlocked = PlayerPrefs.GetInt(UnlockedKey, PlayerPrefs.GetInt(OldLevelKey, 0));
            level = unlocked;

            // Testing link: ...?level=5 opens that level for this visit only (nothing is saved).
            var url = Application.absoluteURL;
            int at = string.IsNullOrEmpty(url) ? -1 : url.IndexOf("level=", System.StringComparison.Ordinal);
            if (at >= 0)
            {
                int end = at + 6;
                while (end < url.Length && char.IsDigit(url[end])) end++;
                if (int.TryParse(url.Substring(at + 6, end - at - 6), out int n) && n >= 1)
                {
                    level = n - 1;
                    unlocked = Mathf.Max(unlocked, level);
                }
            }
            BuildLevel();
            state = State.Menu;
        }

        void ChooseLayout()
        {
            tall = cam.aspect < 0.7f;
            if (tall)
            {
                BaseY = -6.2f; BeltY = -1.6f; BinY = 2.6f;
                PartSize = 0.74f; GrabRadius = 0.65f; BinW = 1.45f; BinH = 1.2f;
                L1 = 4.8f; L2 = 4.7f;
                ObstacleX = 1.6f; ObstacleY = -3.9f; ObstacleW = 0.8f; ObstacleH = 1.6f;
                camHome = new Vector3(-0.15f, -1.4f, -10f);
            }
            else
            {
                BaseY = -4.6f; BeltY = -1.0f; BinY = 1.5f;
                PartSize = 0.62f; GrabRadius = 0.55f; BinW = 1.3f; BinH = 1.0f;
                L1 = 3.4f; L2 = 3.2f;
                ObstacleX = 1.5f; ObstacleY = -2.9f; ObstacleW = 0.7f; ObstacleH = 1.2f;
                camHome = new Vector3(0f, -0.4f, -10f);
            }
        }

        void BuildBackground()
        {
            var grid = new Color(1f, 1f, 1f, 0.022f);
            for (int x = -10; x <= 10; x++)
                Shapes.Make("GridV", Shapes.Square, grid, world, new Vector2(x, 0f), new Vector2(0.02f, 40f), -10);
            for (int y = -12; y <= 10; y++)
                Shapes.Make("GridH", Shapes.Square, grid, world, new Vector2(0f, y), new Vector2(40f, 0.02f), -10);
        }

        void BuildLevel()
        {
            ChooseLayout();
            if (world != null) Destroy(world.gameObject);
            parts.Clear();
            bins.Clear();
            stripes.Clear();
            sparks.Clear();
            floaters.Clear();
            obstacles.Clear();
            held = null;
            world = new GameObject("World").transform;
            BuildBackground();
            combo = 0;
            bestCombo = 0;
            score = 0;

            colorCount = Mathf.Clamp(2 + level / 5, 2, Palette.Length);
            total = Mathf.Min(6 + level * 2, 40);
            toSpawn = total;
            sorted = 0;
            misses = 0;
            beltSpeed = Mathf.Min(0.8f + level * 0.08f, 2.6f);
            spawnEvery = Mathf.Max(0.6f, 2.0f - level * 0.05f);
            spawnTimer = 0.5f;

            Shapes.MakeRound("BeltShadow", new Color(0f, 0f, 0f, 0.3f), world, new Vector2(0.08f, BeltY - 0.12f),
                             new Vector2(BeltHalf * 2f + 0.8f, 1.05f), -1);
            Shapes.MakeRound("Belt", BeltColor, world, new Vector2(0f, BeltY), new Vector2(BeltHalf * 2f + 0.8f, 1.0f), 0);
            for (int i = 0; i < StripeCount; i++)
            {
                var s = Shapes.Make("Stripe", Shapes.Square, StripeColor, world,
                                    new Vector2(-BeltHalf + i * StripeGap, BeltY), new Vector2(0.12f, 0.9f), 1);
                stripes.Add(s.transform);
            }

            // Chute at the left end: parts come out from under it.
            Shapes.MakeRound("Chute", ChuteColor, world, new Vector2(SpawnX, BeltY + 0.2f), new Vector2(1.2f, 1.6f), 6);
            Shapes.MakeRound("ChuteLip", ArmJoint, world, new Vector2(SpawnX, BeltY + 0.98f), new Vector2(1.3f, 0.16f), 6);

            float span = 4.4f;
            float w = colorCount >= 4 ? Mathf.Min(BinW, 1.1f) : BinW;
            float bh = BinH;
            for (int i = 0; i < colorCount; i++)
            {
                float x = Mathf.Lerp(-span / 2f, span / 2f, i / (float)(colorCount - 1));
                var b = new Bin { color = i };
                b.t = new GameObject("Bin").transform;
                b.t.SetParent(world, false);
                b.t.localPosition = new Vector2(x, BinY);
                Shapes.MakeRound("Shadow", new Color(0f, 0f, 0f, 0.3f), b.t, new Vector2(0.08f, -0.12f), new Vector2(w + 0.2f, bh + 0.2f), -1);
                Shapes.MakeRound("Rim", Palette[i], b.t, Vector2.zero, new Vector2(w + 0.2f, bh + 0.2f), 0);
                b.inside = Shapes.MakeRound("Inside", Background, b.t, new Vector2(0f, -0.04f), new Vector2(w - 0.04f, bh - 0.08f), 1);
                var mark = Palette[i];
                mark.a = 0.35f;
                Shapes.Make("Mark", ShapeFor(i), mark, b.t, Vector2.zero, Vector2.one * (bh * 0.42f), 2);
                b.area = new Rect(x - w / 2f - 0.15f, BinY - bh / 2f - 0.15f, w + 0.3f, bh + 0.3f);
                bins.Add(b);
            }

            var armGo = new GameObject("Arm");
            armGo.transform.SetParent(world, false);
            armGo.transform.localPosition = new Vector2(0f, BaseY);
            arm = armGo.AddComponent<RobotArm>();
            arm.L1 = L1;
            arm.L2 = L2;
            arm.Build(ArmBody, ArmJoint);

            if (level >= FirstObstacleLevel) AddObstacle();
        }

        // A block below the belt on one side. The arm can always reach every target
        // with one of its two elbow poses, so the player has to pick the right one.
        void AddObstacle()
        {
            float side = level % 2 == 0 ? -1f : 1f;
            float w = ObstacleW, h = ObstacleH;
            float cx = side * ObstacleX, cy = ObstacleY;
            var o = new Obstacle { moves = level >= MovingObstacleLevel, baseY = cy };
            o.t = new GameObject("Obstacle").transform;
            o.t.SetParent(world, false);
            o.t.localPosition = new Vector2(cx, cy);
            Shapes.MakeRound("Shadow", new Color(0f, 0f, 0f, 0.3f), o.t, new Vector2(0.08f, -0.12f), new Vector2(w + 0.12f, h + 0.12f), 1);
            Shapes.MakeRound("Edge", ArmJoint, o.t, Vector2.zero, new Vector2(w + 0.12f, h + 0.12f), 2);
            o.body = Shapes.MakeRound("Block", ChuteColor, o.t, Vector2.zero, new Vector2(w, h), 3);
            for (int i = 0; i < 3; i++)
                Shapes.Make("Stripe", Shapes.Square, StripeColor, o.t, new Vector2(0f, -h / 3f + i * h / 3f), new Vector2(w, 0.08f), 4);
            o.rect = new Rect(cx - w / 2f, cy - h / 2f, w, h);
            obstacles.Add(o);
            arm.Obstacles.Add(o.rect);
            // Start with the elbow on the block's side, so about half the targets need a flip.
            arm.ElbowSign = side > 0f ? 1 : -1;
        }

        void MoveObstacles()
        {
            for (int i = 0; i < obstacles.Count; i++)
            {
                var o = obstacles[i];
                if (!o.moves) continue;
                float y = o.baseY + Mathf.Sin(Time.time * 0.7f) * (tall ? 0.8f : 0.6f);
                o.t.localPosition = new Vector2(o.t.localPosition.x, y);
                o.rect.y = y - o.rect.height / 2f;
                arm.Obstacles[i] = o.rect;
            }
        }

        void FlipElbow()
        {
            arm.FlipElbow();
            audioSrc.PlayOneShot(sGrab, 0.35f);
        }

        static bool FlipKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var k = UnityEngine.InputSystem.Keyboard.current;
            return k != null && k.spaceKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Space);
#endif
        }

        void Begin(int index)
        {
            level = Mathf.Clamp(index, 0, unlocked);
            BuildLevel();
            state = State.Playing;
            blockPress = true; // the tap that pressed the button should not move the arm
        }

        void ToMenu()
        {
            level = Mathf.Min(level, unlocked);
            BuildLevel();
            state = State.Menu;
        }

        static Sprite ShapeFor(int color) => color % 2 == 0 ? Shapes.RoundSquare : Shapes.Circle;

        void Update()
        {
            float dt = Time.deltaTime;
            // Screen turned or resized between portrait and wide: rebuild the layout,
            // but never in the middle of a level.
            if ((cam.aspect < 0.7f) != tall && (state == State.Menu || state == State.Levels)) BuildLevel();
            FitCamera();

            bool pressed = PointerHeld(out Vector2 screen);
            if (pressed && !wasPressed &&
                (screen.y > Screen.height * 0.88f || flipRect.Contains(new Vector2(screen.x, Screen.height - screen.y))))
                blockPress = true; // presses that start on a button are not arm moves
            if (!pressed) blockPress = false;
            wasPressed = pressed;
            bool armDown = pressed && !blockPress;

            if (state == State.Playing)
            {
                PlayUpdate(dt, armDown, screen);
            }
            else
            {
                MoveStripes(dt * 0.3f);
                if (state == State.Menu || state == State.Levels)
                {
                    float t = Time.time;
                    arm.Target = new Vector2(Mathf.Sin(t * 0.8f) * 2.2f, BinY - 0.6f + Mathf.Sin(t * 1.3f) * 0.8f);
                }
                arm.Closed = false;
                armWasDown = false;
            }

            UpdateFx(dt);
        }

        void PlayUpdate(float dt, bool armDown, Vector2 screen)
        {
            MoveStripes(dt);
            MoveObstacles();
            if (obstacles.Count > 0 && FlipKeyPressed()) FlipElbow();

            if (armDown)
            {
                Vector3 wp = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
                arm.Target = wp;
                if (held == null) TryGrab();
            }
            else if (armWasDown && held != null)
            {
                Drop();
            }
            armWasDown = armDown;
            arm.Closed = held != null;

            if (held != null)
                held.t.position = Vector3.Lerp(held.t.position, arm.Tip, 1f - Mathf.Exp(-30f * dt));

            for (int i = parts.Count - 1; i >= 0; i--)
            {
                var p = parts[i];
                if (!p.onBelt) continue;
                p.t.position += Vector3.right * (beltSpeed * dt);
                if (p.t.position.x > BeltHalf + 0.3f)
                {
                    parts.RemoveAt(i);
                    Burst(p.t.position, Palette[p.color], 6);
                    Float(p.t.position + Vector3.up * 0.6f, "Missed", WrongFlash);
                    Destroy(p.t.gameObject);
                    Miss();
                }
            }

            if (toSpawn > 0)
            {
                spawnTimer -= dt;
                if (spawnTimer <= 0f)
                {
                    Spawn();
                    spawnTimer = spawnEvery;
                }
            }

            if (misses > MaxMisses) Lose();
            else if (toSpawn == 0 && parts.Count == 0 && held == null) Win();
        }

        void MoveStripes(float step)
        {
            foreach (var s in stripes)
            {
                var p = s.localPosition;
                p.x += beltSpeed * step;
                if (p.x > BeltHalf + 0.4f) p.x -= StripeCount * StripeGap;
                s.localPosition = p;
            }
        }

        void Spawn()
        {
            toSpawn--;
            int c = Random.Range(0, colorCount);
            var sr = Shapes.Make("Part", ShapeFor(c), Palette[c], world,
                                 new Vector2(SpawnX, BeltY), Vector2.one * PartSize, 5);
            var sh = Shapes.Make("Shadow", ShapeFor(c), new Color(0f, 0f, 0f, 0.35f), sr.transform,
                                 new Vector2(0.1f, -0.14f), Vector2.one, 4);
            parts.Add(new Part { t = sr.transform, sr = sr, color = c, shadow = sh.transform });
        }

        void TryGrab()
        {
            Part best = null;
            float bestD = GrabRadius;
            foreach (var p in parts)
            {
                float d = Vector2.Distance(p.t.position, arm.Tip);
                if (d < bestD)
                {
                    bestD = d;
                    best = p;
                }
            }
            if (best == null) return;
            parts.Remove(best);
            held = best;
            held.onBelt = false;
            held.sr.sortingOrder = 7;
            held.t.localScale = Vector3.one * (PartSize * 1.15f);
            held.shadow.localPosition = new Vector3(0.3f, -0.45f, 0f); // lifted: shadow falls further away
            held.shadow.GetComponent<SpriteRenderer>().sortingOrder = 6;
            audioSrc.PlayOneShot(sGrab, 0.6f);
        }

        void Drop()
        {
            var p = held;
            held = null;
            p.t.localScale = Vector3.one * PartSize;
            p.sr.sortingOrder = 5;
            p.shadow.localPosition = new Vector3(0.1f, -0.14f, 0f);
            p.shadow.GetComponent<SpriteRenderer>().sortingOrder = 4;
            Vector2 pos = p.t.position;

            foreach (var b in bins)
            {
                if (!b.area.Contains(pos)) continue;
                Vector3 center = b.t.position;
                if (b.color == p.color)
                {
                    sorted++;
                    combo = Time.time - lastGoodTime <= ComboWindow ? combo + 1 : 1;
                    lastGoodTime = Time.time;
                    bestCombo = Mathf.Max(bestCombo, combo);
                    score += combo;
                    b.flash = 1f;
                    Burst(center, Palette[p.color], 14 + combo * 3);
                    Float(center + Vector3.up * 0.8f, combo > 1 ? $"+{combo}  combo x{combo}" : "+1", Palette[p.color]);
                    comboSrc.pitch = 1f + 0.08f * Mathf.Min(combo - 1, 8);
                    comboSrc.PlayOneShot(sGood, 0.8f);
                    if (combo >= 3) shake = Mathf.Max(shake, 0.12f);
                }
                else
                {
                    b.flash = -1f;
                    Burst(center, WrongFlash, 8);
                    Float(center + Vector3.up * 0.8f, "Wrong bin", WrongFlash);
                    Miss();
                }
                Destroy(p.t.gameObject);
                return;
            }

            // Dropped near the belt: it rides again. Anywhere else: it stays put until grabbed.
            if (Mathf.Abs(pos.y - BeltY) < 0.8f && Mathf.Abs(pos.x) < BeltHalf + 0.3f)
            {
                p.onBelt = true;
                p.t.position = new Vector3(pos.x, BeltY, 0f);
            }
            parts.Add(p);
        }

        void Miss()
        {
            misses++;
            combo = 0;
            shake = 0.3f;
            audioSrc.PlayOneShot(sBad, 0.7f);
        }

        void Win()
        {
            state = State.Won;
            stars = misses == 0 ? 3 : misses == 1 ? 2 : 1;
            if (stars > PlayerPrefs.GetInt(StarsKey(level), 0)) PlayerPrefs.SetInt(StarsKey(level), stars);
            unlocked = Mathf.Max(unlocked, level + 1);
            PlayerPrefs.SetInt(UnlockedKey, unlocked);
            PlayerPrefs.Save();
            arm.Closed = false;
            for (int i = 0; i < bins.Count; i++) Burst(bins[i].t.position, Palette[bins[i].color], 10);
            audioSrc.PlayOneShot(sWin, 0.8f);
        }

        void Lose()
        {
            state = State.Lost;
            arm.Closed = false;
        }

        void Burst(Vector3 pos, Color color, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float size = Random.Range(0.12f, 0.24f);
                var sr = Shapes.Make("Spark", Shapes.Circle, color, world, pos, Vector2.one * size, 8);
                float life = Random.Range(0.45f, 0.8f);
                Vector2 v = Random.insideUnitCircle.normalized * Random.Range(2f, 5f) + Vector2.up * 2f;
                sparks.Add(new Spark { t = sr.transform, sr = sr, v = v, life = life, maxLife = life, size = size });
            }
        }

        void Float(Vector3 pos, string text, Color color)
        {
            floaters.Add(new Floater { pos = pos, text = text, color = color, life = 0.9f });
        }

        void UpdateFx(float dt)
        {
            blockSoundCooldown -= dt;
            if (state == State.Playing && arm.Blocked)
            {
                foreach (var o in obstacles) o.flash = 1f;
                if (blockSoundCooldown <= 0f)
                {
                    audioSrc.PlayOneShot(sBad, 0.25f);
                    blockSoundCooldown = 0.5f;
                }
            }
            foreach (var o in obstacles)
            {
                o.body.color = Color.Lerp(ChuteColor, WrongFlash, o.flash);
                o.flash = Mathf.MoveTowards(o.flash, 0f, dt * 4f);
            }

            foreach (var b in bins)
            {
                float f = Mathf.Abs(b.flash);
                var target = b.flash >= 0f ? Palette[b.color] : WrongFlash;
                b.inside.color = Color.Lerp(Background, target, f * 0.8f);
                b.t.localScale = Vector3.one * (1f + 0.12f * f);
                b.flash = Mathf.MoveTowards(b.flash, 0f, dt * 3f);
            }

            for (int i = sparks.Count - 1; i >= 0; i--)
            {
                var s = sparks[i];
                s.life -= dt;
                if (s.life <= 0f || s.t == null)
                {
                    if (s.t != null) Destroy(s.t.gameObject);
                    sparks.RemoveAt(i);
                    continue;
                }
                s.v += Vector2.down * (7f * dt);
                s.t.position += (Vector3)(s.v * dt);
                float k = s.life / s.maxLife;
                s.t.localScale = Vector3.one * (s.size * k);
                var c = s.sr.color;
                c.a = k;
                s.sr.color = c;
            }

            for (int i = floaters.Count - 1; i >= 0; i--)
            {
                floaters[i].life -= dt;
                floaters[i].pos += Vector3.up * (1.2f * dt);
                if (floaters[i].life <= 0f) floaters.RemoveAt(i);
            }

            shake = Mathf.MoveTowards(shake, 0f, dt * 1.5f);
            cam.transform.position = camHome + (Vector3)(Random.insideUnitCircle * (shake * 0.4f));
        }

        void FitCamera()
        {
            // Keep the play area, chute included (about 8 units wide), visible on any screen shape.
            cam.orthographicSize = tall
                ? 4.05f / Mathf.Max(cam.aspect, 0.01f)
                : Mathf.Max(5.2f, 4.1f / Mathf.Max(cam.aspect, 0.01f));
        }

        static bool PointerHeld(out Vector2 screen)
        {
#if ENABLE_INPUT_SYSTEM
            var p = UnityEngine.InputSystem.Pointer.current;
            if (p != null && p.press.isPressed)
            {
                screen = p.position.ReadValue();
                return true;
            }
#else
            if (Input.touchCount > 0)
            {
                screen = Input.GetTouch(0).position;
                return true;
            }
            if (Input.GetMouseButton(0))
            {
                screen = Input.mousePosition;
                return true;
            }
#endif
            screen = default;
            return false;
        }

        // ---------- UI ----------

        // Rounded button background; GUIStyle.border makes Unity 9-slice it so corners stay round.
        static Texture2D RoundTex(Color c)
        {
            const int n = 32, r = 10;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, r, n - r);
                    float cy = Mathf.Clamp(y + 0.5f, r, n - r);
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    float a = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                    t.SetPixel(x, y, new Color(c.r, c.g, c.b, c.a * a));
                }
            }
            t.wrapMode = TextureWrapMode.Clamp;
            t.Apply();
            return t;
        }

        static Texture2D Tex(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        void EnsureStyles()
        {
            if (button != null) return;
            white = Tex(Color.white);
            btnTex = RoundTex(ArmJoint);
            btnHover = RoundTex(new Color(1f, 0.66f, 0.34f));
            btnGray = RoundTex(new Color(0.22f, 0.25f, 0.33f));

            big = new GUIStyle { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            big.normal.textColor = Color.white;
            small = new GUIStyle { alignment = TextAnchor.MiddleCenter, wordWrap = true };
            small.normal.textColor = new Color(0.85f, 0.88f, 0.95f);

            button = new GUIStyle { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            button.normal.background = btnTex;
            button.hover.background = btnHover;
            button.active.background = btnHover;
            button.normal.textColor = button.hover.textColor = button.active.textColor = Color.white;
            button.border = new RectOffset(10, 10, 10, 10);

            smallButton = new GUIStyle(button);
            smallButton.normal.background = btnGray;
        }

        void OnGUI()
        {
            EnsureStyles();
            float h = Screen.height;
            float sw = Screen.width;
            float u = Mathf.Min(h, sw * 1.6f) / 100f;
            float top = h - Screen.safeArea.yMax;
            big.fontSize = Mathf.RoundToInt(u * 7f);
            small.fontSize = Mathf.RoundToInt(u * 2.8f);
            button.fontSize = Mathf.RoundToInt(u * 3.4f);
            smallButton.fontSize = Mathf.RoundToInt(u * 2.6f);

            float bw = Mathf.Min(sw * 0.7f, u * 42f);
            float bh = u * 8f;
            float cx = sw / 2f;

            // Dim the scene behind menus and result screens so the text and buttons stand out.
            if (state != State.Playing)
            {
                var prevColor = GUI.color;
                GUI.color = new Color(0.04f, 0.05f, 0.08f, 0.62f);
                GUI.DrawTexture(new Rect(0, 0, sw, h), white);
                GUI.color = prevColor;
            }

            switch (state)
            {
                case State.Menu:
                    GUI.Label(new Rect(0, top + h * 0.06f, sw, u * 10f), "Arm Sort", big);
                    GUI.Label(new Rect(sw * 0.1f, top + h * 0.06f + u * 10f, sw * 0.8f, u * 5f),
                              "Drive the arm. Sort every part before it falls off the belt.", small);
                    if (GUI.Button(new Rect(cx - bw / 2f, h * 0.62f, bw, bh), $"Play level {level + 1}", button)) Begin(level);
                    if (GUI.Button(new Rect(cx - bw / 2f, h * 0.62f + bh * 1.3f, bw, bh), "Levels", smallButton)) state = State.Levels;
                    break;

                case State.Levels:
                    DrawLevels(u, top, sw, h);
                    break;

                case State.Playing:
                    DrawHud(u, top, sw);
                    if (level == 0 && sorted == 0 && misses == 0)
                        GUI.Label(new Rect(sw * 0.1f, top + u * 14f, sw * 0.8f, u * 6f),
                                  "Drag to move the arm. Touch a part to grab it, let go over the bin of its color.", small);
                    if (level == FirstObstacleLevel && sorted < 2)
                        GUI.Label(new Rect(sw * 0.1f, top + u * 14f, sw * 0.8f, u * 6f),
                                  "The arm can't pass through the block. Tap Flip elbow to bend it the other way.", small);
                    if (obstacles.Count > 0)
                    {
                        float fw = u * 22f, fh = u * 7f;
                        flipRect = new Rect(u * 2f, Screen.safeArea.yMin > 0 ? h - Screen.safeArea.yMin - fh - u * 2f : h - fh - u * 3f, fw, fh);
                        if (GUI.Button(flipRect, "Flip elbow", button)) FlipElbow();
                    }
                    else flipRect = Rect.zero;
                    break;

                case State.Won:
                    DrawHud(u, top, sw);
                    GUI.Label(new Rect(0, h * 0.2f, sw, u * 10f), "Sorted!", big);
                    DrawStars(new Vector2(cx, h * 0.2f + u * 12f), stars, u * 4f);
                    GUI.Label(new Rect(0, h * 0.2f + u * 17f, sw, u * 5f), $"Score {score}     Best combo x{bestCombo}", small);
                    if (GUI.Button(new Rect(cx - bw / 2f, h * 0.62f, bw, bh), "Next level", button)) Begin(level + 1);
                    if (GUI.Button(new Rect(cx - bw / 2f, h * 0.62f + bh * 1.3f, bw, bh), "Menu", smallButton)) ToMenu();
                    break;

                case State.Lost:
                    DrawHud(u, top, sw);
                    GUI.Label(new Rect(0, h * 0.2f, sw, u * 10f), "Too many misses", big);
                    if (GUI.Button(new Rect(cx - bw / 2f, h * 0.62f, bw, bh), "Try again", button)) Begin(level);
                    if (GUI.Button(new Rect(cx - bw / 2f, h * 0.62f + bh * 1.3f, bw, bh), "Menu", smallButton)) ToMenu();
                    break;
            }

            foreach (var f in floaters)
            {
                Vector3 sp = cam.WorldToScreenPoint(f.pos);
                var c = f.color;
                c.a = Mathf.Clamp01(f.life / 0.4f);
                var prev = small.normal.textColor;
                small.normal.textColor = c;
                int prevSize = small.fontSize;
                small.fontSize = Mathf.RoundToInt(u * 3.6f);
                float fx = Mathf.Clamp(sp.x, u * 15f, sw - u * 15f); // keep the text on screen
                GUI.Label(new Rect(fx - u * 15f, h - sp.y - u * 3f, u * 30f, u * 6f), f.text, small);
                small.fontSize = prevSize;
                small.normal.textColor = prev;
            }
        }

        void DrawHud(float u, float top, float sw)
        {
            if (GUI.Button(new Rect(u * 2f, top + u * 2f, u * 14f, u * 5.5f), "Menu", smallButton)) ToMenu();
            // Stats sit on their own row under the Menu button so they never overlap on narrow phones.
            GUI.Label(new Rect(0, top + u * 8.5f, sw, u * 5f),
                      $"Level {level + 1}     Sorted {sorted}/{total}     Score {score}     Misses {misses}/{MaxMisses}", small);
            if (state == State.Playing && combo > 1 && Time.time - lastGoodTime <= ComboWindow)
            {
                var prev = big.fontSize;
                big.fontSize = Mathf.RoundToInt(u * 5f);
                GUI.Label(new Rect(sw - u * 22f, top + u * 2f, u * 20f, u * 6f), $"x{combo}", big);
                big.fontSize = prev;
            }
        }

        void DrawStars(Vector2 center, int count, float size)
        {
            for (int i = 0; i < 3; i++)
            {
                float x = center.x + (i - 1) * size * 1.6f - size / 2f;
                var prev = GUI.color;
                GUI.color = i < count ? Gold : Dim;
                var r = new Rect(x, center.y - size / 2f, size, size);
                var m = GUI.matrix;
                GUIUtility.RotateAroundPivot(45f, r.center);
                GUI.DrawTexture(r, white);
                GUI.matrix = m;
                GUI.color = prev;
            }
        }

        void DrawLevels(float u, float top, float sw, float h)
        {
            GUI.Label(new Rect(0, top + u * 3f, sw, u * 9f), "Levels", big);
            int count = unlocked + 1;
            int cols = 5;
            float cell = Mathf.Min(sw * 0.84f / cols, u * 13f);
            float gridW = cell * cols;
            float rowH = cell * 1.25f;
            int rows = Mathf.CeilToInt(count / (float)cols);
            var view = new Rect(0, 0, gridW, rows * rowH);
            var area = new Rect((sw - gridW) / 2f, top + u * 14f, gridW + u * 2f, h * 0.62f);

            levelScroll = GUI.BeginScrollView(area, levelScroll, view, false, false);
            for (int i = 0; i < count; i++)
            {
                int col = i % cols;
                int row = i / cols;
                var r = new Rect(col * cell + cell * 0.08f, row * rowH, cell * 0.84f, cell * 0.84f);
                if (GUI.Button(r, (i + 1).ToString(), i == unlocked ? button : smallButton)) Begin(i);
                DrawStars(new Vector2(r.center.x, r.yMax + cell * 0.14f), PlayerPrefs.GetInt(StarsKey(i), 0), cell * 0.09f);
            }
            GUI.EndScrollView();

            float bw = Mathf.Min(sw * 0.7f, u * 42f);
            if (GUI.Button(new Rect(sw / 2f - bw / 2f, h * 0.84f, bw, u * 8f), "Back", smallButton)) state = State.Menu;
        }
    }
}
