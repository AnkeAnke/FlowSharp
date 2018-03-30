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

        //}            string locDataFolder = "E:/Anke/Dev/Data/Shaheen_8/s"; //"E:/Anke/Dev/Data/First/s";
        static string locDataFolder = "D:/EddyData/s"; //"E:/Anke/Dev/Data/First/s";
        static string locDataFolderSubstep = "E:/RedSeaSubsteps/s";
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
            string dir = RedSea.Singleton.GetFilename(step, substep, member, var);

            // Look for raw file.
            if (substep != null || var == RedSea.Variable.VELOCITY_Z)
            {
                var loader = new LoaderRaw(var);
                loader.Range.SetMember(RedSea.Dimension.TIME, step);
                loader.Range.SetMember(RedSea.Dimension.SUBTIME, substep??0);
                loader.Range.SetMember(RedSea.Dimension.MEMBER, member??0);
                return loader;
            }
            else
            {
                return new LoaderNCF(dir);
            }
        }

        public static string RedSeaFilenames(int step, int? substep, int? member, RedSea.Variable var)
        {
            string dir = locDataFolder + (step + 1);

            // Look for raw file.
            //if (substep != null || var == RedSea.Variable.VELOCITY_Z)
            //{
            substep = substep ?? 0;
            dir = locDataFolderSubstep + (step + 1);

            // Not the W case: go into the inner folder.
            if (substep != null)
                dir += locFolderName + member + '/';

            //string filename = RedSea.GetShortName(var) + ".0*" + (substep + 1) + ".data_scaled_end";
            Console.WriteLine("Step {0}, Substep {1}", step, substep);
            int numZeros = 10 - (substep == 0 ? 1 : (int)(Math.Log10((int)substep * 9) + 1));
            string filename = RedSea.GetShortName(var) + "." + new string('0', numZeros) + (substep * 9) + ".data";
            Console.WriteLine(filename);
            string[] rawDirs = Directory.GetFiles(dir, filename, SearchOption.TopDirectoryOnly);
            Debug.Assert(rawDirs.Length == 1, "Exactly one matching file expected!");

            return rawDirs[0];
            //}
            //else
            //{
            //    dir += locFileName;
            //    return dir;
            //}
        }

        public static void LoadData()
        {
            System.Globalization.CultureInfo customCulture = (System.Globalization.CultureInfo)System.Threading.Thread.CurrentThread.CurrentCulture.Clone();
            customCulture.NumberFormat.NumberDecimalSeparator = ".";

            System.Threading.Thread.CurrentThread.CurrentCulture = customCulture;

            Console.WriteLine("Output works.");
            Console.WriteLine("Using " + (Environment.Is64BitProcess ? "x64" : "x32"));
            bool loadData = true;

            int numTimeSlices = 60;
            RedSea.Singleton.NumTimeSlices = numTimeSlices;
            //string locDataFolder = "E:/Anke/Dev/Data/Shaheen_8/s"; //"E:/Anke/Dev/Data/First/s";
            //string locFileName = "/Posterior_Diag.nc";
            //string locFolderName = "/advance_temp";
            //string locWFileName = ".0000000108.data";

            RedSea.Singleton.GetLoader = RedSeaLoader; //= (step, substep, var) => locDataFolder + (step + 1) + ((substep == null)?(var == RedSea.Variable.VELOCITY_Z? "/W" + locWFileName : locFileName) : (locFolderName + substep) + "/" + "S" + locWFileName);
            RedSea.Singleton.GetFilename = RedSeaFilenames;
            RedSea.Singleton.DonutFileName ="D:/KTH/Projects/EddyRedo/Data/Donut";
            RedSea.Singleton.DiskFileName = "D:/KTH/Projects/EddyRedo/Data/Disks/Disk";
            RedSea.Singleton.CoreFileName = "D:/KTH/Projects/EddyRedo/Data/Core";
            RedSea.Singleton.SnapFileName = "D:/KTH/Projects/EddyRedo/Data/Screenshots/";
            RedSea.Singleton.RingFileName = "D:/KTH/Projects/EddyRedo/Data/Rings/";
            //Tests.CopyBeginningOfFile(RedSea.Singleton.GetFilename(0), 100000);

            //LoaderNCF ncFile = RedSea.Singleton.GetLoaderNCF(0);
            //ScalarField[] u = new ScalarField[numTimeSlices];
            //LoaderNCF.SliceRange sliceU = new LoaderNCF.SliceRange(ncFile, RedSea.Variable.VELOCITY_X);
            //sliceU.SetMember(RedSea.Dimension.MEMBER, 0); // Average
            //sliceU.SetMember(RedSea.Dimension.TIME, 0);
            //sliceU.SetMember(RedSea.Dimension.CENTER_Z, 0);
            ////sliceU.SetRange(RedSea.Dimension.GRID_X, 300, 100);
            ////sliceU.SetRange(RedSea.Dimension.CENTER_Y, 20, 100);

            //ScalarField[] v = new ScalarField[numTimeSlices];
            //LoaderNCF.SliceRange sliceV = new LoaderNCF.SliceRange(ncFile, RedSea.Variable.VELOCITY_Y);
            //sliceV.SetMember(RedSea.Dimension.MEMBER, 0);
            //sliceV.SetMember(RedSea.Dimension.TIME, 0);
            //sliceV.SetMember(RedSea.Dimension.CENTER_Z, 0);
            ////sliceV.SetRange(RedSea.Dimension.CENTER_X, 300, 100);
            ////sliceV.SetRange(RedSea.Dimension.GRID_Y, 20, 100);

            //ensembleU = new LoaderNCF.SliceRange(ncFile, RedSea.Variable.VELOCITY_X);
            //ensembleU.SetMember(RedSea.Dimension.TIME, 0);
            //ensembleU.SetMember(RedSea.Dimension.CENTER_Z, 0);
            //ensembleU.SetRange(RedSea.Dimension.MEMBER, 2, 50);
            ////ensembleU.SetRange(RedSea.Dimension.GRID_X, 100, 160);
            ////ensembleU.SetRange(RedSea.Dimension.CENTER_Y, 10, 70);
            //ensembleV = new LoaderNCF.SliceRange(ncFile, RedSea.Variable.VELOCITY_Y);
            //ensembleV.SetMember(RedSea.Dimension.TIME, 0);
            //ensembleV.SetMember(RedSea.Dimension.CENTER_Z, 0);
            //ensembleV.SetRange(RedSea.Dimension.MEMBER, 2, 50);
            ////ensembleV.SetRange(RedSea.Dimension.CENTER_X, 100, 160);
            ////ensembleV.SetRange(RedSea.Dimension.GRID_Y, 10, 70);

            Loader.SliceRange rawU = new LoaderRaw.SliceRangeRaw(RedSea.Variable.VELOCITY_X);
            rawU.SetMember(RedSea.Dimension.GRID_Z, 0);
            rawU.SetMember(RedSea.Dimension.MEMBER, 0);
            rawU.SetMember(RedSea.Dimension.SUBTIME, 0);
            Loader.SliceRange rawV = new LoaderRaw.SliceRangeRaw(RedSea.Variable.VELOCITY_Y);
            rawV.SetMember(RedSea.Dimension.GRID_Z, 0);
            rawV.SetMember(RedSea.Dimension.MEMBER, 0);
            rawV.SetMember(RedSea.Dimension.SUBTIME, 0);

            //rawU.SetMember(RedSea.Dimension.TIME, 40);

            //rawV.SetMember(RedSea.Dimension.TIME, 40);
            //ncFile.Close();


            //if (loadData)
            //{
            //    //velocity = LoaderRaw.LoadTimeSeries(new Loader.SliceRange[] { sliceU, sliceV }, 0, numTimeSlices);
            //    velocity = LoaderRaw.LoadTimeSeries(new Loader.SliceRange[] { rawU, rawV });
                
            //    // Scale the field from m/s to (0.1 degree per 3 days).
            //    velocity.ScaleToGrid(new Vec2(RedSea.Singleton.TimeScale));
            //}
            //else
            //{
            //    velocity = Tests.CreateCircle(new Vec2(0), 100, new Vec2(0/*.2f*/), numTimeSlices, 8);
            //    //velocity = Tests.CreatePathlineSpiral(99, 100, 2);
            //    velocity.ScaleToGrid(new Vec2(RedSea.Singleton.DomainScale));
            //}


            Console.WriteLine("Completed loading data.");

            CriticalPointSet2D[] cps = new CriticalPointSet2D[numTimeSlices];
            for (int time = 0; time < numTimeSlices; ++time)
            {
                //                cps[time] = FieldAnalysis.ComputeCriticalPointsRegularSubdivision2D(velocity.GetTimeSlice(time), 5, 0.3f);
                //                cps[time].SelectTypes(new CriticalPoint2D.TypeCP[] { CriticalPoint2D.TypeCP.ATTRACTING_FOCUS, CriticalPoint2D.TypeCP.REPELLING_FOCUS }).ToBasicSet();

                Console.WriteLine("Completed processing step " + time + '.');
            }

            redSea = new Plane(new Vector3(-10, -3, -5), Vector3.UnitX * 0.1f, Vector3.UnitY * 0.1f, -Vector3.UnitZ, 0.4f/*10f/size*/, 0.1f);
            //            mapperCP = new CriticalPointTracking(cps, velocity, redSea);
            //Console.WriteLine("Found CP.");
            //mapperPathCore = new PathlineCoreTracking(velocity, redSea);
            //Console.WriteLine("Found Pathline Cores.");
            mapperComparison = new MemberComparison(/*new LoaderNCF.SliceRange[] { sliceU, sliceV },*/ redSea);
            if (velocity != null)
            {
                mapperOW = new OkuboWeiss(velocity, redSea);
                Console.WriteLine("Computed Okubo-Weiss.");
            }


            Console.WriteLine("Computed all data necessary.");
        }

        public static void CreateRenderables()
        {
            RedSea.Singleton.SetMapper(RedSea.Display.CP_TRACKING, mapperCP);
            RedSea.Singleton.SetMapper(RedSea.Display.PATHLINE_CORES, mapperPathCore);
            RedSea.Singleton.SetMapper(RedSea.Display.MEMBER_COMPARISON, mapperComparison);
            RedSea.Singleton.SetMapper(RedSea.Display.OKUBO_WEISS, mapperOW);

            //mapperFlowMap = new FlowMapMapper(new LoaderNCF.SliceRange[] { ensembleU, ensembleV }, redSea, velocity);
            //RedSea.Singleton.SetMapper(RedSea.Display.FLOW_MAP_UNCERTAIN, mapperFlowMap);

            //mapperPathLength = new PathlineLengthMapper(velocity, redSea);
            //RedSea.Singleton.SetMapper(RedSea.Display.PATHLINE_LENGTH, mapperPathLength);

            //mapperCutDiffusion = new DiffusionMapper(velocity, redSea);
            //RedSea.Singleton.SetMapper(RedSea.Display.CUT_DIFFUSION_MAP, mapperCutDiffusion);

            //mapperLocalDiffusion = new LocalDiffusionMapper(velocity, redSea);
            //RedSea.Singleton.SetMapper(RedSea.Display.LOCAL_DIFFUSION_MAP, mapperLocalDiffusion);

            //DataMapper pathlines = new PathlineRadius(velocity, redSea);
            //RedSea.Singleton.SetMapper(RedSea.Display.PATHLINE_RADIUS, pathlines);
            //SubstepViewer substep = new SubstepViewer(redSea);
            //RedSea.Singleton.SetMapper(RedSea.Display.SUBSTEP_VIEWER, substep);

            //DataMapper lineStatistics = new LineStatisticsMapper(velocity, redSea);
            //RedSea.Singleton.SetMapper(RedSea.Display.LINE_STATISTICS, lineStatistics);

            DataMapper coreDistance = new CoreDistanceMapper(12, redSea);
            RedSea.Singleton.SetMapper(RedSea.Display.CORE_DISTANCE, coreDistance);

            DataMapper predCoreDistance = new PredictedCoreDistanceMapper(12, redSea);
            RedSea.Singleton.SetMapper(RedSea.Display.PREDICTOR_CORE_ANGLE, predCoreDistance);

            DataMapper circleCoreDistance = new ConcentricDistanceMapper(12, redSea);
            RedSea.Singleton.SetMapper(RedSea.Display.CONCENTRIC_DISTANCE, circleCoreDistance);

            DataMapper circleCoreTube = new ConcentricTubeMapper(12, redSea);
            RedSea.Singleton.SetMapper(RedSea.Display.CONCENTRIC_TUBE, circleCoreTube);

            DataMapper ftle = new MapperFTLE(12, redSea);
            RedSea.Singleton.SetMapper(RedSea.Display.FTLE_CONCENTRIC, ftle);

            DataMapper coreOkubo = new CoreOkuboMapper(12, redSea);
            RedSea.Singleton.SetMapper(RedSea.Display.OKUBO_CONCENTRIC, coreOkubo);

            DataMapper pathDist = new DistanceMapper(12, redSea);
            RedSea.Singleton.SetMapper(RedSea.Display.PATHLINE_DISTANCE, pathDist);

            DataMapper donut = new DonutAnalyzer(redSea);
            RedSea.Singleton.SetMapper(RedSea.Display.DONUT_ANALYSIS, donut);

            if (mapperOW != null)
                RedSea.Singleton.SetMapper(RedSea.Display.OKUBO_WEISS, mapperOW);

            RedSea.Singleton.SetMapper(RedSea.Display.PLAYGROUND, new PlaygroundMapper(redSea));


            DataMapper editor = new ConcentricEditorMapper(12, redSea);
            RedSea.Singleton.SetMapper(RedSea.Display.AREA_EDITOR, editor);

            DataMapper coherency = new CoherencyMapper(12, redSea);
            RedSea.Singleton.SetMapper(RedSea.Display.COHERENCY, coherency);
        }
    }
}
