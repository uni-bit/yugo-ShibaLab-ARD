using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// デバッグ用ステージ構成の自動生成を行う静的ユーティリティクラス。
/// <para>
/// <see cref="EnsureStageSetup"/> が中心的なエントリポイントで、<br/>
/// <see cref="StageSequenceController"/> の Awake / OnValidate / SyncStageSetup から呼ばれる。
/// </para>
/// <para>
/// 処理の流れ:
/// <list type="number">
/// <item>レガシー生成物の削除</item>
/// <item>"Stage Setup" コンテナを FindOrCreate</item>
/// <item>Stage1〜4 Root を FindOrCreate し <see cref="StageRootMarker"/> を付与</item>
/// <item>各ステージ固有コンポーネント・GameObjectを EnsureStage1〜4 で補完</item>
/// </list>
/// </para>
/// </summary>
public static class StageSequenceDebugBuilder
{
    private const string StageContainerName = "Stage Setup";
    private const string LegacyDebugStageContainerName = "Debug Stage Setup";
    private const string BuiltInFontName = "LegacyRuntime.ttf";
    private static readonly string[] LegacyGeneratedRootNames =
    {
        "Stage1 Debug Root",
        "Stage2 Debug Root",
        "Stage3 Debug Root",
        "Stage4 Debug Root"
    };
    private static Font builtInFont;
 
    public static GameObject[] EnsureStageSetup(Transform parent, GameObject[] existingStageRoots = null)
    {
        RemoveLegacyGeneratedRoots(parent);
        Transform container = FindOrCreateContainer(parent);
        GameObject[] stageRoots = existingStageRoots != null && existingStageRoots.Length == 4
            ? existingStageRoots
            : new GameObject[4];

        stageRoots[0] = ResolveStageRootReference(container, stageRoots[0], 0, "Stage1 Root");
        stageRoots[1] = ResolveStageRootReference(container, stageRoots[1], 1, "Stage2 Root");
        stageRoots[2] = ResolveStageRootReference(container, stageRoots[2], 2, "Stage3 Root");
        stageRoots[3] = ResolveStageRootReference(container, stageRoots[3], 3, "Stage4 Root");

        stageRoots[0] = EnsureStageRoot(container, stageRoots[0], 0, "Stage1 Root");
        stageRoots[1] = EnsureStageRoot(container, stageRoots[1], 1, "Stage2 Root");
        stageRoots[2] = EnsureStageRoot(container, stageRoots[2], 2, "Stage3 Root");
        stageRoots[3] = EnsureStageRoot(container, stageRoots[3], 3, "Stage4 Root");

        EnsureStage1(stageRoots[0].transform);
        EnsureStage2(stageRoots[1].transform);
        EnsureStage3(stageRoots[2].transform);
        EnsureStage4(stageRoots[3].transform);

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
        EnsureStageSpotlightSettings(root, true, 18f, 60f, 16f, Color.white);

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
        EnsureStageSpotlightSettings(root, true, 20f, 68f, 18f, Color.white);
        RemoveGeneratedStage2Label(root);
        RemoveGeneratedStage2CompleteMarker(root);

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

        EnsureStage2CodeLock(root);
        puzzle.Configure(revealTargets, new GameObject[0], new GameObject[0]);
    }

    private static void EnsureStage3(Transform root)
    {
        EnsureRockHintStage(
            root,
            3,
            "Find the hidden green and blue stones",
            new Vector3(14f, 0.2f, 18f),
            new Color(0.08f, 0.08f, 0.1f, 1f),
            new Vector3(0f, 3f, 5.4f),
            new Vector3(0f, 0f, 8.5f),
            new Vector3(-4.7f, 0.72f, 6.1f),
            new Vector3(3.8f, 0f, 13.4f),
            new Vector3(0f, 0.7f, 0.85f),
            true,
            3,
            true);
    }

    private static void EnsureStage4(Transform root)
    {
        EnsureRockHintStage(
            root,
            4,
            "Follow the same light logic deeper inside",
            new Vector3(16f, 0.2f, 20f),
            new Color(0.09f, 0.07f, 0.08f, 1f),
            new Vector3(0f, 3f, 5.6f),
            new Vector3(0f, 0f, 9.4f),
            new Vector3(4.6f, 0.78f, 7.3f),
            new Vector3(-4.2f, 0f, 14.2f),
            new Vector3(0f, 0.72f, 0.95f),
            false,
            3,
            false);

        Stage4SequenceController stage4Seq = root.GetComponent<Stage4SequenceController>();
        if (stage4Seq == null)
        {
            root.gameObject.AddComponent<Stage4SequenceController>();
        }
    }

