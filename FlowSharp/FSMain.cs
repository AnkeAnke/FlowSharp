using SlimDX;
using System;
using PointSet = FlowSharp.PointSet<FlowSharp.Point>;
using Microsoft.Research.ScientificDataSet.NetCDF4;
using System.Diagnostics;

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

        static DataMapper mapperCP;
        static DataMapper mapperPathCore;
        static DataMapper mapperComparison;
        static DataMapper mapperOW;
        static Loader.SliceRange ensembleU, ensembleV;
        static Plane redSea;
        static DataMapper mapperFlowMap;
        //static PointSet[] completeCPSets;

        public static void LoadData()
        {
            Console.WriteLine("Output works.");
            Console.WriteLine("Using " + ((IntPtr.Size == 8) ? "x64" : "x32"));


            int numTimeSlices = 10;
            RedSea.Singleton.DataFolder = "E:/Anke/Dev/Data/First/s";
            RedSea.Singleton.FileName = "/Posterior_Diag.nc";
            Loader ncFile = new Loader(RedSea.Singleton.DataFolder + 1 + RedSea.Singleton.FileName);
            ScalarField[] u = new ScalarField[numTimeSlices];
            Loader.SliceRange sliceU = new Loader.SliceRange(ncFile, RedSea.Variable.VELOCITY_X);
            sliceU.SetMember(RedSea.Dimension.MEMBER, 0); // Average
            sliceU.SetMember(RedSea.Dimension.TIME, 0);
            sliceU.SetMember(RedSea.Dimension.CENTER_Z, 0);

            ScalarField[] v = new ScalarField[numTimeSlices];
            Loader.SliceRange sliceV = new Loader.SliceRange(ncFile, RedSea.Variable.VELOCITY_Y);
            sliceV.SetMember(RedSea.Dimension.MEMBER, 0);
            sliceV.SetMember(RedSea.Dimension.TIME, 0);
            sliceV.SetMember(RedSea.Dimension.CENTER_Z, 0);

            ensembleU = new Loader.SliceRange(ncFile, RedSea.Variable.VELOCITY_X);
            ensembleU.SetMember(RedSea.Dimension.TIME, 0);
            ensembleU.SetMember(RedSea.Dimension.CENTER_Z, 0);
            ensembleU.SetRange(RedSea.Dimension.MEMBER, 2, 50);
            ensembleV = new Loader.SliceRange(ncFile, RedSea.Variable.VELOCITY_Y);
            ensembleV.SetMember(RedSea.Dimension.TIME, 0);
            ensembleV.SetMember(RedSea.Dimension.CENTER_Z, 0);
            ensembleV.SetRange(RedSea.Dimension.MEMBER, 2, 50);

            //float[] data = new float[5];
            //NetCDF.ResultCode ncState = NetCDF.nc_get_vara_float(ncFile.GetID(), (int)sliceV.GetVariable(), new int[] { 0, 0, 0, 0, 0 }, new int[] { 1, 1, 1, 1, 1 }, data);
            //Debug.Assert(ncState == NetCDF.ResultCode.NC_NOERR, ncState.ToString());
            //Console.WriteLine(ncState.ToString());
            // Load first time slice.
            u[0] = ncFile.LoadFieldSlice(sliceU);
            v[0] = ncFile.LoadFieldSlice(sliceV);

            ncFile.Close();

            for (int time = 1; time < numTimeSlices; ++time)
            {
                ncFile = new Loader(RedSea.Singleton.DataFolder + (time + 1) + RedSea.Singleton.FileName);
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
//                cps[time] = FieldAnalysis.ComputeCriticalPointsRegularSubdivision2D(velocity.GetTimeSlice(time), 5, 0.3f);
                //cps.SelectTypes(new CriticalPoint2D.TypeCP[] { CriticalPoint2D.TypeCP.ATTRACTING_FOCUS, CriticalPoint2D.TypeCP.REPELLING_FOCUS }).ToBasicSet();

                Console.WriteLine("Completed processing step " + time + '.');
            }

            redSea = new Plane(new Vector3(-10,-3, -5), Vector3.UnitX, Vector3.UnitY, -Vector3.UnitZ * 3, 0.4f/*10f/size*/, 0.1f);
            //            mapperCP = new CriticalPointTracking(cps, velocity, redSea);
            //            Console.WriteLine("Found CP.")
            //            mapperPathCore = new PathlineCoreTracking(velocity, redSea);
            //            Console.WriteLine("Found Pathline Cores.")
            mapperComparison = new MemberComparison(new Loader.SliceRange[] { sliceU, sliceV }, redSea);
            // mapperOW = new OkuboWeiss(velocity, redSea);
            //            Console.WriteLine("Computed Okubo-Weiss.")
            

            Console.WriteLine("Computed all data necessary.");
        }

        public static void CreateRenderables()
        {
            RedSea.Singleton.SetMapper(RedSea.Display.CP_TRACKING, mapperCP);
            RedSea.Singleton.SetMapper(RedSea.Display.PATHLINE_CORES, mapperPathCore);
            RedSea.Singleton.SetMapper(RedSea.Display.MEMBER_COMPARISON, mapperComparison);
            RedSea.Singleton.SetMapper(RedSea.Display.OKUBO_WEISS, mapperOW);
            mapperFlowMap = new FlowMapMapper(new Loader.SliceRange[] { ensembleU, ensembleV }, redSea);
            RedSea.Singleton.SetMapper(RedSea.Display.FLOW_MAP_UNCERTAIN, mapperFlowMap);
        }
    }
}
