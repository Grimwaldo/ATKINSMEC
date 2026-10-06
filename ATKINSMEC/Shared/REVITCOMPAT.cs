using Autodesk.Revit.DB;

namespace ATKINSMEC
{
    // ElementId went 64-bit in Revit 2024: IntegerValue and the int
    // constructor were deprecated then, and IntegerValue is gone in
    // 2027. Everything in this project goes through these two helpers
    // so the same source builds for every supported version.
    public static class REVITCOMPAT
    {
        public static long ToValue(this ElementId id)
        {
            if (id == null)
                return -1;

#if REVIT2024_OR_NEWER
            return id.Value;
#else
            return id.IntegerValue;
#endif
        }

        public static ElementId NewId(long value)
        {
#if REVIT2024_OR_NEWER
            return new ElementId(value);
#else
            return new ElementId((int)value);
#endif
        }
    }
}
