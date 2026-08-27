using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MiniMart
{
    /// <summary>The shopkeeper: walks the farm and the shop floor, harvests crates and stocks shelves.</summary>
    public class PlayerShopper : MonoBehaviour
    {
        private enum TargetKind { None, Harvest, Shelf, Upgrade, Checkout }

        private const string StaticModelPath = "Characters/FarmPlayer";

        /// <summary>Roughly the capsule height, so the body reads at the right size next to the shelves.</summary>
        private const float TargetBodyHeight = 1.6f;

        private CharacterController controller;
        private Transform visual;
        private Transform leftLeg;
        private Transform rightLeg;
        private Transform leftArm;
        private Transform rightArm;
        private Transform carryVisual;
        private ProductKind? carryVisualKind;
        private CharacterLocomotion locomotion;

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
            BuildVisual();
        }

        /// <summary>
        /// Best available body, in order: the animated Mixamo rig, the static farm player mesh,
        /// then primitives with a hand rolled walk cycle.
        /// </summary>
        private void BuildVisual()
        {
            Material yellow = MiniMartGameManager.Instance.MaterialFor("PlayerYellow", new Color(1f, 0.82f, 0.10f));
            if (TryBuildAnimatedModel(yellow)) return;

            GameObject staticAsset = Resources.Load<GameObject>(StaticModelPath);
            if (staticAsset != null)
            {
                GameObject imported = Instantiate(staticAsset, visual);
                imported.name = "Farm_Player_Asset";
                imported.transform.localPosition = Vector3.zero;
                imported.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                imported.transform.localScale = Vector3.one * 0.72f;
                CharacterRig.Paint(imported, yellow);
                CharacterRig.StripColliders(imported);
                return;
            }

            ToyCharacter.Build(visual, new Color(1f, 0.82f, 0.10f), new Color(1f, 0.82f, 0.10f), "Player", false);
            BuildWalkRig();
        }

        /// <summary>
        /// Sets up the skinned Mixamo body with its run take plus the two idles. Returns false when the
        /// model or its clip is missing, which means the rig import settings need attention.
        /// </summary>
        private bool TryBuildAnimatedModel(Material material)
        {
            AnimationClip run = CharacterRig.LoadClip(CharacterRig.RunClip);
            if (run == null)
            {
                Debug.LogWarning(CharacterRig.RunClip + " imported without an animation clip. Tick Import "
                    + "Animation on the model and press Play again.");
                return false;
            }

            CharacterRig.Rig rig = CharacterRig.Build(visual, material, TargetBodyHeight, 0);
            if (rig == null) return false;
            rig.Model.name = "Farm_Player_Rig";

            // Every clip comes off the same Mixamo rig, so the idles play on this hierarchy too.
            AnimationClip idle = CharacterRig.LoadClip(CharacterRig.IdleClip);
            AnimationClip carryIdle = CharacterRig.LoadClip(CharacterRig.CarryIdleClip);

            locomotion = gameObject.AddComponent<CharacterLocomotion>();
            if (locomotion.Setup(rig.Animator, run, idle, carryIdle, rig.Pelvis))
            {
                Debug.Log("Player rig ready: run '" + run.name + "' " + run.length.ToString("0.00") + "s"
                    + " (authored " + locomotion.AuthoredSpeed.ToString("0.00") + " u/s)"
                    + ", idle " + (idle != null ? idle.length.ToString("0.00") + "s" : "missing")
                    + ", carry idle " + (carryIdle != null ? carryIdle.length.ToString("0.00") + "s" : "missing")
                    + ", body scaled x" + rig.Scale.ToString("0.000"));
                return true;
            }

            Debug.LogWarning("Could not start the run clip '" + run.name + "' on the player rig.");
            Destroy(locomotion);
            locomotion = null;
            Destroy(rig.Model);
            return false;
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

            AnimateBody();

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

        /// <summary>
        /// Feeds real ground speed to the locomotion blend, so the stride keeps pace with the
        /// movement and standing still settles into an idle instead of freezing mid stride.
        /// </summary>
        private void AnimateBody()
        {
            if (locomotion == null || !locomotion.IsReady) return;

            float groundSpeed = new Vector3(moveVelocity.x, 0f, moveVelocity.z).magnitude;
            locomotion.Advance(groundSpeed, carrying != null, Time.deltaTime);
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

            // Rebuild when the product changes: eggs use the supplied model, everything else a crate.
            if (carryVisual != null && carryVisualKind != carrying)
            {
                Destroy(carryVisual.gameObject);
                carryVisual = null;
            }
            if (carryVisual == null)
            {
                carryVisual = BuildCarryVisual(game, carrying.Value);
                carryVisualKind = carrying;
            }

            carryVisual.gameObject.SetActive(true);
            if (carrying.Value == ProductKind.Egg) return;
            float bulk = Mathf.Lerp(0.28f, 0.46f, Mathf.Clamp01(carryAmount / 12f));
            carryVisual.localScale = new Vector3(0.42f, bulk, 0.34f);
        }

        private Transform BuildCarryVisual(MiniMartGameManager game, ProductKind kind)
        {
            Material material = game.MaterialFor("Carry_" + kind, game.ProductColor(kind));

            if (kind == ProductKind.Egg)
            {
                // Same egg mesh as the one sitting in the nest, sized to fit in a hand.
                Transform egg = ModelKit.SpawnProp(transform, ModelKit.EggModel, material, 0.17f, 3, ModelKit.ZUpFix);
                if (egg != null)
                {
                    egg.name = "Carry_Item";
                    egg.localPosition = new Vector3(0f, 0.62f, 0.44f);
                    egg.localRotation = Quaternion.Euler(0f, 25f, 12f);
                    return egg;
                }

                GameObject legacyAsset = Resources.Load<GameObject>(ModelKit.LegacyEggModel);
                if (legacyAsset != null)
                {
                    GameObject legacy = Instantiate(legacyAsset, transform);
                    legacy.name = "Carry_Item";
                    legacy.transform.localPosition = new Vector3(0f, 0.64f, 0.46f);
                    legacy.transform.localRotation = Quaternion.Euler(0f, 25f, 0f);
                    legacy.transform.localScale = Vector3.one * 0.20f;
                    ModelKit.Paint(legacy, material);
                    ModelKit.StripColliders(legacy);
                    return legacy.transform;
                }
            }

            GameObject crate = game.CreateDecor(PrimitiveType.Cube, "Carry_Item", transform.position,
                new Vector3(0.42f, 0.36f, 0.34f), material, transform);
            crate.transform.localPosition = new Vector3(0f, 0.70f, 0.46f);
            return crate.transform;
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
