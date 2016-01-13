using SlimDX;
using System;
using PointSet = FlowSharp.PointSet<FlowSharp.Point>;
using Microsoft.Research.ScientificDataSet.NetCDF4;
using System.Diagnostics;
using System.IO;

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
        static LoaderNCF.SliceRange ensembleU, ensembleV;
        static Plane redSea;
        static DataMapper mapperFlowMap;
        static PathlineLengthMapper mapperPathLength;
        static DiffusionMapper mapperCutDiffusion;
        static LocalDiffusionMapper mapperLocalDiffusion;
        //static PointSet[] completeCPSets;


        //public static ScalarField LoadField(LoaderNCF.SliceRange range, int timestep, int? subtimestep)
        //{
        //    //ScalarField field;
        //    //// Do we need to look at raw data?
        //    //if (range.GetVariable() == RedSea.Variable.VELOCITY_Z || subtimestep != null || subtimestep != 108)
        //    //{
        //    //}
        //    //// We can read the CDF file.
        //    //else
        //    //{
        //    //    field = 
        //    //}

        //    return field;
        //}            string locDataFolder = "E:/Anke/Dev/Data/Shaheen_8/s"; //"E:/Anke/Dev/Data/First/s";
        static string locDataFolder = "E:/Anke/Dev/Data/Shaheen_8/s"; //"E:/Anke/Dev/Data/First/s";
        static string locFileName = "/Posterior_Diag.nc";
        static string locFolderName = "/advance_temp";
        //        string locWFileName = ".0000000108.data";

        /// <summary>
        /// Loads the field using either 
        /// </summary>
        /// <param name="step"></param>
        /// <param name="substep"></param>
        /// <param name="var"></param>
        /// <returns>Should the NetCDF loader be used?</returns>
        public static Loader RedSeaLoader(int step, int? substep, int? member, RedSea.Variable var)
        {
            string dir = locDataFolder + (step + 1);

            // Look for raw file.
            if(substep != null || var == RedSea.Variable.VELOCITY_Z)
            {
                // Not the W case: go into the inner folder.
                if(substep != null)
                    dir += locFolderName + member +'/';

                string filename = RedSea.GetShortName(var) + ".0*" + (substep + 1) + ".data_scaled_end";
                string[] rawDirs = Directory.GetFiles(dir, RedSea.GetShortName(var) + ".0*" + (substep + 1) + ".data_scaled_end", SearchOption.TopDirectoryOnly);
                Debug.Assert(rawDirs.Length == 1, "Exactly one matching file expected!");

                dir = rawDirs[0];
                return new LoaderRaw(dir);
            }
            else
            {
                dir += locFileName;
                return new LoaderNCF(dir);
            }

//            return (step, substep, var) => locDataFolder + (step + 1) + ((substep == null) ? (var == RedSea.Variable.VELOCITY_Z ? "/W" + locWFileName : locFileName) : (locFolderName + substep) + "/" + "S" + locWFileName);
        }

        public static void LoadData()
        {
            Console.WriteLine("Output works.");
            Console.WriteLine("Using " + ((IntPtr.Size == 8) ? "x64" : "x32"));
            bool loadData = true;

            int numTimeSlices =  42;
            RedSea.Singleton.NumTimeSlices = numTimeSlices;
            //string locDataFolder = "E:/Anke/Dev/Data/Shaheen_8/s"; //"E:/Anke/Dev/Data/First/s";
            //string locFileName = "/Posterior_Diag.nc";
            //string locFolderName = "/advance_temp";
            //string locWFileName = ".0000000108.data";

            RedSea.Singleton.GetLoader = RedSeaLoader; //= (step, substep, var) => locDataFolder + (step + 1) + ((substep == null)?(var == RedSea.Variable.VELOCITY_Z? "/W" + locWFileName : locFileName) : (locFolderName + substep) + "/" + "S" + locWFileName);

            //Tests.CopyBeginningOfFile(RedSea.Singleton.GetFilename(0), 100000);

            LoaderNCF ncFile = RedSea.Singleton.GetLoaderNCF(0);
            ScalarField[] u = new ScalarField[numTimeSlices];
            LoaderNCF.SliceRange sliceU = new LoaderNCF.SliceRange(ncFile, RedSea.Variable.VELOCITY_X);
            sliceU.SetMember(RedSea.Dimension.MEMBER, 0); // Average
            sliceU.SetMember(RedSea.Dimension.TIME, 0);
            sliceU.SetMember(RedSea.Dimension.CENTER_Z, 0);
            //sliceU.SetRange(RedSea.Dimension.GRID_X, 300, 100);
            //sliceU.SetRange(RedSea.Dimension.CENTER_Y, 20, 100);

            ScalarField[] v = new ScalarField[numTimeSlices];
            LoaderNCF.SliceRange sliceV = new LoaderNCF.SliceRange(ncFile, RedSea.Variable.VELOCITY_Y);
            sliceV.SetMember(RedSea.Dimension.MEMBER, 0);
            sliceV.SetMember(RedSea.Dimension.TIME, 0);
            sliceV.SetMember(RedSea.Dimension.CENTER_Z, 0);
            //sliceV.SetRange(RedSea.Dimension.CENTER_X, 300, 100);
            //sliceV.SetRange(RedSea.Dimension.GRID_Y, 20, 100);

            ensembleU = new LoaderNCF.SliceRange(ncFile, RedSea.Variable.VELOCITY_X);
            ensembleU.SetMember(RedSea.Dimension.TIME, 0);
            ensembleU.SetMember(RedSea.Dimension.CENTER_Z, 0);
            ensembleU.SetRange(RedSea.Dimension.MEMBER, 2, 50);
            //ensembleU.SetRange(RedSea.Dimension.GRID_X, 100, 160);
            //ensembleU.SetRange(RedSea.Dimension.CENTER_Y, 10, 70);
            ensembleV = new LoaderNCF.SliceRange(ncFile, RedSea.Variable.VELOCITY_Y);
            ensembleV.SetMember(RedSea.Dimension.TIME, 0);
            ensembleV.SetMember(RedSea.Dimension.CENTER_Z, 0);
            ensembleV.SetRange(RedSea.Dimension.MEMBER, 2, 50);
            //ensembleV.SetRange(RedSea.Dimension.CENTER_X, 100, 160);
            //ensembleV.SetRange(RedSea.Dimension.GRID_Y, 10, 70);

            ncFile.Close();


            if (loadData)
            {
                velocity = LoaderNCF.LoadTimeSeries(RedSea.Singleton.GetLoaderNCF, new LoaderNCF.SliceRange[] { sliceU, sliceV }, 0, 10);
                // Scale the field from m/s to (0.1 degree per 3 days).
                velocity.ScaleToGrid(new Vec2(RedSea.Singleton.DomainScale));
            }
            else
            {
                velocity = Tests.CreateCircle(new Vec2(0), 50, new Vec2(0f), 10, 8);
                //velocity = Tests.CreatePathlineSpiral(99, 100, 2);
                velocity.ScaleToGrid(new Vec2(1.0f));
            }

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
            mapperComparison = new MemberComparison(new LoaderNCF.SliceRange[] { sliceU, sliceV }, redSea);
            //mapperOW = new OkuboWeiss(velocity, redSea);
            //Console.WriteLine("Computed Okubo-Weiss.");
           

            Console.WriteLine("Computed all data necessary.");
        }

        public static void CreateRenderables()
        {
            RedSea.Singleton.SetMapper(RedSea.Display.CP_TRACKING, mapperCP);
            RedSea.Singleton.SetMapper(RedSea.Display.PATHLINE_CORES, mapperPathCore);
            RedSea.Singleton.SetMapper(RedSea.Display.MEMBER_COMPARISON, mapperComparison);
            RedSea.Singleton.SetMapper(RedSea.Display.OKUBO_WEISS, mapperOW);

            mapperFlowMap = new FlowMapMapper(new LoaderNCF.SliceRange[] { ensembleU, ensembleV }, redSea, velocity);
            RedSea.Singleton.SetMapper(RedSea.Display.FLOW_MAP_UNCERTAIN, mapperFlowMap);

            mapperPathLength = new PathlineLengthMapper(velocity, redSea);
            RedSea.Singleton.SetMapper(RedSea.Display.PATHLINE_LENGTH, mapperPathLength);

            mapperCutDiffusion = new DiffusionMapper(velocity, redSea);
            RedSea.Singleton.SetMapper(RedSea.Display.CUT_DIFFUSION_MAP, mapperCutDiffusion);

            mapperLocalDiffusion = new LocalDiffusionMapper(velocity, redSea);
            RedSea.Singleton.SetMapper(RedSea.Display.LOCAL_DIFFUSION_MAP, mapperLocalDiffusion);

            SubstepViewer substep = new SubstepViewer(redSea);
            RedSea.Singleton.SetMapper(RedSea.Display.SUBSTEP_VIEWER, substep);

            //FieldAnalysis.AlphaStableFFF = 0;
            //var f = new VectorField(velocity, FieldAnalysis.StableFFF, 3, true);
            //Renderer.Singleton.AddRenderable(new FieldPlane(redSea, f.GetSlice(0), FieldPlane.RenderEffect.LIC));

        }
    }
}
