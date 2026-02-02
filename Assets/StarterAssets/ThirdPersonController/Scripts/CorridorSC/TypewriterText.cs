using System.Collections;
using TMPro;
using UnityEngine;

namespace TheGlitch
{
    public class TypewriterTMP : MonoBehaviour
    {
        public TMP_Text Text;
        [TextArea] public string FullText;

        public float CharInterval = 0.03f;
        public bool ClearOnStart = true;

        Coroutine _co;
        bool _playing;

        void Awake()
        {
            if (Text == null) Text = GetComponent<TMP_Text>();
            if (ClearOnStart && Text != null) Text.text = "";
        }

        public void Begin()
        {
            if (Text == null) return;

            // 防止重复开始导致“抖”
            if (_playing) return;

            if (_co != null) StopCoroutine(_co);
            _co = StartCoroutine(CoPlay());
        }

        public void ResetAndClear()
        {
            if (Text == null) return;
            if (_co != null) StopCoroutine(_co);
            _playing = false;
            Text.text = "";
        }

        IEnumerator CoPlay()
        {
            _playing = true;
            Text.text = "";

            for (int i = 0; i < FullText.Length; i++)
            {
                Text.text += FullText[i];
                yield return new WaitForSecondsRealtime(CharInterval);
            }

            _playing = false;
        }
    }
}
