using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("Stages/Stage 2 Completion Sequence")]
public class Stage2CompletionSequence : MonoBehaviour
{
    private enum SequencePhase
    {
        Waiting,
        Collapsing,
        Brightening,
        Complete
    }

    private sealed class CollapsePiece
    {
        public Transform Transform;
        public Vector3 Velocity;
        public Vector3 AngularVelocity;
        public Vector3 BaseScale;
    }

    private sealed class RendererState
    {
        public Renderer Renderer;
        public MaterialPropertyBlock PropertyBlock;
        public Color BaseColor = Color.white;
        public bool HasBaseColor;
        public Color AlbedoColor = Color.white;
        public bool HasAlbedoColor;
        public Color LitColor = Color.white;
        public bool HasLitColor;
        public Color HiddenColor = new Color(1f, 1f, 1f, 0f);
        public bool HasHiddenColor;
        public bool HasEmissionColor;
    }

    [SerializeField] private Transform collapseRoot;
    [SerializeField] private Transform panelTransform;
    [SerializeField] private Transform stageRoot;
    [SerializeField] private float collapseDuration = 1.15f;
    [SerializeField] private Vector2 collapseHorizontalSpeedRange = new Vector2(0.9f, 2.6f);
    [SerializeField] private Vector2 collapseVerticalSpeedRange = new Vector2(0.8f, 2.1f);
    [SerializeField] private float collapseGravity = 4.8f;
    [SerializeField] private float collapseRotationSpeed = 240f;
    [SerializeField] private int collapseColumns = 4;
    [SerializeField] private int collapseRows = 3;
    [SerializeField] private float brightenDuration = 3.2f;
    [SerializeField] private float moveDistance = 6f;
    [SerializeField] private float moveHeight = 0f;
    [SerializeField] private Color finalBrightnessColor = new Color(1f, 0.96f, 0.84f, 1f);
    [SerializeField] private float emissionBoost = 2.4f;
    [SerializeField] private float finalLightIntensity = 6f;
    [SerializeField] private float finalLightRange = 34f;
    [SerializeField] private float hiddenBrightenAlpha = 0.92f;

    private readonly List<CollapsePiece> collapsePieces = new List<CollapsePiece>();
    private readonly List<RendererState> rendererStates = new List<RendererState>();

    private SequencePhase currentPhase = SequencePhase.Waiting;
    private float phaseElapsed;
    private Vector3 brightenStartPosition;
    private Vector3 brightenTargetPosition;
    private Vector3 travelDirection = Vector3.forward;
    private Light finalGlowLight;
    private Renderer panelRenderer;
    private Collider panelCollider;

    public bool IsPlaying => currentPhase == SequencePhase.Collapsing || currentPhase == SequencePhase.Brightening;
    public bool IsComplete => currentPhase == SequencePhase.Complete;

    private void Awake()
    {
        if (stageRoot == null)
        {
            stageRoot = transform;
        }
    }

    private void OnEnable()
    {
        ResetSequenceState();
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        switch (currentPhase)
        {
            case SequencePhase.Collapsing:
                UpdateCollapse();
                break;

            case SequencePhase.Brightening:
                UpdateBrightening();
                break;
        }
    }

    public void Configure(
        Transform collapseRootReference,
        Transform panelReference,
        Transform stageRootReference)
    {
        collapseRoot = collapseRootReference;
        panelTransform = panelReference;
        stageRoot = stageRootReference != null ? stageRootReference : transform;
        panelRenderer = panelTransform != null ? panelTransform.GetComponent<Renderer>() : null;
        panelCollider = panelTransform != null ? panelTransform.GetComponent<Collider>() : null;
        ResetSequenceState();
    }

    public void Play()
    {
        if (currentPhase != SequencePhase.Waiting)
        {
            return;
        }

        BeginCollapse();
    }

    private void ResetSequenceState()
    {
        currentPhase = SequencePhase.Waiting;
        phaseElapsed = 0f;
        collapsePieces.Clear();
        rendererStates.Clear();

        if (panelRenderer != null)
        {
            panelRenderer.enabled = true;
        }

        if (panelCollider != null)
        {
            panelCollider.enabled = true;
        }

        if (finalGlowLight != null)
        {
            finalGlowLight.intensity = 0f;
            finalGlowLight.enabled = false;
        }
    }

