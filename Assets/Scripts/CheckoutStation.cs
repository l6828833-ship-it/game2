using System.Collections.Generic;
using UnityEngine;

namespace MiniMart
{
    /// <summary>The till. Scans whoever is at the front of the queue and pays out the basket.</summary>
    public class CheckoutStation : MonoBehaviour
    {
        private readonly List<CustomerAgent> queue = new List<CustomerAgent>();
        private float scanTimer;

        /// <summary>How close the shopkeeper has to be to count as working the till.</summary>
        private const float ServeRange = 2.6f;

        /// <summary>Where the shopper being served stands: in front of the counter, not inside it.</summary>
        public Vector3 CounterPosition => transform.position + new Vector3(0f, 0f, -1.05f);
        public int QueueLength => queue.Count;

        /// <summary>True while the player is stood at the counter. Nothing gets sold otherwise.</summary>
        public bool IsAttended { get; private set; }

        /// <summary>How far through the current shopper the scan is, for the HUD.</summary>
        public float ScanProgress { get; private set; }

        public void Initialise()
        {
            MiniMartGameManager game = MiniMartGameManager.Instance;
            GameObject counter = game.CreatePrimitive(PrimitiveType.Cube, "Checkout_Counter", transform.position,
                new Vector3(2.35f, 1.35f, 0.85f), game.MaterialFor("Checkout", new Color(0.93f, 0.46f, 0.40f)), transform);
            counter.transform.localPosition = new Vector3(0f, 0.72f, 0f);

            GameObject till = game.CreateDecor(PrimitiveType.Cube, "Cash_Register", transform.position,
                new Vector3(0.48f, 0.3f, 0.35f), game.MaterialFor("Till", new Color(0.36f, 0.36f, 0.49f)), transform);
            till.transform.localPosition = new Vector3(0.35f, 1.52f, -0.1f);
        }

        private void Update()
        {
            MiniMartGameManager game = MiniMartGameManager.Instance;
            if (game == null) return;

            for (int i = queue.Count - 1; i >= 0; i--)
                if (queue[i] == null) queue.RemoveAt(i);

            for (int i = 0; i < queue.Count; i++) queue[i].SetQueueTarget(QueueSpot(i));

            // Someone has to work the counter. An empty till means the queue just waits.
            IsAttended = game.Player != null
                && Vector3.Distance(game.Player.transform.position, CounterPosition) <= ServeRange;

            if (queue.Count == 0 || !IsAttended)
            {
                scanTimer = 0f;
                ScanProgress = 0f;
                return;
            }

            CustomerAgent first = queue[0];
            if (Vector3.Distance(first.transform.position, CounterPosition) > 0.75f) return;

            scanTimer += Time.deltaTime;
            float scanDuration = Mathf.Max(0.5f, 1.3f - game.UpgradeLevel(UpgradeType.Premium) * 0.1f);
            ScanProgress = Mathf.Clamp01(scanTimer / scanDuration);
            if (scanTimer < scanDuration) return;
            ScanProgress = 0f;

            scanTimer = 0f;
            queue.RemoveAt(0);
            if (first.BasketValue > 0)
            {
                game.Sfx.Play(SfxKind.Register);
                game.CompleteSale(first);
            }
            first.FinishShopping();
        }

        public void JoinQueue(CustomerAgent customer)
        {
            if (customer == null || queue.Contains(customer)) return;
            queue.Add(customer);
        }

        public void LeaveQueue(CustomerAgent customer)
        {
            if (queue.Remove(customer) && queue.Count == 0) scanTimer = 0f;
        }

        /// <summary>The queue trails west from the counter along the front lane.</summary>
        public Vector3 QueueSpot(int index) => index == 0
            ? CounterPosition
            : transform.position + new Vector3(-1.15f - (index - 1) * 0.85f, 0f, -1.05f);
    }
}
