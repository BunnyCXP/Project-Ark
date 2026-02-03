using TMPro;
using UnityEngine;

namespace TheGlitch
{
    public class NameTagFollow : MonoBehaviour
    {
        public TMP_Text Label;
        public Transform Target;
        public Vector3 WorldOffset = new Vector3(0, 2.0f, 0);

        private Camera _cam;

        private void Awake()
        {
            _cam = Camera.main;
        }

        private void LateUpdate()
        {
            if (Target == null) { Destroy(gameObject); return; }

            transform.position = Target.position + WorldOffset;

            // ÃæÏòÉãÏñ»ú
            if (_cam != null)
                transform.forward = _cam.transform.forward;
        }

        public void SetText(string text)
        {
            if (Label != null) Label.text = text;
        }
    }
}
