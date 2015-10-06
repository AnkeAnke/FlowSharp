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
            RectlinearGrid grid = new RectlinearGrid(new Index(2, 2), new Vector(0, 2), new Vector(1, 2));
            ScalarField cell0 = new ScalarField(grid);
            ScalarField cell1 = new ScalarField(grid);

            for (int tests = 0; tests < 100; ++tests)
            {
                for(int i = 0; i < 4; ++ i)
                {
                    cell0.Data[i] = (float)rnd.NextDouble() - 0.5f;
                    cell1.Data[i] = (float)rnd.NextDouble() - 0.5f;
                }
                VectorField cell = new VectorField(new ScalarField[] { cell0, cell1 });
                PointSet points = FieldAnalysis.ComputeCriticalPointsRegularAnalytical2D(cell);
            }
        }
    }
}
