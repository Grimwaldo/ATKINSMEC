using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ATKINSMEC
{
    // Works out where the sprinklers go inside one room.
    //
    // The grid is built on the room's minimum-area rectangle rather than
    // on the model axes, so a room whose walls are skewed still gets a
    // layout parallel to its own walls. Grid points that fall outside
    // the room are pulled back to the centre of whatever part of their
    // cell is inside it, which is what makes trapezoidal and L-shaped
    // rooms work.
    //
    // All geometry is in Revit internal units (feet), in host document
    // coordinates.
    public class SPRINKLERLAYOUT
    {
        public class Result
        {
            public List<XYZ> Points { get; set; }

            public double SpacingXFt { get; set; }
            public double SpacingYFt { get; set; }

            // Area actually covered per sprinkler, m2
            public double CoveragePerSprinklerM2 { get; set; }

            // Share of the room within reach of a sprinkler, 0..1
            public double Coverage { get; set; }

            public int Relocated { get; set; }

            // Moved inward to keep NFPA 13's 4 in minimum from the wall
            public int WallAdjusted { get; set; }

            public int Dropped { get; set; }

            // Extra heads placed to close gaps the grid left behind
            public int Added { get; set; }

            // Room area left after holes and wall obstacles, m2
            public double FreeAreaM2 { get; set; }

            public string Note { get; set; }
        }

        // The room outline, captured once while the Revit API context
        // is valid so that later recalculation is pure arithmetic
        public class RoomShape
        {
            public List<UV> Outer { get; set; }
            public List<List<UV>> Holes { get; set; }
        }

        private class Polygon
        {
            public List<UV> Outer { get; set; }
            public List<List<UV>> Holes { get; set; }
        }

        // ==================================================
        // Entry point
        // ==================================================

        public static RoomShape ExtractShape(Room room, Transform linkToHost)
        {
            return ExtractShape(room, linkToHost, null);
        }

        // Wall footprints are added as holes: a sprinkler is then kept
        // out of the wall itself, not merely off the room boundary,
        // which is what stops a head landing inside a wall when the two
        // do not coincide.
        public static RoomShape ExtractShape(
            Room room,
            Transform linkToHost,
            List<List<UV>> obstacles)
        {
            Polygon polygon = GetPolygon(room, linkToHost);

            if (polygon == null)
                return null;

            var holes = new List<List<UV>>(polygon.Holes);

            if (obstacles != null)
                holes.AddRange(obstacles);

            return new RoomShape
            {
                Outer = polygon.Outer,
                Holes = holes
            };
        }

        // Room extents, so only nearby walls need to be considered
        public static void ShapeBounds(
            RoomShape shape,
            out double minX,
            out double minY,
            out double maxX,
            out double maxY)
        {
            minX = double.MaxValue;
            minY = double.MaxValue;
            maxX = double.MinValue;
            maxY = double.MinValue;

            if (shape == null || shape.Outer == null)
                return;

            foreach (UV p in shape.Outer)
            {
                if (p.U < minX) minX = p.U;
                if (p.V < minY) minY = p.V;
                if (p.U > maxX) maxX = p.U;
                if (p.V > maxY) maxY = p.V;
            }
        }

        public static Result Layout(
            RoomShape shape,
            SPRINKLERRULES.HazardRule rule)
        {
            var result = new Result
            {
                Points = new List<XYZ>(),
                Note = string.Empty
            };

            Polygon polygon = shape == null
                ? null
                : new Polygon { Outer = shape.Outer, Holes = shape.Holes };

            if (polygon == null || polygon.Outer == null || polygon.Outer.Count < 3)
            {
                result.Note = "no usable room boundary";
                return result;
            }

            double maxSpacing = MetresToFeet(rule.MaxSpacingM);
            double maxCoverage = SquareMetresToSquareFeet(rule.MaxCoverageM2);
            double minSpacing = MetresToFeet(SPRINKLERRULES.MinSpacingM);
            double wallClearance = MetresToFeet(SPRINKLERRULES.WallClearanceM);

            // 1. Orient the grid to the room's own walls
            double angle;
            UV min;
            UV max;

            MinAreaRectangle(polygon.Outer, out angle, out min, out max);

            double width = max.U - min.U;
            double height = max.V - min.V;

            // 2. Counts that satisfy both the spacing and the area limit
            int nx = Math.Max(1, (int)Math.Ceiling(width / maxSpacing));
            int ny = Math.Max(1, (int)Math.Ceiling(height / maxSpacing));

            double sx = width / nx;
            double sy = height / ny;

            int guard = 0;

            while (sx * sy > maxCoverage && guard++ < 200)
            {
                // Split whichever direction is currently coarser
                if (sx >= sy)
                {
                    nx++;
                    sx = width / nx;
                }
                else
                {
                    ny++;
                    sy = height / ny;
                }
            }

            // Never crowd sprinklers closer than the standard allows
            while (nx > 1 && sx < minSpacing)
            {
                nx--;
                sx = width / nx;
            }

            while (ny > 1 && sy < minSpacing)
            {
                ny--;
                sy = height / ny;
            }

            result.SpacingXFt = sx;
            result.SpacingYFt = sy;

            // 3. Grid points, each at half a cell from the rectangle edge,
            //    which keeps every sprinkler within spacing/2 of the wall
            var points = new List<UV>();
            int relocated = 0;
            int wallAdjusted = 0;
            int dropped = 0;

            for (int i = 0; i < nx; i++)
            {
                for (int j = 0; j < ny; j++)
                {
                    UV local = new UV(
                        min.U + sx * (i + 0.5),
                        min.V + sy * (j + 0.5));

                    UV world = Rotate(local, angle);

                    UV candidate;
                    bool fromCell = false;

                    if (Contains(polygon, world))
                    {
                        candidate = world;
                    }
                    else if (InRing(polygon.Outer, world) &&
                             NearestClear(
                                 polygon,
                                 angle,
                                 local,
                                 sx,
                                 sy,
                                 wallClearance,
                                 out candidate))
                    {
                        // IN A HOLE - a column, or a wall standing in the
                        // room; a column on the grid line is the everyday
                        // case. The head goes just off it, as near its
                        // grid point as the clearance allows: where a
                        // designer would put it.
                        //
                        // This used to go to CellCentroid below, and for a
                        // column near the middle of its cell the centroid
                        // of the rest of the cell lands straight back IN
                        // the column. The cell was then skipped without a
                        // word, and the gap filling put a head at the
                        // worst uncovered point instead - against a wall.
                        // User-reported 2026-10-07 as one head of a room
                        // coming out off its row, by the wall.
                        fromCell = true;
                    }
                    else
                    {
                        // The cell straddles a wall: aim for the middle of
                        // the part of the cell that is inside the room
                        if (!CellCentroid(
                                polygon,
                                angle,
                                local,
                                sx,
                                sy,
                                out candidate))
                            continue;

                        fromCell = true;
                    }

                    // NFPA 13: a sprinkler sits at least 4 in from the
                    // wall. Rather than lose it, move it in off the wall.
                    UV placed;
                    bool moved;

                    if (!PushInside(
                            polygon,
                            candidate,
                            wallClearance,
                            out placed,
                            out moved))
                    {
                        // Squeezed - between two columns, or a column and
                        // a wall - so walking it off the nearest face only
                        // walks it onto the next. The nearest clear point
                        // of its own cell, rather than dropping it and
                        // letting the gap filling put one against a wall.
                        if (!NearestClear(
                                polygon,
                                angle,
                                local,
                                sx,
                                sy,
                                wallClearance,
                                out placed))
                        {
                            dropped++;
                            continue;
                        }

                        moved = true;
                    }

                    points.Add(placed);

                    if (fromCell)
                        relocated++;

                    if (moved)
                        wallAdjusted++;
                }
            }

            result.Relocated = relocated;
            result.WallAdjusted = wallAdjusted;
            result.Dropped = dropped;

            if (points.Count == 0)
            {
                // A room smaller than one cell still needs one sprinkler
                UV centre;

                if (InteriorPoint(polygon, wallClearance, out centre))
                    points.Add(centre);
                else
                    result.Note = "room too small or too thin to place in";
            }

            // 4. Close any gap the grid left: an irregular room loses the
            //    cells that only clipped it, and a dropped head leaves
            //    its cell unserved. Both show up as uncovered area.
            double reach = 0.5 * Math.Sqrt(sx * sx + sy * sy);

            int added;

            points = FillGaps(
                polygon,
                points,
                reach,
                wallClearance,
                minSpacing,
                out added);

            result.Added = added;

            // 5. How much of the room is actually within reach

            double roomAreaFt2;

            result.Coverage = CoverageRatio(
                polygon,
                points,
                reach,
                out roomAreaFt2);

            result.FreeAreaM2 = SquareFeetToSquareMetres(roomAreaFt2);

            if (points.Count > 0)
            {
                result.CoveragePerSprinklerM2 =
                    SquareFeetToSquareMetres(roomAreaFt2 / points.Count);
            }

            // 6. Back to model coordinates
            foreach (UV p in points)
                result.Points.Add(new XYZ(p.U, p.V, 0));

            return result;
        }

        // ==================================================
        // Room boundary
        // ==================================================

        private static Polygon GetPolygon(Room room, Transform linkToHost)
        {
            var options = new SpatialElementBoundaryOptions
            {
                SpatialElementBoundaryLocation =
                    SpatialElementBoundaryLocation.Finish
            };

            IList<IList<BoundarySegment>> loops =
                room.GetBoundarySegments(options);

            if (loops == null || loops.Count == 0)
                return null;

            var rings = new List<List<UV>>();

            foreach (IList<BoundarySegment> loop in loops)
            {
                var ring = new List<UV>();

                foreach (BoundarySegment segment in loop)
                {
                    Curve curve = segment.GetCurve();

                    if (curve == null)
                        continue;

                    // Tessellate so arcs and splines become polylines
                    IList<XYZ> pts = curve.Tessellate();

                    for (int i = 0; i < pts.Count - 1; i++)
                    {
                        XYZ p = linkToHost.OfPoint(pts[i]);
                        ring.Add(new UV(p.X, p.Y));
                    }
                }

                if (ring.Count >= 3)
                    rings.Add(ring);
            }

            if (rings.Count == 0)
                return null;

            // The largest ring is the room; the rest are holes
            rings.Sort((a, b) => Math.Abs(SignedArea(b))
                .CompareTo(Math.Abs(SignedArea(a))));

            return new Polygon
            {
                Outer = rings[0],
                Holes = rings.Skip(1).ToList()
            };
        }

        // ==================================================
        // Minimum-area rectangle, so the grid follows the walls
        // ==================================================

        private static void MinAreaRectangle(
            List<UV> polygon,
            out double angle,
            out UV min,
            out UV max)
        {
            List<UV> hull = ConvexHull(polygon);

            angle = 0;
            min = new UV(0, 0);
            max = new UV(0, 0);

            double bestArea = double.MaxValue;

            if (hull.Count < 3)
            {
                Bounds(polygon, 0, out min, out max);
                return;
            }

            // The best rectangle always has a side along a hull edge
            for (int i = 0; i < hull.Count; i++)
            {
                UV a = hull[i];
                UV b = hull[(i + 1) % hull.Count];

                double edgeAngle = Math.Atan2(b.V - a.V, b.U - a.U);

                UV lo;
                UV hi;

                Bounds(hull, -edgeAngle, out lo, out hi);

                double area = (hi.U - lo.U) * (hi.V - lo.V);

                if (area < bestArea)
                {
                    bestArea = area;
                    angle = edgeAngle;
                    min = lo;
                    max = hi;
                }
            }
        }

        private static void Bounds(
            List<UV> points,
            double angle,
            out UV min,
            out UV max)
        {
            double minU = double.MaxValue;
            double minV = double.MaxValue;
            double maxU = double.MinValue;
            double maxV = double.MinValue;

            foreach (UV p in points)
            {
                UV r = Rotate(p, angle);

                if (r.U < minU) minU = r.U;
                if (r.V < minV) minV = r.V;
                if (r.U > maxU) maxU = r.U;
                if (r.V > maxV) maxV = r.V;
            }

            min = new UV(minU, minV);
            max = new UV(maxU, maxV);
        }

        private static List<UV> ConvexHull(List<UV> points)
        {
            var sorted = points
                .OrderBy(p => p.U)
                .ThenBy(p => p.V)
                .ToList();

            if (sorted.Count < 3)
                return sorted;

            var hull = new List<UV>();

            // Lower then upper chain
            for (int pass = 0; pass < 2; pass++)
            {
                int start = hull.Count;

                var sequence = pass == 0
                    ? sorted
                    : Enumerable.Reverse(sorted).ToList();

                foreach (UV p in sequence)
                {
                    while (hull.Count >= start + 2 &&
                           Cross(hull[hull.Count - 2],
                                 hull[hull.Count - 1],
                                 p) <= 0)
                    {
                        hull.RemoveAt(hull.Count - 1);
                    }

                    hull.Add(p);
                }

                hull.RemoveAt(hull.Count - 1);
            }

            return hull;
        }

        private static double Cross(UV o, UV a, UV b)
        {
            return (a.U - o.U) * (b.V - o.V) - (a.V - o.V) * (b.U - o.U);
        }

        private static UV Rotate(UV p, double angle)
        {
            double c = Math.Cos(angle);
            double s = Math.Sin(angle);

            return new UV(p.U * c - p.V * s, p.U * s + p.V * c);
        }

        // ==================================================
        // Point tests
        // ==================================================

        // Inside the room, ignoring how close to a wall it is
        private static bool Contains(Polygon polygon, UV point)
        {
            if (!InRing(polygon.Outer, point))
                return false;

            foreach (List<UV> hole in polygon.Holes)
            {
                if (InRing(hole, point))
                    return false;
            }

            return true;
        }

        private static bool Inside(
            Polygon polygon,
            UV point,
            double clearance)
        {
            if (!Contains(polygon, point))
                return false;

            if (clearance > 0 &&
                DistanceToEdges(polygon, point) < clearance)
                return false;

            return true;
        }

        // NFPA 13 keeps a sprinkler at least 4 in off the wall. A head
        // that lands inside the room but too close to a wall is walked
        // in along the wall normal until it clears; a corner may need a
        // few passes because moving off one wall approaches the next.
        private static bool PushInside(
            Polygon polygon,
            UV point,
            double clearance,
            out UV adjusted,
            out bool moved)
        {
            adjusted = point;
            moved = false;

            if (!Contains(polygon, point))
                return false;

            const int passes = 12;

            for (int i = 0; i < passes; i++)
            {
                UV closest;

                double distance = ClosestOnEdges(polygon, adjusted, out closest);

                if (distance >= clearance)
                    return true;

                double dx = adjusted.U - closest.U;
                double dy = adjusted.V - closest.V;
                double length = Math.Sqrt(dx * dx + dy * dy);

                if (length < 1e-9)
                {
                    // Sitting exactly on the wall: step along the inward
                    // normal of that edge instead
                    if (!InwardNormal(polygon, closest, out dx, out dy))
                        return false;

                    length = 1;
                }

                UV candidate = new UV(
                    closest.U + dx / length * clearance * 1.05,
                    closest.V + dy / length * clearance * 1.05);

                if (!Contains(polygon, candidate))
                    return false;

                adjusted = candidate;
                moved = true;
            }

            // Too thin to hold a sprinkler and still clear the walls
            return ClosestOnEdges(polygon, adjusted, out _) >= clearance;
        }

        private static double ClosestOnEdges(
            Polygon polygon,
            UV point,
            out UV closest)
        {
            closest = point;

            double best = double.MaxValue;

            var rings = new List<List<UV>> { polygon.Outer };
            rings.AddRange(polygon.Holes);

            foreach (List<UV> ring in rings)
            {
                for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
                {
                    UV candidate = ClosestOnSegment(ring[j], ring[i], point);

                    double distance = Distance(candidate, point);

                    if (distance < best)
                    {
                        best = distance;
                        closest = candidate;
                    }
                }
            }

            return best;
        }

        private static UV ClosestOnSegment(UV a, UV b, UV p)
        {
            double dx = b.U - a.U;
            double dy = b.V - a.V;

            double lengthSquared = dx * dx + dy * dy;

            if (lengthSquared < 1e-12)
                return a;

            double t = ((p.U - a.U) * dx + (p.V - a.V) * dy) / lengthSquared;

            t = Math.Max(0, Math.Min(1, t));

            return new UV(a.U + t * dx, a.V + t * dy);
        }

        // Picks whichever side of the edge at this point lies in the room
        private static bool InwardNormal(
            Polygon polygon,
            UV onEdge,
            out double dx,
            out double dy)
        {
            dx = 0;
            dy = 0;

            const double probe = 1e-3;

            for (int i = 0; i < 8; i++)
            {
                double angle = Math.PI * 2 * i / 8;

                double px = Math.Cos(angle);
                double py = Math.Sin(angle);

                var test = new UV(
                    onEdge.U + px * probe,
                    onEdge.V + py * probe);

                if (Contains(polygon, test))
                {
                    dx = px;
                    dy = py;
                    return true;
                }
            }

            return false;
        }

        private static bool InRing(List<UV> ring, UV point)
        {
            bool inside = false;

            for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
            {
                if ((ring[i].V > point.V) != (ring[j].V > point.V) &&
                    point.U < (ring[j].U - ring[i].U) *
                              (point.V - ring[i].V) /
                              (ring[j].V - ring[i].V) + ring[i].U)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        private static double DistanceToEdges(Polygon polygon, UV point)
        {
            double best = DistanceToRing(polygon.Outer, point);

            foreach (List<UV> hole in polygon.Holes)
                best = Math.Min(best, DistanceToRing(hole, point));

            return best;
        }

        private static double DistanceToRing(List<UV> ring, UV point)
        {
            double best = double.MaxValue;

            for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
            {
                best = Math.Min(best, DistanceToSegment(ring[j], ring[i], point));
            }

            return best;
        }

        private static double DistanceToSegment(UV a, UV b, UV p)
        {
            double dx = b.U - a.U;
            double dy = b.V - a.V;

            double lengthSquared = dx * dx + dy * dy;

            if (lengthSquared < 1e-12)
                return Distance(a, p);

            double t = ((p.U - a.U) * dx + (p.V - a.V) * dy) / lengthSquared;

            t = Math.Max(0, Math.Min(1, t));

            return Distance(new UV(a.U + t * dx, a.V + t * dy), p);
        }

        private static double Distance(UV a, UV b)
        {
            double dx = a.U - b.U;
            double dy = a.V - b.V;

            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static double SignedArea(List<UV> ring)
        {
            double sum = 0;

            for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
                sum += (ring[j].U * ring[i].V) - (ring[i].U * ring[j].V);

            return sum / 2;
        }

        // ==================================================
        // Cells that straddle a wall
        // ==================================================

        private const int CellSamples = 7;

        // Samples the whole cell, so a thin sliver of room is not lost
        // before the wall clearance is applied by the caller
        private static bool CellCentroid(
            Polygon polygon,
            double angle,
            UV cellCentre,
            double sx,
            double sy,
            out UV centroid)
        {
            centroid = new UV(0, 0);

            double sumU = 0;
            double sumV = 0;
            var hits = new List<UV>();

            for (int i = 0; i < CellSamples; i++)
            {
                for (int j = 0; j < CellSamples; j++)
                {
                    UV local = new UV(
                        cellCentre.U + sx * ((i + 0.5) / CellSamples - 0.5),
                        cellCentre.V + sy * ((j + 0.5) / CellSamples - 0.5));

                    UV world = Rotate(local, angle);

                    if (!Contains(polygon, world))
                        continue;

                    sumU += world.U;
                    sumV += world.V;
                    hits.Add(world);
                }
            }

            // Ignore a cell that barely clips the room
            if (hits.Count < CellSamples * CellSamples * 0.12)
                return false;

            var candidate = new UV(sumU / hits.Count, sumV / hits.Count);

            // THE CENTROID OF A CONCAVE SLIVER CAN LAND OUTSIDE THE ROOM, or
            // in a hole. That used to give up on the cell - silently, so
            // the gap filling replaced it with a head against a wall - and
            // a cell with this much room in it deserves its head. The part
            // of the room nearest the centroid instead.
            if (!Contains(polygon, candidate))
                candidate = hits.OrderBy(h => Distance(h, candidate)).First();

            centroid = candidate;
            return true;
        }

        // Finer than CellSamples: this one decides where the head ends up,
        // not merely which way the cell leans - 1/25 of a 4.5 m cell is
        // 180 mm
        private const int NearSamples = 25;

        // The point of this cell nearest its grid point that clears every
        // wall and every hole edge - for a grid point that fell in a hole
        private static bool NearestClear(
            Polygon polygon,
            double angle,
            UV cellCentre,
            double sx,
            double sy,
            double clearance,
            out UV point)
        {
            point = null;

            UV target = Rotate(cellCentre, angle);

            double best = double.MaxValue;

            for (int i = 0; i < NearSamples; i++)
            {
                for (int j = 0; j < NearSamples; j++)
                {
                    UV local = new UV(
                        cellCentre.U + sx * ((i + 0.5) / NearSamples - 0.5),
                        cellCentre.V + sy * ((j + 0.5) / NearSamples - 0.5));

                    UV world = Rotate(local, angle);

                    if (!Inside(polygon, world, clearance))
                        continue;

                    double d = Distance(world, target);

                    if (d < best)
                    {
                        best = d;
                        point = world;
                    }
                }
            }

            return point != null;
        }

        private static bool InteriorPoint(
            Polygon polygon,
            double clearance,
            out UV point)
        {
            UV min;
            UV max;

            Bounds(polygon.Outer, 0, out min, out max);

            point = new UV(
                (min.U + max.U) / 2,
                (min.V + max.V) / 2);

            if (Inside(polygon, point, clearance))
                return true;

            // Concave room: search the bounding box for any inside point
            const int steps = 20;

            double bestDistance = -1;
            bool found = false;

            for (int i = 0; i <= steps; i++)
            {
                for (int j = 0; j <= steps; j++)
                {
                    var candidate = new UV(
                        min.U + (max.U - min.U) * i / steps,
                        min.V + (max.V - min.V) * j / steps);

                    if (!Inside(polygon, candidate, clearance))
                        continue;

                    // Prefer the point furthest from any wall
                    double distance = DistanceToEdges(polygon, candidate);

                    if (distance > bestDistance)
                    {
                        bestDistance = distance;
                        point = candidate;
                        found = true;
                    }
                }
            }

            return found;
        }

        // ==================================================
        // Gap filling
        //
        // Repeatedly finds the point of the room that is furthest from
        // any sprinkler and, if it is out of reach, puts a head there.
        // Every added head still has to clear the walls and keep the
        // minimum spacing, so the standard is never traded away to
        // chase the number.
        // ==================================================

        private const int GapSamples = 48;

        // Stop once the shortfall is under this, and never add more than
        // this many heads to one room
        private const double GapTarget = 0.01;
        private const int MaxAdded = 30;

        private static List<UV> FillGaps(
            Polygon polygon,
            List<UV> heads,
            double reach,
            double clearance,
            double minSpacing,
            out int added)
        {
            added = 0;

            if (heads.Count == 0 || reach <= 0)
                return heads;

            UV min;
            UV max;

            Bounds(polygon.Outer, 0, out min, out max);

            double cellW = (max.U - min.U) / GapSamples;
            double cellH = (max.V - min.V) / GapSamples;

            if (cellW <= 0 || cellH <= 0)
                return heads;

            var inside = new List<UV>();

            for (int i = 0; i < GapSamples; i++)
            {
                for (int j = 0; j < GapSamples; j++)
                {
                    var p = new UV(
                        min.U + cellW * (i + 0.5),
                        min.V + cellH * (j + 0.5));

                    if (Contains(polygon, p))
                        inside.Add(p);
                }
            }

            if (inside.Count == 0)
                return heads;

            var result = new List<UV>(heads);

            for (int pass = 0; pass < MaxAdded; pass++)
            {
                var gaps = new List<KeyValuePair<double, UV>>();

                foreach (UV p in inside)
                {
                    double nearest = double.MaxValue;

                    foreach (UV head in result)
                    {
                        double d = Distance(head, p);

                        if (d < nearest)
                            nearest = d;
                    }

                    if (nearest > reach)
                        gaps.Add(new KeyValuePair<double, UV>(nearest, p));
                }

                if (gaps.Count == 0)
                    break;

                if ((double)gaps.Count / inside.Count < GapTarget)
                    break;

                // Worst first, so one head closes as much as possible
                gaps.Sort((a, b) => b.Key.CompareTo(a.Key));

                bool placed = false;

                foreach (var gap in gaps)
                {
                    UV candidate;
                    bool moved;

                    if (!PushInside(polygon, gap.Value, clearance, out candidate, out moved))
                        continue;

                    bool tooClose = false;

                    foreach (UV head in result)
                    {
                        if (Distance(head, candidate) < minSpacing)
                        {
                            tooClose = true;
                            break;
                        }
                    }

                    if (tooClose)
                        continue;

                    result.Add(candidate);
                    added++;
                    placed = true;
                    break;
                }

                // Nothing legal left to add
                if (!placed)
                    break;
            }

            return result;
        }

        // ==================================================
        // Coverage check
        // ==================================================

        private const int CoverageSamples = 40;

        private static double CoverageRatio(
            Polygon polygon,
            List<UV> sprinklers,
            double reach,
            out double roomAreaFt2)
        {
            UV min;
            UV max;

            Bounds(polygon.Outer, 0, out min, out max);

            double cellW = (max.U - min.U) / CoverageSamples;
            double cellH = (max.V - min.V) / CoverageSamples;
            double cellArea = cellW * cellH;

            int inside = 0;
            int covered = 0;

            for (int i = 0; i < CoverageSamples; i++)
            {
                for (int j = 0; j < CoverageSamples; j++)
                {
                    var p = new UV(
                        min.U + cellW * (i + 0.5),
                        min.V + cellH * (j + 0.5));

                    if (!Inside(polygon, p, 0))
                        continue;

                    inside++;

                    foreach (UV s in sprinklers)
                    {
                        if (Distance(s, p) <= reach)
                        {
                            covered++;
                            break;
                        }
                    }
                }
            }

            roomAreaFt2 = inside * cellArea;

            return inside == 0 ? 0 : (double)covered / inside;
        }

        // ==================================================
        // Units
        // ==================================================

        public static double MetresToFeet(double metres)
        {
            return UnitUtils.ConvertToInternalUnits(metres, UnitTypeId.Meters);
        }

        public static double SquareMetresToSquareFeet(double squareMetres)
        {
            return UnitUtils.ConvertToInternalUnits(
                squareMetres,
                UnitTypeId.SquareMeters);
        }

        public static double SquareFeetToSquareMetres(double squareFeet)
        {
            return UnitUtils.ConvertFromInternalUnits(
                squareFeet,
                UnitTypeId.SquareMeters);
        }

        public static double FeetToMetres(double feet)
        {
            return UnitUtils.ConvertFromInternalUnits(feet, UnitTypeId.Meters);
        }
    }
}
