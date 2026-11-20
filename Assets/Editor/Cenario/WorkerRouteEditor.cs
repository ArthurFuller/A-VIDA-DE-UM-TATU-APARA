using UnityEditor;
using UnityEngine;

// Os pontos são objetos da cena: podem ser movidos com Undo e salvos após o Play Mode.
internal static class WorkerRouteHandles
{
    public static Transform FindCommonPointB()
    {
        foreach (ActionResolver resolver in Object.FindObjectsByType<ActionResolver>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            SerializedProperty point = new SerializedObject(resolver).FindProperty("workerRoutePointB");
            if (point?.objectReferenceValue is Transform marker) return marker;
        }
        return null;
    }

    public static Transform FindHome(int houseNumber)
    {
        if (houseNumber <= 2)
            foreach (WorkerAssignment worker in Object.FindObjectsByType<WorkerAssignment>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (worker.OriginHouseNumber == houseNumber && worker.HomeMarker != null)
                    return worker.HomeMarker;
        foreach (HouseSite site in Object.FindObjectsByType<HouseSite>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (site.HouseNumber == houseNumber) return site.HomeMarker;
        return null;
    }

    public static void Draw(SerializedObject source, string arrayName, string destinationName,
        Vector3? start = null, Vector3? fallbackEnd = null)
    {
        source.Update();
        SerializedProperty points = source.FindProperty(arrayName);
        SerializedProperty destination = source.FindProperty(destinationName);
        Vector3? previous = start;
        Handles.color = new Color(1f, 0.62f, 0.1f);
        for (int i = 0; i < points.arraySize; i++)
        {
            Transform marker = points.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
            if (marker == null) continue;
            if (previous.HasValue) Handles.DrawAAPolyLine(3f, previous.Value, marker.position);
            MoveHandle(marker, "Caminho " + (i + 1));
            previous = marker.position;
        }
        Transform end = destination.objectReferenceValue as Transform;
        if (end == null)
        {
            if (previous.HasValue && fallbackEnd.HasValue)
                Handles.DrawAAPolyLine(3f, previous.Value, fallbackEnd.Value);
            return;
        }
        if (previous.HasValue) Handles.DrawAAPolyLine(3f, previous.Value, end.position);
        MoveHandle(end, "Trabalho");
    }

    public static void Add(SerializedObject source, Transform parent, Vector3 destination,
        string arrayName, string label, Vector3? firstPosition = null)
    {
        source.Update();
        Add(source, source.FindProperty(arrayName), parent, destination, label, firstPosition);
    }

    public static void Add(SerializedObject source, SerializedProperty points, Transform parent,
        Vector3 destination, string label, Vector3? firstPosition = null)
    {
        int index = points.arraySize;
        Vector3 position = firstPosition ?? destination + Vector3.back;
        if (index > 0 && points.GetArrayElementAtIndex(index - 1).objectReferenceValue is Transform last)
            position = Vector3.Lerp(last.position, destination, 0.5f);
        Transform marker = CreateMarker(parent, label + " - caminho " + (index + 1), position);
        points.InsertArrayElementAtIndex(index);
        points.GetArrayElementAtIndex(index).objectReferenceValue = marker;
        source.ApplyModifiedProperties();
    }

    public static void AddDestination(SerializedObject source, Transform parent, Vector3 position,
        string fieldName, string label)
    {
        Transform marker = CreateMarker(parent, label + " - trabalho", position);
        source.Update();
        source.FindProperty(fieldName).objectReferenceValue = marker;
        source.ApplyModifiedProperties();
    }

    private static Transform CreateMarker(Transform parent, string name, Vector3 position)
    {
        var marker = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(marker, "Criar ponto do trajeto");
        Undo.SetTransformParent(marker.transform, parent, "Agrupar ponto do trajeto");
        marker.transform.position = position;
        return marker.transform;
    }

    public static void MoveHandle(Transform marker, string label)
    {
        Handles.Label(marker.position + Vector3.up * 0.25f, label);
        EditorGUI.BeginChangeCheck();
        Vector3 position = Handles.PositionHandle(marker.position, Quaternion.identity);
        if (!EditorGUI.EndChangeCheck()) return;
        Undo.RecordObject(marker, "Mover ponto do trajeto");
        marker.position = position;
    }
}

[CustomEditor(typeof(HouseSite))]
public sealed class HouseSiteRouteEditor : Editor
{
    private int previewHouse = 1;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var site = (HouseSite)target;
        EditorGUILayout.Space();
        previewHouse = GUILayout.Toolbar(previewHouse - 1,
            new[] { "Casa 1", "Casa 2", "Casa 3", "Casa 4", "Casa 5" }) + 1;
        SerializedProperty selected = SelectedRoute();
        if (selected == null)
        {
            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                if (GUILayout.Button("Preparar rotas dos cinco trabalhadores")) CreateRoutes();
            return;
        }
        EditorGUILayout.PropertyField(selected.FindPropertyRelative("waypoints"),
            new GUIContent("Caminho exclusivo da casa " + previewHouse), true);
        serializedObject.ApplyModifiedProperties();
        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
        {
            if (GUILayout.Button("Adicionar ponto para a casa " + previewHouse))
            {
                Transform home = WorkerRouteHandles.FindHome(previewHouse);
                SerializedProperty shared = serializedObject.FindProperty("constructionWaypoints");
                Transform first = shared.arraySize > 0
                    ? shared.GetArrayElementAtIndex(0).objectReferenceValue as Transform : null;
                Vector3 end = first != null ? first.position : site.ConstructionPosition;
                Vector3? initial = home != null ? Vector3.Lerp(home.position, end, 0.5f) : null;
                WorkerRouteHandles.Add(serializedObject, selected.FindPropertyRelative("waypoints"),
                    site.transform.parent, end, site.name + " - casa " + previewHouse, initial);
            }
            if (GUILayout.Button("Adicionar ponto ao caminho da construção"))
                WorkerRouteHandles.Add(serializedObject, site.transform.parent, site.ConstructionPosition,
                    "constructionWaypoints", site.name);
            if (serializedObject.FindProperty("constructionWorkPoint").objectReferenceValue == null &&
                GUILayout.Button("Criar ponto de trabalho"))
                WorkerRouteHandles.AddDestination(serializedObject, site.transform.parent, site.ConstructionPosition,
                    "constructionWorkPoint", site.name);
        }
    }

    private SerializedProperty SelectedRoute()
    {
        serializedObject.Update();
        SerializedProperty routes = serializedObject.FindProperty("workerConstructionRoutes");
        for (int i = 0; i < routes.arraySize; i++)
        {
            SerializedProperty route = routes.GetArrayElementAtIndex(i);
            if (route.FindPropertyRelative("houseNumber").intValue == previewHouse) return route;
        }
        return null;
    }

    private void CreateRoutes()
    {
        SerializedProperty routes = serializedObject.FindProperty("workerConstructionRoutes");
        routes.arraySize = 5;
        for (int i = 0; i < 5; i++)
        {
            SerializedProperty route = routes.GetArrayElementAtIndex(i);
            route.FindPropertyRelative("houseNumber").intValue = i + 1;
            route.FindPropertyRelative("waypoints").arraySize = 0;
        }
        serializedObject.ApplyModifiedProperties();
    }

    private void OnSceneGUI()
    {
        SerializedProperty selected = SelectedRoute();
        if (selected == null) return;
        Handles.color = new Color(0.2f, 0.9f, 1f);
        Transform home = WorkerRouteHandles.FindHome(previewHouse);
        Vector3? previous = home != null ? home.position : (Vector3?)null;
        if (home != null) Handles.Label(home.position + Vector3.up * 0.3f, "Origem casa " + previewHouse);
        DrawPoints(selected.FindPropertyRelative("waypoints"), ref previous, "Casa " + previewHouse);
        DrawPoints(serializedObject.FindProperty("constructionWaypoints"), ref previous, "Construção");
        Transform end = serializedObject.FindProperty("constructionWorkPoint").objectReferenceValue as Transform;
        if (end == null) return;
        if (previous.HasValue) Handles.DrawAAPolyLine(3f, previous.Value, end.position);
        WorkerRouteHandles.MoveHandle(end, "Trabalho");
    }

    private static void DrawPoints(SerializedProperty points, ref Vector3? previous, string label)
    {
        for (int i = 0; i < points.arraySize; i++)
        {
            Transform marker = points.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
            if (marker == null) continue;
            if (previous.HasValue) Handles.DrawAAPolyLine(3f, previous.Value, marker.position);
            WorkerRouteHandles.MoveHandle(marker, label + " " + (i + 1));
            previous = marker.position;
        }
    }
}

[CustomEditor(typeof(Plot))]
public sealed class PlotRouteEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var plot = (Plot)target;
        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
        {
            if (GUILayout.Button("Adicionar ponto ao caminho deste terreno"))
                WorkerRouteHandles.Add(serializedObject, plot.transform.parent, plot.WorkPosition,
                    "workWaypoints", plot.name);
            if (serializedObject.FindProperty("workPoint").objectReferenceValue == null &&
                GUILayout.Button("Criar ponto de trabalho"))
                WorkerRouteHandles.AddDestination(serializedObject, plot.transform.parent, plot.WorkPosition,
                    "workPoint", plot.name);
        }
    }

    private void OnSceneGUI()
    {
        Transform common = WorkerRouteHandles.FindCommonPointB();
        WorkerRouteHandles.Draw(serializedObject, "workWaypoints", "workPoint",
            common != null ? common.position : (Vector3?)null, ((Plot)target).WorkPosition);
    }
}

