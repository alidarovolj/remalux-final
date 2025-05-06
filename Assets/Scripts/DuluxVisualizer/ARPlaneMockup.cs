using UnityEngine;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// Simple mockup of AR plane for testing in editor without AR device
/// </summary>
public class ARPlaneMockup : MonoBehaviour
{
      [SerializeField] private PlaneAlignment alignment = PlaneAlignment.Vertical;
      [SerializeField] private PlaneClassification classification = PlaneClassification.Wall;

      // Unique ID for this plane
      private TrackableId trackableId;

      // Size of the plane
      public Vector2 size { get; private set; }

      // Center of the plane
      public Vector3 center { get; private set; }

      private void Awake()
      {
            // Generate random trackable ID
            trackableId = new TrackableId(
                (ulong)Random.Range(0, int.MaxValue),
                (ulong)Random.Range(0, int.MaxValue)
            );

            // Calculate size from renderer if available
            MeshRenderer renderer = GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                  Bounds bounds = renderer.bounds;
                  size = new Vector2(bounds.size.x, bounds.size.y);
            }
            else
            {
                  // Default size
                  size = new Vector2(2f, 2f);
            }

            // Set center
            center = transform.position;
      }

      // Get plane alignment (horizontal, vertical)
      public PlaneAlignment GetAlignment()
      {
            return alignment;
      }

      // Get plane classification (wall, floor, ceiling, etc.)
      public PlaneClassification GetClassification()
      {
            return classification;
      }

      // Get trackable ID
      public TrackableId GetTrackableId()
      {
            return trackableId;
      }

      // Update the plane's visual representation (for debugging)
      public void UpdateVisual(Color color, float opacity)
      {
            MeshRenderer renderer = GetComponent<MeshRenderer>();
            if (renderer != null && renderer.material != null)
            {
                  Color planeColor = color;
                  planeColor.a = opacity;
                  renderer.material.color = planeColor;
            }
      }
}