    private void BeginCollapse()
    {
        currentPhase = SequencePhase.Collapsing;
        phaseElapsed = 0f;
        travelDirection = ResolveTravelDirection();
        SpawnCollapsePieces();
    }

    private void UpdateCollapse()
    {
        phaseElapsed += Time.deltaTime;

        for (int index = collapsePieces.Count - 1; index >= 0; index--)
        {
            CollapsePiece piece = collapsePieces[index];
            if (piece == null || piece.Transform == null)
            {
                collapsePieces.RemoveAt(index);
                continue;
            }

            piece.Velocity += Vector3.down * (collapseGravity * Time.deltaTime);
            piece.Transform.position += piece.Velocity * Time.deltaTime;
            piece.Transform.Rotate(piece.AngularVelocity * Time.deltaTime, Space.Self);

            float collapseProgress = Mathf.Clamp01(phaseElapsed / Mathf.Max(0.0001f, collapseDuration));
            float scaleMultiplier = Mathf.Lerp(1f, 0.35f, collapseProgress);
            piece.Transform.localScale = piece.BaseScale * scaleMultiplier;
        }

        if (phaseElapsed < collapseDuration)
        {
            return;
        }

        for (int index = 0; index < collapsePieces.Count; index++)
        {
            if (collapsePieces[index] != null && collapsePieces[index].Transform != null)
            {
                Destroy(collapsePieces[index].Transform.gameObject);
            }
        }

        collapsePieces.Clear();

        if (collapseRoot != null)
        {
            Destroy(collapseRoot.gameObject);
        }

        BeginBrightening();
    }

    private void BeginBrightening()
    {
        currentPhase = SequencePhase.Brightening;
        phaseElapsed = 0f;
        CaptureRendererStates();

        if (stageRoot == null)
        {
            stageRoot = transform;
        }

        brightenStartPosition = stageRoot != null ? stageRoot.position : transform.position;
        brightenTargetPosition = brightenStartPosition + (travelDirection * moveDistance) + (Vector3.up * moveHeight);

        EnsureFinalGlowLight();
        if (finalGlowLight != null)
        {
            finalGlowLight.enabled = true;
            finalGlowLight.intensity = 0f;
            finalGlowLight.range = finalLightRange;
        }
    }

    private void UpdateBrightening()
    {
        phaseElapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(phaseElapsed / Mathf.Max(0.0001f, brightenDuration));
        float eased = Mathf.SmoothStep(0f, 1f, progress);

        if (stageRoot != null)
        {
            stageRoot.position = Vector3.Lerp(brightenStartPosition, brightenTargetPosition, eased);
        }

        ApplyBrightening(eased);

        if (finalGlowLight != null)
        {
            finalGlowLight.intensity = Mathf.Lerp(0f, finalLightIntensity, eased);
        }

        if (progress < 1f)
        {
            return;
        }

        currentPhase = SequencePhase.Complete;
    }

    private Vector3 ResolveTravelDirection()
    {
        if (panelTransform != null)
        {
            Vector3 panelForward = panelTransform.forward;
            if (panelForward.sqrMagnitude > 0.0001f)
            {
                return panelForward.normalized;
            }
        }

        if (stageRoot != null)
        {
            Vector3 rootForward = stageRoot.forward;
            if (rootForward.sqrMagnitude > 0.0001f)
            {
                return rootForward.normalized;
            }
        }

        return Vector3.forward;
    }

    private void SpawnCollapsePieces()
    {
        collapsePieces.Clear();

        if (collapseRoot == null)
        {
            return;
        }

        Renderer[] renderers = collapseRoot.GetComponentsInChildren<Renderer>(true);
        for (int index = 0; index < renderers.Length; index++)
        {
            Renderer sourceRenderer = renderers[index];
            if (sourceRenderer == null || sourceRenderer.transform == collapseRoot)
            {
                continue;
            }

            CreateCollapseProxy(sourceRenderer);

            sourceRenderer.enabled = false;

            Collider sourceCollider = sourceRenderer.GetComponent<Collider>();
            if (sourceCollider != null)
            {
                sourceCollider.enabled = false;
            }
        }

        if (panelRenderer != null)
        {
            panelRenderer.enabled = false;
        }

        if (panelCollider != null)
        {
            panelCollider.enabled = false;
        }

        SpawnPanelShards();
    }

