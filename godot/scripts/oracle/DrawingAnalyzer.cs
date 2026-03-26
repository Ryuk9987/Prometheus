#nullable disable
using Godot;
using System.Collections.Generic;
using System.Linq;

public class DrawingFeatures
{
    public float Roundness       { get; set; }
    public float Complexity      { get; set; }
    public float Verticality     { get; set; }
    public float Size            { get; set; }
    public int   StrokeCount     { get; set; }
    public bool  HasCurves       { get; set; }
    public bool  HasStraightLines{ get; set; }
    public bool  IsSymmetric     { get; set; }
    public bool  HasTriangle     { get; set; }
    public bool  HasRectangle    { get; set; }
    public bool  HasCircle       { get; set; }
    public bool  HasLine         { get; set; }
    public bool  TriangleOnRect  { get; set; }  // house shape
    public bool  ArcWithLine     { get; set; }  // bow shape
    public string DominantShape  { get; set; }

    public override string ToString() =>
        $"[{DominantShape}] round:{Roundness:F2} vert:{Verticality:F2} triOnRect:{TriangleOnRect} arcLine:{ArcWithLine}";
}

public static class DrawingAnalyzer
{
    public static DrawingFeatures Analyze(List<DrawnStroke> strokes)
    {
        var f = new DrawingFeatures();
        if (strokes == null || strokes.Count == 0) { f.DominantShape = "empty"; return f; }

        f.StrokeCount    = strokes.Count;
        f.HasTriangle    = strokes.Any(s => s.Tool == DrawTool.Triangle);
        f.HasRectangle   = strokes.Any(s => s.Tool == DrawTool.Rectangle);
        f.HasCircle      = strokes.Any(s => s.Tool == DrawTool.Circle);
        f.HasLine        = strokes.Any(s => s.Tool == DrawTool.Line);
        f.HasCurves      = strokes.Any(s => s.Tool == DrawTool.Circle || s.Tool == DrawTool.Freehand);
        f.HasStraightLines = strokes.Any(s => s.Tool != DrawTool.Freehand && s.Tool != DrawTool.Circle);

        // House detection: triangle + rectangle (Dreieck über Rechteck)
        if (f.HasTriangle && f.HasRectangle)
        {
            var triStroke  = strokes.FirstOrDefault(s => s.Tool == DrawTool.Triangle);
            var rectStroke = strokes.FirstOrDefault(s => s.Tool == DrawTool.Rectangle);
            if (triStroke != null && rectStroke != null && triStroke.Points.Count >= 2 && rectStroke.Points.Count >= 2)
            {
                float triBottom  = triStroke.Points.Max(p => p.Y);
                float rectTop    = rectStroke.Points.Min(p => p.Y);
                // Triangle bottom should be near rectangle top (within 60px)
                f.TriangleOnRect = Mathf.Abs(triBottom - rectTop) < 80f;
            }
        }

        // Bow detection: line + circle/arc, or line + freehand curve
        if ((f.HasLine || f.HasStraightLines) && f.HasCurves)
            f.ArcWithLine = true;

        // Collect all points
        var allPoints = strokes.SelectMany(s => s.Points).ToList();
        if (allPoints.Count == 0) { f.DominantShape = "empty"; return f; }

        float minX = allPoints.Min(p => p.X), maxX = allPoints.Max(p => p.X);
        float minY = allPoints.Min(p => p.Y), maxY = allPoints.Max(p => p.Y);
        float w = maxX - minX + 1f, h = maxY - minY + 1f;
        f.Size        = Mathf.Clamp(w * h / 40000f, 0f, 1f);
        f.Verticality = Mathf.Clamp(h / w, 0f, 3f) / 3f;

        var center   = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
        float avgDist = allPoints.Average(p => p.DistanceTo(center));
        float variance = allPoints.Average(p => Mathf.Abs(p.DistanceTo(center) - avgDist));
        f.Roundness   = Mathf.Clamp(1f - variance / (avgDist + 1f), 0f, 1f);
        f.Complexity  = Mathf.Clamp(allPoints.Count / 200f, 0f, 1f);

        var left  = allPoints.Where(p => p.X < center.X).ToList();
        var right = allPoints.Where(p => p.X >= center.X).ToList();
        f.IsSymmetric = left.Count > 0 && right.Count > 0 &&
                        Mathf.Abs(left.Count - right.Count) < allPoints.Count * 0.3f;

        // Determine dominant shape (priority order matters)
        if (f.TriangleOnRect)
            f.DominantShape = "house";
        else if (f.ArcWithLine && !f.HasRectangle)
            f.DominantShape = "bow";
        else if (f.HasCircle || (f.Roundness > 0.65f && !f.HasTriangle && !f.HasRectangle))
            f.DominantShape = "circle";
        else if (f.HasTriangle || (f.Verticality > 0.6f && f.Roundness < 0.4f))
            f.DominantShape = "triangle";
        else if (f.HasRectangle || (f.IsSymmetric && f.Roundness < 0.4f && f.Complexity < 0.3f))
            f.DominantShape = "rectangle";
        else if (f.HasLine || (f.Complexity < 0.15f && f.StrokeCount <= 2))
            f.DominantShape = "line";
        else
            f.DominantShape = "complex";

        GD.Print($"[DrawingAnalyzer] {f}");
        return f;
    }
}
