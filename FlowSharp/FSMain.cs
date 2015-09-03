using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;     // DLL support
using Microsoft.Research.ScientificDataSet.NetCDF4;

namespace FlowSharp
{
    class FSMain
    {
        public static void Run()
        {
            Console.Out.WriteLine("Running!");

            // Testing NetCDF, getting to know how it works.

            int fileID;
            int dims;
            NetCDF.nc_open("E:/Anke/Dev/Data/First/s1/Posterior_Diag.nc", NetCDF.CreateMode.NC_NOWRITE, out fileID);
            Console.Out.WriteLine(fileID);
            NetCDF.nc_inq_ndims(fileID, out dims);
            Console.Out.WriteLine("Dims: " + dims);
        }
    }
}
