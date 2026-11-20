using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SertaoTerrainEditor : EditorWindow
{
    private const string GroundPrefab = "Assets/Modelos/ChaoGrande2/ChaoGrande2.prefab";
    private const string GroundMaterial = "Assets/Modelos/ChaoGrande2/Materiais/M_ChaoGrande2.mat";
    private const string ZonedMaterial = "Assets/Modelos/ChaoGrande2/Materiais/M_ChaoGrande2_Zonas.mat";
    private const string Cactus = "Assets/Modelos/Cactus/Modelo/Cactus.fbx";
    private const string PaintedRoot = "Vegetação pintada";
    private static readonly string[] Palette = { "Matinho", "Árvore 1", "Árvore 2", "Cacto", "Personalizado" };
    private static readonly string[] Prefabs =
    {
        "Assets/Modelos/Grama/Grama.prefab", "Assets/Modelos/Arvore1/Arvore1.prefab",
        "Assets/Modelos/Arvore2/Arvore2_PivoCentral.prefab", Cactus
    };

    [SerializeField] private float margin = 2f;
    [SerializeField] private float surfaceTop = -0.035f;
    [SerializeField] private bool hideOldGround = true;
    [SerializeField] private bool paint;
    [SerializeField] private int kind;
    [SerializeField] private GameObject customPrefab;
    [SerializeField] private MeshCollider surface;
    [SerializeField] private MeshFilter brushMesh;

    private Mesh cachedBrushMesh;
    private Vector3[] brushVertices;
    private int[] brushTriangles;

    private void CacheBrushMesh(Mesh mesh)
    {
        if (cachedBrushMesh == mesh) return;

        using var snapshot = MeshUtility.AcquireReadOnlyMeshData(mesh);
        var data = snapshot[0];
        using var vertices = new Unity.Collections.NativeArray<Vector3>(
            data.vertexCount, Unity.Collections.Allocator.Temp);
        data.GetVertices(vertices);
        var triangles = new List<int>();
        for (int i = 0; i < data.subMeshCount; i++)
        {
            var part = data.GetSubMesh(i);
            if (part.topology != MeshTopology.Triangles) continue;
            using var indices = new Unity.Collections.NativeArray<int>(
                part.indexCount, Unity.Collections.Allocator.Temp);
            data.GetIndices(indices, i);
            triangles.AddRange(indices.ToArray());
        }
        brushVertices = vertices.ToArray();
        brushTriangles = triangles.ToArray();
        cachedBrushMesh = mesh;
    }

    private bool RaycastBrushMesh(Ray ray, MeshFilter filter, out RaycastHit hit, float distance)
    {
        hit = default;
        if (filter.sharedMesh == null) return false;
        CacheBrushMesh(filter.sharedMesh);
        Matrix4x4 toLocal = filter.transform.worldToLocalMatrix;
        Vector3 origin = toLocal.MultiplyPoint3x4(ray.origin);
        Vector3 direction = toLocal.MultiplyVector(ray.direction);
        Vector3 normal = Vector3.zero;
        float nearest = distance;
        bool found = false;

        for (int i = 0; i < brushTriangles.Length; i += 3)
        {
            Vector3 p = brushVertices[brushTriangles[i]];
            Vector3 e1 = brushVertices[brushTriangles[i + 1]] - p;
            Vector3 e2 = brushVertices[brushTriangles[i + 2]] - p;
            Vector3 cross = Vector3.Cross(direction, e2);
            float det = Vector3.Dot(e1, cross);
            if (Mathf.Abs(det) < 0.000000001f) continue;
            float inverse = 1f / det;
            Vector3 offset = origin - p;
            float u = Vector3.Dot(offset, cross) * inverse;
            if (u < 0f || u > 1f) continue;
            Vector3 edge = Vector3.Cross(offset, e1);
            float v = Vector3.Dot(direction, edge) * inverse;
            if (v < 0f || u + v > 1f) continue;
            float t = Vector3.Dot(e2, edge) * inverse;
            if (t < 0f || t > nearest) continue;
            nearest = t;
            normal = Vector3.Cross(e1, e2);
            found = true;
        }

        if (!found) return false;
        normal = toLocal.transpose.MultiplyVector(normal).normalized;
        if (Vector3.Dot(normal, ray.direction) > 0f) normal = -normal;
        hit = new RaycastHit { point = ray.GetPoint(nearest), normal = normal, distance = nearest };
        return true;
    }

    private GameObject BrushGround => brushMesh != null ? brushMesh.gameObject :
        surface != null ? surface.gameObject : null;

    private Bounds BrushBounds
    {
        get
        {
            if (brushMesh == null) return surface.bounds;
            var renderer = brushMesh.GetComponent<MeshRenderer>();
            return renderer != null ? renderer.bounds : new Bounds(brushMesh.transform.position, Vector3.zero);
        }
    }

    private bool RaycastBrush(Ray ray, out RaycastHit hit, float distance)
    {
        hit = default;
        if (brushMesh != null)
            return RaycastBrushMesh(ray, brushMesh, out hit, distance);

        return surface != null && surface.enabled && surface.Raycast(ray, out hit, distance);
    }
    [SerializeField] private float radius = 0.9f;
    [SerializeField] private int plants = 4;
    [SerializeField] private float height = 0.44f;
    [SerializeField] private float variation = 0.19f;
    private string status;

    [MenuItem("Tools/Sertão/Terreno e Vegetação")]
    private static void Open() => GetWindow<SertaoTerrainEditor>("Terreno e Vegetação");

    private void OnEnable() => SceneView.duringSceneGui += PaintInScene;
    private void OnDisable()
    {
        SceneView.duringSceneGui -= PaintInScene;
        SceneView.RepaintAll();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Chão Grande 2", EditorStyles.boldLabel);
        margin = EditorGUILayout.Slider("Margem da câmera", margin, 0.5f, 8f);
        surfaceTop = EditorGUILayout.FloatField("Altura máxima do chão", surfaceTop);
        hideOldGround = EditorGUILayout.ToggleLeft("Ocultar as malhas dos pisos antigos", hideOldGround);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            if (GUILayout.Button("Encaixar Chão Grande 2 na cena")) FitGround();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Vegetação na Scene View", EditorStyles.boldLabel);
        brushMesh = (MeshFilter)EditorGUILayout.ObjectField("Malha do chão", brushMesh, typeof(MeshFilter), true);
        if (GUILayout.Button("Usar chão selecionado"))
        {
            GameObject selected = Selection.activeGameObject;
            var candidate = selected != null && selected.scene.IsValid()
                ? selected.GetComponentInChildren<MeshFilter>() : null;
            if (candidate != null && candidate.sharedMesh != null && candidate.GetComponent<MeshRenderer>() != null)
            {
                brushMesh = candidate;
                cachedBrushMesh = null;
                paint = true;
                status = $"Pincel ligado em {brushMesh.name}. Clique no chão na aba Scene.";
            }
            else
                status = "Selecione na Hierarchy um chão com Mesh Filter e Mesh Renderer.";
            SceneView.RepaintAll();
        }
        if (brushMesh == null)
            surface = (MeshCollider)EditorGUILayout.ObjectField("Colisor do chão", surface, typeof(MeshCollider), true);
        int next = EditorGUILayout.Popup("Vegetação", kind, Palette);
        if (next != kind)
        {
            kind = next;
            height = kind == 0 ? 0.44f : kind == 3 ? 1.15f : 2.35f;
            plants = kind == 0 ? 4 : 1;
        }
        if (kind == Prefabs.Length)
            customPrefab = (GameObject)EditorGUILayout.ObjectField("Prefab", customPrefab, typeof(GameObject), false);
        radius = EditorGUILayout.Slider("Raio", radius, 0.15f, 4f);
        plants = EditorGUILayout.IntSlider("Peças por clique", plants, 1, 12);
        height = Mathf.Max(0.05f, EditorGUILayout.FloatField("Altura", height));
        variation = EditorGUILayout.Slider("Variação de tamanho", variation, 0f, 0.5f);
        paint = EditorGUILayout.ToggleLeft("Pintar com clique esquerdo na cena", paint);
        if (paint) EditorGUILayout.LabelField("Alt + mouse navega pela cena.");

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(Selection.gameObjects.Length == 0 ||
            EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button("Centralizar pivôs dos modelos selecionados"))
                CenterSelected();
        }
        if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.Info);
    }

    private void FitGround()
    {
        Scene scene = SceneManager.GetActiveScene();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GroundPrefab);
        Material source = AssetDatabase.LoadAssetAtPath<Material>(GroundMaterial);
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/XiloCartoon/SertaoChaoGrandeZonas.shader");
        var prefabFilter = prefab != null ? prefab.GetComponent<MeshFilter>() : null;
        Mesh mesh = prefabFilter != null ? prefabFilter.sharedMesh : null;
        if (mesh == null || source == null || shader == null)
        {
            status = "Confira o prefab ChaoGrande2, o material original e o shader de zonas.";
            return;
        }
        if (!shader.isSupported || ShaderUtil.ShaderHasError(shader))
        {
            status = "O shader do Chao Grande 2 ainda apresenta erro. Confira o Console.";
            return;
        }
        CommunityCamera camera = FindInScene<CommunityCamera>(scene);
        ActionResolver actions = FindInScene<ActionResolver>(scene);
        if (camera == null || actions == null)
        {
            status = "Abra a cena da comunidade para encaixar o terreno.";
            return;
        }

        Bounds farm = FarmBounds(scene);
        Transform forestMarker = ReadTransform(actions, "forestDestination");
        Transform fishMarker = ReadTransform(actions, "fishingDestination");
        Vector3 forest = forestMarker != null ? forestMarker.position : new Vector3(-13f, 0f, 2.35f);
        Bounds lake = LakeBounds(scene, fishMarker != null ? fishMarker.position : new Vector3(-15.17f, 0f, -3.46f));
        Bounds cover = Cover(camera, farm, lake, forest, fishMarker != null ? fishMarker.position : lake.center);
        cover.Expand(new Vector3(2f * margin, 0f, 2f * margin));

        int step = BeginUndo("Encaixar Chão Grande 2");
        MeshFilter existing = FindGround(scene, mesh);
        GameObject ground = existing != null ? existing.gameObject :
            (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        if (existing == null) Undo.RegisterCreatedObjectUndo(ground, "Instanciar Chão Grande 2");

        if (!ground.activeSelf)
        {
            Undo.RecordObject(ground, "Mostrar Chão Grande 2");
            ground.SetActive(true);
            PrefabUtility.RecordPrefabInstancePropertyModifications(ground);
        }
        Transform pose = ground.transform;
        Undo.RecordObject(pose, "Enquadrar Chão Grande 2");
        Bounds native = mesh.bounds;
        float stretch = Mathf.Max(1f, cover.size.x / native.size.x, cover.size.z / native.size.z);
        Vector3 inheritedScale = pose.parent != null ? pose.parent.lossyScale : Vector3.one;
        const float relief = 0.22f;
        pose.rotation = Quaternion.identity;
        pose.localScale = new Vector3(stretch / Mathf.Max(0.001f, Mathf.Abs(inheritedScale.x)),
            relief / Mathf.Max(0.001f, Mathf.Abs(inheritedScale.y)),
            stretch / Mathf.Max(0.001f, Mathf.Abs(inheritedScale.z)));
        pose.position = new Vector3(cover.center.x - native.center.x * stretch,
            surfaceTop - native.max.y * relief, cover.center.z - native.center.z * stretch);
        PrefabUtility.RecordPrefabInstancePropertyModifications(pose);

        var renderer = ground.GetComponent<MeshRenderer>();
        var material = AssetDatabase.LoadAssetAtPath<Material>(ZonedMaterial);
        if (material == null)
        {
            material = new Material(source) { name = "M_ChaoGrande2_Zonas", shader = shader };
            material.CopyPropertiesFromMaterial(source);
            AssetDatabase.CreateAsset(material, ZonedMaterial);
        }
        Undo.RecordObject(material, "Posicionar zonas de cor");
        material.SetVector("_FarmZone", Zone(farm.center, farm.extents.x + 3f, farm.extents.z + 3f));
        material.SetVector("_LakeZone", Zone(lake.center, lake.extents.x + 3.5f, lake.extents.z + 3.5f));
        material.SetVector("_ForestZone", Zone(forest, 6f, 5.3f));
        material.SetFloat("_EspessuraLineArt", 0f);
        EditorUtility.SetDirty(material);
        Undo.RecordObject(renderer, "Aplicar material do chão");
        renderer.sharedMaterial = material;
        renderer.enabled = true;
        PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        if (hideOldGround) HidePreviousGround(scene);
        surface = ground.GetComponent<MeshCollider>();
        brushMesh = null;
        Physics.SyncTransforms();
        Undo.CollapseUndoOperations(step);
        Selection.activeGameObject = ground;
        EditorSceneManager.MarkSceneDirty(scene);
        status = $"Chão encaixado ({native.size.x * stretch:F1} × {native.size.z * stretch:F1}). Confira no Game View.";
        SceneView.RepaintAll();
    }

    private static Vector4 Zone(Vector3 center, float x, float z) => new Vector4(center.x, center.z, x, z);

    private static Bounds FarmBounds(Scene scene)
    {
        var area = new Bounds(new Vector3(-0.7f, 0f, 0.15f), new Vector3(1.48f, 0.1f, 1.57f));
        foreach (Plot plot in Object.FindObjectsByType<Plot>(FindObjectsSortMode.None))
            if (plot.gameObject.scene == scene && !plot.IsWater) area.Encapsulate(PlotBounds(plot));
        return area;
    }

    private static Bounds PlotBounds(Plot plot)
    {
        var field = new SerializedObject(plot).FindProperty("surfaceRenderer");
        var renderer = field?.objectReferenceValue as Renderer;
        return renderer != null ? renderer.bounds :
            new Bounds(plot.transform.position, new Vector3(1.48f, 0.14f, 1.57f));
    }

    private static Bounds LakeBounds(Scene scene, Vector3 fallback)
    {
        var best = new Bounds(fallback, new Vector3(4f, 0.1f, 3.5f));
        float area = 0f;
        foreach (MeshRenderer mesh in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if (mesh.gameObject.scene != scene || !PrefabPath(mesh).StartsWith("Assets/Modelos/Lagoa/")) continue;
            Bounds b = mesh.bounds;
            float footprint = b.size.x * b.size.z;
            if (footprint <= area) continue;
            best = b;
            area = footprint;
        }
        return best;
    }

    private static Bounds Cover(CommunityCamera camera, Bounds farm, Bounds lake, Vector3 forest, Vector3 fish)
    {
        Bounds b = farm;
        b.Encapsulate(lake);
        b.Encapsulate(forest);
        SerializedObject data = new SerializedObject(camera);
        float far = data.FindProperty("farthestDistance").floatValue;
        Transform view = camera.ViewTransform;
        float groundAspect = Mathf.Max(21f / 9f, camera.GetComponent<Camera>().aspect);
        if (view.forward.y < -0.001f)
        {
            Vector3 position = view.position + view.forward * (view.position.y / -view.forward.y - far);
            AddView(ref b, position, view.rotation, camera.FieldOfView, groundAspect);
        }
        float pan = data.FindProperty("farZoomPanRadius").floatValue;
        b.Expand(new Vector3(pan * 2f, 0f, pan * 2f));
        if (data.FindProperty("useCustomViewBounds").boolValue)
        {
            Vector2 min = data.FindProperty("minimumView").vector2Value;
            Vector2 max = data.FindProperty("maximumView").vector2Value;
            b.Encapsulate(new Vector3(min.x, 0f, min.y));
            b.Encapsulate(new Vector3(max.x, 0f, max.y));
        }

        var worker = data.FindProperty("workerShot").objectReferenceValue as CinemachineCamera;
        if (worker != null)
        {
            float distance = data.FindProperty("followDistance").floatValue;
            foreach (Vector3 target in new[] { forest, fish })
                foreach (float offset in new[] { -2f, 2f })
                    AddView(ref b, target + Vector3.right * offset - worker.transform.forward * distance,
                        worker.transform.rotation, worker.Lens.FieldOfView, groundAspect);
        }
        return b;
    }

    private static void AddView(ref Bounds b, Vector3 pos, Quaternion rotation, float fov, float aspect)
    {
        float rise = Mathf.Tan(fov * Mathf.Deg2Rad * 0.5f);
        for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            {
                Vector3 direction = rotation * new Vector3(x * rise * aspect, y * rise, 1f);
                if (direction.y < -0.001f)
                    b.Encapsulate(pos + direction * ((-0.08f - pos.y) / direction.y));
            }
    }

    private static Transform ReadTransform(Object obj, string field) =>
        new SerializedObject(obj).FindProperty(field)?.objectReferenceValue as Transform;

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (T obj in Object.FindObjectsByType<T>(FindObjectsSortMode.None))
            if (obj.gameObject.scene == scene) return obj;
        return null;
    }

    private static MeshFilter FindGround(Scene scene, Mesh native)
    {
        foreach (MeshFilter filter in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (filter.gameObject.scene == scene && filter.sharedMesh == native) return filter;
        return null;
    }

    private static string PrefabPath(Component component) =>
        PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(component.gameObject) ?? string.Empty;

    private static void HidePreviousGround(Scene scene)
    {
        foreach (MeshRenderer renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if (renderer.gameObject.scene != scene || !renderer.enabled) continue;
            const string legacyFolder = "Assets/Modelos/ChaoAreia/";
            var filter = renderer.GetComponent<MeshFilter>();
            string meshPath = filter != null && filter.sharedMesh != null
                ? AssetDatabase.GetAssetPath(filter.sharedMesh) : string.Empty;
            if (!PrefabPath(renderer).StartsWith(legacyFolder, System.StringComparison.OrdinalIgnoreCase) &&
                !meshPath.StartsWith(legacyFolder, System.StringComparison.OrdinalIgnoreCase)) continue;
            Undo.RecordObject(renderer, "Ocultar piso visual antigo");
            renderer.enabled = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }
    }

    private void PaintInScene(SceneView view)
    {
        Event input = Event.current;
        var target = BrushGround;
        if (!paint || EditorApplication.isPlayingOrWillChangePlaymode || target == null ||
            !target.scene.IsValid() || !target.activeInHierarchy || input.alt) return;

        int id = GUIUtility.GetControlID(FocusType.Passive);
        if (input.type == EventType.Layout)
        {
            HandleUtility.AddDefaultControl(id);
            return;
        }
        if (input.type == EventType.MouseMove) view.Repaint();
        bool click = input.type == EventType.MouseDown && input.button == 0;
        if (!click && input.type != EventType.Repaint) return;

        var ray = HandleUtility.GUIPointToWorldRay(input.mousePosition);
        bool hitGround = RaycastBrush(ray, out RaycastHit hit, float.PositiveInfinity);
        if (click)
        {
            if (hitGround) PlaceVegetation(hit.point);
            else status = $"O clique ficou fora da malha de {target.name}.";
            input.Use();
            Repaint();
            return;
        }
        if (hitGround)
        {
            Handles.color = new Color(0.79f, 0.86f, 0.54f, 1f);
            Handles.DrawWireDisc(hit.point + hit.normal * 0.02f, hit.normal, radius);
        }
    }

    private void PlaceVegetation(Vector3 center)
    {
        GameObject source = kind < Prefabs.Length
            ? AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs[kind]) : customPrefab;
        if (source == null || !EditorUtility.IsPersistent(source))
        {
            status = "Selecione um prefab de vegetação na pasta Assets.";
            return;
        }
        Scene scene = BrushGround.scene;
        Bounds groundBounds = BrushBounds;
        int step = BeginUndo("Pintar vegetação");
        Transform group = null;
        var locations = new List<Vector3>(plants);
        float minGap = kind == 0 ? 0.23f : 0.65f;
        for (int i = 0; i < plants * 9 && locations.Count < plants; i++)
        {
            Vector2 offset = i == 0 ? Vector2.zero : Random.insideUnitCircle * radius;
            Vector3 above = new Vector3(center.x + offset.x, groundBounds.max.y + 8f, center.z + offset.y);
            if (!RaycastBrush(new Ray(above, Vector3.down), out RaycastHit hit, 20f + groundBounds.size.y) ||
                locations.Exists(other => (other - hit.point).sqrMagnitude < minGap * minGap)) continue;
            if (group == null) group = LandscapeRoot(scene);
            if (Stamp(source, group, hit.point)) locations.Add(hit.point);
        }
        Undo.CollapseUndoOperations(step);
        if (locations.Count > 0) EditorSceneManager.MarkSceneDirty(scene);
        status = locations.Count > 0 ? $"{locations.Count} peça(s) colocada(s)." :
            "Nenhuma peça foi colocada neste ponto da malha.";
    }

    private static Transform LandscapeRoot(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == PaintedRoot) return root.transform;
        var group = new GameObject(PaintedRoot);
        if (group.scene != scene) SceneManager.MoveGameObjectToScene(group, scene);
        Undo.RegisterCreatedObjectUndo(group, "Criar grupo de vegetação");
        return group.transform;
    }

    private bool Stamp(GameObject prefab, Transform group, Vector3 point)
    {
        string category = prefab.name switch
        {
            "Grama" => "Gramas",
            "Cactus" => "Cactos",
            "Arvore1" or "Arvore2_PivoCentral" => "Árvores",
            _ => null
        };
        if (category != null) group = group.Find(category) ?? group;
        var pivot = new GameObject(prefab.name + " - base");
        Undo.RegisterCreatedObjectUndo(pivot, "Criar pivô na base");
        Undo.SetTransformParent(pivot.transform, group, "Agrupar vegetação");
        Undo.RecordObject(pivot.transform, "Ajustar pivô da vegetação");
        pivot.transform.SetPositionAndRotation(point, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, pivot.transform);
        if (model == null)
        {
            Undo.DestroyObjectImmediate(pivot);
            return false;
        }
        Undo.RegisterCreatedObjectUndo(model, "Colocar vegetação");
        Undo.RecordObjects(new Object[] { pivot.transform, model.transform }, "Ajustar modelo da vegetação");
        model.transform.localPosition = Vector3.zero;
        if (!TryBounds(model, out Bounds b) || b.size.y < 0.0001f)
        {
            Undo.DestroyObjectImmediate(pivot);
            return false;
        }
        if (AssetDatabase.GetAssetPath(prefab) == Cactus)
        {
            var cactusMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Modelos/Cactus/Materiais/M_Cactus.mat");
            if (cactusMaterial != null)
                foreach (Renderer mesh in model.GetComponentsInChildren<Renderer>())
                {
                    Undo.RecordObject(mesh, "Aplicar material do cacto");
                    mesh.sharedMaterial = cactusMaterial;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(mesh);
                }
        }

        model.transform.position += point - new Vector3(b.center.x, b.min.y, b.center.z);
        pivot.transform.localScale = Vector3.one * (height * Random.Range(1f - variation, 1f + variation) / b.size.y);
        PrefabUtility.RecordPrefabInstancePropertyModifications(model.transform);
        return true;
    }

    private void CenterSelected()
    {
        var handled = new HashSet<GameObject>();
        var result = new List<Object>();
        int step = BeginUndo("Centralizar pivôs");
        foreach (Transform selected in Selection.GetTransforms(SelectionMode.TopLevel | SelectionMode.Editable))
        {
            GameObject obj = PrefabUtility.GetOutermostPrefabInstanceRoot(selected.gameObject) ?? selected.gameObject;
            if (!handled.Add(obj) || obj.GetComponentInChildren<Plot>(true) != null ||
                obj.GetComponentInChildren<WorkerAssignment>(true) != null ||
                obj.GetComponentInChildren<Camera>(true) != null ||
                !TryBounds(obj, out Bounds b)) continue;
            Transform model = obj.transform;
            if ((model.position - b.center).sqrMagnitude < 0.000001f) continue;
            Transform parent = model.parent;
            int sibling = model.GetSiblingIndex();
            Vector3 localPosition = model.localPosition;
            Quaternion localRotation = model.localRotation;
            Vector3 localScale = model.localScale;
            var pivot = new GameObject(obj.name + " - pivô central");
            if (pivot.scene != obj.scene) SceneManager.MoveGameObjectToScene(pivot, obj.scene);
            Undo.RegisterCreatedObjectUndo(pivot, "Criar pivô central");
            Undo.SetTransformParent(pivot.transform, parent, "Posicionar pivô");
            Undo.RecordObject(pivot.transform, "Ajustar pivô central");
            pivot.transform.localRotation = Quaternion.identity;
            pivot.transform.localScale = Vector3.one;
            pivot.transform.position = b.center;
            pivot.transform.SetSiblingIndex(sibling);
            Undo.SetTransformParent(model, pivot.transform, "Mover pivô sem deslocar modelo");
            Undo.RecordObject(model, "Compensar posição do modelo");
            model.localPosition = localPosition - pivot.transform.localPosition;
            model.localRotation = localRotation;
            model.localScale = localScale;
            PrefabUtility.RecordPrefabInstancePropertyModifications(model);
            result.Add(pivot);
            EditorSceneManager.MarkSceneDirty(obj.scene);
        }
        Undo.CollapseUndoOperations(step);
        if (result.Count > 0) Selection.objects = result.ToArray();
        status = $"{result.Count} pivô(s) centralizado(s).";
    }

    private static bool TryBounds(GameObject obj, out Bounds b)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        b = default;
        if (renderers.Length == 0) return false;
        b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return true;
    }

    private static int BeginUndo(string name)
    {
        Undo.IncrementCurrentGroup();
        int id = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(name);
        return id;
    }
}
