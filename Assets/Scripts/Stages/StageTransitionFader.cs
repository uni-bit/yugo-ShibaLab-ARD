using System.Collections;
using UnityEngine;

[AddComponentMenu("Stages/Stage Transition Fader")]
public class StageTransitionFader : MonoBehaviour
{
    [SerializeField] private float fadeOutDuration = 0.8f;
    [SerializeField] private float fadeInDuration = 0.9f;
    [SerializeField] private Color fadeColor = Color.black;

    private static Texture2D fadeTexture;
    private float alpha;
    private bool isFading;

    public bool IsFading => isFading;

    public IEnumerator FadeOutIn(System.Action switchStageAction)
    {
        if (isFading)
        {
            yield break;
        }

        isFading = true;

        yield return FadeAlpha(0f, 1f, fadeOutDuration);
        switchStageAction?.Invoke();
        yield return null;
        yield return FadeAlpha(1f, 0f, fadeInDuration);

        alpha = 0f;
        isFading = false;
    }

    private IEnumerator FadeAlpha(float from, float to, float duration)
    {
        alpha = from;
        if (duration <= 0.0001f)
        {
            alpha = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        alpha = to;
    }

    private void OnGUI()
    {
        if (alpha <= 0.001f)
        {
            return;
        }

        EnsureTexture();
        Color previousColor = GUI.color;
        GUI.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, alpha);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), fadeTexture);
        GUI.color = previousColor;
    }

    private static void EnsureTexture()
    {
        if (fadeTexture != null)
        {
            return;
        }

        fadeTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        fadeTexture.SetPixel(0, 0, Color.white);
        fadeTexture.Apply();
    }
}