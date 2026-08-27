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
        /// <summary>
        /// Shoppers are the same body as the player, so colour is what tells them apart. Cached by
        /// index in the material cache, which keeps it to one material per colour rather than one
        /// per shopper.
        /// </summary>
        private static readonly Color[] Tints =
        {
            new Color(0.96f, 0.43f, 0.57f), new Color(0.45f, 0.73f, 0.91f), new Color(0.62f, 0.76f, 0.37f),
            new Color(0.82f, 0.55f, 0.92f), new Color(0.29f, 0.78f, 0.70f), new Color(0.98f, 0.56f, 0.28f),
            new Color(0.55f, 0.60f, 0.92f), new Color(0.88f, 0.80f, 0.42f)
        };

        private static readonly Color[] Skins =
        {
            new Color(1f, 0.76f, 0.58f), new Color(0.63f, 0.40f, 0.28f), new Color(0.44f, 0.25f, 0.16f),
            new Color(0.88f, 0.61f, 0.43f), new Color(0.80f, 0.52f, 0.35f)
        };

        /// <summary>Shoppers get a cheaper mesh than the player: there can be twenty of them.</summary>
        private const int ShopperLod = 2;

        private readonly List<Vector3> path = new List<Vector3>();

        private CustomerState state;
        private Transform visual;
        private Transform moodLight;
        private Renderer moodRenderer;
        private Vector3 moodBaseScale = Vector3.one;
        private Transform basketVisual;
        private ProductKind? basketProduct;
        private ShelfUnit targetShelf;
        private CharacterLocomotion locomotion;
        private float bodyHeight = 1.6f;

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

            visual = new GameObject("CustomerBody").transform;
            visual.SetParent(transform, false);

            int tint = serial % Tints.Length;
            bodyHeight = 1.6f * Random.Range(0.93f, 1.05f);
            BuildBody(game, serial, tint);

            // Only ever seen while this shopper is stuck at an unmanned till, so it starts hidden.
            GameObject mood = game.CreateDecor(PrimitiveType.Sphere, "Customer_Waiting", transform.position,
                new Vector3(0.22f, 0.22f, 0.22f), WaitingMaterial(game), visual);
            mood.transform.localPosition = new Vector3(0f, bodyHeight + 0.24f, 0f);
            mood.SetActive(false);
            moodLight = mood.transform;
            moodBaseScale = moodLight.localScale;
            moodRenderer = mood.GetComponent<Renderer>();

            walkSpeed = Random.Range(1.25f, 1.65f);
            walkPhase = Random.Range(0f, 6.28f);
            maxPatience = Random.Range(26f, 42f);
            patience = maxPatience;
            stateTimer = Random.Range(0.4f, 1.2f);
            state = CustomerState.Entering;
            SetDestination(new Vector3(-7.6f, 0f, -3.1f + Random.Range(-0.6f, 0.6f)));
        }

        /// <summary>
        /// The shared skinned body tinted for this shopper, falling back to the primitive toy when
        /// the imported character is unavailable.
        /// </summary>
        private void BuildBody(MiniMartGameManager game, int serial, int tint)
        {
            Material material = game.MaterialFor("CustomerTint_" + tint, Tints[tint]);
            CharacterRig.Rig rig = CharacterRig.Build(visual, material, bodyHeight, ShopperLod);

            if (rig != null)
            {
                rig.Model.name = "Customer_Body_" + serial;
                AnimationClip walk = CharacterRig.LoadClip(CharacterRig.CustomerWalkClip)
                    ?? CharacterRig.LoadClip(CharacterRig.RunClip);
                if (walk != null)
                {
                    locomotion = gameObject.AddComponent<CharacterLocomotion>();
                    if (!locomotion.Setup(rig.Animator, walk,
                        CharacterRig.LoadClip(CharacterRig.IdleClip),
                        CharacterRig.LoadClip(CharacterRig.CarryIdleClip), rig.Pelvis))
                    {
                        Destroy(locomotion);
                        locomotion = null;
                    }
                }
                return;
            }

            ToyCharacter.Build(visual, Tints[tint], Skins[(serial * 3 + 1) % Skins.Length],
                "Customer" + serial, serial % 2 == 0);
        }

        private void Update()
        {
            MiniMartGameManager game = MiniMartGameManager.Instance;
            if (game == null) return;

            TickPatience(game);
            Move();
            if (locomotion != null && locomotion.IsReady)
            {
                Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
                locomotion.Advance(arrived ? 0f : flat.magnitude, hasItem, Time.deltaTime);
            }
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
                if (locomotion != null) return;
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

            // The imported walk cycle already carries the bob and sway; only the toy needs faking.
            if (locomotion != null) return;
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

        /// <summary>
        /// The marker is a call for service, not a mood meter: it shows up red only when this shopper
        /// is at the till and nobody is behind the counter, and goes away the moment you arrive.
        /// </summary>
        /// <summary>
        /// The red marker only appears when this shopper is about to give up and leave (below 20%
        /// patience). It no longer shows for every waiting customer at an unmanned till.
        /// </summary>
        private void UpdateMood()
        {
            if (moodLight == null) return;

            bool almostGone = PatienceRatio < 0.2f
                && (state == CustomerState.Queuing || state == CustomerState.GoingToShelf);

            if (moodLight.gameObject.activeSelf != almostGone) moodLight.gameObject.SetActive(almostGone);
            if (!almostGone) return;

            // Pulses faster as they get closer to walking out.
            float urgency = Mathf.Lerp(5f, 12f, 1f - PatienceRatio / 0.2f);
            float pulse = 1f + Mathf.Sin(Time.time * urgency) * 0.22f;
            moodLight.localPosition = new Vector3(0f, bodyHeight + 0.24f + Mathf.Sin(Time.time * urgency) * 0.04f, 0f);
            moodLight.localScale = moodBaseScale * pulse;
            if (moodRenderer != null) moodRenderer.sharedMaterial = WaitingMaterial(MiniMartGameManager.Instance);
        }

        private static Material WaitingMaterial(MiniMartGameManager game)
            => game.MaterialFor("CustomerWaiting", new Color(0.95f, 0.20f, 0.20f));

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
            Material material = game.MaterialFor("Product_" + product, game.ProductColor(product));

            // Rebuild when the product changes so shoppers carry out what they actually picked up.
            if (basketVisual != null && basketProduct != product)
            {
                Destroy(basketVisual.gameObject);
                basketVisual = null;
            }
            basketProduct = product;

            if (basketVisual == null && ProductVisuals.TryGet(product, out ProductVisuals.Visual pv))
                basketVisual = ModelKit.SpawnProp(visual, pv.Model, material, pv.HandHeight, pv.ShelfLod, pv.UpFix);

            if (basketVisual == null)
            {
                basketVisual = game.CreateDecor(PrimitiveType.Cube, "Customer_Basket", transform.position,
                    new Vector3(0.3f, 0.24f, 0.24f), material, visual).transform;
            }
            // The holding clips carry the item in front, the toy body tucks it under one arm.
            basketVisual.localPosition = locomotion != null
                ? new Vector3(0f, bodyHeight * 0.58f, 0.26f)
                : new Vector3(0.34f, 0.55f, 0.16f);
            basketVisual.name = "Customer_Basket";
            basketVisual.gameObject.SetActive(true);
            // Covers the primitive as well as the model, whose renderer sits on a child.
            ModelKit.Paint(basketVisual.gameObject, material);
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
