using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MiniMart
{
    /// <summary>The shopkeeper: walks the farm and the shop floor, harvests crates and stocks shelves.</summary>
    public class PlayerShopper : MonoBehaviour
    {
        private enum TargetKind { None, Harvest, Shelf, Upgrade, Checkout }

        private const string AnimatedModelPath = "Characters/FarmPlayerRun";
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
        private CharacterRunAnimator runAnimator;

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

            GameObject animatedAsset = Resources.Load<GameObject>(AnimatedModelPath);
            if (animatedAsset != null && TryBuildAnimatedModel(animatedAsset, yellow)) return;

            GameObject staticAsset = Resources.Load<GameObject>(StaticModelPath);
            if (staticAsset != null)
            {
                GameObject imported = Instantiate(staticAsset, visual);
                imported.name = "Farm_Player_Asset";
                imported.transform.localPosition = Vector3.zero;
                imported.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                imported.transform.localScale = Vector3.one * 0.72f;
                Paint(imported, yellow);
                StripColliders(imported);
                return;
            }

            ToyCharacter.Build(visual, new Color(1f, 0.82f, 0.10f), new Color(1f, 0.82f, 0.10f), "Player", false);
            BuildWalkRig();
        }

        /// <summary>
        /// Sets up the skinned Mixamo character and its run take. Returns false if the model came in
        /// without an Animator or a clip, which means the rig import settings need attention.
        /// </summary>
        private bool TryBuildAnimatedModel(GameObject asset, Material material)
        {
            GameObject model = Instantiate(asset, visual);
            model.name = "Farm_Player_Rig";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;

            Animator animator = model.GetComponent<Animator>() ?? model.GetComponentInChildren<Animator>(true);
            AnimationClip clip = LoadLongestClip(AnimatedModelPath);
            if (animator == null || clip == null)
            {
                Debug.LogWarning("Characters/FarmPlayerRun has no Animator or animation clip. "
                    + "Set its Rig to Generic and tick Import Animation, then press Play again.");
                Destroy(model);
                return false;
            }

            KeepOnlyFirstLod(model);
            FitToController(model.transform);
            Paint(model, material);
            StripColliders(model);

            runAnimator = gameObject.AddComponent<CharacterRunAnimator>();
            if (runAnimator.Setup(animator, clip)) return true;

            Destroy(runAnimator);
            runAnimator = null;
            Destroy(model);
            return false;
        }

        /// <summary>Mixamo exports five stacked LOD skins. Keep one, drop the rest and the group.</summary>
        private static void KeepOnlyFirstLod(GameObject model)
        {
            LODGroup group = model.GetComponent<LODGroup>();
            if (group != null) Destroy(group);

            SkinnedMeshRenderer[] skins = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (skins.Length <= 1) return;

            int keep = 0;
            for (int i = 0; i < skins.Length; i++)
            {
                if (!skins[i].name.EndsWith("LOD0")) continue;
                keep = i;
                break;
            }
            for (int i = 0; i < skins.Length; i++)
            {
                if (i != keep) Destroy(skins[i].gameObject);
            }
        }

        /// <summary>
        /// Scales the imported body to the capsule and drops it so the feet sit on the floor. Measured
        /// from mesh bounds rather than hard coded, because the FBX import scale is not ours to assume.
        /// </summary>
        private void FitToController(Transform model)
        {
            if (!TryMeasureHeight(model, out Bounds bounds) || bounds.size.y <= 0.0001f) return;

            float scale = Mathf.Clamp(TargetBodyHeight / bounds.size.y, 0.005f, 20f);
            model.localScale = Vector3.one * scale;
            model.localPosition = new Vector3(0f, -bounds.min.y * scale, 0f);
        }

        /// <summary>Combined renderer bounds expressed in <paramref name="root"/>'s local space.</summary>
        private static bool TryMeasureHeight(Transform root, out Bounds bounds)
        {
            bounds = new Bounds();
            bool measured = false;

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Mesh mesh = renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh : null;
                if (mesh == null)
                {
                    MeshFilter filter = renderer.GetComponent<MeshFilter>();
                    mesh = filter != null ? filter.sharedMesh : null;
                }
                if (mesh == null) continue;

                Bounds local = mesh.bounds;
                Matrix4x4 toRoot = root.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = new Vector3(
                        (corner & 1) == 0 ? local.min.x : local.max.x,
                        (corner & 2) == 0 ? local.min.y : local.max.y,
                        (corner & 4) == 0 ? local.min.z : local.max.z);
                    Vector3 inRoot = toRoot.MultiplyPoint3x4(point);
                    if (measured) bounds.Encapsulate(inRoot);
                    else { bounds = new Bounds(inRoot, Vector3.zero); measured = true; }
                }
            }
            return measured;
        }

        private static AnimationClip LoadLongestClip(string resourcePath)
        {
            AnimationClip[] clips = Resources.LoadAll<AnimationClip>(resourcePath);
            AnimationClip best = null;
            for (int i = 0; i < clips.Length; i++)
            {
                AnimationClip clip = clips[i];
                if (clip == null || clip.name.StartsWith("__preview__")) continue;
                if (best == null || clip.length > best.length) best = clip;
            }
            return best;
        }

        private static void Paint(GameObject root, Material material)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true)) renderer.sharedMaterial = material;
        }

        private static void StripColliders(GameObject root)
        {
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true)) Destroy(collider);
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
        /// Runs the Mixamo cycle at a rate tied to real ground speed, so the stride stays in step
        /// with the movement instead of sliding.
        /// </summary>
        private void AnimateBody()
        {
            if (runAnimator == null || !runAnimator.IsReady) return;

            float groundSpeed = new Vector3(moveVelocity.x, 0f, moveVelocity.z).magnitude;
            float rate = groundSpeed <= 0.15f
                ? 0f
                : Mathf.Clamp(groundSpeed / GameConfig.PlayerWalkSpeed, 0.45f, 2f);
            runAnimator.Advance(rate, Time.deltaTime);
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
                GameObject eggAsset = Resources.Load<GameObject>("Items/FarmEgg");
                if (eggAsset != null)
                {
                    GameObject egg = Instantiate(eggAsset, transform);
                    egg.name = "Carry_Item";
                    egg.transform.localPosition = new Vector3(0f, 0.64f, 0.46f);
                    egg.transform.localRotation = Quaternion.Euler(0f, 25f, 0f);
                    egg.transform.localScale = Vector3.one * 0.20f;
                    foreach (Renderer renderer in egg.GetComponentsInChildren<Renderer>(true)) renderer.sharedMaterial = material;
                    foreach (Collider collider in egg.GetComponentsInChildren<Collider>(true)) Destroy(collider);
                    return egg.transform;
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
