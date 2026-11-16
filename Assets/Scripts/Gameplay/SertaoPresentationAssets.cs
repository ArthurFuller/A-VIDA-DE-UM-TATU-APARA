using DG.Tweening;
using UnityEngine;

public sealed class SertaoPresentationAssets : ScriptableObject
{
    [SerializeField] private GameObject milho, macaxeira, jerimum;
    [SerializeField] private Sprite[] galinha, vaca;
    [SerializeField] private Texture2D cara, coroa;
    [SerializeField] private Font fonte;

    private static SertaoPresentationAssets current;
    public static SertaoPresentationAssets Current =>
        current != null ? current : current = Resources.Load<SertaoPresentationAssets>("SertaoPresentationAssets");

    public Texture2D Heads => cara;
    public Texture2D Tails => coroa;
    public Font Font => fonte;
    public Sprite[] AnimalFrames(AnimalKind kind) => kind == AnimalKind.Cow ? vaca : galinha;

    private readonly struct PlantingCell
    {
        public readonly Vector2 Center;
        public readonly Vector2 Span;
        public readonly float Yaw;

        public PlantingCell(float x, float z, float width, float depth, float yaw = 0f)
        {
            Center = new Vector2(x, z);
            Span = new Vector2(width, depth);
            Yaw = yaw;
        }
    }

    // As posições e as áreas são proporcionais ao tamanho do slot.
    private static readonly PlantingCell[] CornRows =
    {
        new PlantingCell(0f, -0.27f, 0.86f, 0.22f),
        new PlantingCell(0f, 0f, 0.86f, 0.22f),
        new PlantingCell(0f, 0.27f, 0.86f, 0.22f)
    };

    private static readonly PlantingCell[] Cassava =
    {
        new PlantingCell(-0.22f, 0.17f, 0.35f, 0.31f, -18f),
        new PlantingCell(0.22f, 0.17f, 0.35f, 0.31f, 23f),
        new PlantingCell(0.035f, -0.20f, 0.35f, 0.31f, 7f)
    };

    private static readonly PlantingCell[] Pumpkin =
    {
        new PlantingCell(-0.215f, -0.155f, 0.35f, 0.33f, -16f),
        new PlantingCell(0.215f, 0.155f, 0.35f, 0.33f, 20f)
    };

    public Transform CreateCrop(CropType crop, Transform plot, Bounds ground)
    {
        GameObject prefab;
        PlantingCell[] layout;
        float height;
        switch (crop)
        {
            case CropType.Corn: prefab = milho; layout = CornRows; height = 1.03f; break;
            case CropType.Pumpkin: prefab = macaxeira; layout = Cassava; height = 0.53f; break;
            case CropType.Jerimum: prefab = jerimum; layout = Pumpkin; height = 0.47f; break;
            default: return null;
        }
        if (prefab == null) return null;

        var root = new GameObject(prefab.name + " - cultivo").transform;
        root.SetParent(plot, false);
        root.localPosition = Vector3.up * 0.065f;
        Vector3 plotSize = ground.size;
        Vector3 scale = plot.lossyScale;
        var size = new Vector2(
            plotSize.x > 0f ? plotSize.x / Mathf.Max(0.001f, Mathf.Abs(scale.x)) : 1.48f,
            plotSize.z > 0f ? plotSize.z / Mathf.Max(0.001f, Mathf.Abs(scale.z)) : 1.57f);
        for (int i = 0; i < layout.Length; i++)
        {
            if (FitPlant(root, prefab, layout[i], size, height, crop == CropType.Corn, i + 1)) continue;
            Destroy(root.gameObject);
            return null;
        }
        return root;
    }

    private static bool FitPlant(Transform root, GameObject prefab, PlantingCell cell,
        Vector2 plotSize, float maxHeight, bool alignCorn, int number)
    {
        Vector2 position = Vector2.Scale(cell.Center, plotSize);
        Vector2 span = Vector2.Scale(cell.Span, plotSize);
        var basePivot = new GameObject("Base " + number).transform;
        basePivot.SetParent(root, false);
        basePivot.localPosition = new Vector3(position.x, 0f, position.y);
        basePivot.localRotation = Quaternion.Euler(0f, cell.Yaw, 0f);

        Transform model = Instantiate(prefab, basePivot, false).transform;
        model.localPosition = Vector3.zero;
        MeshFilter[] meshes = model.GetComponentsInChildren<MeshFilter>();
        if (!TryBounds(root, meshes, out Bounds bounds)) return false;
        if (alignCorn && bounds.size.z > bounds.size.x)
        {
            basePivot.localRotation = Quaternion.Euler(0f, 90f, 0f);
            TryBounds(root, meshes, out bounds);
        }

        float scale = Mathf.Min(maxHeight / Mathf.Max(0.001f, bounds.size.y),
            span.x / Mathf.Max(0.001f, bounds.size.x), span.y / Mathf.Max(0.001f, bounds.size.z));
        model.localScale *= scale;
        TryBounds(root, meshes, out bounds);
        var foot = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        model.position += root.TransformVector(basePivot.localPosition - foot);
        basePivot.localScale = new Vector3(1f, alignCorn ? 1.14f : 1f, 1f) * 0.38f;
        return true;
    }

    private static bool TryBounds(Transform root, MeshFilter[] meshes, out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        foreach (MeshFilter filter in meshes)
        {
            if (filter.sharedMesh == null || !filter.TryGetComponent(out MeshRenderer renderer) ||
                !renderer.enabled) continue;
            Bounds local = filter.sharedMesh.bounds;
            Matrix4x4 toRoot = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = local.center + Vector3.Scale(local.extents, new Vector3(x, y, z));
                        Vector3 point = toRoot.MultiplyPoint3x4(corner);
                        if (found) bounds.Encapsulate(point);
                        else
                        {
                            bounds = new Bounds(point, Vector3.zero);
                            found = true;
                        }
                    }
        }
        return found;
    }

    public static void AnimateCropGrowth(Transform root, float progress)
    {
        float scale = Mathf.Lerp(0.40f, 1f, progress);
        float from = root.GetChild(0).localScale.x;
        if (Mathf.Approximately(from, scale)) return;
        root.DOKill();
        DOVirtual.Float(from, scale, 0.38f, value =>
        {
            foreach (Transform plant in root)
            {
                Vector3 localScale = plant.localScale;
                plant.localScale = localScale * (value / Mathf.Max(0.001f, localScale.x));
            }
        }).SetEase(Ease.OutCubic).SetTarget(root).SetLink(root.gameObject);
    }
}
