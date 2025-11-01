using System.Collections.Generic;
using UnityEngine;

public class RTSCameraController : MonoBehaviour
{
    [Header("Camera Settings")]
    public float panSpeed = 200f;
    public float rotationSpeed = 175f;
    public float zoomSpeed = 150f;

    [Header("Zoom Limits")]
    public float minZoomDistance = 10f;
    public float maxZoomDistance = 100f;

    [Header("Pitch Settings")]
    public float pitchAngle = 45f;
    public float minPitchAngle = 20f;
    public float maxPitchAngle = 80f;

    [Header("Focus Settings")]
    public Transform terrain;
    public bool renderFocusPoint = true;

    [Header("Smoothing Settings")]
    public float rotationDamping = 0.9f;
    public float zoomDamping = 0.9f;
    public float focusSmoothing = 5f;

    // This flag disables rotation (set from external scripts)
    [HideInInspector]
    public bool blockRotation = false;

    private float zoomVelocity;
    private float yawVelocity;
    private float pitchVelocity;
    private Vector3 targetFocusPoint;

    private Vector3 focusPoint;
    private float currentZoomDistance = 50f;
    private float currentYaw;

    void Start()
    {
        focusPoint = GetTerrainCenter();
        targetFocusPoint = focusPoint;
        Vector3 offset = transform.position - focusPoint;
        offset.y = 0;
        currentYaw = (offset.sqrMagnitude > 0.001f) ? Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg : 0;
    }

    void Update()
    {
        ProcessZoom();
        ProcessRotation();
        ProcessPan();
        ProcessResetFocus();
        UpdateFocus();
        UpdateCameraPosition();
    }

    private void ProcessZoom()
    {
        float scrollInput = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scrollInput) > 0.01f)
            zoomVelocity += scrollInput * zoomSpeed;
        else
            zoomVelocity *= zoomDamping;

        currentZoomDistance = Mathf.Clamp(
            currentZoomDistance - zoomVelocity * Time.deltaTime,
            minZoomDistance,
            maxZoomDistance
        );
    }

    private void ProcessRotation()
    {
        // Disable rotation if blockRotation is true.
        if (blockRotation)
        {
            yawVelocity = 0;
            pitchVelocity = 0;
            return;
        }

        if (Input.GetMouseButton(0))
        {
            // Rotate camera using mouse movement.
            yawVelocity = Input.GetAxis("Mouse X") * rotationSpeed;
            pitchVelocity = Input.GetAxis("Mouse Y") * rotationSpeed;
        }
        else
        {
            yawVelocity *= rotationDamping;
            pitchVelocity *= rotationDamping;
        }

        currentYaw += yawVelocity * Time.deltaTime;
        pitchAngle = Mathf.Clamp(pitchAngle + pitchVelocity * Time.deltaTime, minPitchAngle, maxPitchAngle);
    }

    private void ProcessPan()
    {
        if (Input.GetMouseButton(2))
        {
            float deltaX = -Input.GetAxis("Mouse X");
            float deltaY = Input.GetAxis("Mouse Y");
            Vector3 right = transform.right;
            Vector3 forward = Vector3.Cross(Vector3.up, right);
            Vector3 inputDirection = right * deltaX + forward * deltaY;
            targetFocusPoint += inputDirection * panSpeed * Time.deltaTime;
        }
        // Keep the focus on terrain during manual pan.
        targetFocusPoint.y = GetTerrainHeight(targetFocusPoint);
    }

    private void ProcessResetFocus()
    {
        if (Input.GetKeyDown(KeyCode.Space))
            targetFocusPoint = GetTerrainCenter();
    }

    private void UpdateFocus()
    {
        focusPoint = Vector3.Lerp(focusPoint, targetFocusPoint, focusSmoothing * Time.deltaTime);
    }

    private void UpdateCameraPosition()
    {
        float yawRad = currentYaw * Mathf.Deg2Rad;
        float pitchRad = pitchAngle * Mathf.Deg2Rad;
        Vector3 offsetPos = new Vector3(
            currentZoomDistance * Mathf.Sin(pitchRad) * Mathf.Sin(yawRad),
            currentZoomDistance * Mathf.Cos(pitchRad),
            currentZoomDistance * Mathf.Sin(pitchRad) * Mathf.Cos(yawRad)
        );
        transform.position = focusPoint + offsetPos;
        transform.LookAt(focusPoint);
    }

    // Returns the terrain height at a given position.
    private float GetTerrainHeight(Vector3 position)
    {
        if (terrain != null)
        {
            Terrain t = terrain.GetComponent<Terrain>();
            if (t != null)
                return t.SampleHeight(position) + terrain.position.y;
        }
        return focusPoint.y;
    }

    // Returns the center point of the terrain.
    private Vector3 GetTerrainCenter()
    {
        if (terrain != null)
        {
            Terrain t = terrain.GetComponent<Terrain>();
            if (t != null)
                return terrain.position + t.terrainData.size / 2f;
            return terrain.position;
        }
        return Vector3.zero;
    }

    // Called externally to update the camera's focus.
    public void SetTargetFocusPoint(Vector3 newTargetFocusPoint)
    {
        targetFocusPoint = newTargetFocusPoint;
    }

    void OnDrawGizmos()
    {
        if (renderFocusPoint)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(focusPoint, 1f);
        }
    }
}
