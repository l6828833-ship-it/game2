using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace MiniMart
{
    /// <summary>
    /// Plays one imported clip (the Mixamo run take) straight through a Playable graph, so the
    /// project still needs no AnimatorController asset. Playback time is driven by hand: that gives
    /// looping without depending on the clip's import settings and lets the stride rate follow how
    /// fast the character is actually travelling.
    /// </summary>
    public class CharacterRunAnimator : MonoBehaviour
    {
        private PlayableGraph graph;
        private AnimationClipPlayable clipPlayable;
        private float clipLength;
        private double clipTime;
        private bool ready;

        public bool IsReady => ready;

        public bool Setup(Animator animator, AnimationClip clip)
        {
            if (animator == null || clip == null || clip.length <= 0.001f) return false;

            // Movement belongs to the character controller, never to the clip.
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            graph = PlayableGraph.Create("MiniMart_PlayerRun");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

            clipPlayable = AnimationClipPlayable.Create(graph, clip);
            clipPlayable.SetApplyFootIK(false);
            clipPlayable.SetSpeed(0d); // time is stepped by hand in Advance

            AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "PlayerPose", animator);
            output.SetSourcePlayable(clipPlayable);
            graph.Play();

            clipLength = clip.length;
            ready = true;
            return true;
        }

        /// <summary>
        /// <paramref name="rate"/> is a multiple of the clip's authored speed. Zero parks the
        /// character on the first frame of the cycle so standing still looks deliberate.
        /// </summary>
        public void Advance(float rate, float deltaTime)
        {
            if (!ready) return;

            if (rate <= 0.01f)
            {
                clipTime = 0d;
            }
            else
            {
                clipTime += deltaTime * rate;
                if (clipTime >= clipLength) clipTime %= clipLength;
            }
            clipPlayable.SetTime(clipTime);
        }

        private void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy();
            ready = false;
        }
    }
}
