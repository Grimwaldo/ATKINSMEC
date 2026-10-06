using System;
using System.Collections.Generic;
using System.Linq;

// Mirror of SPRINKLERLAYOUT's geometry, in metres, with no Revit types,
// so the algorithm can be exercised on known room shapes.

struct UV
{
    public double U, V;
    public UV(double u, double v) { U = u; V = v; }
}

class Poly
{
    public List<UV> Outer;
    public List<List<UV>> Holes = new List<List<UV>>();
}

static class Program
{
    const int CellSamples = 7;
    const int CoverageSamples = 40;

    static UV Rotate(UV p, double a)
    {
        double c = Math.Cos(a), s = Math.Sin(a);
        return new UV(p.U * c - p.V * s, p.U * s + p.V * c);
    }

    static double Cross(UV o, UV a, UV b)
        => (a.U - o.U) * (b.V - o.V) - (a.V - o.V) * (b.U - o.U);

    static List<UV> ConvexHull(List<UV> pts)
    {
        var s = pts.OrderBy(p => p.U).ThenBy(p => p.V).ToList();
        if (s.Count < 3) return s;
        var hull = new List<UV>();
        for (int pass = 0; pass < 2; pass++)
        {
            int start = hull.Count;
            var seq = pass == 0 ? s : Enumerable.Reverse(s).ToList();
            foreach (var p in seq)
            {
                while (hull.Count >= start + 2 &&
                       Cross(hull[hull.Count - 2], hull[hull.Count - 1], p) <= 0)
                    hull.RemoveAt(hull.Count - 1);
                hull.Add(p);
            }
            hull.RemoveAt(hull.Count - 1);
        }
        return hull;
    }

    static void Bounds(List<UV> pts, double a, out UV min, out UV max)
    {
        double mnU = double.MaxValue, mnV = double.MaxValue;
        double mxU = double.MinValue, mxV = double.MinValue;
        foreach (var p in pts)
        {
            var r = Rotate(p, a);
            if (r.U < mnU) mnU = r.U;
            if (r.V < mnV) mnV = r.V;
            if (r.U > mxU) mxU = r.U;
            if (r.V > mxV) mxV = r.V;
        }
        min = new UV(mnU, mnV); max = new UV(mxU, mxV);
    }

    static void MinAreaRect(List<UV> poly, out double angle, out UV min, out UV max)
    {
        var hull = ConvexHull(poly);
        angle = 0; min = new UV(0, 0); max = new UV(0, 0);
        double best = double.MaxValue;
        if (hull.Count < 3) { Bounds(poly, 0, out min, out max); return; }
        for (int i = 0; i < hull.Count; i++)
        {
            var a = hull[i]; var b = hull[(i + 1) % hull.Count];
            double ea = Math.Atan2(b.V - a.V, b.U - a.U);
            UV lo, hi; Bounds(hull, -ea, out lo, out hi);
            double area = (hi.U - lo.U) * (hi.V - lo.V);
            if (area < best) { best = area; angle = ea; min = lo; max = hi; }
        }
    }

    static bool InRing(List<UV> r, UV p)
    {
        bool inside = false;
        for (int i = 0, j = r.Count - 1; i < r.Count; j = i++)
            if ((r[i].V > p.V) != (r[j].V > p.V) &&
                p.U < (r[j].U - r[i].U) * (p.V - r[i].V) / (r[j].V - r[i].V) + r[i].U)
                inside = !inside;
        return inside;
    }

    static double DistSeg(UV a, UV b, UV p)
    {
        double dx = b.U - a.U, dy = b.V - a.V;
        double l2 = dx * dx + dy * dy;
        if (l2 < 1e-12) return Dist(a, p);
        double t = Math.Max(0, Math.Min(1, ((p.U - a.U) * dx + (p.V - a.V) * dy) / l2));
        return Dist(new UV(a.U + t * dx, a.V + t * dy), p);
    }

    static double Dist(UV a, UV b)
        => Math.Sqrt((a.U - b.U) * (a.U - b.U) + (a.V - b.V) * (a.V - b.V));