[CustomEditor(typeof(ActionResolver))]
public sealed class ActionResolverRouteEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
        {
            AddButton("forestWaypoints", "forestDestination", "Floresta");
            AddButton("fishingWaypoints", "fishingDestination", "Pesca");
            if (serializedObject.FindProperty("workerRoutePointA").objectReferenceValue == null &&
                GUILayout.Button("Criar ponto A"))
                WorkerRouteHandles.AddDestination(serializedObject, ((ActionResolver)target).transform,
                    ((ActionResolver)target).transform.position, "workerRoutePointA", "Funcionários - ponto A");
            if (serializedObject.FindProperty("workerRoutePointB").objectReferenceValue == null &&
                GUILayout.Button("Criar ponto B"))
                WorkerRouteHandles.AddDestination(serializedObject, ((ActionResolver)target).transform,
                    ((ActionResolver)target).transform.position, "workerRoutePointB", "Funcionários - ponto B");
        }
    }

    private void AddButton(string arrayName, string destinationName, string label)
    {
        if (!GUILayout.Button("Adicionar ponto ao caminho da " + label.ToLowerInvariant())) return;
        Transform destination = serializedObject.FindProperty(destinationName).objectReferenceValue as Transform;
        Transform passage = serializedObject.FindProperty("fencePassage").objectReferenceValue as Transform;
        Vector3 position = destination != null ? destination.position : ((ActionResolver)target).transform.position;
        WorkerRouteHandles.Add(serializedObject, ((ActionResolver)target).transform,
            position, arrayName, label, passage != null ? passage.position : (Vector3?)null);
    }

    private void OnSceneGUI()
    {
        serializedObject.Update();
        Transform a = serializedObject.FindProperty("workerRoutePointA").objectReferenceValue as Transform;
        Transform b = serializedObject.FindProperty("workerRoutePointB").objectReferenceValue as Transform;
        Handles.color = new Color(0.2f, 0.9f, 1f);
        for (int house = 1; house <= 5; house++)
        {
            Transform home = WorkerRouteHandles.FindHome(house);
            if (home == null) continue;
            bool viaA = house == 2 || house == 3 || house == 5;
            Transform first = viaA && a != null ? a : b;
            if (first != null) Handles.DrawAAPolyLine(2f, home.position, first.position);
            Handles.Label(home.position + Vector3.up * 0.3f, "Casa " + house);
        }
        if (a != null)
        {
            if (b != null) Handles.DrawAAPolyLine(3f, a.position, b.position);
            WorkerRouteHandles.MoveHandle(a, "Ponto A");
        }
        if (b != null) WorkerRouteHandles.MoveHandle(b, "Ponto B");
        DrawArea("forestWaypoints", "forestDestination", b);
        DrawArea("fishingWaypoints", "fishingDestination", b);
    }

    private void DrawArea(string routeField, string destinationField, Transform common)
    {
        SerializedProperty route = serializedObject.FindProperty(routeField);
        Transform passage = serializedObject.FindProperty("fencePassage").objectReferenceValue as Transform;
        if (route.arraySize == 0 && passage != null)
        {
            if (common != null) Handles.DrawAAPolyLine(2f, common.position, passage.position);
            WorkerRouteHandles.Draw(serializedObject, routeField, destinationField, passage.position);
        }
        else WorkerRouteHandles.Draw(serializedObject, routeField, destinationField,
            common != null ? common.position : (Vector3?)null);
    }
}
