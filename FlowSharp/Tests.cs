using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlowSharp
{
    class Tests
    {
        public static void TestCP()
        {
            Random rnd = new Random(DateTime.Today.Millisecond);
            RectlinearGrid grid = new RectlinearGrid(new Index(2, 2));
            ScalarField cell0 = new ScalarField(grid);
            ScalarField cell1 = new ScalarField(grid);

            for (int tests = 0; tests < 100; ++tests)
            {
                for (int i = 0; i < 4; ++i)
                {
                    cell0.Data[i] = (float)rnd.NextDouble() - 0.5f;
                    cell1.Data[i] = (float)rnd.NextDouble() - 0.5f;
                }
                VectorField cell = new VectorField(new ScalarField[] { cell0, cell1 });
                //PointSet<Point> points = FieldAnalysis.ComputeCriticalPointsRegularAnalytical2D(cell);
            }
        }

        public static float RingFieldX(Vector vec)
        {
            float r = (float)Math.Sqrt(vec[0] * vec[0] + vec[1] * vec[1]);
            return -vec[1] / r * (1 - (r - 1) * (r - 1));
        }
        public static float RingFieldY(Vector vec)
        {
            float r = (float)Math.Sqrt(vec[0] * vec[0] + vec[1] * vec[1]);
            return vec[0] / r * (1 - (r - 1) * (r - 1));
        }
        public static VectorField GetRingField(int numCells)
        {
            ScalarField[] fields = new ScalarField[2];
            Index size = new Index(numCells + 1, 2);
            Vector origin = new Vector(-2, 2);
            Vector cell = new Vector(4.0f / numCells, 2);
            fields[0] = ScalarField.FromAnalyticalField(RingFieldX, size, origin, cell);
            fields[1] = ScalarField.FromAnalyticalField(RingFieldY, size, origin, cell);

            return new VectorField(fields);
        }

        public static float CircleX(Vector vec)
        {
            float r = (float)Math.Sqrt(vec[0] * vec[0] + vec[1] * vec[1]);
            if (r == 0 || Math.Abs(1- r)<0)
                return 0; 
            return -vec[1] / r * (1 - (1- r)*(1 - r)) * 20;
        }

        public static float CircleY(Vector vec)
        {
            float r = (float)Math.Sqrt(vec[0] * vec[0] + vec[1] * vec[1]);
            if (r == 0 || Math.Abs(1- r) < 0)
                return 0;
            return vec[0] / r * (1 - (1- r)*(1 - r)) * 20;
        }

        public static VectorFieldUnsteady CreateCircle(Vec2 center, int numCells, Vec2 dir, int numSlices, float domainR = 2)
        {
            Vector origin = center - new Vec2(domainR);
            Vector cell = new Vec2(2* domainR / numCells);
            Index size = new Index(numCells + 1, 2);

            ScalarField[] vX = new ScalarField[numSlices];
            ScalarField[] vY = new ScalarField[numSlices];

            for (int slice = 0; slice < numSlices; ++slice)
            {
                vX[slice] = ScalarField.FromAnalyticalField(CircleX, size, origin + dir*slice, cell);
                vY[slice] = ScalarField.FromAnalyticalField(CircleY, size, origin + dir*slice, cell);
            }

            return new VectorFieldUnsteady(new ScalarFieldUnsteady[] {new ScalarFieldUnsteady(vX), new ScalarFieldUnsteady(vY)});
        }

        public static VectorFieldUnsteady CreatePathlineSpiral(int numCells, int numSlices, float domainR = 2)
        {
            Vector origin = new Vec2(-domainR);
            Vector cell = new Vec2(2 * domainR / numCells);
            Index size = new Index(numCells + 1, 2);

            ScalarField[] vX = new ScalarField[numSlices];
            ScalarField[] vY = new ScalarField[numSlices];

            for (int slice = 0; slice < numSlices; ++slice)
            {
                vX[slice] = ScalarField.FromAnalyticalField(x =>(float)Math.Cos((float)slice / 3)*4, size, origin, cell);
                vY[slice] = ScalarField.FromAnalyticalField(x =>(float)Math.Sin((float)slice / 3)*4, size, origin, cell);
            }

            return new VectorFieldUnsteady(new ScalarFieldUnsteady[] { new ScalarFieldUnsteady(vX), new ScalarFieldUnsteady(vY) });
        }

    }
}
