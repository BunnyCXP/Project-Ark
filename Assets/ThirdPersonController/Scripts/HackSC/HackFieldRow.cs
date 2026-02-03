using TMPro;
using UnityEngine;

namespace TheGlitch
{
    public class HackFieldRow : MonoBehaviour
    {
        public TMP_Text Label;
        public TMP_InputField Input;
        public TMP_Dropdown Dropdown;

        private HackField _field;

        public void Bind(HackField f)
        {
            _field = f;
            Label.text = f.DisplayName;

            Input.gameObject.SetActive(false);
            Dropdown.gameObject.SetActive(false);

            if (f.Type == HackFieldType.Float)
            {
                Input.gameObject.SetActive(true);
                Input.text = f.Value;
            }
            else if (f.Type == HackFieldType.Bool)
            {
                Dropdown.gameObject.SetActive(true);
                Dropdown.ClearOptions();
                Dropdown.AddOptions(new System.Collections.Generic.List<string> { "False", "True" });
                Dropdown.value = (f.Value == "True") ? 1 : 0;
            }
            else // Enum
            {
                Dropdown.gameObject.SetActive(true);
                Dropdown.ClearOptions();
                Dropdown.AddOptions(new System.Collections.Generic.List<string>(f.Options));
                int idx = System.Array.IndexOf(f.Options, f.Value);
                Dropdown.value = idx >= 0 ? idx : 0;
            }
        }

        public HackField Collect()
        {
            if (_field.Type == HackFieldType.Float)
            {
                float.TryParse(Input.text, out float v);
                return new HackField(_field.Id, _field.DisplayName, v);
            }

            if (_field.Type == HackFieldType.Bool)
            {
                bool b = Dropdown.options[Dropdown.value].text == "True";
                return new HackField(_field.Id, _field.DisplayName, b);
            }

            // Enum
            string value = Dropdown.options[Dropdown.value].text;
            return new HackField(_field.Id, _field.DisplayName, value, _field.Options);
        }

    }
}
