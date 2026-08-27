using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace MiniMart
{
    /// <summary>
    /// Blends the player's imported Mixamo clips through a Playable graph, so the project needs no
    /// AnimatorController asset. Handles three things the raw clips do not:
    ///
    /// - crossfades between idle, carrying idle and the run cycle,
    /// - drives the run cycle at a rate tied to real ground speed,
    /// - cancels the horizontal travel baked into the pelvis, which is what made the body slide
    ///   ahead of the character and snap back on every loop.
    /// </summary>
    public class CharacterLocomotion : MonoBehaviour
    {
        /// <summary>Weight units per second, so a state change takes roughly a sixth of a second.</summary>
        private const float BlendSpeed = 6f;

        private PlayableGraph graph;
        private AnimationMixerPlayable mixer;
        private AnimationClipPlayable[] states;
        private float[] lengths;
        private double[] times;
        private float[] weights;

        private Transform pelvis;
        private Vector3 pelvisLock;
        private bool pelvisLocked;

        private int runIndex = -1;
        private int idleIndex = -1;
        private int carryIndex = -1;
        private bool ready;

        public bool IsReady => ready;

        /// <summary>Only <paramref name="run"/> is required; the idles fall back to it when missing.</summary>
        public bool Setup(Animator animator, AnimationClip run, AnimationClip idle, AnimationClip carryIdle, Transform pelvisBone)
        {
            if (animator == null || run == null || run.length <= 0.001f) return false;

            List<AnimationClip> clips = new List<AnimationClip>();
            runIndex = Register(clips, run);
            idleIndex = Register(clips, idle);
            carryIndex = Register(clips, carryIdle);
            if (idleIndex < 0) idleIndex = runIndex;
            if (carryIndex < 0) carryIndex = idleIndex;

            animator.applyRootMotion = false; // the character controller owns movement
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            pelvis = pelvisBone;

            graph = PlayableGraph.Create("MiniMart_PlayerLocomotion");
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

            // Start settled in the idle pose rather than the imported bind pose.
            weights[idleIndex] = 1f;
            mixer.SetInputWeight(idleIndex, 1f);

            AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "PlayerPose", animator);
            output.SetSourcePlayable(mixer);
            graph.Play();

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
        /// <paramref name="moveRate"/> is a multiple of the run clip's authored speed; zero means
        /// standing still, which fades over to one of the idles.
        /// </summary>
        public void Advance(float moveRate, bool carrying, float deltaTime)
        {
            if (!ready) return;

            bool moving = moveRate > 0.01f;
            int target = moving ? runIndex : carrying ? carryIndex : idleIndex;

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
                // Normalised so the blend never bleeds the bind pose back in.
                mixer.SetInputWeight(i, weights[i] / total);
                if (weights[i] <= 0.0001f) continue;

                float rate = i == runIndex ? (moving ? moveRate : 0f) : 1f;
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
        /// The run take travels 1.8m forward in its pelvis curve. Holding the pelvis at its first
        /// evaluated horizontal position keeps the stride but removes the slide, while the vertical
        /// channel is left alone so the body still bobs.
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
