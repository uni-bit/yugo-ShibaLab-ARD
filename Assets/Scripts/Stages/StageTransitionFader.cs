using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("Stages/Stage Transition Fader")]
public class StageTransitionFader : MonoBehaviour
{
    [SerializeField] private float fadeOutDuration = 0.8f;
    [SerializeField] private float fadeInDuration = 0.9f;
    [SerializeField] private Color fadeColor = Color.black;

    private float alpha;
    private bool isFading;
    private Color activeFadeColor = Color.black;
    private Image[] fadeImages;

    public bool IsFading => isFading;
    public float Alpha => alpha;

    public IEnumerator FadeOutIn(System.Action switchStageAction, System.Action onFadeInStart = null)
    {
        if (isFading)
        {
            yield break;
        }

        activeFadeColor = fadeColor;
        isFading = true;
        StartCoroutine(FadeSequence(switchStageAction, onFadeInStart, fadeOutDuration, fadeInDuration));
        while (isFading)
        {
            yield return null;
        }
    }

    public IEnumerator FadeOutInCustom(System.Action switchStageAction, float customFadeOutDuration, float customFadeInDuration, Color customFadeColor, System.Action onFadeInStart = null)
    {
        if (isFading)
        {
            yield break;
        }

        activeFadeColor = customFadeColor;
        isFading = true;
        StartCoroutine(FadeSequence(switchStageAction, onFadeInStart, customFadeOutDuration, customFadeInDuration));
        while (isFading)
        {
            yield return null;
        }
    }

    private IEnumerator FadeSequence(System.Action switchStageAction, System.Action onFadeInStart, float outDuration, float inDuration)
    {
        yield return FadeAlpha(0f, 1f, outDuration);
        switchStageAction?.Invoke();
        yield return null;
        yield return null;
        yield return null;
        onFadeInStart?.Invoke();
        yield return FadeAlpha(1f, 0f, inDuration);
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

    private void Awake()
    {
        EnsureFadeOverlays();
    }

    private void LateUpdate()
    {
        EnsureFadeOverlays();
        UpdateFadeOverlays();
    }

    private void EnsureFadeOverlays()
    {
        int displayCount = Mathf.Max(3, Display.displays.Length);
        if (fadeImages != null && fadeImages.Length >= displayCount)
        {
            return;
        }

        DestroyFadeOverlays();
        fadeImages = new Image[displayCount];

        for (int i = 0; i < displayCount; i++)
        {
            GameObject canvasObj = new GameObject("FadeOverlay_Display" + i);
            canvasObj.transform.SetParent(transform, false);

            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.targetDisplay = i;
            canvas.sortingOrder = 32767;

            canvasObj.AddComponent<CanvasScaler>();

            GameObject imageObj = new GameObject("FadeImage");
            imageObj.transform.SetParent(canvasObj.transform, false);

            Image image = imageObj.AddComponent<Image>();
            image.color = Color.clear;
            image.raycastTarget = false;

            RectTransform rt = image.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;

            fadeImages[i] = image;
            // Canvas は常にアクティブのまま保持。表示切り替えは image.color.a のみで制御する。
        }
    }

    private void DestroyFadeOverlays()
    {
        if (fadeImages == null)
        {
            return;
        }

        for (int i = 0; i < fadeImages.Length; i++)
        {
            if (fadeImages[i] == null)
            {
                continue;
            }

            // parent = Canvas GameObject
            Transform canvasTransform = fadeImages[i].transform.parent;
            if (canvasTransform != null)
            {
                Destroy(canvasTransform.gameObject);
            }
        }

        fadeImages = null;
    }

    private void UpdateFadeOverlays()
    {
        if (fadeImages == null)
        {
            return;
        }

        // alpha=0 のときは完全透明にし、Canvas の SetActive は操作しない。
        // これにより全ディスプレイで ScreenSpaceOverlay が常時有効になり、
        // フェード開始直後から正しく表示される。
        Color overlayColor = alpha > 0.001f
            ? new Color(activeFadeColor.r, activeFadeColor.g, activeFadeColor.b, alpha)
            : Color.clear;

        for (int i = 0; i < fadeImages.Length; i++)
        {
            if (fadeImages[i] == null)
            {
                continue;
            }

            fadeImages[i].color = overlayColor;
        }
    }
}