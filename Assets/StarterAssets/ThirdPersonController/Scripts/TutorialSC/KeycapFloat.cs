using UnityEngine;

public class KeycapFloat : MonoBehaviour
{
    public float FloatHeight = 0.03f;
    public float FloatSpeed = 1.5f;

    Vector3 _startPos;

    void Start()
    {
        _startPos = transform.localPosition;
    }

    void Update()
    {
        float y = Mathf.Sin(Time.time * FloatSpeed) * FloatHeight;
        transform.localPosition = _startPos + Vector3.up * y;
    }
}
