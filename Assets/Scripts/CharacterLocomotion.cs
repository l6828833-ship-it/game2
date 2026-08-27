using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace MiniMart
{
    /// <summary>
    /// Blends a character's imported Mixamo clips through a Playable graph, so the project needs no
    /// AnimatorController asset. Used by the player and by every shopper.
    ///
    /// It handles three things the raw clips do not:
    /// - crossfades between idle, holding idle and the moving clip,
    /// - plays the moving clip at the rate its own stride implies for the current ground speed,
    ///   which is what stops the feet skating,
    /// - cancels the horizontal travel baked into the pelvis, which otherwise slides the body ahead
    ///   of the character and snaps it back on every loop.
    /// </summary>
    public class CharacterLocomotion : MonoBehaviour
    {
        /// <summary>Weight units per second: a state change takes roughly a sixth of a second.</summary>
        private const float BlendSpeed = 6f;
        private const float MoveThreshold = 0.2f;
        private const float MinRate = 0.4f;
        private const float MaxRate = 1.8f;

        private PlayableGraph graph;
        private AnimationMixerPlayable mixer;
        private AnimationClipPlayable[] states;
        private float[] lengths;
        private double[] times;
        private float[] weights;

        private Transform pelvis;
        private Vector3 pelvisLock;
        private bool pelvisLocked;

        private int moveIndex = -1;
        private int idleIndex = -1;
        private int carryIndex = -1;
        private float authoredSpeed;
        private bool ready;

        public bool IsReady => ready;

        /// <summary>Ground speed the moving clip was authored for, in units per second.</summary>
        public float AuthoredSpeed => authoredSpeed;

        /// <summary>Only <paramref name="move"/> is required; the idles fall back to it when missing.</summary>
        public bool Setup(Animator animator, AnimationClip move, AnimationClip idle, AnimationClip carryIdle, Transform pelvisBone)
        {
            if (animator == null || move == null || move.length <= 0.001f) return false;

            List<AnimationClip> clips = new List<AnimationClip>();
            moveIndex = Register(clips, move);
            idleIndex = Register(clips, idle);
            carryIndex = Register(clips, carryIdle);
            if (idleIndex < 0) idleIndex = moveIndex;
            if (carryIndex < 0) carryIndex = idleIndex;

            animator.applyRootMotion = false; // movement belongs to the game, not the clip
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            pelvis = pelvisBone;

            graph = PlayableGraph.Create("MiniMart_Locomotion");
            // Manual: the pose is written exactly once per frame, by Advance.
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);

            mixer = AnimationMixerPlayable.Create(graph, clips.Count);
            states = new AnimationClipPlayable[clips.Count];
            lengths = new float[clips.Count];
            times = new double[clips.Count];
            weights = new float[clips.Count];

            for (int i = 0; i < clips.Count; i++)
            {
                states[i] = AnimationClipPlayable.Create(graph, clips[i]);
                states[i].SetApplyFootIK(false);
                states[i].SetSpeed(0d); // time is stepped by hand
                graph.Connect(states[i], 0, mixer, i);
                lengths[i] = Mathf.Max(0.001f, clips[i].length);
                mixer.SetInputWeight(i, 0f);
            }

            AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "CharacterPose", animator);
            output.SetSourcePlayable(mixer);
            graph.Play();

            authoredSpeed = MeasureAuthoredSpeed(moveIndex);

            // Settle into the idle pose rather than the imported bind pose.
            for (int i = 0; i < weights.Length; i++)
            {
                weights[i] = i == idleIndex ? 1f : 0f;
                mixer.SetInputWeight(i, weights[i]);
                times[i] = 0d;
                states[i].SetTime(0d);
            }
            graph.Evaluate();

            ready = true;
            return true;
        }

        private static int Register(List<AnimationClip> clips, AnimationClip clip)
        {
            if (clip == null || clip.length <= 0.001f) return -1;
            clips.Add(clip);
            return clips.Count - 1;
        }

        /// <summary>
        /// How far the pelvis travels across the moving clip, per second, in world units. Sampling the
        /// clip means any clip works, at any import scale, without hard coded numbers.
        /// </summary>
        private float MeasureAuthoredSpeed(int index)
        {
            if (pelvis == null) return 0f;

            for (int i = 0; i < states.Length; i++) mixer.SetInputWeight(i, i == index ? 1f : 0f);

            states[index].SetTime(0d);
            graph.Evaluate();
            Vector3 start = pelvis.position;

            states[index].SetTime(lengths[index] * 0.995f);
            graph.Evaluate();
            Vector3 end = pelvis.position;

            Vector3 travel = end - start;
            travel.y = 0f;
            return travel.magnitude / lengths[index];
        }

        /// <summary>
        /// <paramref name="groundSpeed"/> is the character's real speed over the floor. Below a
        /// walking threshold it fades to one of the idles.
        /// </summary>
        public void Advance(float groundSpeed, bool carrying, float deltaTime)
        {
            if (!ready) return;

            bool moving = groundSpeed > MoveThreshold;
            int target = moving ? moveIndex : carrying ? carryIndex : idleIndex;
            float moveRate = !moving ? 0f
                : authoredSpeed > 0.05f ? Mathf.Clamp(groundSpeed / authoredSpeed, MinRate, MaxRate)
                : 1f;

            float total = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                weights[i] = Mathf.MoveTowards(weights[i], i == target ? 1f : 0f, deltaTime * BlendSpeed);
                total += weights[i];
            }
            if (total <= 0.0001f)
            {
                weights[target] = 1f;
                total = 1f;
            }

            for (int i = 0; i < weights.Length; i++)
            {
                // Normalised so a blend never bleeds the bind pose back in.
                mixer.SetInputWeight(i, weights[i] / total);
                if (weights[i] <= 0.0001f) continue;

                float rate = i == moveIndex ? moveRate : 1f;
                if (rate > 0f)
                {
                    times[i] += deltaTime * rate;
                    if (times[i] >= lengths[i]) times[i] %= lengths[i];
                }
                states[i].SetTime(times[i]);
            }

            graph.Evaluate();
            LockPelvisDrift();
        }

        /// <summary>
        /// The walk and run takes travel a metre or more in their pelvis curves. Holding the pelvis at
        /// its first evaluated horizontal position keeps the stride but removes the slide, while the
        /// vertical channel is left alone so the body still bobs.
        /// </summary>
        private void LockPelvisDrift()
        {
            if (pelvis == null) return;
            if (!pelvisLocked)
            {
                pelvisLock = pelvis.localPosition;
                pelvisLocked = true;
                return;
            }
            Vector3 current = pelvis.localPosition;
            pelvis.localPosition = new Vector3(pelvisLock.x, current.y, pelvisLock.z);
        }

        private void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy();
            ready = false;
        }
    }
}
