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

        static Plane redSea;

        static DataMapper mapperTetWireframe;

        public static void LoadData()
        {
            System.Globalization.CultureInfo customCulture = (System.Globalization.CultureInfo)System.Threading.Thread.CurrentThread.CurrentCulture.Clone();
            customCulture.NumberFormat.NumberDecimalSeparator = ".";

            System.Threading.Thread.CurrentThread.CurrentCulture = customCulture;

            Console.WriteLine("Output works.");
            Console.WriteLine("Using " + (Environment.Is64BitProcess ? "x64" : "x32"));

            Aneurysm.Singleton.FolderFilename = "C:/Users/Anke/Documents/Vis/Data/Aneurysm/Rupture_01/";
            Aneurysm.Singleton.GeoFilename = "case01_fine_mesh_unsteady2cc.geo";
            Aneurysm.Singleton.SnapFileName = "C:/Users/Anke/Documents/Vis/Data/Aneurysm/Screenshots/";
            Aneurysm.Singleton.Filename = "Rupture_01";


            redSea = new Plane(Vector3.Zero, Vector3.UnitX, Vector3.UnitY, -Vector3.UnitZ, 10f/*10f/size*/, 10f);

            mapperTetWireframe = new HexGridMapper(redSea);
            Console.WriteLine("Computed all data necessary.");
        }

        public static void CreateRenderables()
        {
            Aneurysm.Singleton.SetMapper(Aneurysm.Display.VIEW_GEOMETRY, mapperTetWireframe);
        }
    }
}