    private static void EnsureRockHintStage(
        Transform root,
        int stageNumber,
        string objectiveText,
        Vector3 groundScale,
        Color groundColor,
        Vector3 labelPosition,
        Vector3 pedestalRowPosition,
        Vector3 greenHintPosition,
        Vector3 cavePosition,
        Vector3 blueHintLocalPosition,
        bool advanceToNextStage,
        int nextStageIndex,
        bool brightTransitionToNext = false)
    {
        EnsureStageSpotlightSettings(root, true, 20f, 68f, 18f, Color.white);
        RemoveGeneratedStage3Content(root);

        EnsureGround(root, "Stage" + stageNumber + " Ground", new Vector3(0f, -0.55f, groundScale.z * 0.5f + 0.5f), groundScale, groundColor);

        Transform labelRoot = FindOrCreateChildIfMissing(root, "Stage" + stageNumber + " Label", labelPosition);
        labelRoot.localPosition = labelPosition;
        TextMesh labelText = labelRoot.GetComponent<TextMesh>();
        if (labelText == null)
        {
            labelText = labelRoot.gameObject.AddComponent<TextMesh>();
        }

        labelText.text = "Stage " + stageNumber + "\n" + objectiveText;
        labelText.characterSize = 0.18f;
        labelText.fontSize = 96;
        labelText.alignment = TextAlignment.Center;
        labelText.anchor = TextAnchor.MiddleCenter;
        labelText.color = new Color(0.9f, 0.95f, 1f, 1f);
        ApplyBuiltInFont(labelText);

        Transform puzzleRoot = FindOrCreateChildIfMissing(root, "Stage" + stageNumber + " Puzzle Root", Vector3.zero);
        Transform pedestalRow = FindOrCreateChildIfMissing(puzzleRoot, "Pedestal Row", pedestalRowPosition);
        pedestalRow.localPosition = pedestalRowPosition;
        Transform hiddenHintArea = FindOrCreateChildIfMissing(puzzleRoot, "Hidden Hint Area", Vector3.zero);
        Transform caveRoot = FindOrCreateChildIfMissing(hiddenHintArea, "Blue Hint Cave", cavePosition);
        caveRoot.localPosition = cavePosition;

        Transform redPedestalRock = EnsureStage3Pedestal(pedestalRow, "Red Pedestal", new Vector3(-2.6f, 0f, 0f), new Color(0.24f, 0.12f, 0.1f, 1f), "Red Pedestal Rock", new Color(0.95f, 0.28f, 0.22f, 1f));
        Transform greenPedestalRock = EnsureStage3Pedestal(pedestalRow, "Green Pedestal", new Vector3(0f, 0f, 0f), new Color(0.12f, 0.2f, 0.12f, 1f), "Green Pedestal Rock", new Color(0.2f, 0.9f, 0.35f, 1f));
        Transform bluePedestalRock = EnsureStage3Pedestal(pedestalRow, "Blue Pedestal", new Vector3(2.6f, 0f, 0f), new Color(0.1f, 0.14f, 0.24f, 1f), "Blue Pedestal Rock", new Color(0.24f, 0.58f, 0.98f, 1f));

        Transform greenHintRock = EnsureStage3HiddenRock(hiddenHintArea, "Green Hint Rock", greenHintPosition, new Vector3(0.9f, 0.9f, 0.9f), new Color(0.2f, 0.9f, 0.35f, 1f));
        EnsureStage3Cave(caveRoot);
        Transform blueHintRock = EnsureStage3HiddenRock(caveRoot, "Blue Hint Rock", blueHintLocalPosition, new Vector3(0.82f, 0.82f, 0.82f), new Color(0.24f, 0.58f, 0.98f, 1f));

        Stage3RockHintPuzzle puzzle = root.GetComponent<Stage3RockHintPuzzle>();
        if (puzzle == null)
        {
            puzzle = root.gameObject.AddComponent<Stage3RockHintPuzzle>();
        }

        puzzle.ConfigureDefaults(redPedestalRock, greenPedestalRock, bluePedestalRock, greenHintRock, blueHintRock);
        puzzle.ConfigureTransition(advanceToNextStage, nextStageIndex);
        puzzle.ConfigureBrightTransition(brightTransitionToNext);
    }

