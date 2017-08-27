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

        static Plane redSea;

        static DataMapper mapperTetWireframe, mapperHexCubes, tetTreeMapper;

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
            redSea = new Plane(Vector3.Zero, Vector3.UnitX, Vector3.UnitY, -Vector3.UnitZ, 10f/*10f/size*/, 10f);

            //mapperTetWireframe = new HexTetGridMapper(redSea);
            tetTreeMapper = new TetGridMapper(redSea);
            //mapperHexCubes = new HexGridMapper(redSea);
            Console.WriteLine("Computed all data necessary.");
        }

        public static void CreateRenderables()
        {
            Aneurysm.Singleton.SetMapper(Aneurysm.Display.VIEW_TETRAHEDRONS, tetTreeMapper);
            //Aneurysm.Singleton.SetMapper(Aneurysm.Display.VIEW_HEXAHEDRONS, mapperHexCubes);
        }
    }
}
