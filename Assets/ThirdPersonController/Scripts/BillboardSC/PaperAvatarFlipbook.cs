using UnityEngine;

namespace TheGlitch
{
    public class PaperAvatarFlipbook : MonoBehaviour
    {
        [Header("Renderer")]
        public Renderer Rend;

        [Header("SpriteSheet")]
        public int Columns = 4;
        public int Rows = 2;
        public int IdleStart = 0;
        public int IdleCount = 2;
        public int RunStart = 2;
        public int RunCount = 6;

        [Header("FPS (choppy)")]
        public float IdleFps = 6f;
        public float RunFps = 10f;

        [Header("Jitter")]
        public float PosJitter = 0.015f;
        public float RotJitter = 1.2f;
        public float JitterFreq = 14f;

        private Material _mat;
        private bool _moving;
        private float _t;
        private Vector3 _baseLocalPos;
        private Quaternion _baseLocalRot;

        private void Awake()
        {
            if (Rend == null) Rend = GetComponent<Renderer>();
            if (Rend != null) _mat = Rend.material;

            _baseLocalPos = transform.localPosition;
            _baseLocalRot = transform.localRotation;

            ApplyFrame(IdleStart);
        }

        public void SetMoving(bool moving)
        {
            if (_moving == moving) return;
            _moving = moving;
            _t = 0f;
        }

        private void Update()
        {
            if (_mat == null) return;

            _t += Time.deltaTime;

            int start = _moving ? RunStart : IdleStart;
            int count = Mathf.Max(1, _moving ? RunCount : IdleCount);
            float fps = Mathf.Max(1f, _moving ? RunFps : IdleFps);

            int idx = (int)(_t * fps) % count;
            ApplyFrame(start + idx);

            // ∂∂∂Ø£®∏„π÷÷Ω∆¨∏–£©
            float n1 = (Mathf.PerlinNoise(Time.time * JitterFreq, 0.13f) - 0.5f) * 2f;
            float n2 = (Mathf.PerlinNoise(0.27f, Time.time * JitterFreq) - 0.5f) * 2f;

            transform.localPosition = _baseLocalPos + new Vector3(n1, n2, 0f) * PosJitter;
            transform.localRotation = _baseLocalRot * Quaternion.Euler(n2 * RotJitter, n1 * RotJitter, 0f);
        }

        private void ApplyFrame(int frameIndex)
        {
            frameIndex = Mathf.Max(0, frameIndex);

            int col = frameIndex % Columns;
            int row = frameIndex / Columns;

            float sizeX = 1f / Columns;
            float sizeY = 1f / Rows;

            Vector2 scale = new Vector2(sizeX, sizeY);
            Vector2 offset = new Vector2(col * sizeX, 1f - sizeY - row * sizeY);

            _mat.mainTextureScale = scale;
            _mat.mainTextureOffset = offset;
        }
    }
}