    private static void RemoveGeneratedStage3Content(Transform root)
    {
        string[] generatedChildNames =
        {
            "Code Lock Panel",
            "Code Lock Content",
            "Lock Door",
            "Code Lock Rig"
        };

        for (int index = 0; index < generatedChildNames.Length; index++)
        {
            Transform child = root.Find(generatedChildNames[index]);
            if (child == null)
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

        StageLightCodeLockPuzzle codeLockPuzzle = root.GetComponent<StageLightCodeLockPuzzle>();
        if (codeLockPuzzle != null)
        {
            if (Application.isPlaying)
            {
                Object.Destroy(codeLockPuzzle);
            }
            else
            {
                Object.DestroyImmediate(codeLockPuzzle);
            }
        }

        StageLightCodeDialColumn[] columns = root.GetComponentsInChildren<StageLightCodeDialColumn>(true);
        for (int index = 0; index < columns.Length; index++)
        {
            if (columns[index] == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(columns[index]);
            }
            else
            {
                Object.DestroyImmediate(columns[index]);
            }
        }
    }

    private static Transform EnsureStage3Pedestal(Transform parent, string pedestalName, Vector3 localPosition, Color pedestalColor, string rockName, Color rockColor)
    {
        Transform pedestalRoot = FindOrCreateChildIfMissing(parent, pedestalName, localPosition);
        pedestalRoot.localPosition = localPosition;

        GameObject pedestalBase = EnsurePrimitive(pedestalRoot, "Base", PrimitiveType.Cylinder, new Vector3(0f, 0.5f, 0f), new Vector3(1.05f, 0.5f, 1.05f));
        SetRendererColor(pedestalBase, pedestalColor);

        GameObject pedestalTop = EnsurePrimitive(pedestalRoot, "Top", PrimitiveType.Cylinder, new Vector3(0f, 1.08f, 0f), new Vector3(0.86f, 0.12f, 0.86f));
        SetRendererColor(pedestalTop, pedestalColor * 1.15f);

        return EnsureStage3Rock(pedestalRoot, rockName, new Vector3(0f, 1.62f, 0f), new Vector3(0.78f, 0.62f, 0.72f), rockColor);
    }

    private static Transform EnsureStage3Rock(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Color color)
    {
        bool existed = parent.Find(name) != null;
        GameObject rock = EnsurePrimitive(parent, name, PrimitiveType.Sphere, localPosition, localScale);
        if (!existed)
        {
            rock.transform.localPosition = localPosition;
            rock.transform.localRotation = Quaternion.Euler(12f, 18f, -8f);
            rock.transform.localScale = localScale;
        }

        SetRendererColor(rock, color);
        return rock.transform;
    }

    private static Transform EnsureStage3HiddenRock(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Color color)
    {
        Transform rock = EnsureStage3Rock(parent, name, localPosition, localScale, color);
        SpotlightSensor sensor = EnsureSensor(rock.gameObject, rock, rock.GetComponent<Renderer>(), rock.GetComponent<Collider>());
        if (sensor != null)
        {
            sensor.RefreshState();
        }

        return rock;
    }

    private static void EnsureStage3Cave(Transform caveRoot)
    {
        caveRoot.localPosition = new Vector3(3.8f, 0f, 13.4f);

        GameObject leftWall = EnsurePrimitive(caveRoot, "Left Wall", PrimitiveType.Cube, new Vector3(-1.25f, 1.1f, 0f), new Vector3(0.55f, 2.2f, 2.6f));
        GameObject rightWall = EnsurePrimitive(caveRoot, "Right Wall", PrimitiveType.Cube, new Vector3(1.25f, 1.1f, 0f), new Vector3(0.55f, 2.2f, 2.6f));
        GameObject roof = EnsurePrimitive(caveRoot, "Roof", PrimitiveType.Cube, new Vector3(0f, 2.1f, 0f), new Vector3(3.1f, 0.45f, 2.6f));
        GameObject backWall = EnsurePrimitive(caveRoot, "Back Wall", PrimitiveType.Cube, new Vector3(0f, 1.05f, 1.3f), new Vector3(3.1f, 2.1f, 0.35f));

        Color caveColor = new Color(0.12f, 0.11f, 0.13f, 1f);
        SetRendererColor(leftWall, caveColor);
        SetRendererColor(rightWall, caveColor);
        SetRendererColor(roof, caveColor * 1.08f);
        SetRendererColor(backWall, caveColor * 0.92f);
    }

    private static void RemoveGeneratedStage2CompleteMarker(Transform root)
    {
        Transform completeMarker = root.Find("Stage2 Complete Marker");
        if (completeMarker == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Object.Destroy(completeMarker.gameObject);
        }
        else
        {
            Object.DestroyImmediate(completeMarker.gameObject);
        }
    }

    private static void EnsureStage2CodeLock(Transform root)
    {
        RemoveLegacyRootLevelCodeLockObjects(root);
        Transform rigRoot = FindOrCreateChildIfMissing(root, "Code Lock Rig", Vector3.zero);
        bool panelExists = rigRoot.Find("Code Lock Panel") != null;
        GameObject panel = EnsurePrimitive(rigRoot, "Code Lock Panel", PrimitiveType.Cube, new Vector3(0f, 1.1f, 8f), new Vector3(6.8f, 4.8f, 0.5f));
        if (!panelExists)
        {
            panel.transform.localPosition = new Vector3(0f, 1.1f, 8f);
            panel.transform.localRotation = Quaternion.identity;
            panel.transform.localScale = new Vector3(6.8f, 4.8f, 0.5f);
        }
        SetRendererColor(panel, new Color(0.18f, 0.16f, 0.12f, 1f));
        RemoveLegacyCodeLockPanelChildren(panel.transform);

        Transform contentRoot = FindOrCreateChildIfMissing(rigRoot, "Code Lock Content", new Vector3(0f, 1.1f, 7.72f));

        Transform topTextRoot = FindOrCreateChildIfMissing(contentRoot, "Top Formula Root", new Vector3(-0.3f, 2.57f, 0f));
        topTextRoot.localPosition = new Vector3(-0.3f, 2.57f, 0f);
        topTextRoot.localRotation = Quaternion.identity;
        RemoveLegacyFormulaContent(topTextRoot);
        RemoveComponentIfExists<FaceCameraBillboard>(topTextRoot.gameObject);
        SpotlightSensor formulaSensor = EnsureSensor(topTextRoot.gameObject, topTextRoot, null, null);
        TextMesh topFormula = EnsureLabel(topTextRoot, "Formula State Driver", "○△□ = ???", Vector3.zero, 0.08f).GetComponent<TextMesh>();
        if (topFormula != null)
        {
            ApplyBuiltInFont(topFormula);
            topFormula.fontSize = 160;
            topFormula.color = Color.white;
            MeshRenderer formulaRenderer = topFormula.GetComponent<MeshRenderer>();
            if (formulaRenderer != null)
            {
                formulaRenderer.enabled = false;
            }
        }

        StageCodeFormulaDisplay formulaDisplay = topTextRoot.GetComponent<StageCodeFormulaDisplay>();
        if (formulaDisplay == null)
        {
            formulaDisplay = topTextRoot.gameObject.AddComponent<StageCodeFormulaDisplay>();
        }
        formulaDisplay.Configure(topFormula, formulaSensor);

        StageLightCodeLockPuzzle codeLockPuzzle = root.GetComponent<StageLightCodeLockPuzzle>();
        if (codeLockPuzzle == null)
        {
            codeLockPuzzle = root.gameObject.AddComponent<StageLightCodeLockPuzzle>();
        }

        Stage2PuzzleController puzzleController = root.GetComponent<Stage2PuzzleController>();
        if (puzzleController == null)
        {
            puzzleController = root.gameObject.AddComponent<Stage2PuzzleController>();
        }

        Stage2CompletionSequence completionSequence = root.GetComponent<Stage2CompletionSequence>();
        if (completionSequence == null)
        {
            completionSequence = root.gameObject.AddComponent<Stage2CompletionSequence>();
        }

        StageCodeLockRig rig = rigRoot.GetComponent<StageCodeLockRig>();
        if (rig == null)
        {
            rig = rigRoot.gameObject.AddComponent<StageCodeLockRig>();
        }

        StageLightCodeDialColumn[] columns = new StageLightCodeDialColumn[3];
        Transform[] columnRoots = new Transform[3];
        for (int index = 0; index < columns.Length; index++)
        {
            float x = -1.8f + (index * 1.8f);
            Transform columnRoot = FindOrCreateChildIfMissing(contentRoot, "Dial Column " + (index + 1), new Vector3(x, -0.15f, 0f));
            columnRoots[index] = columnRoot;

            GameObject upButton = EnsureCodeLockButton(columnRoot, "Up Button", new Vector3(0f, 0.95f, 0f), "▲");
            GameObject display = EnsureCodeLockDisplay(columnRoot, "Digit Display", new Vector3(0f, 0f, 0f), "0");
            GameObject downButton = EnsureCodeLockButton(columnRoot, "Down Button", new Vector3(0f, -0.95f, 0f), "▼");
            EnsureDigitSegmentBox(columnRoot, "Digit Segment", new Vector3(0f, 0f, -0.08f), new Vector3(1.32f, 0.92f, 0.08f));

            SpotlightSensor upSensor = EnsureSensor(upButton, upButton.transform, upButton.GetComponent<Renderer>(), upButton.GetComponent<Collider>());
            SpotlightSensor downSensor = EnsureSensor(downButton, downButton.transform, downButton.GetComponent<Renderer>(), downButton.GetComponent<Collider>());
            StageCodeLockButtonIndicator upIndicator = EnsureCodeLockButtonIndicator(columnRoot, upButton, "Up Button Arrow Glyph", upSensor);
            StageCodeLockButtonIndicator downIndicator = EnsureCodeLockButtonIndicator(columnRoot, downButton, "Down Button Arrow Glyph", downSensor);

            StageLightCodeDialColumn column = columnRoot.GetComponent<StageLightCodeDialColumn>();
            if (column == null)
            {
                column = columnRoot.gameObject.AddComponent<StageLightCodeDialColumn>();
            }

            column.Configure(display.GetComponent<TextMesh>(), upSensor, downSensor, upIndicator, downIndicator, rig, 0);
            columns[index] = column;
        }

        bool doorExists = rigRoot.Find("Lock Door") != null;
        GameObject door = EnsurePrimitive(rigRoot, "Lock Door", PrimitiveType.Cube, new Vector3(0f, 1.1f, 7.88f), new Vector3(5.8f, 3.8f, 0.08f));
        if (!doorExists)
        {
            door.transform.localPosition = new Vector3(0f, 1.1f, 7.88f);
            door.transform.localRotation = Quaternion.identity;
            door.transform.localScale = new Vector3(5.8f, 3.8f, 0.08f);
        }
        SetRendererColor(door, new Color(0.08f, 0.09f, 0.1f, 1f));

        for (int c = door.transform.childCount - 1; c >= 0; c--)
        {
            Transform child = door.transform.GetChild(c);
            if (Application.isPlaying)
            {
                Object.Destroy(child.gameObject);
            }
            else
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }

        rig.Configure(panel.transform, contentRoot, topTextRoot, door.transform, columnRoots);
        codeLockPuzzle.Configure(columns, door.transform, topFormula, formulaDisplay, "834");

        StageSymbolNumberRevealPuzzle revealPuzzle = root.GetComponent<StageSymbolNumberRevealPuzzle>();
        completionSequence.Configure(rigRoot, new[] { contentRoot, topTextRoot }, panel.transform, root);
        completionSequence.ConfigureTransition(true, 2);
        puzzleController.Configure(revealPuzzle, codeLockPuzzle, completionSequence);
    }

    private static void RemoveLegacyRootLevelCodeLockObjects(Transform root)
    {
        string[] legacyNames =
        {
            "Code Lock Panel",
            "Code Lock Content",
            "Lock Door"
        };

        for (int index = 0; index < legacyNames.Length; index++)
        {
            Transform child = root.Find(legacyNames[index]);
            if (child == null)
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

    private static Transform FindOrCreateChild(Transform parent, string name, Vector3 localPosition)
    {
        Transform child = parent.Find(name);
        if (child == null)
        {
            child = new GameObject(name).transform;
            child.SetParent(parent, false);
        }

        child.localPosition = localPosition;
        child.localRotation = Quaternion.identity;
        child.localScale = Vector3.one;
        return child;
    }

    private static Transform FindOrCreateChildIfMissing(Transform parent, string name, Vector3 localPosition)
    {
        Transform child = parent.Find(name);
        if (child != null)
        {
            return child;
        }

        child = new GameObject(name).transform;
        child.SetParent(parent, false);
        child.localPosition = localPosition;
        child.localRotation = Quaternion.identity;
        child.localScale = Vector3.one;
        return child;
    }

    private static GameObject EnsureCodeLockButton(Transform parent, string name, Vector3 localPosition, string label)
    {
        bool buttonExists = parent.Find(name) != null;
        GameObject button = EnsurePrimitive(parent, name, PrimitiveType.Cube, localPosition, new Vector3(1.05f, 0.42f, 0.12f));
        if (!buttonExists)
        {
            button.transform.localPosition = localPosition;
            button.transform.localRotation = Quaternion.identity;
            button.transform.localScale = new Vector3(1.05f, 0.42f, 0.12f);
        }
        SetRendererColor(button, new Color(0.2f, 0.2f, 0.22f, 1f));
        RemoveLegacyButtonVisuals(button.transform, name);

        SpotlightSensor sensor = EnsureSensor(button, button.transform, button.GetComponent<Renderer>(), button.GetComponent<Collider>());
        LightReactiveRendererFeedback feedback = button.GetComponent<LightReactiveRendererFeedback>();
        if (feedback != null)
        {
            if (Application.isPlaying)
            {
                Object.Destroy(feedback);
            }
            else
            {
                Object.DestroyImmediate(feedback);
            }
        }

        EnsureArrowGlyph(parent, name + " Arrow Glyph", localPosition + new Vector3(0f, 0f, -0.16f), label == "▲");

        return button;
    }

    private static GameObject EnsureCodeLockDisplay(Transform parent, string name, Vector3 localPosition, string initialText)
    {
        Transform existing = parent.Find(name);
        GameObject display = existing != null ? existing.gameObject : new GameObject(name);
        display.transform.SetParent(parent, false);
        if (existing == null)
        {
            display.transform.localPosition = localPosition + new Vector3(0f, 0f, -0.06f);
            display.transform.localRotation = Quaternion.identity;
            display.transform.localScale = Vector3.one;
        }
        RemoveComponentIfExists<FaceCameraBillboard>(display);

        TextMesh textMesh = display.GetComponent<TextMesh>();
        if (textMesh == null)
        {
            textMesh = display.AddComponent<TextMesh>();
        }

        ApplyBuiltInFont(textMesh);
        textMesh.text = initialText;
        textMesh.characterSize = 0.1f;
        textMesh.fontSize = 180;
        textMesh.alignment = TextAlignment.Center;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.color = Color.white;
        StageSpotlightMaterialUtility.ApplySpotlitText(textMesh, new Color(1f, 1f, 1f, 0f), Color.white);

        StageLightCodeDigitAnimator animator = display.GetComponent<StageLightCodeDigitAnimator>();
        if (animator == null)
        {
            animator = display.AddComponent<StageLightCodeDigitAnimator>();
        }
        animator.SetDigitImmediate(int.TryParse(initialText, out int initialDigit) ? initialDigit : 0);

        MeshRenderer renderer = display.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        return display;
    }

    private static GameObject EnsureDigitSegmentBox(Transform parent, string name, Vector3 localPosition, Vector3 localScale)
    {
        Transform existing = parent.Find(name);
        GameObject segment = existing != null ? existing.gameObject : new GameObject(name);
        segment.name = name;
        segment.transform.SetParent(parent, false);
        segment.transform.localPosition = localPosition;
        segment.transform.localRotation = Quaternion.identity;
        segment.transform.localScale = Vector3.one;

        RemoveComponentIfExists<LineRenderer>(segment);

        const float frameThickness = 0.08f;
        float frameWidth = 1.3162f;
        float frameHeight = 2.847f;
        float frameDepth = Mathf.Max(0.04f, localScale.z);

        GameObject topEdge = EnsurePrimitive(segment.transform, "Frame Top", PrimitiveType.Cube, new Vector3(0f, frameHeight * 0.5f, 0f), new Vector3(frameWidth, frameThickness, frameDepth));
        GameObject bottomEdge = EnsurePrimitive(segment.transform, "Frame Bottom", PrimitiveType.Cube, new Vector3(0f, -frameHeight * 0.5f, 0f), new Vector3(frameWidth, frameThickness, frameDepth));
        GameObject leftEdge = EnsurePrimitive(segment.transform, "Frame Left", PrimitiveType.Cube, new Vector3(-frameWidth * 0.5f, 0f, 0f), new Vector3(frameThickness, frameHeight, frameDepth));
        GameObject rightEdge = EnsurePrimitive(segment.transform, "Frame Right", PrimitiveType.Cube, new Vector3(frameWidth * 0.5f, 0f, 0f), new Vector3(frameThickness, frameHeight, frameDepth));

        ApplySegmentFrameVisual(topEdge);
        ApplySegmentFrameVisual(bottomEdge);
        ApplySegmentFrameVisual(leftEdge);
        ApplySegmentFrameVisual(rightEdge);

        return segment;
    }

    private static void ApplySegmentFrameVisual(GameObject edge)
    {
        if (edge == null)
        {
            return;
        }

        SetRendererColor(edge, new Color(0.88f, 0.95f, 1f, 1f));
        Renderer edgeRenderer = edge.GetComponent<Renderer>();
        if (edgeRenderer == null)
        {
            return;
        }

        StageSpotlightMaterialUtility.ApplySpotlitRenderer(
            edgeRenderer,
            new Color(0.25f, 0.35f, 0.45f, 0f),
            new Color(0.88f, 0.95f, 1f, 1f));
    }

    private static StageCodeLockButtonIndicator EnsureCodeLockButtonIndicator(Transform columnRoot, GameObject button, string arrowName, SpotlightSensor sensor)
    {
        if (button == null)
        {
            return null;
        }

        StageCodeLockButtonIndicator indicator = button.GetComponent<StageCodeLockButtonIndicator>();
        if (indicator == null)
        {
            indicator = button.AddComponent<StageCodeLockButtonIndicator>();
        }

        Renderer arrowRenderer = null;
        if (columnRoot != null)
        {
            Transform arrowTransform = columnRoot.Find(arrowName);
            if (arrowTransform != null)
            {
                arrowRenderer = arrowTransform.GetComponent<Renderer>();
            }
        }

        indicator.Configure(sensor, button.GetComponent<Renderer>(), arrowRenderer);
        return indicator;
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

    private static void RemoveLegacyCodeLockPanelChildren(Transform panel)
    {
        for (int index = panel.childCount - 1; index >= 0; index--)
        {
            Transform child = panel.GetChild(index);
            if (child == null)
            {
                continue;
            }

            bool shouldRemove = child.name == "Top Formula Root"
                || child.name == "Lock Door"
                || child.name.StartsWith("Dial Column ");

            if (!shouldRemove)
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

    private static void RemoveLegacyFormulaContent(Transform topTextRoot)
    {
        for (int index = topTextRoot.childCount - 1; index >= 0; index--)
        {
            Transform child = topTextRoot.GetChild(index);
            if (child == null)
            {
                continue;
            }

            bool keepChild = child.name == "Formula State Driver"
                || child.name == "Circle Symbol"
                || child.name == "Triangle Symbol"
                || child.name == "Square Symbol"
                || child.name == "Formula Value"
                || child.name == "Formula Equals"
                || child.name == "Formula Question";

            if (keepChild)
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

        Transform legacyFormula = topTextRoot.Find("Formula Label");
        if (legacyFormula != null)
        {
            if (Application.isPlaying)
            {
                Object.Destroy(legacyFormula.gameObject);
            }
            else
            {
                Object.DestroyImmediate(legacyFormula.gameObject);
            }
        }
    }

    private static SpotlightSensor EnsureSensor(GameObject target, Transform samplePoint, Renderer sampleRenderer, Collider sampleCollider)
    {
        SpotlightSensor sensor = target.GetComponent<SpotlightSensor>();
        if (sensor == null)
        {
            sensor = target.AddComponent<SpotlightSensor>();
        }

        sensor.Configure(null, null, samplePoint, sampleRenderer, sampleCollider);
        return sensor;
    }

    private static void EnsureArrowGlyph(Transform parent, string glyphName, Vector3 localPosition, bool pointsUp)
    {
        Transform arrowTransform = parent.Find(glyphName);
        bool wasCreated = arrowTransform == null;
        if (arrowTransform == null)
        {
            arrowTransform = new GameObject(glyphName).transform;
            arrowTransform.SetParent(parent, false);
        }

        if (wasCreated)
        {
            arrowTransform.localPosition = localPosition;
            arrowTransform.localRotation = Quaternion.identity;
            arrowTransform.localScale = Vector3.one;
        }

        RemoveComponentIfExists<LineRenderer>(arrowTransform.gameObject);

        MeshFilter meshFilter = arrowTransform.GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = arrowTransform.gameObject.AddComponent<MeshFilter>();
        }

        MeshRenderer meshRenderer = arrowTransform.GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            meshRenderer = arrowTransform.gameObject.AddComponent<MeshRenderer>();
        }

        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        Mesh mesh = meshFilter.sharedMesh;
        if (mesh == null)
        {
            mesh = new Mesh();
            mesh.name = pointsUp ? "Arrow Up Mesh" : "Arrow Down Mesh";
            meshFilter.sharedMesh = mesh;
        }

        Vector3[] vertices;
        if (pointsUp)
        {
            vertices = new[]
            {
                new Vector3(-0.28f, -0.16f, 0f),
                new Vector3(0f, 0.18f, 0f),
                new Vector3(0.28f, -0.16f, 0f),
            };
        }
        else
        {
            vertices = new[]
            {
                new Vector3(-0.28f, 0.16f, 0f),
                new Vector3(0f, -0.18f, 0f),
                new Vector3(0.28f, 0.16f, 0f),
            };
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(0.5f, 1f),
            new Vector2(1f, 0f),
        };
        mesh.triangles = new[] { 0, 1, 2 };
        mesh.normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward };
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(0.7f, 0.7f, 0.1f));

        StageSpotlightMaterialUtility.ApplySpotlitRenderer(meshRenderer, new Color(0.45f, 0.8f, 0.9f, 0f), new Color(1f, 0.95f, 0.55f, 1f));
    }

    private static void RemoveLegacyButtonVisuals(Transform buttonRoot, string buttonName)
    {
        if (buttonRoot == null)
        {
            return;
        }

        RemoveChildIfExists(buttonRoot, buttonName + " Text Root");
        RemoveChildIfExists(buttonRoot, "Arrow Glyph");

        for (int index = buttonRoot.childCount - 1; index >= 0; index--)
        {
            Transform child = buttonRoot.GetChild(index);
            if (child == null)
            {
                continue;
            }

            bool isLegacyLabel = child.name == buttonName + " Label";
            bool isMisplacedArrow = child.name == "Arrow Glyph" && child.parent != buttonRoot;
            if (!isLegacyLabel && !isMisplacedArrow)
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

    private static void RemoveComponentIfExists<T>(GameObject target) where T : Component
    {
        if (target == null)
        {
            return;
        }

        T component = target.GetComponent<T>();
        if (component == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Object.Destroy(component);
        }
        else
        {
            Object.DestroyImmediate(component);
        }
    }

    private static void RemoveChildIfExists(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        if (child == null)
        {
            return;
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

    private static void EnsureBillboard(GameObject target)
    {
        FaceCameraBillboard billboard = target.GetComponent<FaceCameraBillboard>();
        if (billboard == null)
        {
            target.AddComponent<FaceCameraBillboard>();
        }
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
            ApplyBuiltInFont(textMesh);
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
        ApplyBuiltInFont(textMesh);

        MeshRenderer renderer = label.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        return label;
    }

    private static void ApplyBuiltInFont(TextMesh textMesh)
    {
        if (textMesh == null)
        {
            return;
        }

        if (builtInFont == null)
        {
            builtInFont = Resources.GetBuiltinResource<Font>(BuiltInFontName);
        }

        if (builtInFont == null)
        {
            return;
        }

        textMesh.font = builtInFont;

        MeshRenderer renderer = textMesh.GetComponent<MeshRenderer>();
        if (renderer != null && builtInFont.material != null)
        {
            renderer.sharedMaterial = builtInFont.material;
        }
    }

    private static void SetRendererColor(GameObject target, Color color)
    {
        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material sharedMaterial = renderer.sharedMaterial;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (sharedMaterial == null)
            {
                if (shader == null)
                {
                    return;
                }

                sharedMaterial = new Material(shader);
                renderer.sharedMaterial = sharedMaterial;
            }
            else
            {
                Material uniqueMaterial = new Material(sharedMaterial);
                uniqueMaterial.name = target.name + " Material";
                renderer.sharedMaterial = uniqueMaterial;
                sharedMaterial = uniqueMaterial;
            }

            if (sharedMaterial.HasProperty("_BaseColor"))
            {
                sharedMaterial.SetColor("_BaseColor", color);
            }

            sharedMaterial.color = color;
        }
    }

    private static void EnsureStageSpotlightSettings(Transform root, bool enabled, float angle, float lightRange, float intensity, Color color)
    {
        if (root == null)
        {
            return;
        }

        StageSpotlightSettings settings = root.GetComponent<StageSpotlightSettings>();
        if (settings == null)
        {
            settings = root.gameObject.AddComponent<StageSpotlightSettings>();
            settings.Configure(enabled, angle, lightRange, intensity, color);
        }
    }

}