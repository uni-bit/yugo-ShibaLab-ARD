using UnityEngine;

public static class StageSpotlightMaterialUtility
{
    private const string ShaderName = "ShibaLab/Stage Spotlight Text";

    public static void ApplySpotlitText(TextMesh textMesh, Color hiddenColor, Color litColor)
    {
        if (textMesh == null)
        {
            return;
        }

        MeshRenderer renderer = textMesh.GetComponent<MeshRenderer>();
        if (renderer == null)
        {
            return;
        }

        Texture mainTexture = null;
        if (textMesh.font != null && textMesh.font.material != null)
        {
            mainTexture = textMesh.font.material.mainTexture;
        }

        Material material = EnsureMaterial(renderer, mainTexture);
        if (material == null)
        {
            return;
        }

        material.SetColor("_HiddenColor", hiddenColor);
        material.SetColor("_LitColor", litColor);
        renderer.sharedMaterial = material;
        renderer.enabled = true;

        RemoveLegacyFeedback(textMesh.gameObject);
    }

    public static void ApplySpotlitLine(LineRenderer lineRenderer, Color hiddenColor, Color litColor)
    {
        if (lineRenderer == null)
        {
            return;
        }

        Material material = EnsureMaterial(lineRenderer, Texture2D.whiteTexture);
        if (material == null)
        {
            return;
        }

        material.SetColor("_HiddenColor", hiddenColor);
        material.SetColor("_LitColor", litColor);
        lineRenderer.sharedMaterial = material;
        lineRenderer.enabled = true;

        RemoveLegacyFeedback(lineRenderer.gameObject);
    }

    public static void ApplySpotlitRenderer(Renderer renderer, Color hiddenColor, Color litColor)
    {
        if (renderer == null)
        {
            return;
        }

        Material material = EnsureMaterial(renderer, Texture2D.whiteTexture);
        if (material == null)
        {
            return;
        }

        material.SetColor("_HiddenColor", hiddenColor);
        material.SetColor("_LitColor", litColor);
        renderer.sharedMaterial = material;
        renderer.enabled = true;

        RemoveLegacyFeedback(renderer.gameObject);
    }

    private static Material EnsureMaterial(Renderer renderer, Texture mainTexture)
    {
        Shader shader = Shader.Find(ShaderName);
        if (renderer == null || shader == null)
        {
            return null;
        }

        Material material = renderer.sharedMaterial;
        if (material == null || material.shader == null || material.shader.name != ShaderName)
        {
            material = new Material(shader);
            material.name = renderer.gameObject.name + " Spotlight Material";
        }

        if (mainTexture != null)
        {
            material.SetTexture("_MainTex", mainTexture);
        }

        return material;
    }

    private static void RemoveLegacyFeedback(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        LightReactiveTextFeedback textFeedback = target.GetComponent<LightReactiveTextFeedback>();
        if (textFeedback != null)
        {
            if (Application.isPlaying)
            {
                Object.Destroy(textFeedback);
            }
            else
            {
                Object.DestroyImmediate(textFeedback);
            }
        }

        LightReactiveLineFeedback lineFeedback = target.GetComponent<LightReactiveLineFeedback>();
        if (lineFeedback != null)
        {
            if (Application.isPlaying)
            {
                Object.Destroy(lineFeedback);
            }
            else
            {
                Object.DestroyImmediate(lineFeedback);
            }
        }
    }
}