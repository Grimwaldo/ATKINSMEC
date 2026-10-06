using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ATKINSMEC
{
    public class AirData
    {
        public double FreshAir { get; set; }
        public double ExhaustAir { get; set; }
    }
    public class SpaceKey
    {
        public string SpaceName { get; set; }
        public string RoomNumber { get; set; }
        public string Level { get; set; }

        public override bool Equals(object obj)
        {
            var other = obj as SpaceKey;

            if (other == null)
                return false;

            return SpaceName == other.SpaceName
                && RoomNumber == other.RoomNumber
                && Level == other.Level;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + (SpaceName?.GetHashCode() ?? 0);
                hash = hash * 23 + RoomNumber.GetHashCode();
                hash = hash * 23 + (Level?.GetHashCode() ?? 0);
                return hash;
            }
        }
    }
}