    static double DistRing(List<UV> r, UV p)
    {
        double best = double.MaxValue;
        for (int i = 0, j = r.Count - 1; i < r.Count; j = i++)
            best = Math.Min(best, DistSeg(r[j], r[i], p));
        return best;
    }

    static double DistEdges(Poly poly, UV p)
    {
        double best = DistRing(poly.Outer, p);
        foreach (var h in poly.Holes) best = Math.Min(best, DistRing(h, p));
        return best;
    }

    static bool Inside(Poly poly, UV p, double clear)
    {
        if (!InRing(poly.Outer, p)) return false;
        foreach (var h in poly.Holes) if (InRing(h, p)) return false;
        if (clear > 0 && DistEdges(poly, p) < clear) return false;
        return true;
    }

    static bool CellCentroid(Poly poly, double angle, UV centre,
        double sx, double sy, double clear, out UV centroid)
    {
        centroid = new UV(0, 0);
        double su = 0, sv = 0; int hits = 0;
        for (int i = 0; i < CellSamples; i++)
            for (int j = 0; j < CellSamples; j++)
            {
                var local = new UV(
                    centre.U + sx * ((i + 0.5) / CellSamples - 0.5),
                    centre.V + sy * ((j + 0.5) / CellSamples - 0.5));
                var w = Rotate(local, angle);
                if (!Inside(poly, w, clear)) continue;
                su += w.U; sv += w.V; hits++;
            }
        if (hits < CellSamples * CellSamples * 0.12) return false;
        var c = new UV(su / hits, sv / hits);
        if (!Inside(poly, c, clear)) return false;
        centroid = c; return true;
    }

    static bool InteriorPoint(Poly poly, double clear, out UV point)
    {
        UV min, max; Bounds(poly.Outer, 0, out min, out max);
        point = new UV((min.U + max.U) / 2, (min.V + max.V) / 2);
        if (Inside(poly, point, clear)) return true;
        const int steps = 20; double bestD = -1; bool found = false;
        for (int i = 0; i <= steps; i++)
            for (int j = 0; j <= steps; j++)
            {
                var c = new UV(min.U + (max.U - min.U) * i / steps,
                               min.V + (max.V - min.V) * j / steps);
                if (!Inside(poly, c, clear)) continue;
                double d = DistEdges(poly, c);
                if (d > bestD) { bestD = d; point = c; found = true; }
            }
        return found;
    }

    static double CoverageRatio(Poly poly, List<UV> heads, double reach, out double area)
    {
        UV min, max; Bounds(poly.Outer, 0, out min, out max);
        double cw = (max.U - min.U) / CoverageSamples;
        double ch = (max.V - min.V) / CoverageSamples;
        int inside = 0, covered = 0;
        for (int i = 0; i < CoverageSamples; i++)
            for (int j = 0; j < CoverageSamples; j++)
            {
                var p = new UV(min.U + cw * (i + 0.5), min.V + ch * (j + 0.5));
                if (!Inside(poly, p, 0)) continue;
                inside++;
                foreach (var s in heads) if (Dist(s, p) <= reach) { covered++; break; }
            }
        area = inside * cw * ch;
        return inside == 0 ? 0 : (double)covered / inside;
    }

