using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI; // For GUI text styling

public class SelectionBoxController : MonoBehaviour
{
    [Header("References")]
    public Transform terrain;
    public string terrainLayerName = "Terrain";
    public RTSCameraController cameraController;

    [Header("Selection Settings")]
    public float pointSpacing = 1f;
    public float clickThreshold = 5f;
    public LayerMask selectableLayer;

    [Header("Smoothing Settings")]
    public bool useSmoothing = true;
    public int interpolationSteps = 10;

    public List<GameObject> selectedObjects = new List<GameObject>();
    public float lineOffset = 0.1f;

    private List<GameObject> lastSelectedObjects = new List<GameObject>();



    [Header("2D Selection Box Settings")]
    public Color selectionBoxFillColor = new Color(1, 0, 0, 0.3f);    // Red with 30% opacity
    public Color selectionBoxOutlineColor = Color.red;

    public Color outlineColor = Color.white;

    // --- 3D selection (normal) variables ---
    bool isSelecting = false;
    bool isDragging = false;
    Vector3 startWorldPoint;
    Vector3 currentWorldPoint;
    Vector3 startScreenPoint;
    LineRenderer lineRenderer;

    // 2D selection variables.
    bool is2DSelecting = false;
    Vector2 startScreenPoint2D;
    Vector2 currentScreenPoint2D;

    // --- Polygon Selection Mode variables ---
    bool polygonSelectionMode = false;  // Toggled by pressing S.
    bool polygonDrawing = false;        // Right mouse drag in progress.
    bool polygonCompleted = false;      // True once a polygon has been drawn.
    List<Vector3> polygonVertices = new List<Vector3>();  // The polygon’s vertices.
    List<GameObject> vertexMarkers = new List<GameObject>(); // Visual markers for vertices.

    // For edge/vertex interactions.
    int hoveredEdgeIndex = -1;
    float edgeHoverThreshold = 1f;    // How close (in world units) the mouse must be to an edge.
    float vertexHoverThreshold = 1f;  // How close the mouse must be to a vertex marker.

    // For dragging vertices.
    bool isDraggingVertex = false;
    int draggedVertexIndex = -1;

    // Default and highlight colors for the main line renderer.
    Color defaultLineColor = Color.white;
    Color highlightLineColor = new Color(1f, 0.5f, 0f);

