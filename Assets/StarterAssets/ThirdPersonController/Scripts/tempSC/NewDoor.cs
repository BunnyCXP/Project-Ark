using UnityEngine;

namespace TheGlitch
{
    public class DoorSimple : MonoBehaviour
    {
        public Transform OpenTo;
        public float OpenSpeed = 6f;

        private Vector3 _closedPos;
        private bool _open;

        private void Awake() => _closedPos = transform.position;

        public void SetOpen(bool open) => _open = open;

        private void Update()
        {
            Vector3 target = (_open && OpenTo != null) ? OpenTo.position : _closedPos;
            transform.position = Vector3.Lerp(transform.position, target, Time.deltaTime * OpenSpeed);
        }
    }
}
