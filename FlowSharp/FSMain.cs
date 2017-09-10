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
        static VectorField velocity;

        static Plane basePlane;

        static DataMapper tetTreeMapper;
        static DataMapper hitMapper;
        static DataMapper stressMapper;

        public static void LoadData()
        {
            System.Globalization.CultureInfo customCulture = (System.Globalization.CultureInfo)System.Threading.Thread.CurrentThread.CurrentCulture.Clone();
            customCulture.NumberFormat.NumberDecimalSeparator = ".";

            System.Threading.Thread.CurrentThread.CurrentCulture = customCulture;

            Console.WriteLine("Output works.");
            Console.WriteLine("Using " + (Environment.Is64BitProcess ? "x64" : "x32"));

            int rupture = 1;

            string mainFolder = $"C:/Users/Anke/Documents/Vis/Data/Aneurysm/Rupture_0{rupture}/";
            Aneurysm.Singleton.EnsightFolderFilename = mainFolder;
            Aneurysm.Singleton.EnsightGeoFilename = "ruptured.geo";
            Aneurysm.Singleton.SnapFileName = "C:/Users/Anke/Documents/Vis/Data/Aneurysm/Screenshots/";
            Aneurysm.Singleton.EnsightFilename = "wobble";
            Aneurysm.Singleton.VtuFolderFilename = mainFolder + "vtu/tets/";
            Aneurysm.Singleton.VtuDataFilename = "tets_0_";
            Aneurysm.Singleton.OctreeFolderFilename = mainFolder;
            basePlane = new Plane(Vector3.Zero, Vector3.UnitX, Vector3.UnitY, -Vector3.UnitZ, 10f/*10f/size*/, 10f);

            tetTreeMapper = new AneurysmViewMapper(basePlane);

            hitMapper = new HitTimeMapper(basePlane);

            stressMapper = new WallShearMapper(basePlane);

            Console.WriteLine("Computed all data necessary.");
        }

        public static void CreateRenderables()
        {
            Aneurysm.Singleton.SetMapper(
                Aneurysm.Display.View_Tetrahedrons,
                tetTreeMapper);

            Aneurysm.Singleton.SetMapper(
                Aneurysm.Display.Particle_Hits,
                hitMapper);

            Aneurysm.Singleton.SetMapper(
                Aneurysm.Display.Wall_Shear_Stress,
                stressMapper);
        }
    }
}
