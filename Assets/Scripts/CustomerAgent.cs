using System.Collections.Generic;
using UnityEngine;

namespace MiniMart
{
    public enum CustomerState { Entering, Browsing, GoingToShelf, Queuing, Leaving }

    /// <summary>
    /// A shopper. Walks the aisles instead of through them, grabs one item, queues at the till
    /// and gives up if the wait drags on.
    /// </summary>
    public class CustomerAgent : MonoBehaviour
    {
        private static readonly Color[] Shirts =
        {
            new Color(0.96f, 0.43f, 0.57f), new Color(0.45f, 0.73f, 0.91f), new Color(0.62f, 0.76f, 0.37f),
            new Color(0.82f, 0.55f, 0.92f), new Color(1f, 0.72f, 0.18f), new Color(0.29f, 0.78f, 0.70f)
        };

        private static readonly Color[] Skins =
        {
            new Color(1f, 0.76f, 0.58f), new Color(0.63f, 0.40f, 0.28f), new Color(0.44f, 0.25f, 0.16f),
            new Color(0.88f, 0.61f, 0.43f), new Color(0.80f, 0.52f, 0.35f)
        };

        private readonly List<Vector3> path = new List<Vector3>();

        private CustomerState state;
        private Transform visual;
        private Transform moodLight;
        private Renderer moodRenderer;
        private Transform basketVisual;
        private ShelfUnit targetShelf;

        private Vector3 destination;
        private Vector3 velocity;
        private int pathIndex;
        private bool arrived;
        private bool hasItem;
        private int shelfAttempts;
        private float walkSpeed;
        private float walkPhase;
        private float stateTimer;
        private float patience;
        private float maxPatience;

        public int BasketValue { get; private set; }
        public CustomerState State => state;
        public float PatienceRatio => maxPatience <= 0f ? 1f : Mathf.Clamp01(patience / maxPatience);

        public void Initialise(int serial)
        {
            MiniMartGameManager game = MiniMartGameManager.Instance;

            visual = new GameObject("CustomerToy").transform;
            visual.SetParent(transform, false);
            ToyCharacter.Build(visual, Shirts[serial % Shirts.Length], Skins[(serial * 3 + 1) % Skins.Length],
                "Customer" + serial, serial % 2 == 0);

            GameObject mood = game.CreateDecor(PrimitiveType.Sphere, "Customer_Mood", transform.position,
                new Vector3(0.2f, 0.2f, 0.2f), MoodMaterial(), visual);
            mood.transform.localPosition = new Vector3(0f, 1.55f, 0f);
            moodLight = mood.transform;
            moodRenderer = mood.GetComponent<Renderer>();

            walkSpeed = Random.Range(1.55f, 2.15f);
            walkPhase = Random.Range(0f, 6.28f);
            maxPatience = Random.Range(26f, 42f);
            patience = maxPatience;
            stateTimer = Random.Range(0.4f, 1.2f);
            state = CustomerState.Entering;
            SetDestination(new Vector3(-7.6f, 0f, -3.1f + Random.Range(-0.6f, 0.6f)));
        }

        private void Update()
        {
            MiniMartGameManager game = MiniMartGameManager.Instance;
            if (game == null) return;

            TickPatience(game);
            Move();
            UpdateMood();
            if (arrived) OnArrived(game);
        }

        // ------------------------------------------------------------------ moving

        private void SetDestination(Vector3 point)
        {
            destination = point;
            MiniMartNav.BuildPath(transform.position, point, path);
            pathIndex = 0;
            arrived = path.Count == 0;
        }

