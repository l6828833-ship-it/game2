using UnityEngine;

namespace MiniMart
{
    /// <summary>Smoothed isometric chase camera. Pulls back a little while the player sprints.</summary>
    public class CameraFollower : MonoBehaviour
    {
        public Transform target;

        private static readonly Vector3 Offset = new Vector3(-13.5f, 18f, -15.5f);
        private static readonly Vector3 FramingOffset = new Vector3(6f, 0f, 2f);
        private static readonly Quaternion Angle = Quaternion.Euler(55f, 45f, 0f);

        private Camera view;
        private Vector3 lastTargetPosition;

        private void Awake()
        {
            view = GetComponent<Camera>();
        }

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 desired = target.position + FramingOffset + Offset;
            transform.position = Vector3.Lerp(transform.position, desired, Time.deltaTime * 4f);
            transform.rotation = Quaternion.Slerp(transform.rotation, Angle, Time.deltaTime * 5f);

            if (view == null || !view.orthographic) return;
            float speed = Time.deltaTime > 0f ? (target.position - lastTargetPosition).magnitude / Time.deltaTime : 0f;
            lastTargetPosition = target.position;
            float wanted = Mathf.Lerp(14.5f, 16f, Mathf.Clamp01(speed / 7f));
            view.orthographicSize = Mathf.Lerp(view.orthographicSize, wanted, Time.deltaTime * 2.5f);
        }
    }
}