    void Awake()
    {
        if (pointSpacing <= 0f)
            pointSpacing = 1f;

        // Setup our primary line renderer.
        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null)
            lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startWidth = 0.5f;
        lineRenderer.endWidth = 0.5f;
        AnimationCurve curve = new AnimationCurve();
        curve.AddKey(0f, 0.5f);
        curve.AddKey(1f, 0.5f);
        lineRenderer.widthCurve = curve;
        lineRenderer.loop = true;
        lineRenderer.positionCount = 0;
        lineRenderer.sortingLayerName = "UI";
        lineRenderer.sortingOrder = 1000;
        lineRenderer.useWorldSpace = true;  // Keep in world space
        lineRenderer.startColor = defaultLineColor;
        lineRenderer.endColor = defaultLineColor;
    }

    void Update()
    {
        if (Camera.main == null)
            return;

        // --- Toggle Polygon Mode ---
        if (Input.GetKeyDown(KeyCode.S))
        {
            polygonSelectionMode = !polygonSelectionMode;
            if (!polygonSelectionMode)
            {
                // Exiting polygon mode: clear polygon data.
                polygonDrawing = false;
                polygonCompleted = false;
                polygonVertices.Clear();
                ClearVertexMarkers();
                lineRenderer.positionCount = 0;
                // Reset the line color
                lineRenderer.startColor = defaultLineColor;
                lineRenderer.endColor = defaultLineColor;
                // Ensure camera rotation is enabled.
                if (cameraController != null)
                    cameraController.blockRotation = false;
            }
        }

        // Process either Normal or Polygon selection.
        if (polygonSelectionMode)
            HandlePolygonSelection();
        else
            HandleNormalSelection();
    }

    #region Normal Selection

    void HandleNormalSelection()
    {
        // Start selection
        if (Input.GetMouseButtonDown(1))
        {
            if (Input.GetKey(KeyCode.LeftAlt))
            {
                is2DSelecting = true;
                startScreenPoint2D = Input.mousePosition;
                isSelecting = false;
                ClearLine();
            }
            else
            {
                isSelecting = true;
                isDragging = false;
                startScreenPoint = Input.mousePosition;

                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;
                TerrainCollider tc = terrain.GetComponent<TerrainCollider>();

                if (tc != null && tc.Raycast(ray, out hit, 1000f))
                    startWorldPoint = hit.point;
                else
                {
                    Plane plane = new Plane(Vector3.up, Vector3.zero);
                    float distance;
                    startWorldPoint = plane.Raycast(ray, out distance) ? ray.GetPoint(distance) : ray.origin;
                }

                if (terrain != null)
                {
                    Terrain t = terrain.GetComponent<Terrain>();
                    if (t != null)
                    {
                        Vector3 terrainPos = terrain.position;
                        Vector3 terrainSize = t.terrainData.size;
                        startWorldPoint.x = Mathf.Clamp(startWorldPoint.x, terrainPos.x, terrainPos.x + terrainSize.x);
                        startWorldPoint.z = Mathf.Clamp(startWorldPoint.z, terrainPos.z, terrainPos.z + terrainSize.z);
                    }
                }
                ClearLine();
            }
        }

        // Update selection box while dragging
        if (Input.GetMouseButton(1))
        {
            if (is2DSelecting)
                currentScreenPoint2D = Input.mousePosition;
            else if (isSelecting)
            {
                if (Vector3.Distance(Input.mousePosition, startScreenPoint) > clickThreshold)
                {
                    Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                    RaycastHit hit;
                    TerrainCollider tc = terrain.GetComponent<TerrainCollider>();
                    if (tc != null && tc.Raycast(ray, out hit, 1000f))
                    {
                        isDragging = true;
                        currentWorldPoint = hit.point;
                        DrawSelectionBox();
                    }
                    else
                        ClearLine();
                }
                else
                    ClearLine();
            }
        }

        // End selection
        if (Input.GetMouseButtonUp(1))
        {
            if (is2DSelecting)
            {
                SelectObjectsInScreenRect();
                is2DSelecting = false;
            }
            else if (isSelecting)
            {
                if (isDragging)
                    SelectObjectsInRectangle();
                else
                    SingleSelect();

                isSelecting = false;
                isDragging = false;
                ClearLine();
                UpdateSelectionOutlines();
            }
        }
    }

    #endregion

    #region Polygon Selection Mode

    void HandlePolygonSelection()
    {
        // --- 1. Start & Draw the Polygon as a Selection Box ---
        if (Input.GetMouseButtonDown(1))
        {
            // Only start drawing if we haven't finalized a polygon yet.
            if (!polygonCompleted)
            {
                polygonDrawing = true;
                startScreenPoint = Input.mousePosition;

                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;
                TerrainCollider tc = terrain.GetComponent<TerrainCollider>();
                if (tc != null && tc.Raycast(ray, out hit, 1000f))
                    startWorldPoint = hit.point;
                else
                {
                    Plane plane = new Plane(Vector3.up, Vector3.zero);
                    float distance;
                    startWorldPoint = plane.Raycast(ray, out distance) ? ray.GetPoint(distance) : ray.origin;
                }
            }
        }

        if (Input.GetMouseButton(1) && polygonDrawing)
        {
            // As the user drags, update the selection box using the same method.
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (terrain.GetComponent<TerrainCollider>().Raycast(ray, out hit, 1000f))
            {
                currentWorldPoint = hit.point;
                DrawSelectionBox(); // Re-use the same selection box drawing as normal mode.
            }
        }

        if (Input.GetMouseButtonUp(1) && polygonDrawing)
        {
            // When right click is released, finalize the polygon as a rectangle with 4 edges.
            polygonDrawing = false;
            polygonCompleted = true;

            float minX = Mathf.Min(startWorldPoint.x, currentWorldPoint.x);
            float maxX = Mathf.Max(startWorldPoint.x, currentWorldPoint.x);
            float minZ = Mathf.Min(startWorldPoint.z, currentWorldPoint.z);
            float maxZ = Mathf.Max(startWorldPoint.z, currentWorldPoint.z);

            Vector3 bottomLeft = new Vector3(minX, GetTerrainHeight(new Vector3(minX, 0, minZ)), minZ);
            Vector3 bottomRight = new Vector3(maxX, GetTerrainHeight(new Vector3(maxX, 0, minZ)), minZ);
            Vector3 topRight = new Vector3(maxX, GetTerrainHeight(new Vector3(maxX, 0, maxZ)), maxZ);
            Vector3 topLeft = new Vector3(minX, GetTerrainHeight(new Vector3(minX, 0, maxZ)), maxZ);

            polygonVertices = new List<Vector3>() { bottomLeft, bottomRight, topRight, topLeft };
            UpdatePolygonLine();  // Update the polygon line with these four edges.
            CreateVertexMarkers();
        }

        // --- 2. Edit the Polygon (if completed) ---
        if (polygonCompleted)
        {
            // Get the mouse's world point on the terrain.
            Ray rayEdit = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hitEdit;
            Vector3 mouseWorld = Vector3.zero;
            if (terrain.GetComponent<TerrainCollider>().Raycast(rayEdit, out hitEdit, 1000f))
                mouseWorld = hitEdit.point;
            else
            {
                Plane plane = new Plane(Vector3.up, Vector3.zero);
                float distance;
                mouseWorld = plane.Raycast(rayEdit, out distance) ? rayEdit.GetPoint(distance) : rayEdit.origin;
            }

            // Check if the mouse is near any vertex marker.
            int vertexHoverIndex = -1;
            for (int i = 0; i < polygonVertices.Count; i++)
            {
                if (Vector3.Distance(new Vector3(polygonVertices[i].x, 0, polygonVertices[i].z),
                                     new Vector3(mouseWorld.x, 0, mouseWorld.z)) < vertexHoverThreshold)
                {
                    vertexHoverIndex = i;
                    break;
                }
            }

            // If hovering near a vertex, allow dragging it.
            if (vertexHoverIndex != -1)
            {
                if (Input.GetMouseButtonDown(0) && !isDraggingVertex)
                {
                    isDraggingVertex = true;
                    draggedVertexIndex = vertexHoverIndex;
                }
            }

            if (isDraggingVertex)
            {
                // Update the dragged vertex.
                polygonVertices[draggedVertexIndex] = new Vector3(mouseWorld.x, GetTerrainHeight(mouseWorld), mouseWorld.z);
                UpdatePolygonLine();
                UpdateVertexMarkerPosition(draggedVertexIndex);
                // While dragging, ensure the line shows default color.
                lineRenderer.startColor = defaultLineColor;
                lineRenderer.endColor = defaultLineColor;
                // Block camera rotation while dragging.
                if (cameraController != null)
                    cameraController.blockRotation = true;
                if (Input.GetMouseButtonUp(0))
                {
                    isDraggingVertex = false;
                    draggedVertexIndex = -1;
                    if (cameraController != null)
                        cameraController.blockRotation = false;
                }
            }
            else
            {
                // If not dragging a vertex, check if we’re hovering over an edge.
                hoveredEdgeIndex = -1;
                for (int i = 0; i < polygonVertices.Count; i++)
                {
                    int nextIndex = (i + 1) % polygonVertices.Count;
                    // Compare only the XZ coordinates.
                    float dist = DistancePointToLineSegment(
                        new Vector3(mouseWorld.x, 0, mouseWorld.z),
                        new Vector3(polygonVertices[i].x, 0, polygonVertices[i].z),
                        new Vector3(polygonVertices[nextIndex].x, 0, polygonVertices[nextIndex].z)
                    );
                    if (dist < edgeHoverThreshold)
                    {
                        hoveredEdgeIndex = i;
                        break;
                    }
                }

                // Highlight the edge by changing the line color if needed.
                if (hoveredEdgeIndex != -1)
                {
                    lineRenderer.startColor = highlightLineColor;
                    lineRenderer.endColor = highlightLineColor;
                    // Also block camera rotation when hovering over an edge with left mouse held.
                    if (Input.GetMouseButton(0) && cameraController != null)
                        cameraController.blockRotation = true;
                    else if (cameraController != null)
                        cameraController.blockRotation = false;
                }
                else
                {
                    lineRenderer.startColor = defaultLineColor;
                    lineRenderer.endColor = defaultLineColor;
                    if (cameraController != null)
                        cameraController.blockRotation = false;
                }

                // If an edge is hovered and the user left-clicks, add a new vertex.
                if (hoveredEdgeIndex != -1 && Input.GetMouseButtonDown(0))
                {
                    int insertIndex = hoveredEdgeIndex + 1;
                    Vector3 newVertex = new Vector3(mouseWorld.x, GetTerrainHeight(mouseWorld), mouseWorld.z);
                    polygonVertices.Insert(insertIndex, newVertex);
                    CreateVertexMarkerAt(insertIndex, newVertex);
                    UpdatePolygonLine();
                }
            }
        }
    }

    void UpdatePolygonLine()
    {
        if (polygonVertices.Count < 2)
            return;

        List<Vector3> polyPoints = new List<Vector3>();

        // Loop through each edge of the polygon.
        for (int i = 0; i < polygonVertices.Count; i++)
        {
            int next = (i + 1) % polygonVertices.Count;
            List<Vector3> edgePoints = new List<Vector3>();

            // Subdivide the edge between polygonVertices[i] and polygonVertices[next].
            AddEdgePoints(edgePoints, polygonVertices[i], polygonVertices[next]);

            // Remove the first point for all but the first edge to avoid duplicates.
            if (i > 0 && edgePoints.Count > 0)
                edgePoints.RemoveAt(0);

            polyPoints.AddRange(edgePoints);
        }

        // Ensure the loop is closed.
        if (polyPoints.Count > 0 && polyPoints[0] != polyPoints[polyPoints.Count - 1])
            polyPoints.Add(polyPoints[0]);

        // Update the line renderer with the subdivided points.
        lineRenderer.positionCount = polyPoints.Count;
        for (int i = 0; i < polyPoints.Count; i++)
        {
            Vector3 pos = polyPoints[i];
            pos.y += lineOffset; // Ensure the line is slightly above the terrain.
            lineRenderer.SetPosition(i, pos);
        }
    }

    void CreateVertexMarkers()
    {
        ClearVertexMarkers();
        for (int i = 0; i < polygonVertices.Count; i++)
        {
            CreateVertexMarkerAt(i, polygonVertices[i]);
        }
    }

    void CreateVertexMarkerAt(int index, Vector3 position)
    {
        // Create the marker using the standard primitive settings.
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        // Increase the scale for better visibility.
        marker.transform.position = new Vector3(position.x, GetTerrainHeight(position) + lineOffset, position.z);
        marker.transform.localScale = Vector3.one * 1.5f;
        // Disable its collider so it doesn’t interfere.
        Collider col = marker.GetComponent<Collider>();
        if (col != null)
            col.enabled = false;
        // Attach an unlit white material.
        Renderer rend = marker.GetComponent<Renderer>();
        rend.material = new Material(Shader.Find("Unlit/Color"));
        rend.material.color = Color.white;
        vertexMarkers.Insert(index, marker);
    }

    void UpdateVertexMarkerPosition(int index)
    {
        if (index < 0 || index >= vertexMarkers.Count)
            return;
        Vector3 pos = polygonVertices[index];
        pos.y = GetTerrainHeight(new Vector3(pos.x, 0, pos.z)) + lineOffset;
        vertexMarkers[index].transform.position = pos;
    }

    void ClearVertexMarkers()
    {
        foreach (GameObject marker in vertexMarkers)
        {
            Destroy(marker);
        }
        vertexMarkers.Clear();
    }

    float DistancePointToLineSegment(Vector3 point, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        Vector3 ap = point - a;
        float t = Vector3.Dot(ap, ab) / (ab.sqrMagnitude);
        t = Mathf.Clamp01(t);
        Vector3 closest = a + t * ab;
        return Vector3.Distance(point, closest);
    }

    #endregion

    #region Selection Box, Outlines & Other Helpers

    void DrawSelectionBox()
    {
        float minX = Mathf.Min(startWorldPoint.x, currentWorldPoint.x);
        float maxX = Mathf.Max(startWorldPoint.x, currentWorldPoint.x);
        float minZ = Mathf.Min(startWorldPoint.z, currentWorldPoint.z);
        float maxZ = Mathf.Max(startWorldPoint.z, currentWorldPoint.z);
        if (Mathf.Approximately(minX, maxX) && Mathf.Approximately(minZ, maxZ))
        {
            ClearLine();
            return;
        }
        Vector3 bottomLeft = new Vector3(minX, GetTerrainHeight(new Vector3(minX, 0, minZ)), minZ);
        Vector3 bottomRight = new Vector3(maxX, GetTerrainHeight(new Vector3(maxX, 0, minZ)), minZ);
        Vector3 topRight = new Vector3(maxX, GetTerrainHeight(new Vector3(maxX, 0, maxZ)), maxZ);
        Vector3 topLeft = new Vector3(minX, GetTerrainHeight(new Vector3(minX, 0, maxZ)), maxZ);

        List<Vector3> points = new List<Vector3>();
        AddEdgePoints(points, bottomLeft, bottomRight);
        AddEdgePoints(points, bottomRight, topRight);
        AddEdgePoints(points, topRight, topLeft);
        AddEdgePoints(points, topLeft, bottomLeft);

        List<Vector3> finalPoints = useSmoothing && points.Count >= 4 ? SmoothCurve(points, interpolationSteps) : points;
        UpdateLineRenderer(finalPoints);
    }

    void UpdateLineRenderer(List<Vector3> positions)
    {
        if (positions == null || positions.Count == 0)
        {
            lineRenderer.positionCount = 0;
            return;
        }
        lineRenderer.positionCount = positions.Count;
        for (int i = 0; i < positions.Count; i++)
        {
            Vector3 pos = positions[i];
            pos.y += lineOffset;
            lineRenderer.SetPosition(i, pos);
        }
    }

    void UpdateSelectionOutlines()
    {
        foreach (GameObject obj in lastSelectedObjects)
        {
            if (!selectedObjects.Contains(obj))
            {
                Outline outline = obj.GetComponent<Outline>();
                if (outline != null)
                    outline.enabled = false;
            }
        }
        foreach (GameObject obj in selectedObjects)
        {
            Outline outline = obj.GetComponent<Outline>();
            if (outline == null)
            {
                outline = obj.AddComponent<Outline>();
                outline.OutlineMode = Outline.Mode.OutlineAll;
                outline.OutlineColor = outlineColor;
                outline.OutlineWidth = 5f;
            }
            else
                outline.enabled = true;
        }
        lastSelectedObjects = new List<GameObject>(selectedObjects);
    }

    List<Vector3> SmoothCurve(List<Vector3> points, int steps)
    {
        List<Vector3> smoothed = new List<Vector3>();
        int count = points.Count;
        if (count < 4)
            return new List<Vector3>(points);
        for (int i = 0; i < count; i++)
        {
            Vector3 p0 = points[(i - 1 + count) % count];
            Vector3 p1 = points[i];
            Vector3 p2 = points[(i + 1) % count];
            Vector3 p3 = points[(i + 2) % count];
            for (int j = 0; j < steps; j++)
            {
                float t = j / (float)steps;
                Vector3 newPoint = CatmullRom(p0, p1, p2, p3, t);
                smoothed.Add(newPoint);
            }
        }
        if (smoothed.Count > 0)
            smoothed.Add(smoothed[0]);
        return smoothed;
    }

    Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * ((2f * p1) +
                       (-p0 + p2) * t +
                       (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                       (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    void ClearLine()
    {
        if (lineRenderer != null)
            lineRenderer.positionCount = 0;
    }

    bool IsValidVector(Vector3 v)
    {
        return !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
                 float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
    }

    float GetTerrainHeight(Vector3 worldPos)
    {
        if (terrain != null)
        {
            Terrain t = terrain.GetComponent<Terrain>();
            if (t != null)
                return t.SampleHeight(worldPos) + terrain.position.y;
        }
        return worldPos.y;
    }

    void AddEdgePoints(List<Vector3> points, Vector3 start, Vector3 end)
    {
        float edgeLength = Vector3.Distance(start, end);
        if (edgeLength < 0.001f)
        {
            points.Add(start);
            return;
        }
        int numPoints = Mathf.Max(1, Mathf.CeilToInt(edgeLength / pointSpacing));
        for (int i = 0; i <= numPoints; i++)
        {
            float t = i / (float)numPoints;
            Vector3 pos = Vector3.Lerp(start, end, t);
            pos.y = GetTerrainHeight(new Vector3(pos.x, 0, pos.z));
            if (IsValidVector(pos))
                points.Add(pos);
        }
    }

    void SelectObjectsInRectangle()
    {
        bool ctrlDown = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        float minX = Mathf.Min(startWorldPoint.x, currentWorldPoint.x);
        float maxX = Mathf.Max(startWorldPoint.x, currentWorldPoint.x);
        float minZ = Mathf.Min(startWorldPoint.z, currentWorldPoint.z);
        float maxZ = Mathf.Max(startWorldPoint.z, currentWorldPoint.z);

        Vector3 center = new Vector3((minX + maxX) / 2f, 0, (minZ + maxZ) / 2f);
        Vector3 halfExtents = new Vector3((maxX - minX) / 2f, 1000, (maxZ - minZ) / 2f);
        Collider[] colliders = Physics.OverlapBox(center, halfExtents, Quaternion.identity, selectableLayer);
        if (!ctrlDown)
            selectedObjects.Clear();
        foreach (Collider col in colliders)
        {
            if (col.gameObject != terrain.gameObject && !selectedObjects.Contains(col.gameObject))
                selectedObjects.Add(col.gameObject);
        }
        if (selectedObjects.Count > 0)
        {
            Vector3 sum = Vector3.zero;
            foreach (GameObject obj in selectedObjects)
                sum += new Vector3(obj.transform.position.x, 0, obj.transform.position.z);
            Vector3 avg = sum / selectedObjects.Count;
            float y = GetTerrainHeight(new Vector3(avg.x, 0, avg.z));
            Vector3 targetFocus = new Vector3(avg.x, y, avg.z);
            if (cameraController != null)
                cameraController.SetTargetFocusPoint(targetFocus);
        }
    }

    void SingleSelect()
    {
        bool ctrlDown = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, 1000f, selectableLayer))
        {
            GameObject obj = hit.collider.gameObject;
            if (obj != terrain.gameObject)
            {
                if (ctrlDown)
                {
                    if (selectedObjects.Contains(obj))
                        selectedObjects.Remove(obj);
                    else
                        selectedObjects.Add(obj);
                }
                else
                {
                    selectedObjects.Clear();
                    selectedObjects.Add(obj);
                }
                if (selectedObjects.Count > 0)
                {
                    Vector3 sum = Vector3.zero;
                    foreach (GameObject sel in selectedObjects)
                        sum += new Vector3(sel.transform.position.x, 0, sel.transform.position.z);
                    Vector3 avg = sum / selectedObjects.Count;
                    float y = GetTerrainHeight(new Vector3(avg.x, 0, avg.z));
                    Vector3 targetFocus = new Vector3(avg.x, y, avg.z);
                    if (cameraController != null)
                        cameraController.SetTargetFocusPoint(targetFocus);
                }
                return;
            }
        }
        if (!ctrlDown)
        {
            selectedObjects.Clear();
            Ray terrainRay = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit terrainHit;
            TerrainCollider tc = terrain.GetComponent<TerrainCollider>();
            if (tc != null && tc.Raycast(terrainRay, out terrainHit, 1000f))
            {
                Vector3 focusPos = terrainHit.point;
                focusPos.y = GetTerrainHeight(new Vector3(focusPos.x, 0, focusPos.z));
                if (cameraController != null)
                    cameraController.SetTargetFocusPoint(focusPos);
            }
        }
    }

    void SelectObjectsInScreenRect()
    {
        bool ctrlDown = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        if (!ctrlDown)
            selectedObjects.Clear();

        Rect selectionRect = GetScreenRect(startScreenPoint2D, currentScreenPoint2D);
        Collider[] colliders = FindObjectsByType<Collider>(FindObjectsSortMode.None);
        foreach (Collider col in colliders)
        {
            if ((selectableLayer.value & (1 << col.gameObject.layer)) == 0)
                continue;
            if (col.gameObject == terrain.gameObject)
                continue;

            Vector3 screenPos = Camera.main.WorldToScreenPoint(col.transform.position);
            Vector2 guiPoint = new Vector2(screenPos.x, Screen.height - screenPos.y);
            if (selectionRect.Contains(guiPoint) && !selectedObjects.Contains(col.gameObject))
                selectedObjects.Add(col.gameObject);
        }

        if (selectedObjects.Count > 0)
        {
            Vector3 sum = Vector3.zero;
            foreach (GameObject obj in selectedObjects)
                sum += new Vector3(obj.transform.position.x, 0, obj.transform.position.z);
            Vector3 avg = sum / selectedObjects.Count;
            float y = GetTerrainHeight(new Vector3(avg.x, 0, avg.z));
            Vector3 targetFocus = new Vector3(avg.x, y, avg.z);
            if (cameraController != null)
                cameraController.SetTargetFocusPoint(targetFocus);
        }
        UpdateSelectionOutlines();
    }

    Rect GetScreenRect(Vector2 screenPosition1, Vector2 screenPosition2)
    {
        screenPosition1.y = Screen.height - screenPosition1.y;
        screenPosition2.y = Screen.height - screenPosition2.y;
        Vector2 topLeft = Vector2.Min(screenPosition1, screenPosition2);
        Vector2 bottomRight = Vector2.Max(screenPosition1, screenPosition2);
        return new Rect(topLeft.x, topLeft.y, bottomRight.x - topLeft.x, bottomRight.y - topLeft.y);
    }

    void OnGUI()
    {
        // Draw 2D selection box if active.
        if (is2DSelecting)
        {
            Rect rect = GetScreenRect(startScreenPoint2D, Input.mousePosition);
            Color prevColor = GUI.color;
            GUI.color = selectionBoxFillColor;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = selectionBoxOutlineColor;
            GUI.Box(rect, "");
            GUI.color = prevColor;
        }
        // Display "Selection Mode" label at the top right in polygon mode.
        if (polygonSelectionMode)
        {
            GUIStyle style = new GUIStyle();
            style.fontSize = 20;
            style.normal.textColor = Color.green;
            GUI.Label(new Rect(Screen.width - 150, 10, 140, 30), "Selection Mode", style);
        }
    }

    #endregion
}
