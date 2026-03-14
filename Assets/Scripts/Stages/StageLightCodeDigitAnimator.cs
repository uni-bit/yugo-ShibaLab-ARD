using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[AddComponentMenu("Stages/Stage Code Digit Animator")]
public class StageLightCodeDigitAnimator : MonoBehaviour
{
    [SerializeField] private TextMesh primaryText;
    [Header("Timing")]
    [SerializeField] private float animationDuration = 0.34f;
    [SerializeField] private AnimationCurve travelCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Layout")]
    [SerializeField] private float digitSpacing = 1.3f;
    [SerializeField] private float radiusMultiplier = 1.2f;

    private readonly TextMesh[] wheelTexts = new TextMesh[10];

    private int displayedDigit;
    private float currentScroll; 
    private float targetScroll;
    private float startScroll;
    private float animationTime;
    private bool isAnimating;
    private Color baseTextColor = Color.white;

    private void Reset()
    {
        EnsureTexts();
        SetDigitImmediate(0);
    }

    private void Awake()
    {
        EnsureTexts();
        SetDigitImmediate(displayedDigit);
    }

    private void OnEnable()
    {
        EnsureTexts();
        SetDigitImmediate(displayedDigit);
    }

    private void OnValidate()
    {
        EnsureTexts();
        if (!isAnimating)
        {
            SetDigitImmediate(displayedDigit);
        }
    }

    private void Update()
    {
        if (!isAnimating)
        {
            return;
        }

        animationTime += Time.deltaTime;
        float normalized = Mathf.Clamp01(animationDuration <= 0.0001f ? 1f : animationTime / animationDuration);
        float eased = EvaluateTravel(normalized);

        currentScroll = Mathf.Lerp(startScroll, targetScroll, eased);
        ApplyDialState(currentScroll);

        if (normalized >= 1f)
        {
            isAnimating = false;
            displayedDigit = ((Mathf.RoundToInt(targetScroll) % 10) + 10) % 10;
            currentScroll = displayedDigit; 
            targetScroll = currentScroll;
            ApplyDialState(currentScroll);
        }
    }

    public void SetDigitImmediate(int digit)
    {
        displayedDigit = ((digit % 10) + 10) % 10;
        currentScroll = displayedDigit;
        targetScroll = currentScroll;
        isAnimating = false;
        animationTime = 0f;
        EnsureTexts();
        CacheBaseColor();
        ApplyDialState(currentScroll);

        if (primaryText != null)
        {
            MeshRenderer r = primaryText.GetComponent<MeshRenderer>();
            if (r != null) r.enabled = false;
        }
    }

    public void AnimateToDigit(int digit, int direction)
    {
        EnsureTexts();
        CacheBaseColor();

        int normalizedDigit = ((digit % 10) + 10) % 10;
        if (normalizedDigit == displayedDigit && !isAnimating)
        {
            SetDigitImmediate(normalizedDigit);
            return;
        }

        startScroll = currentScroll;
        float currentMod = currentScroll % 10f;
        if (currentMod < 0)
        {
            currentMod += 10f;
        }

        float diff = normalizedDigit - currentMod;
        
        if (direction > 0 && diff <= 0f)
        {
            diff += 10f;
        }
        if (direction < 0 && diff >= 0f)
        {
            diff -= 10f;
        }

        targetScroll = startScroll + diff;
        animationDirection = direction >= 0 ? 1 : -1;
        animationTime = 0f;
        isAnimating = true;

        if (primaryText != null)
        {
            MeshRenderer r = primaryText.GetComponent<MeshRenderer>();
            if (r != null) r.enabled = false;
        }
    }

