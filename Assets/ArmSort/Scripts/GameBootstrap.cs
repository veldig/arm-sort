using UnityEngine;

namespace ArmSort
{
    // Starts the game in whatever scene is open, so no scene setup is needed.
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Object.FindAnyObjectByType<Game>() == null)
                new GameObject("ArmSort").AddComponent<Game>();
        }
    }
}
