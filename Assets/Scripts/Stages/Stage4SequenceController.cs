using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Stage 4 の導入演出とエンディング表示を制御する。
/// 暗転フェード後に自動でメッセージを表示し、Stage2 へ復帰する。
/// </summary>
[AddComponentMenu("Stages/Stage 4 Sequence Controller")]
public class Stage4SequenceController : MonoBehaviour, IStageActivationHandler
{
    [Header("Return")]
    [SerializeField] private float holdBeforeReturnSeconds = 5f;
    [SerializeField] private float returnFadeOutDuration = 2.5f;
    [SerializeField] private float returnFadeInDuration = 2.5f;
    [SerializeField] private Color returnFadeColor = new Color(0.02f, 0.02f, 0.03f, 1f);

    [Header("Reveal")]
    [SerializeField] private Color stage4StartAmbientColor = new Color(0.02f, 0.02f, 0.03f, 1f);
    [SerializeField] private Color stage4AmbientColor = new Color(0.12f, 0.12f, 0.15f, 1f);
    [SerializeField] private float revealDelaySeconds = 0.25f;
    [SerializeField] private AnimationCurve revealCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private float revealBlackFadeDuration = 2.2f;
    [SerializeField] private float autoCompletionDelaySeconds = 0.4f;

    [Header("Completion Message")]
    [SerializeField] private string completionMessage = "Congratulations. Welcome to the moon!";
    [SerializeField] private float completionFadeOutDuration = 1.4f;
    [SerializeField] private float messageDelaySeconds = 2f;
    [SerializeField] private float messageFadeDuration = 1.8f;
    [SerializeField] private Color completionBackgroundColor = Color.black;
    [SerializeField] private Color completionTextColor = new Color(0.96f, 0.96f, 0.98f, 1f);
    [SerializeField] private int completionFontSize = 64;
    [SerializeField] private float completionMessageWidth = 1800f;
    [SerializeField] private float completionMessageHeight = 180f;
    [SerializeField] private int leftProjectionDisplay = 1;
    [SerializeField] private int rightProjectionDisplay = 2;

    private const int ReturnStageIndex = 1;
    private const int Stage4StageIndex = 3;

    private sealed class OverlayDisplay
    {
        public int DisplayIndex;
        public Canvas Canvas;
        public Image Background;
        public Text Message;
    } 

    private StageSequenceController cachedController;
    private Stage3RockHintPuzzle stagePuzzle;
    private OverlayDisplay[] overlayDisplays;
    private Coroutine revealCoroutine;
    private Coroutine completionCoroutine;
    private int activationVersion;
    private bool completionStarted;
    private float overlayBlackAlpha;
    private float overlayMessageAlpha;
    private Font overlayFont;
    private Color cachedAmbientLight;
    private bool hasCachedAmbientLight;

    public int ActivationCount { get; private set; }
    public int MessageShownCount { get; private set; }
    public int ReturnTriggeredCount { get; private set; }

    private void Awake()
    {
        stagePuzzle = GetComponent<Stage3RockHintPuzzle>();
        DisableStage4PuzzleIfPresent();
        EnsurePresentationOverlays();
        ApplyOverlayState();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            CleanupEditorOverlayArtifacts();
            return;
        }

        cachedController = FindFirstObjectByType<StageSequenceController>();
        stagePuzzle = GetComponent<Stage3RockHintPuzzle>();
        DisableStage4PuzzleIfPresent();
        EnsurePresentationOverlays();
        ResetPresentationState();

