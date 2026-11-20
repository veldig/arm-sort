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
            float t2 = -Mathf.Acos(c2);
            float t1 = Mathf.Atan2(rel.y, rel.x) - Mathf.Atan2(L2 * Mathf.Sin(t2), L1 + L2 * Mathf.Cos(t2));

            q1 = Mathf.SmoothDampAngle(q1, t1 * Mathf.Rad2Deg, ref v1, smoothTime, maxSpeed);
            q2 = Mathf.SmoothDampAngle(q2, t2 * Mathf.Rad2Deg, ref v2, smoothTime, maxSpeed);
            grip = Mathf.MoveTowards(grip, Closed ? 0f : 1f, Time.deltaTime * 9f);
            Apply();
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
