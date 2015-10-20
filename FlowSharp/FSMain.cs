using SlimDX;
using System;
using PointSet = FlowSharp.PointSet<FlowSharp.Point>;

namespace FlowSharp
{
    class FSMain
    {
        static VectorFieldUnsteady velocity;
        //static VectorField velocityT0;
        //static VectorField velocityT1;
        ////static VectorField fff;
        //static CriticalPointSet2D seedData;
        //static CriticalPointSet2D cpT0;
        //static CriticalPointSet2D cpT1;
        //static LineSet cpLinesPos, cpLinesNeg;

        //static PointSet<Point> colorCoded;

        //static CriticalPointSet2D[] allCpSlices;
        //static VectorField[] allTimeSlices;
        //static LineSet[] allCPLines;
        //static PointSet<Point>[] allCPLinesPoints;

        static DataMapper mapper;
        //static PointSet[] completeCPSets;

        public static void LoadData()
        {
            // Playground.
            //SquareMatrix J = new SquareMatrix(new Vec3[] { new Vec3(-74, 3, 7), new Vec3(2, -61, 81), new Vec3(3, 6, -40) });
            //float ux, uy, ut, vx, vy, vt;
            //ux = J[0][0]; uy = J[1][0]; ut = J[2][0];
            //vx = J[0][1]; vy = J[1][1]; vt = J[2][1];
            //Vec3 correct = new Vec3(uy * vt - ut * vy, ut * vx - ux * vt, ux * vy - uy * vx);
            //Vec3 det = new Vec3(new SquareMatrix(new Vec2[] { J[1].ToVec2(), J[2].ToVec2() }).Determinant(),
            //         new SquareMatrix(new Vec2[] { J[2].ToVec2(), J[0].ToVec2() }).Determinant(),
            //         new SquareMatrix(new Vec2[] { J[0].ToVec2(), J[1].ToVec2() }).Determinant());
            //Vec3 cross = Vec3.Cross(J.Row(0).AsVec3(), J.Row(1).AsVec3());
            //Console.WriteLine("Correct: " + correct);
            //Console.WriteLine("det: " + det);
            //Console.WriteLine("Cross: " + cross);




            int numTimeSlices = 8;
            Loader ncFile = new Loader("E:/Anke/Dev/Data/First/s1/Posterior_Diag.nc");
            ScalarField[] u = new ScalarField[numTimeSlices];
            Loader.SliceRange sliceU = new Loader.SliceRange(ncFile, RedSea.Variable.VELOCITY_X);
            sliceU.SetOffset(RedSea.Dimension.MEMBER, 0); // Average
            sliceU.SetOffset(RedSea.Dimension.TIME, 0);
            sliceU.SetOffset(RedSea.Dimension.CENTER_Z, 0);

            ScalarField[] v = new ScalarField[numTimeSlices];
            Loader.SliceRange sliceV = new Loader.SliceRange(ncFile, RedSea.Variable.VELOCITY_Y);
            sliceV.SetOffset(RedSea.Dimension.MEMBER, 0);
            sliceV.SetOffset(RedSea.Dimension.TIME, 0);
            sliceV.SetOffset(RedSea.Dimension.CENTER_Z, 0);

            // Load first time slice.
            u[0] = ncFile.LoadFieldSlice(sliceU);
            v[0] = ncFile.LoadFieldSlice(sliceV);

            ncFile.Close();

            for (int time = 1; time < numTimeSlices; ++time)
            {
                ncFile = new Loader("E:/Anke/Dev/Data/First/s" + (time + 1) + "/Posterior_Diag.nc");
                u[time] = ncFile.LoadFieldSlice(sliceU);
                v[time] = ncFile.LoadFieldSlice(sliceV);
                ncFile.Close();
            }

            ScalarFieldUnsteady uTime = new ScalarFieldUnsteady(u);
            ScalarFieldUnsteady vTime = new ScalarFieldUnsteady(v);
            velocity = new VectorFieldUnsteady(new ScalarFieldUnsteady[] { uTime, vTime });
            //Random rnd = new Random(732623);

            //ScalarField[] uRnd = new ScalarField[numTimeSlices];
            //ScalarField[] vRnd = new ScalarField[numTimeSlices];
            //float size = 8;
            //uRnd[0] = ScalarField.FromAnalyticalField(x => (float)Math.Sin(x[0])/*(float)rnd.NextDouble() - 0.5f*/, new Index(numTimeSlices, 2), new Vector(- size/2, 2), new Vector(size / (numTimeSlices-1), 2));
            //vRnd[0] = ScalarField.FromAnalyticalField(x => (float)Math.Cos(x[1])/*(float)rnd.NextDouble() - 0.5f*/, new Index(numTimeSlices, 2), new Vector(- size/2, 2), new Vector(size / (numTimeSlices-1), 2));

            //for (int time = 1; time < numTimeSlices; ++time)
            //{
            //    uRnd[time] = new ScalarField(uRnd[time - 1], (s, g) => (s + (float)rnd.NextDouble()) - 0.5f);
            //    vRnd[time] = new ScalarField(vRnd[time - 1], (s, g) => (s + (float)rnd.NextDouble()) - 0.5f);
            //}                                                               

            //ScalarFieldUnsteady uRndTime = new ScalarFieldUnsteady(uRnd, 0, 1);
            //ScalarFieldUnsteady vRndTime = new ScalarFieldUnsteady(vRnd, 0, 1);
            //velocity = new VectorFieldUnsteady(new ScalarFieldUnsteady[] { uRndTime, vRndTime });
            //((RectlinearGrid)velocity.Grid).CellSize.T = 0.3f;

            Console.WriteLine("Completed loading data.");

            CriticalPointSet2D[] cps = new CriticalPointSet2D[numTimeSlices];
            for (int time = 0; time < numTimeSlices; ++time)
            {
                cps[time] = FieldAnalysis.ComputeCriticalPointsRegularSubdivision2D(velocity.GetTimeSlice(time), 8, 0.3f);
                //cps.SelectTypes(new CriticalPoint2D.TypeCP[] { CriticalPoint2D.TypeCP.ATTRACTING_FOCUS, CriticalPoint2D.TypeCP.REPELLING_FOCUS }).ToBasicSet();

                Console.WriteLine("Completed processing step " + time + '.');
            }

            Plane redSea = new Plane(new Vector3(-10,-3, -5), Vector3.UnitX, Vector3.UnitY, -Vector3.UnitZ * 3, 0.4f/*10f/size*/, 0.1f);
            mapper = new CriticalPointTracking(cps, velocity, redSea);

            Console.WriteLine("Computed all data necessary.");
        }

        public static void CreateRenderables()
        {
            RedSea.Singleton.SetMapper(RedSea.Display.CP_TRACKING, mapper);
        }
    }
}
