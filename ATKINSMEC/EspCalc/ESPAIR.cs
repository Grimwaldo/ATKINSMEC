using System;

namespace ATKINSMEC
{
    // THE AIR SIDE OF THE ESP CALCULATION, and nothing else: areas,
    // velocities, friction and velocity pressure. SI throughout - metres,
    // m3/s, Pa - because the Revit side converts once on the way in and
    // everything after that is arithmetic.
    //
    // Revit-free, so scratchpad/esptest checks it against numbers worked
    // independently rather than against itself.
    public static class ESPAIR
    {
        // Air at about 20 C. Density is a setting (ESPSETTINGS) because a
        // job in a hot climate or at altitude wants its own; viscosity
        // moves the answer far less and stays fixed.
        public const double Viscosity = 1.81e-5;   // Pa.s

        public static double Area(ESPNET.Shape shape, double diameter, double width, double height)
        {
            switch (shape)
            {
                case ESPNET.Shape.Round:
                    return Math.PI * diameter * diameter / 4;

                case ESPNET.Shape.Rectangular:
                    return width * height;

                case ESPNET.Shape.Oval:
                    {
                        // Flat oval: a rectangle with a half circle on
                        // each end, whichever way round it was drawn
                        double major = Math.Max(width, height);
                        double minor = Math.Min(width, height);

                        return Math.PI * minor * minor / 4 + minor * (major - minor);
                    }
            }

            return 0;
        }

        public static double Perimeter(ESPNET.Shape shape, double diameter, double width, double height)
        {
            switch (shape)
            {
                case ESPNET.Shape.Round:
                    return Math.PI * diameter;

                case ESPNET.Shape.Rectangular:
                    return 2 * (width + height);

                case ESPNET.Shape.Oval:
                    {
                        double major = Math.Max(width, height);
                        double minor = Math.Min(width, height);

                        return Math.PI * minor + 2 * (major - minor);
                    }
            }

            return 0;
        }

        // 4A/P. Darcy-Weisbach with the hydraulic diameter and the ACTUAL
        // velocity is the standard treatment of a non-circular duct in
        // turbulent flow.
        public static double HydraulicDiameter(ESPNET.Shape shape, double diameter, double width, double height)
        {
            double perimeter = Perimeter(shape, diameter, width, height);

            return perimeter <= 0
                ? 0
                : 4 * Area(shape, diameter, width, height) / perimeter;
        }

        public static double VelocityPressure(double velocity, double density)
        {
            return density * velocity * velocity / 2;
        }

        public static double Reynolds(double velocity, double hydraulicDiameter, double density)
        {
            return density * Math.Abs(velocity) * hydraulicDiameter / Viscosity;
        }

        // The Darcy friction factor. Laminar below Re 2300 (64/Re), and
        // Colebrook-White above it, solved by iteration from Swamee-Jain.
        // The transition zone is taken as turbulent: a duct carrying air
        // at any velocity worth calculating is well past it.
        public static double FrictionFactor(double reynolds, double relativeRoughness)
        {
            if (reynolds <= 0)
                return 0;

            if (reynolds < 2300)
                return 64 / reynolds;

            double rr = Math.Max(0, relativeRoughness);

            // Swamee-Jain, to start from somewhere close
            double start = Math.Log10(rr / 3.7 + 5.74 / Math.Pow(reynolds, 0.9));

            double x = 1 / Math.Sqrt(0.25 / (start * start));

            for (int i = 0; i < 50; i++)
            {
                double next = -2 * Math.Log10(rr / 3.7 + 2.51 * x / reynolds);

                if (Math.Abs(next - x) < 1e-12)
                {
                    x = next;
                    break;
                }

                x = next;
            }

            return 1 / (x * x);
        }

        // Pa per metre of straight duct carrying this flow
        public static double FrictionPerMetre(
            double flow, ESPNET.Shape shape, double diameter, double width, double height,
            double roughness, double density, out double velocity)
        {
            velocity = 0;

            double area = Area(shape, diameter, width, height);
            double dh = HydraulicDiameter(shape, diameter, width, height);

            if (area <= 0 || dh <= 0 || flow <= 0)
                return 0;

            velocity = flow / area;

            double f = FrictionFactor(Reynolds(velocity, dh, density), roughness / dh);

            return f / dh * VelocityPressure(velocity, density);
        }
    }
}