        if (IsStage4Active())
        {
            BeginStage4Sequence();
        }
    }

    private void OnDisable()
    {
        StopRunningCoroutines();
        ResetPresentationState();
        RestoreAmbientLight();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        EnsurePresentationOverlays();
        ApplyOverlayState();
    }

    private void Update()
    {
        if (!Application.isPlaying || !IsStage4Active())
        {
            return;
        }
    }

    private void LateUpdate()
    {
        if (OverlayDisplaysNeedRefresh())
        {
            EnsurePresentationOverlays();
        }

        ApplyOverlayState();
    }

    public void OnStageActivated()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        cachedController = FindFirstObjectByType<StageSequenceController>();
        stagePuzzle = GetComponent<Stage3RockHintPuzzle>();
        DisableStage4PuzzleIfPresent();
        BeginStage4Sequence();
    }

    private void BeginStage4Sequence()
    {
        CacheAmbientLightIfNeeded();
        ActivationCount++;
        activationVersion++;
        completionStarted = false;
        StopRunningCoroutines();
        ResetPresentationState();
        ApplyRevealStartState();
        revealCoroutine = StartCoroutine(RevealStageCoroutine(activationVersion));
    }

    private void BeginCompletionSequence()
    {
        activationVersion++;
        completionStarted = true;
        StopRunningCoroutines();
        completionCoroutine = StartCoroutine(CompletionSequenceCoroutine(activationVersion));
    }

    private IEnumerator RevealStageCoroutine(int version)
    {
        if (revealDelaySeconds > 0f)
        {
            yield return new WaitForSeconds(revealDelaySeconds);
        }

        float elapsed = 0f;
        float duration = Mathf.Max(0.0001f, revealBlackFadeDuration);

        while (elapsed < duration)
        {
            if (version != activationVersion)
            {
                yield break;
            }

            elapsed += Time.deltaTime;
            float revealT = EvaluateReveal(elapsed / duration);
            overlayBlackAlpha = Mathf.Lerp(1f, 0f, revealT);
            yield return null;
        }

        overlayBlackAlpha = 0f;
        RenderSettings.ambientLight = stage4StartAmbientColor;

        if (!completionStarted && version == activationVersion)
        {
            if (autoCompletionDelaySeconds > 0f)
            {
                yield return new WaitForSeconds(autoCompletionDelaySeconds);
            }

            if (version == activationVersion)
            {
                BeginCompletionSequence();
            }
        }
    }

    private IEnumerator CompletionSequenceCoroutine(int version)
    {
        yield return FadeCompletionState(version, 0f, 1f, 0f, 0f, completionFadeOutDuration);

        if (version != activationVersion)
        {
            yield break;
        }

        if (messageDelaySeconds > 0f)
        {
            yield return new WaitForSeconds(messageDelaySeconds);
        }

        if (version != activationVersion)
        {
            yield break;
        }

        yield return FadeCompletionState(version, 1f, 1f, 0f, 1f, messageFadeDuration);
    MessageShownCount++;

        if (version != activationVersion)
        {
            yield break;
        }

        if (holdBeforeReturnSeconds > 0f)
        {
            yield return new WaitForSeconds(holdBeforeReturnSeconds);
        }

        if (version != activationVersion)
        {
            yield break;
        }

        StageSequenceController controller = cachedController != null
            ? cachedController
            : FindFirstObjectByType<StageSequenceController>();
        if (controller != null)
        {
            ReturnTriggeredCount++;
            controller.FadeToStageWithOptions(ReturnStageIndex, returnFadeOutDuration, returnFadeInDuration, returnFadeColor, false);
        }

        RestoreAmbientLight();
    }

    private void DisableStage4PuzzleIfPresent()
    {
        if (stagePuzzle == null)
        {
            return;
        }

        if (stagePuzzle.enabled)
        {
            stagePuzzle.enabled = false;
        }
    }

    private IEnumerator FadeCompletionState(int version, float fromBlack, float toBlack, float fromMessage, float toMessage, float duration)
    {
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.0001f, duration);

        while (elapsed < safeDuration)
        {
            if (version != activationVersion)
            {
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);
            float eased = EvaluateReveal(t);

            overlayBlackAlpha = Mathf.Lerp(fromBlack, toBlack, eased);
            overlayMessageAlpha = Mathf.Lerp(fromMessage, toMessage, eased);
            RenderSettings.ambientLight = stage4StartAmbientColor;
            yield return null;
        }

        overlayBlackAlpha = toBlack;
        overlayMessageAlpha = toMessage;
        RenderSettings.ambientLight = stage4StartAmbientColor;
    }

    private void ApplyRevealStartState()
    {
        RenderSettings.ambientLight = stage4StartAmbientColor;
        overlayBlackAlpha = 1f;
        overlayMessageAlpha = 0f;
    }

    private void EnsurePresentationOverlays()
    {
        int displayCount = Mathf.Max(3, Display.displays.Length);
        if (overlayDisplays != null
            && overlayDisplays.Length == displayCount
            && !OverlayDisplaysNeedRefresh())
        {
            return;
        }

        overlayFont = overlayFont != null
            ? overlayFont
            : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

        Dictionary<int, OverlayDisplay> existingDisplays = CollectExistingOverlayDisplays();
        overlayDisplays = new OverlayDisplay[displayCount];

        for (int index = 0; index < displayCount; index++)
        {
            if (!existingDisplays.TryGetValue(index, out OverlayDisplay display) || display == null)
            {
                display = CreateOverlayDisplay(index);
            }

            overlayDisplays[index] = display;

            ConfigureOverlayDisplay(display, index);
        }
    }

    private void CleanupEditorOverlayArtifacts()
    {
        overlayDisplays = null;

        for (int index = transform.childCount - 1; index >= 0; index--)
        {
            Transform child = transform.GetChild(index);
            if (child == null || !child.name.StartsWith("Stage4 Overlay Display"))
            {
                continue;
            }

            DestroyOverlayObject(child.gameObject);
        }
    }

    private OverlayDisplay CreateOverlayDisplay(int displayIndex)
    {
        GameObject canvasObject = new GameObject("Stage4 Overlay Display" + displayIndex);
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.targetDisplay = displayIndex;
        canvas.sortingOrder = 32760;

        canvasObject.AddComponent<CanvasScaler>();

        GameObject backgroundObject = new GameObject("Background");
        backgroundObject.transform.SetParent(canvasObject.transform, false);
        Image background = backgroundObject.AddComponent<Image>();
        background.raycastTarget = false;
        StretchRect(background.rectTransform);

        GameObject messageObject = new GameObject("Completion Message");
        messageObject.transform.SetParent(canvasObject.transform, false);
        Text message = messageObject.AddComponent<Text>();
        message.raycastTarget = false;
        message.alignment = TextAnchor.MiddleCenter;
        message.font = overlayFont;
        message.fontSize = completionFontSize;
        message.text = completionMessage;
        StretchRect(message.rectTransform);

        return new OverlayDisplay
        {
            DisplayIndex = displayIndex,
            Canvas = canvas,
            Background = background,
            Message = message
        };
    }

    private Dictionary<int, OverlayDisplay> CollectExistingOverlayDisplays()
    {
        Dictionary<int, OverlayDisplay> displays = new Dictionary<int, OverlayDisplay>();

        for (int index = transform.childCount - 1; index >= 0; index--)
        {
            Transform child = transform.GetChild(index);
            if (child == null || !child.name.StartsWith("Stage4 Overlay Display"))
            {
                continue;
            }

            Canvas canvas = child.GetComponent<Canvas>();
            if (!TryBuildOverlayDisplay(canvas, out OverlayDisplay display))
            {
                DestroyOverlayObject(child.gameObject);
                continue;
            }

            int displayIndex = Mathf.Max(0, canvas.targetDisplay);
            if (displays.ContainsKey(displayIndex))
            {
                DestroyOverlayObject(child.gameObject);
                continue;
            }

            displays.Add(displayIndex, display);
        }

        return displays;
    }

    private bool TryBuildOverlayDisplay(Canvas canvas, out OverlayDisplay display)
    {
        display = null;
        if (canvas == null)
        {
            return false;
        }

        Image background = null;
        Text message = null;

        for (int index = 0; index < canvas.transform.childCount; index++)
        {
            Transform child = canvas.transform.GetChild(index);
            if (child == null)
            {
                continue;
            }

            if (background == null)
            {
                background = child.GetComponent<Image>();
            }

            if (message == null)
            {
                message = child.GetComponent<Text>();
            }
        }

        if (background == null || message == null)
        {
            return false;
        }

        display = new OverlayDisplay
        {
            DisplayIndex = Mathf.Max(0, canvas.targetDisplay),
            Canvas = canvas,
            Background = background,
            Message = message
        };
        return true;
    }

    private void ConfigureOverlayDisplay(OverlayDisplay display, int displayIndex)
    {
        if (display == null || display.Canvas == null || display.Background == null || display.Message == null)
        {
            return;
        }

        display.DisplayIndex = displayIndex;
        display.Canvas.name = "Stage4 Overlay Display" + displayIndex;
        display.Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        display.Canvas.targetDisplay = displayIndex;
        display.Canvas.sortingOrder = 32760;

        if (display.Canvas.GetComponent<CanvasScaler>() == null)
        {
            display.Canvas.gameObject.AddComponent<CanvasScaler>();
        }

        display.Background.name = "Background";
        display.Background.raycastTarget = false;
        StretchRect(display.Background.rectTransform);

        display.Message.name = "Completion Message";
        display.Message.raycastTarget = false;
        display.Message.alignment = TextAnchor.MiddleCenter;
        display.Message.font = overlayFont;
        display.Message.fontSize = completionFontSize;
        display.Message.text = completionMessage;
        StretchRect(display.Message.rectTransform);
        ConfigureMessageRect(display.Message.rectTransform, displayIndex);
    }

    private bool OverlayDisplaysNeedRefresh()
    {
        if (overlayDisplays == null || overlayDisplays.Length == 0)
        {
            return true;
        }

        for (int index = 0; index < overlayDisplays.Length; index++)
        {
            OverlayDisplay display = overlayDisplays[index];
            if (display == null
                || display.Canvas == null
                || display.Background == null
                || display.Message == null
                || display.Canvas.targetDisplay != index)
            {
                return true;
            }
        }

        return false;
    }

    private void ConfigureMessageRect(RectTransform rectTransform, int displayIndex)
    {
        rectTransform.sizeDelta = new Vector2(completionMessageWidth, completionMessageHeight);

        if (displayIndex == leftProjectionDisplay)
        {
            rectTransform.anchorMin = new Vector2(1f, 0.5f);
            rectTransform.anchorMax = new Vector2(1f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            return;
        }

        if (displayIndex == rightProjectionDisplay)
        {
            rectTransform.anchorMin = new Vector2(0f, 0.5f);
            rectTransform.anchorMax = new Vector2(0f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            return;
        }

        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
    }

    private void DestroyPresentationOverlays()
    {
        if (overlayDisplays == null)
        {
            return;
        }

        for (int index = 0; index < overlayDisplays.Length; index++)
        {
            OverlayDisplay display = overlayDisplays[index];
            if (display == null || display.Canvas == null)
            {
                continue;
            }

            DestroyOverlayObject(display.Canvas.gameObject);
        }

        overlayDisplays = null;
    }

    private void DestroyOverlayObject(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
            return;
        }

        DestroyImmediate(target);
    }

    private void ResetPresentationState()
    {
        overlayBlackAlpha = 0f;
        overlayMessageAlpha = 0f;
        completionStarted = false;
        ApplyOverlayState();
    }

    private void ApplyOverlayState()
    {
        if (overlayDisplays == null)
        {
            return;
        }

        Color backgroundColor = new Color(completionBackgroundColor.r, completionBackgroundColor.g, completionBackgroundColor.b, overlayBlackAlpha);

        for (int index = 0; index < overlayDisplays.Length; index++)
        {
            OverlayDisplay display = overlayDisplays[index];
            if (display == null)
            {
                continue;
            }

            if (display.Background != null)
            {
                display.Background.color = backgroundColor;
            }

            if (display.Message != null)
            {
                float messageAlpha = (display.DisplayIndex == leftProjectionDisplay || display.DisplayIndex == rightProjectionDisplay)
                    ? overlayMessageAlpha
                    : 0f;
                Color messageColor = new Color(completionTextColor.r, completionTextColor.g, completionTextColor.b, messageAlpha);
                display.Message.text = completionMessage;
                display.Message.color = messageColor;
                display.Message.fontSize = completionFontSize;
                display.Message.alignment = TextAnchor.MiddleCenter;
            }
        }
    }

    private static void StretchRect(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.sizeDelta = Vector2.zero;
        rectTransform.anchoredPosition = Vector2.zero;
    }

    private float EvaluateReveal(float normalized)
    {
        float clamped = Mathf.Clamp01(normalized);
        if (revealCurve == null || revealCurve.length == 0)
        {
            return Mathf.SmoothStep(0f, 1f, clamped);
        }

        return Mathf.Clamp01(revealCurve.Evaluate(clamped));
    }

    private bool IsStage4Active()
    {
        return cachedController == null || cachedController.CurrentStageIndex == Stage4StageIndex;
    }

    private void StopRunningCoroutines()
    {
        if (revealCoroutine != null)
        {
            StopCoroutine(revealCoroutine);
            revealCoroutine = null;
        }

        if (completionCoroutine != null)
        {
            StopCoroutine(completionCoroutine);
            completionCoroutine = null;
        }
    }

    private void CacheAmbientLightIfNeeded()
    {
        if (hasCachedAmbientLight)
        {
            return;
        }

        cachedAmbientLight = RenderSettings.ambientLight;
        hasCachedAmbientLight = true;
    }

    private void RestoreAmbientLight()
    {
        if (!hasCachedAmbientLight)
        {
            return;
        }

        RenderSettings.ambientLight = cachedAmbientLight;
        hasCachedAmbientLight = false;
    }
}
