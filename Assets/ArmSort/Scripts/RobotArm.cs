using System.Collections.Generic;
using UnityEngine;

namespace ArmSort
{
    // Two-link planar arm. The player sets Target; analytic inverse kinematics
    // gives the joint angles, and each joint is smoothed so motion has no jumps.
    public class RobotArm : MonoBehaviour
    {
        public float L1 = 3.4f;
        public float L2 = 3.2f;
        public float smoothTime = 0.07f;
        public float maxSpeed = 720f; // degrees per second per joint

        public Vector2 Target;
        public bool Closed;
        public Vector2 Tip { get; private set; }

        // Obstacles the links cannot pass through (world space). A move that would put
        // a link inside one is refused, so the player has to route around it.
        public readonly List<Rect> Obstacles = new List<Rect>();
        // Which of the two inverse kinematics solutions to use: -1 or +1 (elbow one way or the other).
        public int ElbowSign = -1;
        public bool Blocked { get; private set; }
        const float LinkRadius = 0.2f;

        float q1 = 90f, q2 = 0f, v1, v2;
        float grip = 1f; // 1 open, 0 closed
        Transform link1, link2, gripper, fingerA, fingerB;

        public void Build(Color body, Color joint)
        {
            Shapes.Make("Base", Shapes.Square, new Color(body.r * 0.75f, body.g * 0.75f, body.b * 0.75f, 1f), transform,
                        new Vector2(0f, -0.45f), new Vector2(1.8f, 0.9f), 2);
            link1 = MakeLink("Link1", L1, transform, body, joint, 0.42f);
            link2 = MakeLink("Link2", L2, link1, body, joint, 0.34f);
            link2.localPosition = new Vector3(L1, 0f, 0f);

            gripper = new GameObject("Gripper").transform;
            gripper.SetParent(link2, false);
            gripper.localPosition = new Vector3(L2, 0f, 0f);
            fingerA = MakeFinger(joint);
            fingerB = MakeFinger(joint);
            Shapes.Make("Claw", Shapes.Circle, joint, gripper, Vector2.zero, Vector2.one * 0.45f, 6);

            Target = (Vector2)transform.position + new Vector2(0f, L1 + L2 - 1.5f);
            Apply();
        }

        static Transform MakeLink(string name, float len, Transform parent, Color body, Color joint, float width)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            Shapes.Make("Bar", Shapes.Square, body, t, new Vector2(len / 2f, 0f), new Vector2(len, width), 3);
            Shapes.Make("Joint", Shapes.Circle, joint, t, Vector2.zero, Vector2.one * (width + 0.25f), 4);
            return t;
        }

        Transform MakeFinger(Color joint)
        {
            var t = new GameObject("Finger").transform;
            t.SetParent(gripper, false);
            Shapes.Make("Bar", Shapes.Square, joint, t, new Vector2(0.24f, 0f), new Vector2(0.48f, 0.12f), 6);
            return t;
        }

        void Update()
        {
            Vector2 rel = Target - (Vector2)transform.position;
            float d = Mathf.Clamp(rel.magnitude, Mathf.Abs(L1 - L2) + 0.05f, L1 + L2 - 0.01f);
            rel = rel.sqrMagnitude < 1e-6f ? Vector2.up * d : rel.normalized * d;

            float c2 = Mathf.Clamp((d * d - L1 * L1 - L2 * L2) / (2f * L1 * L2), -1f, 1f);
            float t2 = ElbowSign * Mathf.Acos(c2);
            float t1 = Mathf.Atan2(rel.y, rel.x) - Mathf.Atan2(L2 * Mathf.Sin(t2), L1 + L2 * Mathf.Cos(t2));

            float nv1 = v1, nv2 = v2;
            float n1 = Mathf.SmoothDampAngle(q1, t1 * Mathf.Rad2Deg, ref nv1, smoothTime, maxSpeed);
            float n2 = Mathf.SmoothDampAngle(q2, t2 * Mathf.Rad2Deg, ref nv2, smoothTime, maxSpeed);

            // Refuse a step into an obstacle. If the arm is already touching one (a moving
            // block pushed into it), let it move so it can never get stuck for good.
            Blocked = Obstacles.Count > 0 && Collides(n1, n2) && !Collides(q1, q2);
            if (Blocked)
            {
                v1 = 0f;
                v2 = 0f;
            }
            else
            {
                q1 = n1;
                q2 = n2;
                v1 = nv1;
                v2 = nv2;
            }
            grip = Mathf.MoveTowards(grip, Closed ? 0f : 1f, Time.deltaTime * 9f);
            Apply();
        }

        public void FlipElbow() => ElbowSign = -ElbowSign;

        bool Collides(float a1Deg, float a2Deg)
        {
            float a1 = a1Deg * Mathf.Deg2Rad;
            float a12 = (a1Deg + a2Deg) * Mathf.Deg2Rad;
            Vector2 b = transform.position;
            Vector2 e = b + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * L1;
            Vector2 t = e + new Vector2(Mathf.Cos(a12), Mathf.Sin(a12)) * L2;
            foreach (var r in Obstacles)
            {
                var g = new Rect(r.xMin - LinkRadius, r.yMin - LinkRadius, r.width + 2f * LinkRadius, r.height + 2f * LinkRadius);
                if (SegmentHitsRect(b, e, g) || SegmentHitsRect(e, t, g)) return true;
            }
            return false;
        }

        // Liang-Barsky clipping: does segment p-q touch rectangle r?
        static bool SegmentHitsRect(Vector2 p, Vector2 q, Rect r)
        {
            Vector2 d = q - p;
            float t0 = 0f, t1 = 1f;
            return Clip(-d.x, p.x - r.xMin, ref t0, ref t1)
                && Clip(d.x, r.xMax - p.x, ref t0, ref t1)
                && Clip(-d.y, p.y - r.yMin, ref t0, ref t1)
                && Clip(d.y, r.yMax - p.y, ref t0, ref t1);
        }

        static bool Clip(float den, float num, ref float t0, ref float t1)
        {
            if (Mathf.Approximately(den, 0f)) return num >= 0f;
            float t = num / den;
            if (den < 0f)
            {
                if (t > t1) return false;
                if (t > t0) t0 = t;
            }
            else
            {
                if (t < t0) return false;
                if (t < t1) t1 = t;
            }
            return true;
        }

        void Apply()
        {
            link1.localRotation = Quaternion.Euler(0f, 0f, q1);
            link2.localRotation = Quaternion.Euler(0f, 0f, q2);
            float a = Mathf.Lerp(8f, 38f, grip);
            fingerA.localRotation = Quaternion.Euler(0f, 0f, a);
            fingerB.localRotation = Quaternion.Euler(0f, 0f, -a);
            Tip = gripper.position;
        }
    }
}
