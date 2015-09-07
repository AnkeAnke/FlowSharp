using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Research.ScientificDataSet.NetCDF4;
using System.Diagnostics;

namespace FlowSharp
{
    /// <summary>
    /// Class for loading and accessing data. Currently limited to NetCDF files of the Red Sea, can be exteded later. No error handling so far.
    /// </summary>
    class Loader
    {
        /// <summary>
        /// NetCDF file ID.
        /// </summary>
        protected int _fileID;
        /// <summary>
        /// Number of variables in the file.
        /// </summary>
        protected int _numVars;

        /// <summary>
        /// Create a Loader object and open a NetCDF file.
        /// </summary>
        /// <param name="file">Path of the file.</param>
        public Loader(string file)
        {
            NetCDF.nc_open(file, NetCDF.CreateMode.NC_NOWRITE, out _fileID);
            NetCDF.nc_inq_nvars(_fileID, out _numVars);
            //if (status != (int)NetCDF.ResultCode.NC_NOERR)
            //    throw (status);
        }

        public int GetID() { return _fileID; }

        /// <summary>
        /// Display variable names in the console.
        /// </summary>
        public void DisplayContent()
        {
            StringBuilder name = new StringBuilder(null);
            int nDims;
            for (int i = 0; i < _numVars; ++i)
            {
                NetCDF.nc_inq_varname(_fileID, i, name);
                NetCDF.nc_inq_varndims(_fileID, i, out nDims);
                Console.Out.WriteLine("Var " + i + ":\t" + name + ",\tNumDims: " + nDims);
                name = new StringBuilder(null);
            }
        }

        public void TestStuff()
        {
            int value;


            //NetCDF.nc_inq_natts(_fileID, out numAtts);
            //Console.Out.WriteLine("VNum attributes:\t" + numAtts);

            StringBuilder name = new StringBuilder(null);

            //for (int i = 0; i < _numVars; ++i)
            //{
            //    for (int j = 0; j < numAtts; ++j)
            //    {
            //        NetCDF.nc_inq_attname(_fileID, i, j, name);
            //        Console.Out.WriteLine("Var " + i + ", Att. " + j + ":\t" + name);
            //        name = new StringBuilder(null);
            //    }
            //}
            for (int i = 0; i < 14; ++i)
            {
                NetCDF.nc_inq_dimname(_fileID, i, name);
                NetCDF.nc_inq_dimlen(_fileID, i, out value);
                Console.Out.WriteLine("Dim name " + i + ":\t" + name + ", Dim Length: " + value);
            }
            //int groups[] = new int[]
            //NetCDF.nc_inq_grps(_fileID, out value, null);
            //Console.Out.WriteLine("Num Groups: " + value);

            //for (int i = 0; i < value; ++i)
            //{
            //    NetCDF.nc_inq_grpname(i, name);
            //    Console.WriteLine("Groupname: " + name);
            //}
            NetCDF.NcType type;
            NetCDF.nc_inq_vartype(_fileID, 12, out type);
            NetCDF.nc_inq_varndims(_fileID, 12, out value);
            Console.WriteLine("Var 12, type:\t" + type.ToString() + ", Num Dims:\t" + value);
            int[] dimIDs = new int[value];
            NetCDF.nc_inq_vardimid(_fileID, 12, dimIDs);
            for (int i = 0; i < value; ++i)
                Console.Write("DimID " + i + ": " + dimIDs[i] + '\t');
            NetCDF.nc_inq_varnatts(_fileID, 12, out value);
            Console.WriteLine("Num Atts: " + value);

            NetCDF.NcEndian endian;
            int status = NetCDF.nc_inq_var_endian(_fileID, 12, out endian);
            Console.WriteLine("Endianess: " + endian.ToString());
            if (status != 0)
                status = 0;

            float[] data = new float[210 * 450]; //Enough to read one slice.
            float[] variance = new float[210 * 450]; //Enough to read one slice.
            int[] origin = new int[] { 0, 0, 0, 0, 0 };
            int[] size = new int[] { 1, 1, 1, 210, 450 };
            status = NetCDF.nc_get_vara_float(_fileID, 12, origin, size, data);
            if (status != 0)
                status = 0;
            origin = new int[] { 0, 1, 0, 0, 0 };
            NetCDF.nc_get_vara_float(_fileID, 12, origin, size, variance);

            Random RND = new Random();

            for(int i = 0; i < 100; ++i)
            {
                int rnd = RND.Next(450 * 210);
                byte[] bytes = BitConverter.GetBytes(data[rnd]);
                byte[] swapped = new byte[] { bytes[3], bytes[2], bytes[1], bytes[0]};
                float result = BitConverter.ToSingle(swapped, 0);
                Console.WriteLine("Value at " + rnd + ": " + data[rnd] + ", variance: " + variance[rnd]);
            }
            // NetCDF.nc_get_vara_float(_fileID, )
        }

//        /// <summary>
//        /// Load one value into memory completely.
//        /// </summary>
//        /// <param name="var"></param>
//        /// <returns></returns>
//        public ScalarField LoadField(RedSea.Variable var)
//        {
//            ScalarField field;
//            // Query number of dimensions, since it may differ for each variable.
//            int numDims;
//            NetCDF.nc_inq_varndims(_fileID, (int)var, out numDims);

//            // Query sizes of all relevant dimensions to know the scalar fields size.
//            Index size = new Index(numDims);
//            int[] dimIDs = new int[numDims];
//            NetCDF.nc_inq_vardimid(_fileID, (int)var, dimIDs);

//            // Fill index directly.
//            for(int dim = 0; dim < numDims; ++dim)
//            {
//                int sizeDim;
//                NetCDF.nc_inq_dimlen(_fileID, dimIDs[dim], out sizeDim);
//                size[dim] = sizeDim;
//            }

//#if DEBUG
//            Console.WriteLine("Field dimensions of " + var.ToString() + ": " + size.ToString());

//            // Assert the type is float.
//            NetCDF.NcType type;
//            NetCDF.nc_inq_vartype(_fileID, (int)var, out type);
//            Debug.Assert(type == NetCDF.NcType.NC_FLOAT);
//#endif

//            // Create a grid descriptor for the field. 
//            // TODO: Actually load this data.
//            RectlinearGrid grid = new RectlinearGrid(size, new )

//            // Create scalar field instance and fill it with data.
//            field = new ScalarField(size);
//            int[] zero = new int[numDims];
//            NetCDF.nc_get_var_float(_fileID, (int)var, field.Data);

//            return field;
//        }

