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
        static PathlineLengthMapper mapperPathLength;
        static DiffusionMapper mapperDiffusion;
        //static PointSet[] completeCPSets;

        public static void LoadData()
        {
            Console.WriteLine("Output works.");
            Console.WriteLine("Using " + ((IntPtr.Size == 8) ? "x64" : "x32"));


            int numTimeSlices = 10;
            RedSea.Singleton.DataFolder = "E:/Anke/Dev/Data/First/s";
            RedSea.Singleton.FileName = "/Posterior_Diag.nc";

            //Loader ncFile = new Loader(RedSea.Singleton.DataFolder + 1 + RedSea.Singleton.FileName);
            //ScalarField[] u = new ScalarField[numTimeSlices];
            //Loader.SliceRange sliceU = new Loader.SliceRange(ncFile, RedSea.Variable.VELOCITY_X);
            //sliceU.SetMember(RedSea.Dimension.MEMBER, 0); // Average
            //sliceU.SetMember(RedSea.Dimension.TIME, 0);
            //sliceU.SetMember(RedSea.Dimension.CENTER_Z, 0);
            ////sliceU.SetRange(RedSea.Dimension.GRID_X, 2, 100);
            ////sliceU.SetRange(RedSea.Dimension.CENTER_Y, 20, 50);

            //ScalarField[] v = new ScalarField[numTimeSlices];
            //Loader.SliceRange sliceV = new Loader.SliceRange(ncFile, RedSea.Variable.VELOCITY_Y);
            //sliceV.SetMember(RedSea.Dimension.MEMBER, 0);
            //sliceV.SetMember(RedSea.Dimension.TIME, 0);
            //sliceV.SetMember(RedSea.Dimension.CENTER_Z, 0);
            ////sliceV.SetRange(RedSea.Dimension.CENTER_X, 0, 448);
            ////sliceV.SetRange(RedSea.Dimension.GRID_Y, 10, 70);

            //ensembleU = new Loader.SliceRange(ncFile, RedSea.Variable.VELOCITY_X);
            //ensembleU.SetMember(RedSea.Dimension.TIME, 0);
            //ensembleU.SetMember(RedSea.Dimension.CENTER_Z, 0);
            //ensembleU.SetRange(RedSea.Dimension.MEMBER, 2, 50);
            ////ensembleU.SetRange(RedSea.Dimension.GRID_X, 100, 160);
            ////ensembleU.SetRange(RedSea.Dimension.CENTER_Y, 10, 70);
            //ensembleV = new Loader.SliceRange(ncFile, RedSea.Variable.VELOCITY_Y);
            //ensembleV.SetMember(RedSea.Dimension.TIME, 0);
            //ensembleV.SetMember(RedSea.Dimension.CENTER_Z, 0);
            //ensembleV.SetRange(RedSea.Dimension.MEMBER, 2, 50);
            ////ensembleV.SetRange(RedSea.Dimension.CENTER_X, 100, 160);
            ////ensembleV.SetRange(RedSea.Dimension.GRID_Y, 10, 70);

            //ncFile.Close();


            //velocity = Loader.LoadTimeSeries(RedSea.Singleton.DataFolder, RedSea.Singleton.FileName, new Loader.SliceRange[] { sliceU, sliceV }, 0, 10);
            //// Scale the field from m/s to (0.1 degree per 3 days).
            //velocity.ScaleToGrid(new Vec2(RedSea.Singleton.DomainScale));
            velocity = Tests.CreateCircle(new Vec2(0), 200, new Vec2(0.25f), 10, 8);
            //velocity = Tests.CreatePathlineSpiral(99, 100, 2);
            velocity.ScaleToGrid(new Vec2(1.0f));

            Console.WriteLine("Completed loading data.");

            CriticalPointSet2D[] cps = new CriticalPointSet2D[numTimeSlices];
            for (int time = 0; time < numTimeSlices; ++time)
            {
//                cps[time] = FieldAnalysis.ComputeCriticalPointsRegularSubdivision2D(velocity.GetTimeSlice(time), 5, 0.3f);
//                cps[time].SelectTypes(new CriticalPoint2D.TypeCP[] { CriticalPoint2D.TypeCP.ATTRACTING_FOCUS, CriticalPoint2D.TypeCP.REPELLING_FOCUS }).ToBasicSet();

                Console.WriteLine("Completed processing step " + time + '.');
            }

            redSea = new Plane(new Vector3(-10,-3, -5), Vector3.UnitX*0.1f, Vector3.UnitY*0.1f, -Vector3.UnitZ * 3, 0.4f/*10f/size*/, 0.1f);
//            mapperCP = new CriticalPointTracking(cps, velocity, redSea);
            //Console.WriteLine("Found CP.");
            //mapperPathCore = new PathlineCoreTracking(velocity, redSea);
            //Console.WriteLine("Found Pathline Cores.");
//            mapperComparison = new MemberComparison(new Loader.SliceRange[] { sliceU, sliceV }, redSea);
//            mapperOW = new OkuboWeiss(velocity, redSea);
            //Console.WriteLine("Computed Okubo-Weiss.");
            

            Console.WriteLine("Computed all data necessary.");
        }

        public static void CreateRenderables()
        {
            RedSea.Singleton.SetMapper(RedSea.Display.CP_TRACKING, mapperCP);
            RedSea.Singleton.SetMapper(RedSea.Display.PATHLINE_CORES, mapperPathCore);
            RedSea.Singleton.SetMapper(RedSea.Display.MEMBER_COMPARISON, mapperComparison);
            RedSea.Singleton.SetMapper(RedSea.Display.OKUBO_WEISS, mapperOW);
            //mapperFlowMap = new FlowMapMapper(new Loader.SliceRange[] { ensembleU, ensembleV }, redSea, velocity);
            RedSea.Singleton.SetMapper(RedSea.Display.FLOW_MAP_UNCERTAIN, mapperFlowMap);
            mapperPathLength = new PathlineLengthMapper(velocity, redSea);
            RedSea.Singleton.SetMapper(RedSea.Display.PATHLINE_LENGTH, mapperPathLength);
            mapperDiffusion = new DiffusionMapper(velocity, redSea);
            RedSea.Singleton.SetMapper(RedSea.Display.DIFFUSION_MAP, mapperDiffusion);
        }
    }
}