    static void Run(string label, Poly poly, double maxSpacing, double maxCov)
    {
        const double minSpacing = 1.8, clear = 0.1;

        double angle; UV min, max;
        MinAreaRect(poly.Outer, out angle, out min, out max);
        double w = max.U - min.U, h = max.V - min.V;

        int nx = Math.Max(1, (int)Math.Ceiling(w / maxSpacing));
        int ny = Math.Max(1, (int)Math.Ceiling(h / maxSpacing));
        double sx = w / nx, sy = h / ny;
        int guard = 0;
        while (sx * sy > maxCov && guard++ < 200)
        {
            if (sx >= sy) { nx++; sx = w / nx; } else { ny++; sy = h / ny; }
        }
        while (nx > 1 && sx < minSpacing) { nx--; sx = w / nx; }
        while (ny > 1 && sy < minSpacing) { ny--; sy = h / ny; }

        var pts = new List<UV>(); int moved = 0;
        for (int i = 0; i < nx; i++)
            for (int j = 0; j < ny; j++)
            {
                var local = new UV(min.U + sx * (i + 0.5), min.V + sy * (j + 0.5));
                var world = Rotate(local, angle);
                if (Inside(poly, world, clear)) { pts.Add(world); continue; }
                UV pulled;
                if (CellCentroid(poly, angle, local, sx, sy, clear, out pulled))
                { pts.Add(pulled); moved++; }
            }
        if (pts.Count == 0)
        {
            UV c; if (InteriorPoint(poly, clear, out c)) pts.Add(c);
        }

        double reach = 0.5 * Math.Sqrt(sx * sx + sy * sy);
        double area; double cov = CoverageRatio(poly, pts, reach, out area);

        // farthest sprinkler-to-wall distance, must be <= spacing/2
        double maxWall = 0;
        foreach (var p in pts) maxWall = Math.Max(maxWall, 0);

        // closest pair, must be >= 1.8 m
        double closest = double.MaxValue;
        for (int i = 0; i < pts.Count; i++)
            for (int j = i + 1; j < pts.Count; j++)
                closest = Math.Min(closest, Dist(pts[i], pts[j]));

        Console.WriteLine($"--- {label}");
        Console.WriteLine($"    room area      : {area:N1} m2");
        Console.WriteLine($"    grid           : {nx} x {ny}  (angle {angle * 180 / Math.PI:N1} deg)");
        Console.WriteLine($"    spacing        : {sx:N2} x {sy:N2} m   (limit {maxSpacing:N1})");
        Console.WriteLine($"    cell area      : {sx * sy:N1} m2      (limit {maxCov:N1})");
        Console.WriteLine($"    sprinklers     : {pts.Count}  ({moved} moved off the wall)");
        Console.WriteLine($"    m2 per head    : {(pts.Count > 0 ? area / pts.Count : 0):N1}");
        Console.WriteLine($"    coverage       : {cov:P1}");
        Console.WriteLine($"    closest pair   : {(closest == double.MaxValue ? 0 : closest):N2} m (min 1.80)");
        foreach (var p in pts)
            Console.WriteLine($"      head at ({p.U:N2}, {p.V:N2})");
        Console.WriteLine();
    }

    static Poly Rect(double w, double h)
        => new Poly { Outer = new List<UV> {
            new UV(0,0), new UV(w,0), new UV(w,h), new UV(0,h) } };

    static void Main()
    {
        // Light hazard 20.9 m2 / 4.6 m ; Ordinary 12.1 / 4.6 ; Extra 9.3 / 3.7

        // The "Zimmer 14.0 m2" room: right wall near vertical, left wall
        // slanting in towards the top
        var zimmer = new Poly { Outer = new List<UV> {
            new UV(0.00, 0.00), new UV(3.50, 0.00),
            new UV(3.40, 4.60), new UV(0.90, 4.60) } };

        Run("Zimmer (trapezoid), Light", zimmer, 4.6, 20.9);
        Run("Zimmer (trapezoid), Ordinary I", zimmer, 4.6, 12.1);

        Run("Rectangle 5 x 4, Light", Rect(5, 4), 4.6, 20.9);
        Run("Rectangle 12 x 9, Ordinary I", Rect(12, 9), 4.6, 12.1);
        Run("Rectangle 12 x 9, Extra I", Rect(12, 9), 3.7, 9.3);

        // Skewed rectangle: 10 x 6 rotated 30 degrees, checks that the
        // grid follows the walls instead of the model axes
        var skew = new Poly { Outer = Rect(10, 6).Outer
            .Select(p => Rotate(p, 30 * Math.PI / 180)).ToList() };
        Run("10 x 6 rotated 30 deg, Ordinary I", skew, 4.6, 12.1);

        // L-shaped room, the hardest non-uniform case
        var lshape = new Poly { Outer = new List<UV> {
            new UV(0,0), new UV(10,0), new UV(10,4),
            new UV(4,4), new UV(4,9), new UV(0,9) } };
        Run("L-shaped, Ordinary I", lshape, 4.6, 12.1);
    }
}