        /// <summary>
        /// Load a slice from the file.
        /// </summary>
        /// <param name="slice">Carries variable to load, dimensions in file and what to load.</param>
        /// <returns></returns>
        public ScalarField LoadFieldSlice(SliceRange slice)
        {
            ScalarField field;
            int[] offsets = slice.GetOffsets();

            int[] sizeInFile = new int[offsets.Length];
            // Probably has less dimensions.
            int[] sizeField = new int[offsets.Length];
            int numDimsField = 0;
            //int currDimSlice = 0;
            for (int dim = 0; dim < offsets.Length; ++dim)
            {
                if (offsets[dim] != -1)
                {
                    // Take one slice in this dimension only. Start = offset (keep), size = 1.
                    sizeInFile[dim] = 1;
                }
                else
                {
                    // Fill size.
                    int sizeDim;
                    NetCDF.nc_inq_dimlen(_fileID, slice.GetDimensionID(dim), out sizeDim);
                    sizeInFile[dim] = sizeDim;

                    // Set offset to one. offset = 0, size = size of dimension.
                    offsets[dim] = 0;

                    // Save size in size-vector for the scalar field.
                    sizeField[numDimsField++] = sizeDim;
                }
            }

            // Generate size index for field class.
            Index fieldSize = new Index(numDimsField);
            Array.Copy(sizeField, fieldSize.Data, numDimsField);

            // Create a grid descriptor for the field. 
            // TODO: Actually load this data.
            RectlinearGrid grid = new RectlinearGrid(fieldSize, new Vector(0.0f, fieldSize.Length), new Vector(0.1f, fieldSize.Length));

            // Create scalar field instance and fill it with data.
            field = new ScalarField(grid);
            NetCDF.nc_get_vara_float(_fileID, (int)slice.GetVariable(), offsets, sizeInFile, field.Data);

            return field;
        }

        /// <summary>
        /// Close the file.
        /// </summary>
        public void Close()
        {
            NetCDF.nc_close(_fileID);
        }

        /// <summary>
        /// Class to define a slice to be loaded. Dimensions may be either included completely, or only one value is taken.
        /// </summary>
        public class SliceRange
        {
            /// <summary>
            /// Offset for each dimension. If -1, the dimension will be included completely.
            /// </summary>
            private RedSea.Dimension[] _presentDims;
            private int[] _dimOffsets;
            private RedSea.Variable _var;

            public int GetDimensionID(int index) { return (int)_presentDims[index]; }
            public int[] GetOffsets() { return _dimOffsets; }
            public RedSea.Variable GetVariable() { return _var; }

            public SliceRange(Loader file, RedSea.Variable var)
            {
                _var = var;

                // Query number of dimensions of variable.
                int numDims;
                NetCDF.nc_inq_varndims(file.GetID(), (int)var, out numDims);
                int[] dimIDs = new int[numDims];

                // Query relevant dimensions.
                NetCDF.nc_inq_vardimid(file.GetID(), (int)var, dimIDs);

                _presentDims = new RedSea.Dimension[numDims];
                _dimOffsets = new int[numDims];

                // Fill arrays.
                for (int dim = 0; dim < numDims; ++dim)
                {
                    // Dimensions in correct order.
                    _presentDims[dim] = (RedSea.Dimension)dimIDs[dim];

                    // "Activate" all dimensions.
                    _dimOffsets[dim] = -1;
                }
            }

            /// <summary>
            /// Only include this slice of the data in this dimension.
            /// </summary>
            /// <param name="dim"></param>
            /// <param name="slice"></param>
            public void SetOffset(RedSea.Dimension dim, int slice)
            {
                // Search for position of dimension in present dimensions.
                int dimPos = -1;
                for(int pos = 0; pos < _presentDims.Length; ++pos)
                {
                    if(_presentDims[pos] == dim)
                    {
                        dimPos = pos;
                        break;
                    }
                }

                // Dimension found?
                if (dimPos == -1)
                    return;

                _dimOffsets[dimPos] = slice;
            }
        }
    }
}
