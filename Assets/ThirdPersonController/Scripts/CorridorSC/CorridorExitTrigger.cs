using System.Collections;
using UnityEngine;

public class CorridorExitTrigger : MonoBehaviour
{
    public Transform TeleportTarget;
    public CanvasGroup CurtainGroup;
    public float FadeInTime = 0.25f;
    public float FadeOutTime = 0.3f;
    public float HoldTime = 0.15f;

    private bool _triggered;

    private void Start()
    {
        if (CurtainGroup != null) CurtainGroup.alpha = 0f;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_triggered) return;
        if (!other.CompareTag("Player")) return;

        _triggered = true;
        StartCoroutine(DoTransition(other.transform));
    }

    private IEnumerator DoTransition(Transform player)
    {
        // 1) Fade in
        if (CurtainGroup != null)
        {
            float t = 0f;
            while (t < FadeInTime)
            {
                t += Time.deltaTime;
                CurtainGroup.alpha = Mathf.Clamp01(t / FadeInTime);
                yield return null;
            }
            CurtainGroup.alpha = 1f;
        }

        // 2) Hold
        yield return new WaitForSeconds(HoldTime);

        // 3) Teleport
        if (TeleportTarget != null)
        {
            player.position = TeleportTarget.position;
            player.rotation = TeleportTarget.rotation;
        }

        // 4) Fade out
        if (CurtainGroup != null)
        {
            float t = 0f;
            while (t < FadeOutTime)
            {
                t += Time.deltaTime;
                CurtainGroup.alpha = 1f - Mathf.Clamp01(t / FadeOutTime);
                yield return null;
            }
            CurtainGroup.alpha = 0f;
        }

        _triggered = false; // 如果你想允许回头再触发就留着；不想就删掉这行
    }
}

