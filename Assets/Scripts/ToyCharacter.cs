using UnityEngine;

namespace MiniMart
{
    /// <summary>
    /// Builds the chunky toy figures used for the player fallback and every shopper.
    /// All parts are collider free so they never fight the character controller.
    /// </summary>
    public static class ToyCharacter
    {
        public static void Build(Transform root, Color shirt, Color skin, string label, bool withCap)
        {
            MiniMartGameManager game = MiniMartGameManager.Instance;

            GameObject body = game.CreateDecor(PrimitiveType.Capsule, label + "_Body", root.position,
                new Vector3(0.52f, 0.48f, 0.44f), game.MaterialFor(label + "_Body", shirt), root);
            body.transform.localPosition = new Vector3(0f, 0.42f, 0f);

            GameObject head = game.CreateDecor(PrimitiveType.Sphere, label + "_Head", root.position,
                new Vector3(0.66f, 0.61f, 0.61f), game.MaterialFor(label + "_Head", skin), root);
            head.transform.localPosition = new Vector3(0f, 0.92f, 0.02f);

            for (int side = -1; side <= 1; side += 2)
            {
                GameObject foot = game.CreateDecor(PrimitiveType.Sphere, label + "_Foot", root.position,
                    new Vector3(0.22f, 0.18f, 0.29f), game.MaterialFor(label + "_Feet", shirt * 0.72f), root);
                foot.transform.localPosition = new Vector3(side * 0.18f, 0.12f, 0.07f);
            }

            GameObject face = game.CreateDecor(PrimitiveType.Sphere, label + "_Face", root.position,
                new Vector3(0.25f, 0.14f, 0.05f), game.MaterialFor("ToyFace", new Color(0.12f, 0.18f, 0.25f)), root);
            face.transform.localPosition = new Vector3(0f, 0.92f, 0.31f);

            if (!withCap) return;
            GameObject cap = game.CreateDecor(PrimitiveType.Cylinder, label + "_Cap", root.position,
                new Vector3(0.42f, 0.10f, 0.42f), game.MaterialFor(label + "_Cap", shirt * 0.8f), root);
            cap.transform.localPosition = new Vector3(0f, 1.26f, 0.02f);
        }
    }
}
