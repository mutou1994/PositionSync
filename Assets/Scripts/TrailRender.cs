using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TrailRender : MonoBehaviour
{
    public int pointCount = 10;
    LineRenderer lineRenderer;
    List<Vector3> positions;
    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        positions = new List<Vector3>(pointCount);
        positions.Add(transform.position);
        lineRenderer.positionCount = positions.Count;
        lineRenderer.SetPositions(positions.ToArray());
    }

    // Update is called once per frame
    void Update()
    {
        if (positions[positions.Count-1] == transform.position || Vector3.Distance(positions[positions.Count - 1], transform.position) < 3f) return;
        positions.Add(transform.position);
        lineRenderer.positionCount = positions.Count;
        lineRenderer.SetPositions(positions.ToArray());
        if (positions.Count > pointCount) positions.RemoveRange(0, positions.Count - pointCount);
    }
}
