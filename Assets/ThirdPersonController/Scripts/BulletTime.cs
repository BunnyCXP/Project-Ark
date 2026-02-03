using UnityEngine;

namespace TheGlitch
{
    public static class BulletTime
    {
        public static float NormalFixedDelta { get; private set; } = 0.02f;

        public static void Init()
        {
            NormalFixedDelta = Time.fixedDeltaTime;
        }

        public static void Set(bool on, float timeScale = 0.2f)
        {
            if (on)
            {
                Time.timeScale = timeScale;
                Time.fixedDeltaTime = NormalFixedDelta * Time.timeScale;
            }
            else
            {
                Time.timeScale = 1f;
                Time.fixedDeltaTime = NormalFixedDelta;
            }
        }
    }
}