    private void SpawnPanelShards()
    {
        if (panelTransform == null || panelRenderer == null)
        {
            return;
        }

        Vector3 panelScale = panelTransform.lossyScale;
        float pieceWidth = panelScale.x / Mathf.Max(1, collapseColumns);
        float pieceHeight = panelScale.y / Mathf.Max(1, collapseRows);
        float pieceDepth = Mathf.Max(0.05f, panelScale.z * 0.9f);

        Transform shardParent = stageRoot != null ? stageRoot : transform;
        Material shardMaterial = panelRenderer.sharedMaterial;
        Color shardColor = panelRenderer.sharedMaterial != null && panelRenderer.sharedMaterial.HasProperty("_BaseColor")
            ? panelRenderer.sharedMaterial.GetColor("_BaseColor")
            : panelRenderer.sharedMaterial != null && panelRenderer.sharedMaterial.HasProperty("_Color")
                ? panelRenderer.sharedMaterial.color
                : new Color(0.18f, 0.16f, 0.12f, 1f);

        for (int row = 0; row < Mathf.Max(1, collapseRows); row++)
        {
            for (int column = 0; column < Mathf.Max(1, collapseColumns); column++)
            {
                GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shard.name = "Panel Shard " + row + "-" + column;
                shard.transform.SetParent(shardParent, true);
                shard.transform.rotation = panelTransform.rotation;

                float horizontalOffset = ((column + 0.5f) / Mathf.Max(1, collapseColumns)) - 0.5f;
                float verticalOffset = ((row + 0.5f) / Mathf.Max(1, collapseRows)) - 0.5f;

                Vector3 localOffset = new Vector3(horizontalOffset * panelScale.x, verticalOffset * panelScale.y, 0f);
                shard.transform.position = panelTransform.TransformPoint(localOffset);
                shard.transform.localScale = new Vector3(pieceWidth * 0.94f, pieceHeight * 0.94f, pieceDepth);

                Renderer shardRenderer = shard.GetComponent<Renderer>();
                if (shardRenderer != null)
                {
                    if (shardMaterial != null)
                    {
                        shardRenderer.sharedMaterial = shardMaterial;
                    }

                    MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
                    shardRenderer.GetPropertyBlock(propertyBlock);
                    propertyBlock.SetColor("_Color", shardColor);
                    propertyBlock.SetColor("_BaseColor", shardColor);
                    shardRenderer.SetPropertyBlock(propertyBlock);
                }

                Collider shardCollider = shard.GetComponent<Collider>();
                if (shardCollider != null)
                {
                    Destroy(shardCollider);
                }

                Vector3 direction = (panelTransform.right * horizontalOffset * 1.35f)
                    + (panelTransform.up * (0.2f + (verticalOffset * 0.45f)))
                    + (panelTransform.forward * 0.7f);
                if (direction.sqrMagnitude <= 0.0001f)
                {
                    direction = panelTransform.forward;
                }

                direction.Normalize();

                CollapsePiece piece = new CollapsePiece
                {
                    Transform = shard.transform,
                    Velocity = direction * Random.Range(collapseHorizontalSpeedRange.x, collapseHorizontalSpeedRange.y)
                        + (panelTransform.up * Random.Range(collapseVerticalSpeedRange.x, collapseVerticalSpeedRange.y)),
                    AngularVelocity = new Vector3(
                        Random.Range(-collapseRotationSpeed, collapseRotationSpeed),
                        Random.Range(-collapseRotationSpeed, collapseRotationSpeed),
                        Random.Range(-collapseRotationSpeed, collapseRotationSpeed)),
                    BaseScale = shard.transform.localScale
                };

                collapsePieces.Add(piece);
            }
        }
    }

