#nullable disable
using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Analyzes a finished drawing and extracts geometric features.
/// No AI — pure geometry. The meaning comes from NPC interpretation.
/// </summary>
public class DrawingFeatures
{
    public float Roundness      { get; set; }   // 0=angular, 1=circular
    public float Complexity     { get; set; }   // stroke count / point count
    public float Verticality    { get; set; }   // tall vs wide
    public float Size           { get; set; }   // bounding box area (normalized)
    public int   StrokeCount    { get; set; }
    public bool  HasCurves      { get; set; }
    public bool  HasStraightLines { get; set; }
    public bool  IsSymmetric    { get; set; }
    public string DominantShape { get; set; }   // "circle","line","triangle","rect","complex"

    public override string ToString() =>
        $"[{DominantShape}] round:{Roundness:F2} complex:{Complexity:F2} vert:{Verticality:F2} size:{Size:F2}";
}

public static class DrawingAnalyzer
{
    public static DrawingFeatures Analyze(List<DrawnStroke> strokes)
    {
        var f = new DrawingFeatures();
        if (strokes == null || strokes.Count == 0)
        {
            f.DominantShape = "empty";
            return f;
        }

        f.StrokeCount = strokes.Count;

        // Collect all points
        var allPoints = new List<Vector2>();
        int curveCount = 0, lineCount = 0;
        foreach (var s in strokes)
        {
            allPoints.AddRange(s.Points);
            if (s.Tool == DrawTool.Circle)   curveCount++;
            if (s.Tool == DrawTool.Freehand) curveCount++;
            if (s.Tool == DrawTool.Line || s.Tool == DrawTool.Rectangle || s.Tool == DrawTool.Triangle)
                lineCount++;
        }
        if (allPoints.Count == 0) { f.DominantShape = "empty"; return f; }

        f.HasCurves       = curveCount > 0;
        f.HasStraightLines = lineCount > 0;

        // Bounding box
        float minX = allPoints.Min(p => p.X), maxX = allPoints.Max(p => p.X);
        float minY = allPoints.Min(p => p.Y), maxY = allPoints.Max(p => p.Y);
        float w = maxX - minX + 1f, h = maxY - minY + 1f;
        f.Size        = Mathf.Clamp((w * h) / 40000f, 0f, 1f);
        f.Verticality = Mathf.Clamp(h / w, 0f, 3f) / 3f;

        // Roundness: compare perimeter² / area to circle ratio
        var center = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
        float avgDist = allPoints.Average(p => p.DistanceTo(center));
        float distVariance = allPoints.Average(p => Mathf.Abs(p.DistanceTo(center) - avgDist));
        f.Roundness = Mathf.Clamp(1f - (distVariance / (avgDist + 1f)), 0f, 1f);

        // Complexity: normalized point density
        f.Complexity = Mathf.Clamp(allPoints.Count / 200f, 0f, 1f);

        // Symmetry: check if left/right halves are roughly mirrored
        var leftPts  = allPoints.Where(p => p.X < center.X).ToList();
        var rightPts = allPoints.Where(p => p.X >= center.X).ToList();
        f.IsSymmetric = leftPts.Count > 0 && rightPts.Count > 0 &&
                        Mathf.Abs(leftPts.Count - rightPts.Count) < allPoints.Count * 0.3f;

        // Dominant shape
        bool hasCircle   = strokes.Any(s => s.Tool == DrawTool.Circle);
        bool hasTriangle = strokes.Any(s => s.Tool == DrawTool.Triangle);
        bool hasRect     = strokes.Any(s => s.Tool == DrawTool.Rectangle);
        bool hasLine     = strokes.Any(s => s.Tool == DrawTool.Line);

        if (hasCircle || (f.Roundness > 0.65f && !hasTriangle && !hasRect))
            f.DominantShape = "circle";
        else if (hasTriangle || (f.Verticality > 0.6f && f.Roundness < 0.4f))
            f.DominantShape = "triangle";
        else if (hasRect || (f.IsSymmetric && f.Roundness < 0.4f && f.Complexity < 0.3f))
            f.DominantShape = "rectangle";
        else if (hasLine || (f.Complexity < 0.15f && f.StrokeCount <= 2))
            f.DominantShape = "line";
        else
            f.DominantShape = "complex";

        return f;
    }
}