    private void ApplyDialState(float scrollValue)
    {
        float circumference = Mathf.Max(0.0001f, digitSpacing) * 10f;
        float radius = (circumference / (2f * Mathf.PI)) * radiusMultiplier;

        float wheelAngle = scrollValue * -36f;

        for (int i = 0; i < 10; i++)
        {
            TextMesh tm = wheelTexts[i];
            if (tm == null)
            {
                continue;
            }

            float digitAngle = wheelAngle + (i * 36f);
            
            // Normalize angle to -180 ~ 180 to ensure precise alpha logic
            while (digitAngle > 180f) digitAngle -= 360f;
            while (digitAngle < -180f) digitAngle += 360f;

            float rad = digitAngle * Mathf.Deg2Rad;

            // 完全に平面（Z=0）でY軸のみ移動させる
            float yOffset = Mathf.Sin(rad) * radius * 1.5f; // 離すための調整
            
            // localPositionにYオフセットを適用
            Vector3 newPos = Vector3.zero;
            newPos.y = yOffset;
            tm.transform.localPosition = newPos;

            // 回転やスケールは一切いじらない
            tm.transform.localRotation = Quaternion.identity;
            
            Vector3 baseScale = primaryText != null ? primaryText.transform.localScale : Vector3.one;
            tm.transform.localScale = baseScale * 0.5f;

            // 角度（離れ具合）に応じてアルファを調整（正面に近いほど濃い）
            float cosStr = Mathf.Cos(rad);
            float alpha = Mathf.InverseLerp(0.8f, 1.0f, cosStr); 

            Color c = baseTextColor;
            c.a = Mathf.Max(baseTextColor.a, 1f) * alpha; // 透明度で表示/非表示をコントロール
            tm.color = c;
            
            tm.gameObject.SetActive(alpha > 0.01f);
        }
        
        if (primaryText != null)
        {
            MeshRenderer r = primaryText.GetComponent<MeshRenderer>();
            if (r != null) r.enabled = false;
        }
    }

    private int animationDirection = 1;

    public void RefreshVisualStyle()
    {
        EnsureTexts();
        CopyPrimaryStyle();
        ApplyDialState(currentScroll);
    }

    private void EnsureTexts()
    {
        if (primaryText == null)
        {
            primaryText = GetComponent<TextMesh>();
        }
        
        // Force the primary text to be empty so it never renders under any circumstance
        if (primaryText != null)
        {
            primaryText.text = "";
            MeshRenderer r = primaryText.GetComponent<MeshRenderer>();
            if (r != null) r.enabled = false;
        }

        for (int i = 0; i < 10; i++)
        {
            string digitName = "WheelDigit_" + i;
            Transform child = transform.Find(digitName);
            if (child == null)
            {
                child = new GameObject(digitName).transform;
                child.SetParent(transform, false);
            }

            TextMesh tm = child.GetComponent<TextMesh>();
            if (tm == null)
            {
                tm = child.gameObject.AddComponent<TextMesh>();
            }

            tm.text = i.ToString();
            wheelTexts[i] = tm;
        }

        CopyPrimaryStyle();
    }

    private void CopyPrimaryStyle()
    {
        if (primaryText == null)
        {
            return;
        }

        MeshRenderer primaryRenderer = primaryText.GetComponent<MeshRenderer>();
        if (primaryRenderer != null)
        {
            StageSpotlightMaterialUtility.ClearPropertyBlock(primaryRenderer);
        }

        for (int i = 0; i < 10; i++)
        {
            TextMesh tm = wheelTexts[i];
            if (tm == null || tm == primaryText)
            {
                continue;
            }

            tm.font = primaryText.font;
            tm.fontSize = primaryText.fontSize;
            tm.characterSize = primaryText.characterSize;
            tm.anchor = primaryText.anchor;
            tm.alignment = primaryText.alignment;
            tm.richText = primaryText.richText;
            
            // 修正: 優先テキストの1/2のスケールを適用
            tm.transform.localScale = primaryText.transform.localScale * 0.5f;

            MeshRenderer secR = tm.GetComponent<MeshRenderer>();
            if (secR != null)
            {
                secR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                secR.receiveShadows = false;
                StageSpotlightMaterialUtility.ClearPropertyBlock(secR);
                if (primaryRenderer != null && primaryRenderer.sharedMaterial != null)
                {
                    secR.sharedMaterial = primaryRenderer.sharedMaterial;
                }
            }
        }

        CacheBaseColor();
    }

    private void CacheBaseColor()
    {
        if (primaryText != null)
        {
            // Always trust the primaryText color, ensuring HDR / glow is respected correctly during resets and successes.
            baseTextColor = primaryText.color;
        }
    }

    private float EvaluateTravel(float normalized)
    {
        float clamped = Mathf.Clamp01(normalized);
        if (travelCurve == null || travelCurve.length == 0)
        {
            return Mathf.SmoothStep(0f, 1f, clamped);
        }

        return Mathf.Clamp01(travelCurve.Evaluate(clamped));
    }
}