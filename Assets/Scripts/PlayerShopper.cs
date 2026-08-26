using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MiniMart
{
    /// <summary>The shopkeeper: walks the farm and the shop floor, harvests crates and stocks shelves.</summary>
    public class PlayerShopper : MonoBehaviour
    {
        private enum TargetKind { None, Harvest, Shelf, Upgrade, Checkout }

        private CharacterController controller;
        private Transform visual;
        private Transform leftLeg;
        private Transform rightLeg;
        private Transform leftArm;
        private Transform rightArm;
        private Transform carryVisual;

        private ProductKind? carrying;
        private int carryAmount;
        private Vector3 moveVelocity;
        private float walkPhase;
        private bool sprinting;

        private TargetKind targetKind;
        private FarmProducer targetFarm;
        private ShelfUnit targetShelf;
        private UpgradeStation targetUpgrade;

        public ProductKind? Carrying => carrying;
        public int CarryAmount => carryAmount;

        /// <summary>What the HUD shows for whatever is in reach right now.</summary>
        public string Prompt { get; private set; } = string.Empty;

        public void Initialise()
        {
            controller = gameObject.AddComponent<CharacterController>();
            controller.radius = 0.38f;
            controller.height = 1.52f;
            controller.center = new Vector3(0f, 0.76f, 0f);

            visual = new GameObject("Yellow_Farm_Player").transform;
            visual.SetParent(transform, false);

            GameObject playerAsset = Resources.Load<GameObject>("Characters/FarmPlayer");
            if (playerAsset != null)
            {
                GameObject imported = Instantiate(playerAsset, visual);
                imported.name = "Farm_Player_Asset";
                imported.transform.localPosition = Vector3.zero;
                imported.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                imported.transform.localScale = Vector3.one * 0.72f;
                Material yellow = MiniMartGameManager.Instance.MaterialFor("PlayerYellow", new Color(1f, 0.82f, 0.10f));
                foreach (Renderer renderer in imported.GetComponentsInChildren<Renderer>(true)) renderer.sharedMaterial = yellow;
                foreach (Collider collider in imported.GetComponentsInChildren<Collider>(true)) Destroy(collider);
            }
            else
            {
                ToyCharacter.Build(visual, new Color(1f, 0.82f, 0.10f), new Color(1f, 0.82f, 0.10f), "Player", false);
                BuildWalkRig();
            }
        }

        /// <summary>
        /// Swinging limbs for the primitive stand in only. The imported FarmPlayer model gets no
        /// overlay geometry: extra capsules poked through the mesh.
        /// </summary>
        private void BuildWalkRig()
        {
            MiniMartGameManager game = MiniMartGameManager.Instance;
            Transform rig = new GameObject("Player_Walk_Rig").transform;
            rig.SetParent(visual, false);

            Material limb = game.MaterialFor("PlayerLimbYellow", new Color(1f, 0.72f, 0.05f));
            GameObject shadow = game.CreateDecor(PrimitiveType.Cylinder, "Player_Grounded_Shadow", visual.position,
                new Vector3(0.44f, 0.02f, 0.34f), game.MaterialFor("PlayerShadow", new Color(0.2f, 0.26f, 0.22f)), rig);
            shadow.transform.localPosition = new Vector3(0f, 0.025f, 0f);

            leftLeg = CreateLimb(rig, "Player_Left_Leg", limb, new Vector3(-0.16f, 0.32f, 0.02f), new Vector3(0.13f, 0.28f, 0.13f));
            rightLeg = CreateLimb(rig, "Player_Right_Leg", limb, new Vector3(0.16f, 0.32f, 0.02f), new Vector3(0.13f, 0.28f, 0.13f));
            leftArm = CreateLimb(rig, "Player_Left_Arm", limb, new Vector3(-0.38f, 0.86f, 0f), new Vector3(0.105f, 0.24f, 0.105f));
            rightArm = CreateLimb(rig, "Player_Right_Arm", limb, new Vector3(0.38f, 0.86f, 0f), new Vector3(0.105f, 0.24f, 0.105f));
        }

        private Transform CreateLimb(Transform parent, string limbName, Material material, Vector3 localPosition, Vector3 scale)
        {
            GameObject limb = MiniMartGameManager.Instance.CreateDecor(PrimitiveType.Capsule, limbName, visual.position, scale, material, parent);
            limb.transform.localPosition = localPosition;
            return limb.transform;
        }

        // ------------------------------------------------------------------ update

        private void Update()
        {
            MiniMartGameManager game = MiniMartGameManager.Instance;
            if (game == null || game.IsPaused) return;

            Vector2 input = ReadMovement();
            Vector3 direction = new Vector3(input.x, 0f, input.y);
            if (direction.sqrMagnitude > 1f) direction.Normalize();

            float speed = GameConfig.PlayerWalkSpeed * (sprinting ? GameConfig.PlayerSprintMultiplier : 1f);
            moveVelocity = Vector3.Lerp(moveVelocity, direction * speed, 1f - Mathf.Exp(-12f * Time.deltaTime));
            controller.Move(moveVelocity * Time.deltaTime + Vector3.down * 2f * Time.deltaTime);

            if (direction.sqrMagnitude > 0.01f)
            {
                transform.forward = Vector3.Slerp(transform.forward, direction, 12f * Time.deltaTime);
                walkPhase += Time.deltaTime * (sprinting ? 13f : 9f);
                AnimateWalk(1f);
            }
            else
            {
                AnimateWalk(0f);
            }

            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;

            ResolveTarget();
            UpdatePrompt();
            UpdateCarryVisual();
        }

        private Vector2 ReadMovement()
        {
            Keyboard keyboard = Keyboard.current;
            sprinting = false;
            if (keyboard == null) return Vector2.zero;

            Vector2 input = Vector2.zero;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1f;
            sprinting = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;

            if (keyboard.eKey.wasPressedThisFrame) Interact();
            if (keyboard.qKey.wasPressedThisFrame) DropCarry();
            return input;
        }

        private void AnimateWalk(float blend)
        {
            if (leftLeg == null || rightLeg == null || leftArm == null || rightArm == null) return;
            float legSwing = Mathf.Sin(walkPhase) * 27f * blend;
            float armSwing = Mathf.Sin(walkPhase) * 22f * blend;
            leftLeg.localRotation = Quaternion.Slerp(leftLeg.localRotation, Quaternion.Euler(legSwing, 0f, 0f), Time.deltaTime * 14f);
            rightLeg.localRotation = Quaternion.Slerp(rightLeg.localRotation, Quaternion.Euler(-legSwing, 0f, 0f), Time.deltaTime * 14f);
            leftArm.localRotation = Quaternion.Slerp(leftArm.localRotation, Quaternion.Euler(-armSwing, 0f, -8f), Time.deltaTime * 14f);
            rightArm.localRotation = Quaternion.Slerp(rightArm.localRotation, Quaternion.Euler(armSwing, 0f, 8f), Time.deltaTime * 14f);
        }

        // ------------------------------------------------------------- interaction

        /// <summary>
        /// Works out the single thing E will act on. The HUD prompt and Interact() both read this,
        /// so what you are told is always what happens.
        /// </summary>
        private void ResolveTarget()
        {
            MiniMartGameManager game = MiniMartGameManager.Instance;
            targetKind = TargetKind.None;
            targetFarm = null;
            targetShelf = null;
            targetUpgrade = null;

            float range = GameConfig.InteractRange;

            if (carrying == null)
            {
                targetFarm = FindClosest(game.FarmProducers, range);
                if (targetFarm != null) { targetKind = TargetKind.Harvest; return; }
            }
            else
            {
                targetShelf = ClosestShelfForCarry(range);
                if (targetShelf != null) { targetKind = TargetKind.Shelf; return; }
            }

            targetUpgrade = FindClosest(game.Upgrades, range);
            if (targetUpgrade != null) { targetKind = TargetKind.Upgrade; return; }

            if (carrying != null)
            {
                targetShelf = FindClosest(game.Shelves, range);
                if (targetShelf != null) { targetKind = TargetKind.Shelf; return; }
            }

            if (game.Checkout != null && Vector3.Distance(transform.position, game.Checkout.transform.position) < 2.8f)
                targetKind = TargetKind.Checkout;
        }

        /// <summary>Matching shelf with room first, then any matching shelf so we can explain why.</summary>
        private ShelfUnit ClosestShelfForCarry(float range)
        {
            MiniMartGameManager game = MiniMartGameManager.Instance;
            ShelfUnit withRoom = null;
            ShelfUnit anyMatch = null;
            float bestRoom = range;
            float bestMatch = range;
            for (int i = 0; i < game.Shelves.Count; i++)
            {
                ShelfUnit shelf = game.Shelves[i];
                if (shelf == null || shelf.Product != carrying.Value) continue;
                float distance = Vector3.Distance(transform.position, shelf.transform.position);
                if (!shelf.IsFull && distance < bestRoom) { bestRoom = distance; withRoom = shelf; }
                if (distance < bestMatch) { bestMatch = distance; anyMatch = shelf; }
            }
            return withRoom ?? anyMatch;
        }

        private void Interact()
        {
            MiniMartGameManager game = MiniMartGameManager.Instance;
            switch (targetKind)
            {
                case TargetKind.Harvest:
                    if (!targetFarm.TryHarvest()) return;
                    carrying = targetFarm.Product;
                    carryAmount = game.CrateSize;
                    game.Sfx.Play(SfxKind.Harvest);
                    game.UI.SetNotification("Picked up " + carryAmount + " " + GameConfig.ProductLabel(carrying.Value)
                        + ". Take the crate to the matching shelf.", 2.2f);
                    return;

                case TargetKind.Shelf:
                    StockShelf(targetShelf);
                    return;

                case TargetKind.Upgrade:
                    targetUpgrade.TryPurchase();
                    return;

                case TargetKind.Checkout:
                    game.UI.SetNotification("The till scans shoppers on its own, just keep the shelves full.", 2f);
                    return;

                default:
                    game.UI.SetNotification(carrying == null
                        ? "Nothing in reach. Head to a crop plot or the egg nest and press E."
                        : "Carry the crate to a " + GameConfig.ProductLabel(carrying.Value) + " shelf and press E.", 2f);
                    return;
            }
        }

        private void StockShelf(ShelfUnit shelf)
        {
            MiniMartGameManager game = MiniMartGameManager.Instance;
            if (carrying == null) return;

            if (shelf.Product != carrying.Value)
            {
                game.Sfx.Play(SfxKind.Deny);
                game.UI.SetNotification("That shelf holds " + GameConfig.ProductLabel(shelf.Product)
                    + ", you are carrying " + GameConfig.ProductLabel(carrying.Value) + ".", 2.2f);
                return;
            }

            int placed = shelf.Restock(carrying.Value, carryAmount);
            if (placed <= 0)
            {
                game.Sfx.Play(SfxKind.Deny);
                game.UI.SetNotification("This shelf is already full.", 1.8f);
                return;
            }

            carryAmount -= placed;
            game.Sfx.Play(SfxKind.Stock);
            if (carryAmount <= 0)
            {
                carrying = null;
                carryAmount = 0;
                game.UI.SetNotification("Shelf stocked with " + placed + ".", 1.4f);
            }
            else
            {
                game.UI.SetNotification("Placed " + placed + ", still carrying " + carryAmount + ".", 1.6f);
            }
        }

        private void DropCarry()
        {
            if (carrying == null) return;
            MiniMartGameManager game = MiniMartGameManager.Instance;
            game.UI.SetNotification("Dropped the " + GameConfig.ProductLabel(carrying.Value) + " crate.", 1.5f);
            carrying = null;
            carryAmount = 0;
        }

        private void UpdatePrompt()
        {
            MiniMartGameManager game = MiniMartGameManager.Instance;
            switch (targetKind)
            {
                case TargetKind.Harvest:
                    Prompt = targetFarm.IsReady
                        ? "[E]  Harvest " + targetFarm.Label + "  (crate of " + game.CrateSize + ")"
                        : targetFarm.Label + " regrows in " + Mathf.CeilToInt(targetFarm.RegrowRemaining) + "s";
                    return;

                case TargetKind.Shelf:
                    if (carrying == null) { Prompt = string.Empty; return; }
                    if (targetShelf.Product != carrying.Value)
                    {
                        Prompt = "Wrong shelf: it holds " + GameConfig.ProductLabel(targetShelf.Product);
                        return;
                    }
                    Prompt = targetShelf.IsFull
                        ? GameConfig.ProductLabel(targetShelf.Product) + " shelf is full (" + targetShelf.Stock + "/" + GameConfig.ShelfCapacity + ")"
                        : "[E]  Stock " + GameConfig.ProductLabel(targetShelf.Product) + " shelf  (" + targetShelf.Stock + "/" + GameConfig.ShelfCapacity + ")";
                    return;

                case TargetKind.Upgrade:
                    Prompt = targetUpgrade.PromptText();
                    return;

                case TargetKind.Checkout:
                    Prompt = "Till: " + game.Checkout.QueueLength + " in the queue";
                    return;

                default:
                    Prompt = string.Empty;
                    return;
            }
        }

        private void UpdateCarryVisual()
        {
            MiniMartGameManager game = MiniMartGameManager.Instance;
            if (carrying == null)
            {
                if (carryVisual != null) carryVisual.gameObject.SetActive(false);
                return;
            }

            if (carryVisual == null)
            {
                carryVisual = game.CreateDecor(PrimitiveType.Cube, "Carry_Crate", transform.position, Vector3.one,
                    game.MaterialFor("Carry_" + carrying.Value, game.ProductColor(carrying.Value)), transform).transform;
            }

            carryVisual.gameObject.SetActive(true);
            carryVisual.localPosition = new Vector3(0f, 0.75f, 0.48f);
            float bulk = Mathf.Lerp(0.28f, 0.46f, Mathf.Clamp01(carryAmount / 12f));
            carryVisual.localScale = new Vector3(0.42f, bulk, 0.34f);
            Renderer renderer = carryVisual.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = game.MaterialFor("Carry_" + carrying.Value, game.ProductColor(carrying.Value));
        }

        private T FindClosest<T>(IEnumerable<T> candidates, float radius) where T : Component
        {
            T closest = null;
            float best = radius;
            foreach (T item in candidates)
            {
                if (item == null) continue;
                float distance = Vector3.Distance(transform.position, item.transform.position);
                if (distance >= best) continue;
                best = distance;
                closest = item;
            }
            return closest;
        }
    }
}