    private void CreateCollapseProxy(Renderer sourceRenderer)
    {
        if (sourceRenderer == null)
        {
            return;
        }

        Bounds bounds = sourceRenderer.bounds;
        if (bounds.size.sqrMagnitude <= 0.000001f)
        {
            return;
        }

        GameObject proxy = GameObject.CreatePrimitive(PrimitiveType.Cube);
        proxy.name = sourceRenderer.gameObject.name + " Collapse Proxy";

        Transform proxyTransform = proxy.transform;
        proxyTransform.SetParent(stageRoot != null ? stageRoot : transform, true);
        proxyTransform.position = bounds.center;
        proxyTransform.rotation = sourceRenderer.transform.rotation;
        proxyTransform.localScale = new Vector3(
            Mathf.Max(0.08f, bounds.size.x),
            Mathf.Max(0.08f, bounds.size.y),
            Mathf.Max(0.08f, bounds.size.z));

        Renderer proxyRenderer = proxy.GetComponent<Renderer>();
        if (proxyRenderer != null)
        {
            Material sourceMaterial = sourceRenderer.sharedMaterial;
            if (sourceMaterial != null)
            {
                proxyRenderer.sharedMaterial = sourceMaterial;
            }

            Color proxyColor = ResolveRendererColor(sourceRenderer);
            MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
            proxyRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_Color", proxyColor);
            propertyBlock.SetColor("_BaseColor", proxyColor);
            propertyBlock.SetColor("_EmissionColor", proxyColor * 0.2f);
            proxyRenderer.SetPropertyBlock(propertyBlock);
        }

        Collider proxyCollider = proxy.GetComponent<Collider>();
        if (proxyCollider != null)
        {
            Destroy(proxyCollider);
        }

        Vector3 centerOffset = bounds.center - collapseRoot.position;
        Vector3 direction = collapseRoot.forward * 0.85f;
        direction += collapseRoot.right * Mathf.Sign(centerOffset.x) * Mathf.Lerp(0.25f, 1.2f, Mathf.Clamp01(Mathf.Abs(centerOffset.x) / 3f));
        direction += collapseRoot.up * Mathf.Lerp(0.15f, 0.75f, Mathf.Clamp01((centerOffset.y + 2f) / 4f));

        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = collapseRoot.forward;
        }

        direction.Normalize();

        CollapsePiece piece = new CollapsePiece
        {
            Transform = proxyTransform,
            Velocity = direction * Random.Range(collapseHorizontalSpeedRange.x * 0.7f, collapseHorizontalSpeedRange.y * 1.15f)
                + (collapseRoot.up * Random.Range(collapseVerticalSpeedRange.x * 0.8f, collapseVerticalSpeedRange.y * 1.2f)),
            AngularVelocity = new Vector3(
                Random.Range(-collapseRotationSpeed, collapseRotationSpeed),
                Random.Range(-collapseRotationSpeed, collapseRotationSpeed),
                Random.Range(-collapseRotationSpeed, collapseRotationSpeed)),
            BaseScale = proxyTransform.localScale
        };

