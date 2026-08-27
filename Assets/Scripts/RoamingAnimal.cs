using UnityEngine;

namespace MiniMart
{
    /// <summary>
    /// A farm animal pottering about inside a patch of ground. None of these models have a skeleton,
    /// so the life is procedural: short walks to a random spot in the patch, a bob while stepping,
    /// grazing or pecking during the pauses, and a hop when a watched nest fills up.
    ///
    /// The patch is the whole behaviour: an animal can only ever pick a target inside it, so keeping
    /// livestock out of the shop and off the crop beds is a matter of where the patch is, not of
    /// avoidance code that could fail.
    /// </summary>
    public class RoamingAnimal : MonoBehaviour
    {
        private Transform body;
        private FarmProducer watched;
        private Rect patch;
        private Vector3 target;

        private float speed;
        private float bobHeight;
        private float waitTimer;
        private float bobPhase;
        private float graze;
        private float hop;
        private bool grazes;
        private bool watchedWasReady;

        /// <summary>
        /// <paramref name="area"/> is in world XZ, with Rect.y standing in for z.
        /// <paramref name="grazesWhileResting"/> dips the head during pauses, which suits the birds
        /// and the grazers; the rest just breathe.
        /// </summary>
        public void Initialise(Transform bodyPivot, Rect area, float moveSpeed, float bob,
            bool grazesWhileResting, FarmProducer watchedProducer = null)
        {
            body = bodyPivot;
            patch = area;
            speed = moveSpeed;
            bobHeight = bob;
            grazes = grazesWhileResting;
            watched = watchedProducer;

            bobPhase = Random.Range(0f, 6.28f);
            waitTimer = Random.Range(0.4f, 3f);
            target = transform.position;
            watchedWasReady = watched == null || watched.IsReady;
            transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        }

        private void Update()
        {
            if (body == null) return;
            WatchNest();

            if (hop > 0f)
            {
                Hop();
                return;
            }

            Vector3 delta = target - transform.position;
            delta.y = 0f;

            if (delta.sqrMagnitude < 0.03f)
            {
                Rest();
                return;
            }

            transform.position += delta.normalized * speed * Time.deltaTime;
            transform.forward = Vector3.Slerp(transform.forward, delta.normalized, 5f * Time.deltaTime);

            // A quick bob reads as walking on a model with no legs to animate.
            bobPhase += Time.deltaTime * 9f;
            graze = Mathf.Lerp(graze, 0f, Time.deltaTime * 8f);
            body.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(bobPhase)) * bobHeight, 0f);
            body.localRotation = Quaternion.Euler(graze, 0f, Mathf.Sin(bobPhase) * 3f);
        }

        /// <summary>Standing still: graze or breathe, then choose somewhere else in the patch.</summary>
        private void Rest()
        {
            waitTimer -= Time.deltaTime;

            float wanted = grazes && Mathf.Sin(Time.time * 3.5f + bobPhase) > 0.5f ? 30f : 0f;
            graze = Mathf.Lerp(graze, wanted, Time.deltaTime * 7f);
            body.localPosition = Vector3.Lerp(body.localPosition,
                new Vector3(0f, Mathf.Sin(Time.time * 1.4f + bobPhase) * bobHeight * 0.25f, 0f), Time.deltaTime * 5f);
            body.localRotation = Quaternion.Slerp(body.localRotation, Quaternion.Euler(graze, 0f, 0f), Time.deltaTime * 7f);

            if (waitTimer > 0f) return;
            waitTimer = Random.Range(1.5f, 5f);
            target = new Vector3(Random.Range(patch.xMin, patch.xMax), transform.position.y, Random.Range(patch.yMin, patch.yMax));
        }

        /// <summary>Little celebration when the nest fills up, so a new egg is noticeable.</summary>
        private void Hop()
        {
            hop -= Time.deltaTime;
            float phase = Mathf.Clamp01(1f - hop / 0.45f);
            body.localPosition = new Vector3(0f, Mathf.Sin(phase * Mathf.PI) * bobHeight * 4.5f, 0f);
            body.localRotation = Quaternion.Euler(-14f * Mathf.Sin(phase * Mathf.PI), 0f, 0f);
            if (hop <= 0f) body.localPosition = Vector3.zero;
        }

        private void WatchNest()
        {
            if (watched == null) return;
            bool ready = watched.IsReady;
            if (ready && !watchedWasReady)
            {
                hop = 0.45f;
                waitTimer = Random.Range(0.6f, 1.6f);
            }
            watchedWasReady = ready;
        }
    }
}
