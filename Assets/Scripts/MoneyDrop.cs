using UnityEngine;

namespace MiniMart
{
    /// <summary>
    /// A coin/bill that sits on the checkout counter after a sale. The player has to walk up and
    /// collect it (press E or walk over it). It spins slowly and bobs so it reads from the camera.
    /// </summary>
    public class MoneyDrop : MonoBehaviour
    {
        private int value;
        private Transform visual;
        private float phase;
        private float spawnTime;

        /// <summary>Where on the counter this drop landed, used for stacking.</summary>
        public int Value => value;

        public void Initialise(int amount, Transform model)
        {
            value = amount;
            visual = model;
            phase = Random.Range(0f, 6.28f);
            spawnTime = Time.time;
        }

        private void Update()
        {
            if (visual == null) return;

            // Gentle spin and bob.
            visual.Rotate(0f, 90f * Time.deltaTime, 0f, Space.Self);
            float bob = Mathf.Sin((Time.time - spawnTime) * 3f + phase) * 0.04f;
            visual.localPosition = new Vector3(0f, bob, 0f);

            // Auto collect when the player walks close enough.
            MiniMartGameManager game = MiniMartGameManager.Instance;
            if (game == null || game.Player == null) return;
            float distance = Vector3.Distance(transform.position, game.Player.transform.position);
            if (distance < 1.6f) Collect();
        }

        public void Collect()
        {
            MiniMartGameManager game = MiniMartGameManager.Instance;
            if (game == null) return;
            game.CollectMoney(value);
            Destroy(gameObject);
        }
    }
}
