using System.Collections.Generic;

// STAND-INS for the few Revit types SPRINKLERLAYOUT.cs names, so the REAL
// file compiles here unchanged. The Revit API cannot run outside Revit -
// its NuGet package is reference assemblies only - and the layout uses
// these as plain data:
//
//   - UV and XYZ: constructed and read (.U .V / .X .Y .Z), nothing else;
//   - UnitUtils: metres and square metres to and from Revit's internal
//     feet, with the exact factor (1 ft = 0.3048 m) - THE ONE PLACE A
//     STAND-IN COULD MAKE A TEST LIE, so keep it exact;
//   - the rest serve ExtractShape / GetPolygon, which read a real room's
//     boundary and are never called here.
namespace Autodesk.Revit.DB
{
    public class UV
    {
        public UV(double u, double v)
        {
            U = u;
            V = v;
        }

        public double U { get; }
        public double V { get; }
    }

    public class XYZ
    {
        public XYZ(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public double X { get; }
        public double Y { get; }
        public double Z { get; }
    }

    public class ForgeTypeId
    {
        public ForgeTypeId(string id)
        {
            TypeId = id;
        }

        public string TypeId { get; }
    }

    public static class UnitTypeId
    {
        public static ForgeTypeId Meters { get; } = new ForgeTypeId("autodesk.unit.unit:meters");
        public static ForgeTypeId SquareMeters { get; } = new ForgeTypeId("autodesk.unit.unit:squareMeters");
    }

    public static class UnitUtils
    {
        private const double Foot = 0.3048;

        public static double ConvertToInternalUnits(double value, ForgeTypeId unit)
        {
            return unit == UnitTypeId.SquareMeters ? value / (Foot * Foot) : value / Foot;
        }

        public static double ConvertFromInternalUnits(double value, ForgeTypeId unit)
        {
            return unit == UnitTypeId.SquareMeters ? value * (Foot * Foot) : value * Foot;
        }
    }

    // ---- for ExtractShape / GetPolygon only, never called here ----

    public class Transform
    {
        public XYZ OfPoint(XYZ point)
        {
            return point;
        }
    }

    public class Curve
    {
        public IList<XYZ> Tessellate()
        {
            return new List<XYZ>();
        }
    }

    public class BoundarySegment
    {
        public Curve GetCurve()
        {
            return null;
        }
    }

    public enum SpatialElementBoundaryLocation
    {
        Finish,
        Center,
        CoreBoundary,
        CoreCenter
    }

    public class SpatialElementBoundaryOptions
    {
        public SpatialElementBoundaryLocation SpatialElementBoundaryLocation { get; set; }
    }
}

namespace Autodesk.Revit.DB.Architecture
{
    public class Room
    {
        public IList<IList<Autodesk.Revit.DB.BoundarySegment>> GetBoundarySegments(
            Autodesk.Revit.DB.SpatialElementBoundaryOptions options)
        {
            return null;
        }
    }
}
