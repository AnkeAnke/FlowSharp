using System;

namespace FlowSharp
{
    class FSMain
    {
        public static void Run()
        {
            // Loading the temperature variable to have a frame of reference (should be ~20-35 degree at upper levels).
            Loader ncFile = new Loader("E:/Anke/Dev/Data/First/s1/Posterior_Diag.nc");

            Loader.SliceRange slice = new Loader.SliceRange(ncFile, RedSea.Variable.TEMPERATURE);
            slice.SetOffset(RedSea.Dimension.MEMBER, 0);
            slice.SetOffset(RedSea.Dimension.TIME, 0);
            slice.SetOffset(RedSea.Dimension.CENTER_Z, 25);
            ScalarField temperature = ncFile.LoadFieldSlice(slice);

            ncFile.Close();
        }
    }
}
