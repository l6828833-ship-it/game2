using UnityEngine;

namespace MiniMart
{
    /// <summary>
    /// A hen pottering about near her nest. The model has no skeleton, so the life comes from
    /// procedural motion: short walks inside a small radius, a bob while stepping, pecking at the
    /// ground during the pauses, and a hop when a fresh egg turns up.
    /// </summary>
    public class ChickenAgent : MonoBehaviour
    {
        private Transform body;
        private FarmProducer nest;
        private Vector3 home;
        private Vector3 target;
        private float roam;
        private float speed;
        private float waitTimer;
        private float bobPhase;
        private float peck;
        private float hop;
        private bool nestWasReady;

        public void Initialise(Transform bodyPivot, Vector3 homePoint, float roamRadius, FarmProducer watchedNest)
        {
            body = bodyPivot;
            home = homePoint;
            roam = Mathf.Max(0.1f, roamRadius);
            nest = watchedNest;
            speed = Random.Range(0.35f, 0.6f);
            bobPhase = Random.Range(0f, 6.28f);
            waitTimer = Random.Range(0.4f, 2.2f);
            target = transform.position;
            nestWasReady = nest == null || nest.IsReady;
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

            if (delta.sqrMagnitude < 0.02f)
            {
                Rest();
                return;
            }

            transform.position += delta.normalized * speed * Time.deltaTime;
            transform.forward = Vector3.Slerp(transform.forward, delta.normalized, 6f * Time.deltaTime);

            // A quick two step bob reads as scurrying on a model with no legs to animate.
            bobPhase += Time.deltaTime * 11f;
            peck = Mathf.Lerp(peck, 0f, Time.deltaTime * 8f);
            body.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(bobPhase)) * 0.035f, 0f);
            body.localRotation = Quaternion.Euler(peck, 0f, Mathf.Sin(bobPhase) * 4f);
        }

        /// <summary>Standing still: peck at the ground now and then, then pick a new spot.</summary>
        private void Rest()
        {
            waitTimer -= Time.deltaTime;
            peck = Mathf.Lerp(peck, Mathf.Sin(Time.time * 4.5f + bobPhase) > 0.6f ? 34f : 0f, Time.deltaTime * 9f);
            body.localPosition = Vector3.Lerp(body.localPosition, Vector3.zero, Time.deltaTime * 6f);
            body.localRotation = Quaternion.Slerp(body.localRotation, Quaternion.Euler(peck, 0f, 0f), Time.deltaTime * 9f);

            if (waitTimer > 0f) return;
            waitTimer = Random.Range(1.2f, 3.6f);
            Vector2 offset = Random.insideUnitCircle * roam;
            target = home + new Vector3(offset.x, 0f, offset.y);
        }

        /// <summary>Little celebration when the nest fills up, so the new egg is noticeable.</summary>
        private void Hop()
        {
            hop -= Time.deltaTime;
            float phase = Mathf.Clamp01(1f - hop / 0.45f);
            body.localPosition = new Vector3(0f, Mathf.Sin(phase * Mathf.PI) * 0.16f, 0f);
            body.localRotation = Quaternion.Euler(-14f * Mathf.Sin(phase * Mathf.PI), 0f, 0f);
            if (hop <= 0f) body.localPosition = Vector3.zero;
        }

        private void WatchNest()
        {
            if (nest == null) return;
            bool ready = nest.IsReady;
            if (ready && !nestWasReady)
            {
                hop = 0.45f;
                waitTimer = Random.Range(0.6f, 1.6f);
            }
            nestWasReady = ready;
        }
    }
}