        collapsePieces.Add(piece);
    }

    private static Color ResolveRendererColor(Renderer sourceRenderer)
    {
        if (sourceRenderer == null || sourceRenderer.sharedMaterial == null)
        {
            return new Color(0.85f, 0.85f, 0.85f, 1f);
        }

        Material sourceMaterial = sourceRenderer.sharedMaterial;
        if (sourceMaterial.HasProperty("_LitColor"))
        {
            return sourceMaterial.GetColor("_LitColor");
        }

        if (sourceMaterial.HasProperty("_BaseColor"))
        {
            return sourceMaterial.GetColor("_BaseColor");
        }

        if (sourceMaterial.HasProperty("_Color"))
        {
            return sourceMaterial.color;
        }

        return new Color(0.85f, 0.85f, 0.85f, 1f);
    }

    private void CaptureRendererStates()
    {
        rendererStates.Clear();

        Transform searchRoot = stageRoot != null ? stageRoot : transform;
        Renderer[] renderers = searchRoot.GetComponentsInChildren<Renderer>(true);
        for (int index = 0; index < renderers.Length; index++)
        {
            Renderer renderer = renderers[index];
            if (renderer == null)
            {
                continue;
            }

            Material sharedMaterial = renderer.sharedMaterial;
            RendererState state = new RendererState
            {
                Renderer = renderer,
                PropertyBlock = new MaterialPropertyBlock()
            };

            if (sharedMaterial != null)
            {
                if (sharedMaterial.HasProperty("_BaseColor"))
                {
                    state.BaseColor = sharedMaterial.GetColor("_BaseColor");
                    state.HasBaseColor = true;
                }

                if (sharedMaterial.HasProperty("_Color"))
                {
                    state.AlbedoColor = sharedMaterial.color;
                    state.HasAlbedoColor = true;
                }

                if (sharedMaterial.HasProperty("_LitColor"))
                {
                    state.LitColor = sharedMaterial.GetColor("_LitColor");
                    state.HasLitColor = true;
                }

                if (sharedMaterial.HasProperty("_HiddenColor"))
                {
                    state.HiddenColor = sharedMaterial.GetColor("_HiddenColor");
                    state.HasHiddenColor = true;
                }

                state.HasEmissionColor = sharedMaterial.HasProperty("_EmissionColor");
            }

            rendererStates.Add(state);
        }
    }

    private void ApplyBrightening(float progress)
    {
        Color revealedHiddenColor = new Color(finalBrightnessColor.r, finalBrightnessColor.g, finalBrightnessColor.b, hiddenBrightenAlpha);

        for (int index = 0; index < rendererStates.Count; index++)
        {
            RendererState state = rendererStates[index];
            if (state == null || state.Renderer == null)
            {
                continue;
            }

            state.Renderer.GetPropertyBlock(state.PropertyBlock);

            if (state.HasBaseColor)
            {
                state.PropertyBlock.SetColor("_BaseColor", Color.Lerp(state.BaseColor, finalBrightnessColor, progress));
            }

            if (state.HasAlbedoColor)
            {
                state.PropertyBlock.SetColor("_Color", Color.Lerp(state.AlbedoColor, finalBrightnessColor, progress));
            }

            if (state.HasLitColor)
            {
                state.PropertyBlock.SetColor("_LitColor", Color.Lerp(state.LitColor, finalBrightnessColor, progress));
            }

            if (state.HasHiddenColor)
            {
                state.PropertyBlock.SetColor("_HiddenColor", Color.Lerp(state.HiddenColor, revealedHiddenColor, progress));
            }

            if (state.HasEmissionColor)
            {
                state.PropertyBlock.SetColor("_EmissionColor", finalBrightnessColor * (progress * emissionBoost));
            }

            state.Renderer.SetPropertyBlock(state.PropertyBlock);

            Material sharedMaterial = state.Renderer.sharedMaterial;
            if (sharedMaterial == null)
            {
                continue;
            }

            if (state.HasBaseColor)
            {
                sharedMaterial.SetColor("_BaseColor", Color.Lerp(state.BaseColor, finalBrightnessColor, progress));
            }

            if (state.HasAlbedoColor)
            {
                sharedMaterial.color = Color.Lerp(state.AlbedoColor, finalBrightnessColor, progress);
            }

            if (state.HasLitColor)
            {
                sharedMaterial.SetColor("_LitColor", Color.Lerp(state.LitColor, finalBrightnessColor, progress));
            }

            if (state.HasHiddenColor)
            {
                sharedMaterial.SetColor("_HiddenColor", Color.Lerp(state.HiddenColor, revealedHiddenColor, progress));
            }

            if (state.HasEmissionColor)
            {
                sharedMaterial.SetColor("_EmissionColor", finalBrightnessColor * (progress * emissionBoost));
                sharedMaterial.EnableKeyword("_EMISSION");
            }
        }
    }

    private void EnsureFinalGlowLight()
    {
        if (stageRoot == null)
        {
            stageRoot = transform;
        }

        if (finalGlowLight != null)
        {
            return;
        }

        Transform glowTransform = stageRoot.Find("Final Glow Light");
        if (glowTransform == null)
        {
            glowTransform = new GameObject("Final Glow Light").transform;
            glowTransform.SetParent(stageRoot, false);
        }

        glowTransform.localPosition = new Vector3(0f, 2.8f, 5.5f);
        glowTransform.localRotation = Quaternion.identity;

        finalGlowLight = glowTransform.GetComponent<Light>();
        if (finalGlowLight == null)
        {
            finalGlowLight = glowTransform.gameObject.AddComponent<Light>();
        }

        finalGlowLight.type = LightType.Point;
        finalGlowLight.color = finalBrightnessColor;
        finalGlowLight.range = finalLightRange;
        finalGlowLight.intensity = 0f;
        finalGlowLight.enabled = false;
    }
}