using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CityPeople
{
    public class CityPeople : MonoBehaviour
    {
        private AnimationClip[] myClips;
        private Animator animator;

        void Start()
        {
            animator = GetComponent<Animator>();
            Debug.Log("Animation script is running.");
            if (animator != null)
            {
                AnimationClip idleClip = animator.runtimeAnimatorController.animationClips
                    .FirstOrDefault(clip => clip.name.ToLower().Contains("idle"));

                if (idleClip != null)
                {
                    animator.CrossFadeInFixedTime(idleClip.name, 1.0f);
                }
                else
                {
                    Debug.LogWarning("Idle animation not found!");
                }
            }
        }
        void PlayAnyClip()
        {
            var cl = myClips[Random.Range(0, myClips.Length)];
            animator.CrossFadeInFixedTime(cl.name, 1.0f, -1, Random.value * cl.length);
        }

        IEnumerator ShuffleClips()
        {
            while (true)
            {
                yield return new WaitForSeconds(15.0f + Random.value * 5.0f);
                PlayAnyClip();
            }
        }

    }
}
