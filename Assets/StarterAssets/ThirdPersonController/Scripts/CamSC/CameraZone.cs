using UnityEngine;
using Cinemachine;

public class CameraZone : MonoBehaviour
{
    public CinemachineVirtualCamera ZoneCamera;
    public int ActivePriority = 40;
    public int InactivePriority = 5;

    private void Start()
    {
        if (ZoneCamera != null)
            ZoneCamera.Priority = InactivePriority;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        if (ZoneCamera != null)
            ZoneCamera.Priority = ActivePriority;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        if (ZoneCamera != null)
            ZoneCamera.Priority = InactivePriority;
    }
}
