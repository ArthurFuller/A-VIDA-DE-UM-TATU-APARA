using System.Linq;
using UnityEditor;
using UnityEngine;

public static class SertaoPresentationSetup
{
    private const string AssetPath = "Assets/Resources/SertaoPresentationAssets.asset";

    [InitializeOnLoadMethod]
    private static void QueueSetup() => EditorApplication.delayCall += CreateIfMissing;

    private static void CreateIfMissing()
    {
        if (AssetDatabase.LoadAssetAtPath<SertaoPresentationAssets>(AssetPath) == null)
            LinkAssets();
    }

    [MenuItem("Tools/Sertão/Vincular artes de cultivo e aposta")]
    private static void LinkAssets()
    {
        var assets = AssetDatabase.LoadAssetAtPath<SertaoPresentationAssets>(AssetPath);
        bool create = assets == null;
        if (create) assets = ScriptableObject.CreateInstance<SertaoPresentationAssets>();
        var data = new SerializedObject(assets);

        if (!Set<GameObject>("milho", "Assets/Modelos/Milho/Milho_PivoCentral.prefab") ||
            !Set<GameObject>("macaxeira", "Assets/Modelos/Mandioca/Mandioca.prefab") ||
            !Set<GameObject>("jerimum", "Assets/Modelos/Abobora/Abobora.prefab") ||
            !Set<Texture2D>("cara", "Assets/Arte/Efeitos/Moeda_Cara.png") ||
            !Set<Texture2D>("coroa", "Assets/Arte/Efeitos/Moeda_Coroa.png") ||
            !Set<Font>("fonte", "Assets/Fonts/xilosa_.ttf") ||
            !Frames("galinha", "SpriteSheet_Galinha") ||
            !Frames("vaca", "SpriteSheet_Vaca"))
        {
            if (create) Object.DestroyImmediate(assets);
            Debug.LogWarning("Confira as artes da Mandala e da aposta, depois use Tools > Sertão > Vincular artes de cultivo e aposta.");
            return;
        }
        data.ApplyModifiedPropertiesWithoutUndo();
        if (create) AssetDatabase.CreateAsset(assets, AssetPath);
        AssetDatabase.SaveAssets();

        bool Set<T>(string field, string path) where T : Object
        {
            T value = AssetDatabase.LoadAssetAtPath<T>(path);
            data.FindProperty(field).objectReferenceValue = value;
            return value != null;
        }

        bool Frames(string field, string name)
        {
            Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath("Assets/Arte/SpriteSheets/" + name + ".png")
                .OfType<Sprite>().OrderBy(frame => frame.name, System.StringComparer.Ordinal).ToArray();
            if (frames.Length != 10) return false;
            var array = data.FindProperty(field);
            array.arraySize = frames.Length;
            for (int i = 0; i < frames.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
            return true;
        }
    }
}
