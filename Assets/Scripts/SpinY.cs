using UnityEngine;

namespace MiniMart
{
    /// <summary>Spins the object around its local Y axis. Used for the HUD money icon and pickups.</summary>
    public class SpinY : MonoBehaviour
    {
        public float speed = 60f;

        private void Update()
        {
            transform.Rotate(0f, speed * Time.deltaTime, 0f, Space.Self);
        }
    }
}
