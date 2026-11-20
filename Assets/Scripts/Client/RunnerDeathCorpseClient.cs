using System.Collections;
using KaplaoGames.Shared;
using UnityEngine;

namespace KaplaoGames.Client
{
    /// <summary>
    /// Client visual spawned via ClientRpc on every peer at the same world position.
    /// </summary>
    public static class RunnerDeathCorpseClient
    {
        static readonly int DeathId = Animator.StringToHash("Death");

        public static void Spawn(
            Vector2 worldPosition,
            int characterIndex,
            bool facingRight,
            int sortingOrder,
            float lieDuration,
            float fadeDuration)
        {
            RunnerCharacterLibrary library = RunnerCharacterLibrary.LoadDefault();
            if (library == null)
                return;

            RuntimeAnimatorController controller = library.GetController(characterIndex);
            if (controller == null)
                return;

            var root = new GameObject("RunnerDeathCorpse");
            root.transform.position = new Vector3(worldPosition.x, worldPosition.y, 0f);

            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = new Vector3(facingRight ? 1f : -1f, 1f, 1f);

            var spriteRenderer = visual.AddComponent<SpriteRenderer>();
            spriteRenderer.sortingOrder = sortingOrder;

            var animator = visual.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.Play("Idle", 0, 0f);
            animator.Update(0f);
            animator.ResetTrigger(DeathId);
            animator.SetTrigger(DeathId);

            root.AddComponent<RunnerDeathCorpseLifecycle>().Begin(
                animator,
                spriteRenderer,
                lieDuration,
                fadeDuration);
        }
    }

    sealed class RunnerDeathCorpseLifecycle : MonoBehaviour
    {
        const string DeathStateName = "Dead";
        const float DeathAnimFallback = 0.65f;

        Animator animator;
        SpriteRenderer spriteRenderer;
        float lieDuration;
        float fadeDuration;

        public void Begin(
            Animator targetAnimator,
            SpriteRenderer targetSpriteRenderer,
            float lieSeconds,
            float fadeSeconds)
        {
            animator = targetAnimator;
            spriteRenderer = targetSpriteRenderer;
            lieDuration = Mathf.Max(0f, lieSeconds);
            fadeDuration = Mathf.Max(0f, fadeSeconds);
            StartCoroutine(LifecycleRoutine());
        }

        IEnumerator LifecycleRoutine()
        {
            float deathAnimDeadline = Time.time + DeathAnimFallback;
            yield return null;

            while (Time.time < deathAnimDeadline)
            {
                if (animator == null)
                    break;

                AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
                if (state.IsName(DeathStateName) && state.normalizedTime >= 1f)
                    break;

                yield return null;
            }

            if (animator != null)
                animator.speed = 0f;

            if (lieDuration > 0f)
                yield return new WaitForSeconds(lieDuration);

            if (spriteRenderer != null && fadeDuration > 0f)
            {
                Color color = spriteRenderer.color;
                float elapsed = 0f;

                while (elapsed < fadeDuration)
                {
                    elapsed += Time.deltaTime;
                    color.a = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
                    spriteRenderer.color = color;
                    yield return null;
                }
            }

            Destroy(gameObject);
        }
    }
}
