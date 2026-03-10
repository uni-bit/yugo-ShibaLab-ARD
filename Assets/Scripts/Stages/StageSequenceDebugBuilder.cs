using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public static class StageSequenceDebugBuilder
{
    private const string StageContainerName = "Stage Setup";
    private const string LegacyDebugStageContainerName = "Debug Stage Setup";
    private static readonly string[] LegacyGeneratedRootNames =
    {
        "Stage1 Debug Root",
        "Stage2 Debug Root",
        "Stage3 Debug Root"
    };
 
    public static GameObject[] EnsureStageSetup(Transform parent, GameObject[] existingStageRoots = null)
    {
        RemoveLegacyGeneratedRoots(parent);
        Transform container = FindOrCreateContainer(parent);
        GameObject[] stageRoots = existingStageRoots != null && existingStageRoots.Length == 3
            ? existingStageRoots
            : new GameObject[3];

        stageRoots[0] = ResolveStageRootReference(container, stageRoots[0], 0, "Stage1 Root");
        stageRoots[1] = ResolveStageRootReference(container, stageRoots[1], 1, "Stage2 Root");
        stageRoots[2] = ResolveStageRootReference(container, stageRoots[2], 2, "Stage3 Root");

        stageRoots[0] = EnsureStageRoot(container, stageRoots[0], 0, "Stage1 Root");
        stageRoots[1] = EnsureStageRoot(container, stageRoots[1], 1, "Stage2 Root");
        stageRoots[2] = EnsureStageRoot(container, stageRoots[2], 2, "Stage3 Root");

        EnsureStage1(stageRoots[0].transform);
        EnsureStage2(stageRoots[1].transform);
        EnsureStage3(stageRoots[2].transform);

        return stageRoots;
    }

    private static GameObject ResolveStageRootReference(Transform container, GameObject currentRoot, int stageIndex, string defaultName)
    {
        if (currentRoot == null)
        {
            return null;
        }

        if (IsSceneObject(currentRoot))
        {
            return currentRoot;
        }

        GameObject instantiatedRoot = InstantiateStageRootPrefab(container, currentRoot, defaultName);
        return EnsureMarker(instantiatedRoot, stageIndex, defaultName);
    }

    private static bool IsSceneObject(GameObject target)
    {
        return target != null && target.scene.IsValid();
    }

    private static GameObject InstantiateStageRootPrefab(Transform parent, GameObject prefabAsset, string defaultName)
    {
        if (parent == null || prefabAsset == null)
        {
            return null;
        }

#if UNITY_EDITOR
        GameObject prefabInstance = PrefabUtility.InstantiatePrefab(prefabAsset, parent) as GameObject;
#else
        GameObject prefabInstance = Object.Instantiate(prefabAsset, parent);
#endif
        if (prefabInstance == null)
        {
            return null;
        }

        prefabInstance.name = defaultName;
        prefabInstance.transform.SetParent(parent, false);
        return prefabInstance;
    }

    private static void RemoveLegacyGeneratedRoots(Transform parent)
    {
        for (int index = parent.childCount - 1; index >= 0; index--)
        {
            Transform child = parent.GetChild(index);
            if (child == null)
            {
                continue;
            }

            if (child.name == StageContainerName || child.name == LegacyDebugStageContainerName)
            {
                continue;
            }

            for (int nameIndex = 0; nameIndex < LegacyGeneratedRootNames.Length; nameIndex++)
            {
                if (child.name != LegacyGeneratedRootNames[nameIndex])
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Object.Destroy(child.gameObject);
                }
                else
                {
                    Object.DestroyImmediate(child.gameObject);
                }

                break;
            }
        }
    }

    private static void EnsureStage1(Transform root)
    {
        if (root.childCount > 0)
        {
            return;
        }

        CreateGround(root, "Stage1 Ground", new Vector3(0f, -0.55f, 8f), new Vector3(8f, 0.2f, 8f), new Color(0.15f, 0.18f, 0.14f, 1f));
        CreateLabel(root, "Stage1 Label", "Stage 1\nLight creatures in order", new Vector3(0f, 2.2f, 5.6f), 0.32f);

        StageLightOrderedPuzzle puzzle = root.gameObject.AddComponent<StageLightOrderedPuzzle>();
        StageLightCreatureTarget[] creatureTargets = new StageLightCreatureTarget[3];

        for (int index = 0; index < creatureTargets.Length; index++)
        {
            float x = -2.4f + (index * 2.4f);
            GameObject creature = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            creature.name = "Creature " + (index + 1);
            creature.transform.SetParent(root, false);
            creature.transform.localPosition = new Vector3(x, 0f, 8f);
            creature.transform.localScale = new Vector3(0.8f, 0.9f, 0.8f);
            SetRendererColor(creature, index == 1 ? new Color(0.45f, 0.9f, 0.5f, 1f) : new Color(0.95f, 0.88f, 0.45f, 1f));

            GameObject numberLabel = CreateLabel(creature.transform, "Order Label", (index + 1).ToString(), new Vector3(0f, 1.2f, 0f), 0.22f);
            SpotlightSensor sensor = creature.AddComponent<SpotlightSensor>();
            sensor.Configure(null, null, creature.transform, creature.GetComponent<Renderer>(), creature.GetComponent<Collider>());

            StageLightCreatureTarget creatureTarget = creature.AddComponent<StageLightCreatureTarget>();
            StageLightCreatureTarget.ReactionMode reaction = index % 2 == 0
                ? StageLightCreatureTarget.ReactionMode.Hide
                : StageLightCreatureTarget.ReactionMode.Hop;
            creatureTarget.Configure(sensor, creature.transform, reaction);
            creatureTargets[index] = creatureTarget;

            numberLabel.transform.SetParent(creature.transform, false);
        }

        GameObject completeMarker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        completeMarker.name = "Stage1 Complete Marker";
        completeMarker.transform.SetParent(root, false);
        completeMarker.transform.localPosition = new Vector3(0f, 0.35f, 11.5f);
        completeMarker.transform.localScale = new Vector3(0.55f, 0.35f, 0.55f);
        SetRendererColor(completeMarker, new Color(0.35f, 0.85f, 1f, 1f));
        completeMarker.SetActive(false);

        CreateLabel(completeMarker.transform, "Solved Text", "Solved", new Vector3(0f, 1.4f, 0f), 0.22f);
        puzzle.Configure(creatureTargets, new[] { completeMarker }, new GameObject[0]);
    }

    private static void EnsureStage2(Transform root)
    {
        EnsureGround(root, "Stage2 Ground", new Vector3(0f, -0.55f, 8f), new Vector3(10f, 0.2f, 8f), new Color(0.12f, 0.12f, 0.2f, 1f));
        RemoveGeneratedStage2Label(root);

        StageSymbolNumberRevealPuzzle puzzle = root.GetComponent<StageSymbolNumberRevealPuzzle>();
        if (puzzle == null)
        {
            puzzle = root.gameObject.AddComponent<StageSymbolNumberRevealPuzzle>();
        }

        StageSymbolNumberRevealTarget[] revealTargets = new StageSymbolNumberRevealTarget[3];
        string[] mappingTexts = { "□=4", "△=3", "○=8" };
        Vector3[] defaultPositions =
        {
            new Vector3(-3.1f, 0.7f, 8f),
            new Vector3(0f, 0.7f, 8f),
            new Vector3(3.1f, 0.7f, 8f)
        };

        for (int index = 0; index < revealTargets.Length; index++)
        {
            GameObject stageObject = EnsureStage2Object(root, index, mappingTexts[index], defaultPositions[index]);
            SpotlightSensor sensor = stageObject.GetComponent<SpotlightSensor>();
            if (sensor == null)
            {
                sensor = stageObject.AddComponent<SpotlightSensor>();
            }
            sensor.Configure(null, null, stageObject.transform, null, null);

            Transform displayRoot = stageObject.transform.Find("Mapping Display");
            StageSymbolNumberRevealTarget revealTarget = stageObject.GetComponent<StageSymbolNumberRevealTarget>();
            if (revealTarget == null)
            {
                revealTarget = stageObject.AddComponent<StageSymbolNumberRevealTarget>();
            }
            revealTarget.Configure(sensor, displayRoot);
            revealTargets[index] = revealTarget;
        }

        GameObject completeMarker = EnsurePrimitive(root, "Stage2 Complete Marker", PrimitiveType.Sphere, new Vector3(0f, 0.75f, 11.2f), Vector3.one * 0.9f);
        SetRendererColor(completeMarker, new Color(1f, 0.8f, 0.2f, 1f));
        completeMarker.SetActive(false);
        EnsureLabel(completeMarker.transform, "Solved Text", "All Numbers Found", new Vector3(0f, 1.1f, 0f), 0.18f);
        puzzle.Configure(revealTargets, new[] { completeMarker }, new GameObject[0]);
    }

    private static void EnsureStage3(Transform root)
    {
        if (root.childCount > 0)
        {
            return;
        }

        CreateGround(root, "Stage3 Ground", new Vector3(0f, -0.55f, 8f), new Vector3(10f, 0.2f, 8f), new Color(0.14f, 0.1f, 0.12f, 1f));
        CreateLabel(root, "Stage3 Label", "Stage 3\nLight-driven code lock", new Vector3(0f, 2.4f, 5.8f), 0.32f);

        GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        panel.name = "Code Lock Panel";
        panel.transform.SetParent(root, false);
        panel.transform.localPosition = new Vector3(0f, 1.1f, 8f);
        panel.transform.localScale = new Vector3(6.8f, 4.8f, 0.5f);
        SetRendererColor(panel, new Color(0.18f, 0.16f, 0.12f, 1f));

        GameObject topTextRoot = new GameObject("Top Formula Root");
        topTextRoot.transform.SetParent(panel.transform, false);
        topTextRoot.transform.localPosition = new Vector3(0f, 1.55f, -0.34f);
        topTextRoot.AddComponent<FaceCameraBillboard>();
        TextMesh topFormula = CreateLabel(topTextRoot.transform, "Formula Label", "○△□ = ???", Vector3.zero, 0.26f).GetComponent<TextMesh>();
        if (topFormula != null)
        {
            topFormula.fontSize = 88;
            topFormula.color = new Color(0.55f, 0.9f, 1f, 1f);
        }

        StageLightCodeLockPuzzle codeLockPuzzle = root.gameObject.AddComponent<StageLightCodeLockPuzzle>();
        StageLightCodeDialColumn[] columns = new StageLightCodeDialColumn[3];

        for (int index = 0; index < columns.Length; index++)
        {
            float x = -1.9f + (index * 1.9f);
            GameObject columnRoot = new GameObject("Dial Column " + (index + 1));
            columnRoot.transform.SetParent(panel.transform, false);
            columnRoot.transform.localPosition = new Vector3(x, -0.3f, -0.34f);

            GameObject upButton = CreateButton(columnRoot.transform, "Up Button", new Vector3(0f, 1.0f, 0f), "▲");
            GameObject display = CreateDisplay(columnRoot.transform, "Digit Display", new Vector3(0f, 0f, 0f), "0");
            GameObject downButton = CreateButton(columnRoot.transform, "Down Button", new Vector3(0f, -1.0f, 0f), "▼");

            SpotlightSensor upSensor = upButton.AddComponent<SpotlightSensor>();
            upSensor.Configure(null, null, upButton.transform, upButton.GetComponent<Renderer>(), upButton.GetComponent<Collider>());

            SpotlightSensor downSensor = downButton.AddComponent<SpotlightSensor>();
            downSensor.Configure(null, null, downButton.transform, downButton.GetComponent<Renderer>(), downButton.GetComponent<Collider>());

            StageLightCodeDialColumn column = columnRoot.AddComponent<StageLightCodeDialColumn>();
            column.Configure(display.GetComponent<TextMesh>(), upSensor, downSensor, 0);
            columns[index] = column;
        }

        GameObject door = GameObject.CreatePrimitive(PrimitiveType.Cube);
        door.name = "Lock Door";
        door.transform.SetParent(panel.transform, false);
        door.transform.localPosition = new Vector3(0f, 0f, -0.29f);
        door.transform.localScale = new Vector3(6.1f, 4.1f, 0.08f);
        SetRendererColor(door, new Color(0.08f, 0.09f, 0.1f, 1f));

        codeLockPuzzle.Configure(columns, door.transform, topFormula, "834");
    }

    private static GameObject CreateButton(Transform parent, string name, Vector3 localPosition, string label)
    {
        GameObject button = GameObject.CreatePrimitive(PrimitiveType.Cube);
        button.name = name;
        button.transform.SetParent(parent, false);
        button.transform.localPosition = localPosition;
        button.transform.localScale = new Vector3(1.2f, 0.6f, 0.18f);
        SetRendererColor(button, new Color(0.2f, 0.2f, 0.22f, 1f));

        GameObject textRoot = new GameObject(name + " Text Root");
        textRoot.transform.SetParent(button.transform, false);
        textRoot.transform.localPosition = new Vector3(0f, 0f, -0.16f);
        textRoot.AddComponent<FaceCameraBillboard>();
        TextMesh text = CreateLabel(textRoot.transform, name + " Label", label, Vector3.zero, 0.22f).GetComponent<TextMesh>();
        if (text != null)
        {
            text.fontSize = 84;
            text.color = new Color(0.5f, 0.9f, 1f, 1f);
        }

        return button;
    }

    private static GameObject CreateDisplay(Transform parent, string name, Vector3 localPosition, string initialText)
    {
        GameObject display = new GameObject(name);
        display.transform.SetParent(parent, false);
        display.transform.localPosition = localPosition + new Vector3(0f, 0f, -0.16f);
        display.AddComponent<FaceCameraBillboard>();

        TextMesh textMesh = display.AddComponent<TextMesh>();
        textMesh.text = initialText;
        textMesh.characterSize = 0.34f;
        textMesh.fontSize = 112;
        textMesh.alignment = TextAlignment.Center;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.color = new Color(0.45f, 0.95f, 1f, 1f);

        MeshRenderer renderer = display.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        return display;
    }

    private static GameObject EnsureStage2Object(Transform root, int index, string mappingText, Vector3 defaultLocalPosition)
    {
        string objectName = "Stage2 Object " + (index + 1);
        Transform existing = root.Find(objectName);
        GameObject stageObject = existing != null ? existing.gameObject : new GameObject(objectName);

        stageObject.name = objectName;
        stageObject.transform.SetParent(root, false);
        if (existing == null)
        {
            stageObject.transform.localPosition = defaultLocalPosition;
            stageObject.transform.localRotation = Quaternion.identity;
            stageObject.transform.localScale = Vector3.one;
        }

        RemoveGeneratedStage2Board(stageObject);

        RemoveLegacyStage2GeneratedContent(stageObject.transform);

        StageSymbolMappingDisplay display = stageObject.GetComponent<StageSymbolMappingDisplay>();
        if (display == null)
        {
            display = stageObject.AddComponent<StageSymbolMappingDisplay>();
        }

        display.Configure(mappingText);
        return stageObject;
    }

    private static void RemoveGeneratedStage2Label(Transform root)
    {
        Transform label = root.Find("Stage2 Label");
        if (label == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Object.Destroy(label.gameObject);
        }
        else
        {
            Object.DestroyImmediate(label.gameObject);
        }
    }

    private static void RemoveGeneratedStage2Board(GameObject stageObject)
    {
        if (stageObject == null)
        {
            return;
        }

        Renderer renderer = stageObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            if (Application.isPlaying)
            {
                Object.Destroy(renderer);
            }
            else
            {
                Object.DestroyImmediate(renderer);
            }
        }

        Collider collider = stageObject.GetComponent<Collider>();
        if (collider != null)
        {
            if (Application.isPlaying)
            {
                Object.Destroy(collider);
            }
            else
            {
                Object.DestroyImmediate(collider);
            }
        }

        MeshFilter meshFilter = stageObject.GetComponent<MeshFilter>();
        if (meshFilter != null)
        {
            if (Application.isPlaying)
            {
                Object.Destroy(meshFilter);
            }
            else
            {
                Object.DestroyImmediate(meshFilter);
            }
        }
    }

    private static GameObject EnsurePrimitive(Transform parent, string name, PrimitiveType primitiveType, Vector3 localPosition, Vector3 localScale)
    {
        Transform existing = parent.Find(name);
        GameObject target = existing != null ? existing.gameObject : GameObject.CreatePrimitive(primitiveType);
        target.name = name;
        target.transform.SetParent(parent, false);
        if (existing == null)
        {
            target.transform.localPosition = localPosition;
            target.transform.localRotation = Quaternion.identity;
            target.transform.localScale = localScale;
        }

        return target;
    }

    private static void RemoveLegacyStage2GeneratedContent(Transform stageObject)
    {
        for (int index = stageObject.childCount - 1; index >= 0; index--)
        {
            Transform child = stageObject.GetChild(index);
            if (child == null)
            {
                continue;
            }

            bool isGeneratedDisplay = child.name == "Mapping Display" || child.name == "Lit Text Root";
            bool isLegacyCharacterRoot = child.name.StartsWith("Char ");
            bool isLegacySegment = child.name == "Segment";

            if (!isGeneratedDisplay && !isLegacyCharacterRoot && !isLegacySegment)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(child.gameObject);
            }
            else
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }
    }

    private static void EnsureGround(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Color color)
    {
        GameObject ground = EnsurePrimitive(parent, name, PrimitiveType.Cube, localPosition, localScale);
        SetRendererColor(ground, color);
    }

    private static GameObject EnsureLabel(Transform parent, string name, string text, Vector3 localPosition, float characterSize)
    {
        Transform existing = parent.Find(name);
        GameObject label = existing != null ? existing.gameObject : CreateLabel(parent, name, text, localPosition, characterSize);
        label.name = name;
        label.transform.SetParent(parent, false);
        if (existing == null)
        {
            label.transform.localPosition = localPosition;
            label.transform.localRotation = Quaternion.identity;
        }

        TextMesh textMesh = label.GetComponent<TextMesh>();
        if (textMesh != null)
        {
            textMesh.text = text;
            textMesh.characterSize = characterSize;
        }

        return label;
    }

    private static Transform FindOrCreateContainer(Transform parent)
    {
        Transform existing = parent.Find(StageContainerName);
        if (existing != null)
        {
            return existing;
        }

        Transform legacy = parent.Find(LegacyDebugStageContainerName);
        if (legacy != null)
        {
            legacy.name = StageContainerName;
            return legacy;
        }

        GameObject container = new GameObject(StageContainerName);
        container.transform.SetParent(parent, false);
        container.transform.localPosition = Vector3.zero;
        container.transform.localRotation = Quaternion.identity;
        return container.transform;
    }

    private static GameObject EnsureStageRoot(Transform parent, GameObject currentRoot, int stageIndex, string defaultName)
    {
        if (parent == null)
        {
            return null;
        }

        if (currentRoot != null)
        {
            GameObject markedRoot = EnsureMarker(currentRoot, stageIndex, defaultName);
            if (markedRoot != null)
            {
                return markedRoot;
            }
        }

        StageRootMarker[] markers = parent.GetComponentsInChildren<StageRootMarker>(true);
        for (int index = 0; index < markers.Length; index++)
        {
            StageRootMarker marker = markers[index];
            if (marker != null && marker.StageIndex == stageIndex)
            {
                GameObject markedRoot = EnsureMarker(marker.gameObject, stageIndex, defaultName);
                if (markedRoot != null)
                {
                    return markedRoot;
                }
            }
        }

        GameObject stageRoot = CreateStageRoot(parent, defaultName, Vector3.zero);
        return EnsureMarker(stageRoot, stageIndex, defaultName);
    }

    private static GameObject CreateStageRoot(Transform parent, string name, Vector3 localPosition)
    {
        GameObject stageRoot = new GameObject(name);
        stageRoot.transform.SetParent(parent, false);
        stageRoot.transform.localPosition = localPosition;
        stageRoot.transform.localRotation = Quaternion.identity;
        return stageRoot;
    }

    private static GameObject EnsureMarker(GameObject target, int stageIndex, string stageName)
    {
        if (target == null)
        {
            return null;
        }

        StageRootMarker marker = target.GetComponent<StageRootMarker>();
        if (marker == null)
        {
            marker = target.AddComponent<StageRootMarker>();
        }

        if (marker == null)
        {
            return null;
        }

        marker.Configure(stageIndex, stageName);
        if (string.IsNullOrEmpty(target.name) || target.name.Contains("Debug Root"))
        {
            target.name = stageName;
        }

        return target;
    }

    private static void CreateGround(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Color color)
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = name;
        ground.transform.SetParent(parent, false);
        ground.transform.localPosition = localPosition;
        ground.transform.localScale = localScale;
        SetRendererColor(ground, color);
    }

    private static GameObject CreateLabel(Transform parent, string name, string text, Vector3 localPosition, float characterSize)
    {
        GameObject label = new GameObject(name);
        label.transform.SetParent(parent, false);
        label.transform.localPosition = localPosition;
        label.transform.localRotation = Quaternion.identity;

        TextMesh textMesh = label.AddComponent<TextMesh>();
        textMesh.text = text;
        textMesh.characterSize = characterSize;
        textMesh.fontSize = 96;
        textMesh.alignment = TextAlignment.Center;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.color = Color.white;

        MeshRenderer renderer = label.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        return label;
    }

    private static void SetRendererColor(GameObject target, Color color)
    {
        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material sharedMaterial = renderer.sharedMaterial;
            if (sharedMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (shader == null)
                {
                    return;
                }

                sharedMaterial = new Material(shader);
                renderer.sharedMaterial = sharedMaterial;
            }

            if (sharedMaterial.HasProperty("_BaseColor"))
            {
                sharedMaterial.SetColor("_BaseColor", color);
            }

            sharedMaterial.color = color;
        }
    }

}