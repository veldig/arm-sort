# Arm Sort

**[Play it in your browser](https://veldig.github.io/arm-sort/)**

A mobile puzzle game made in Unity. Parts ride a conveyor; you drag a two-joint
robot arm to grab each one and drop it in the bin of its color before it falls
off the belt.

## How the arm works

The arm is a planar two-link manipulator. Each frame, the finger position is the
end-effector target, and the joint angles come from closed-form inverse kinematics:

- clamp the target to the reachable annulus, `|L1 - L2| < d < L1 + L2`
- elbow angle from the law of cosines: `q2 = -acos((d² - L1² - L2²) / (2·L1·L2))`
- shoulder angle: `q1 = atan2(y, x) - atan2(L2·sin q2, L1 + L2·cos q2)`

Each joint is then smoothed with a critically damped spring and a speed limit, so the
arm accelerates and settles like a real one instead of snapping to the finger. The
idea comes from my robotics research on vibration in a SCARA arm, where limiting
acceleration and jerk is what keeps motion smooth.

## What's in it

- Procedural levels: more parts, a faster belt and up to four colors as you go
- Star ratings, saved progress and a level select
- Sound effects generated in code, no audio files
- All art drawn from code at runtime, no image assets

## Project layout

- `Assets/ArmSort/Scripts`: game code (`Game.cs` loop and UI, `RobotArm.cs` kinematics, `Sfx.cs`, `Shapes.cs`)
- `Assets/ArmSort/Editor`: one-time project setup and the `Arm Sort > Build Web` menu
- `web/index.html`: the full-screen page the browser build loads in
- `.github/workflows/web.yml`: builds the game in Unity on every push and publishes it to GitHub Pages

Built with Unity 6 (Universal 2D). Open the folder in Unity Hub and press Play.

Free to play, no ads, no in-app purchases.