        private void Move()
        {
            if (arrived || path.Count == 0)
            {
                velocity = Vector3.Lerp(velocity, Vector3.zero, Time.deltaTime * 8f);
                visual.localPosition = Vector3.Lerp(visual.localPosition, Vector3.zero, Time.deltaTime * 8f);
                visual.localRotation = Quaternion.Slerp(visual.localRotation, Quaternion.identity, Time.deltaTime * 8f);
                return;
            }

            Vector3 waypoint = path[pathIndex];
            Vector3 delta = new Vector3(waypoint.x - transform.position.x, 0f, waypoint.z - transform.position.z);
            bool lastLeg = pathIndex == path.Count - 1;
            float threshold = lastLeg ? 0.2f : 0.45f;
            if (delta.sqrMagnitude <= threshold * threshold)
            {
                pathIndex++;
                if (pathIndex >= path.Count) arrived = true;
                return;
            }

            Vector3 desired = delta.normalized * walkSpeed + Separation();
            velocity = Vector3.Lerp(velocity, desired, 1f - Mathf.Exp(-7f * Time.deltaTime));
            transform.position += velocity * Time.deltaTime;

            if (velocity.sqrMagnitude > 0.05f)
                transform.forward = Vector3.Slerp(transform.forward, velocity.normalized, 9f * Time.deltaTime);

            walkPhase += Time.deltaTime * 7f;
            visual.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(walkPhase)) * 0.05f, 0f);
            visual.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(walkPhase) * 2.5f);
        }

        /// <summary>Keeps shoppers from stacking on top of each other in the aisle and the queue.</summary>
        private Vector3 Separation()
        {
            MiniMartGameManager game = MiniMartGameManager.Instance;
            Vector3 push = Vector3.zero;
            for (int i = 0; i < game.Customers.Count; i++)
            {
                CustomerAgent other = game.Customers[i];
                if (other == null || other == this) continue;
                Vector3 away = transform.position - other.transform.position;
                away.y = 0f;
                float distance = away.magnitude;
                if (distance > 0.85f || distance < 0.0001f) continue;
                push += away / distance * (0.85f - distance) * 1.6f;
            }
            return push;
        }

        // ----------------------------------------------------------------- patience

        private void TickPatience(MiniMartGameManager game)
        {
            if (state != CustomerState.Queuing && state != CustomerState.GoingToShelf) return;
            patience -= Time.deltaTime;
            if (patience > 0f) return;

            if (hasItem && targetShelf != null) targetShelf.ReturnOne();
            hasItem = false;
            BasketValue = 0;
            game.Checkout.LeaveQueue(this);
            game.ReportUnhappyCustomer("A shopper gave up on the queue and walked out.", 6f);
            BeginLeaving();
        }

        private void UpdateMood()
        {
            if (moodLight == null) return;
            moodLight.localPosition = new Vector3(0f, 1.55f + Mathf.Sin(Time.time * 2f + walkPhase) * 0.03f, 0f);
            if (moodRenderer != null) moodRenderer.sharedMaterial = MoodMaterial();
        }

        private Material MoodMaterial()
        {
            MiniMartGameManager game = MiniMartGameManager.Instance;
            float ratio = PatienceRatio;
            if (state == CustomerState.Leaving && !hasItem && BasketValue <= 0)
                return game.MaterialFor("MoodDone", new Color(0.72f, 0.76f, 0.80f));
            if (ratio > 0.6f) return game.MaterialFor("MoodHappy", new Color(0.36f, 0.87f, 0.45f));
            if (ratio > 0.3f) return game.MaterialFor("MoodWaiting", new Color(1f, 0.79f, 0.20f));
            return game.MaterialFor("MoodAngry", new Color(0.95f, 0.26f, 0.25f));
        }

        // ------------------------------------------------------------------- states

        private void OnArrived(MiniMartGameManager game)
        {
            stateTimer -= Time.deltaTime;

            switch (state)
            {
                case CustomerState.Entering:
                    if (stateTimer > 0f) return;
                    state = CustomerState.Browsing;
                    stateTimer = Random.Range(0.3f, 1f);
                    return;

                case CustomerState.Browsing:
                    if (stateTimer > 0f) return;
                    ChooseShelf(game);
                    return;

                case CustomerState.GoingToShelf:
                    PickUpItem(game);
                    return;

                case CustomerState.Queuing:
                    return;

                default:
                    if (stateTimer <= 0f) game.RemoveCustomer(this);
                    return;
            }
        }

        private void ChooseShelf(MiniMartGameManager game)
        {
            targetShelf = game.PickStockedShelf();
            if (targetShelf == null)
            {
                game.ReportUnhappyCustomer("A shopper left: the shelves were empty.", 2.5f);
                BeginLeaving();
                return;
            }
            state = CustomerState.GoingToShelf;
            SetDestination(targetShelf.transform.position + new Vector3(Random.Range(-0.38f, 0.38f), 0f, -1.05f));
        }

        private void PickUpItem(MiniMartGameManager game)
        {
            if (targetShelf != null && targetShelf.TakeOne())
            {
                hasItem = true;
                BasketValue = game.GetSaleValue(targetShelf.Product);
                ShowBasket(game, targetShelf.Product);
                state = CustomerState.Queuing;
                stateTimer = 0f;
                game.Checkout.JoinQueue(this);
                SetDestination(game.Checkout.QueueSpot(Mathf.Max(0, game.Checkout.QueueLength - 1)));
                return;
            }

            // Somebody beat them to the last one. Try another shelf before giving up.
            shelfAttempts++;
            if (shelfAttempts >= 3)
            {
                game.ReportUnhappyCustomer("A shopper could not find what they wanted.", 2.5f);
                BeginLeaving();
                return;
            }
            state = CustomerState.Browsing;
            stateTimer = Random.Range(0.2f, 0.6f);
            arrived = true;
        }

        private void ShowBasket(MiniMartGameManager game, ProductKind product)
        {
            if (basketVisual == null)
            {
                basketVisual = game.CreateDecor(PrimitiveType.Cube, "Customer_Basket", transform.position,
                    new Vector3(0.3f, 0.24f, 0.24f), game.MaterialFor("Product_" + product, game.ProductColor(product)), visual).transform;
            }
            basketVisual.localPosition = new Vector3(0.34f, 0.55f, 0.16f);
            basketVisual.gameObject.SetActive(true);
            Renderer renderer = basketVisual.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = game.MaterialFor("Product_" + product, game.ProductColor(product));
        }

        /// <summary>Called by the till once the basket is paid for.</summary>
        public void FinishShopping()
        {
            hasItem = false;
            BasketValue = 0;
            if (basketVisual != null) basketVisual.gameObject.SetActive(false);
            BeginLeaving();
        }

        /// <summary>Closing time: anyone already holding an item gets served, everyone heads out.</summary>
        public void SendHome()
        {
            if (state == CustomerState.Leaving) return;
            MiniMartGameManager game = MiniMartGameManager.Instance;
            game.Checkout.LeaveQueue(this);
            if (hasItem && BasketValue > 0) game.CompleteSale(this);
            FinishShopping();
        }

        public void SetQueueTarget(Vector3 queueTarget)
        {
            if (state != CustomerState.Queuing) return;
            Vector3 flat = new Vector3(queueTarget.x, 0f, queueTarget.z);
            if ((flat - new Vector3(destination.x, 0f, destination.z)).sqrMagnitude < 0.09f) return;
            SetDestination(flat);
        }

        private void BeginLeaving()
        {
            state = CustomerState.Leaving;
            stateTimer = 1.2f;
            SetDestination(MiniMartGameManager.Instance.CustomerExit.position);
        }
    }
}
