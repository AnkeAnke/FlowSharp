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

        protected int _timeSlice;

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
            Index offsets = new Index(slice.GetOffsets());
            NetCDF.ResultCode ncState = NetCDF.ResultCode.NC_NOERR;

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
                    ncState = NetCDF.nc_inq_dimlen(_fileID, slice.GetDimensionID(dim), out sizeDim);
                    Debug.Assert(ncState == NetCDF.ResultCode.NC_NOERR);
                    sizeInFile[dim] = sizeDim;

                    // Set offset to one. offset = 0, size = size of dimension.
                    offsets[dim] = 0;

                    // Save size in size-vector for the scalar field.
                    sizeField[numDimsField++] = sizeDim;
                }
            }

            if (slice.IsTimeDependent())
                numDimsField++;

            // Generate size index for field class.
            Index fieldSize = new Index(numDimsField);
            Array.Copy(sizeField, fieldSize.Data, numDimsField);

            // When the field has several time slices, add a time dimension.
            if (slice.IsTimeDependent())
                fieldSize[numDimsField - 1] = slice.GetNumTimeSlices();

            //TODO: HACK! REMOVE!
            int tmp = fieldSize[0];
            fieldSize[0] = fieldSize[1];
            fieldSize[1] = tmp;

            // Create a grid descriptor for the field. 
            // TODO: Actually load this data.
            RectlinearGrid grid = new RectlinearGrid(fieldSize, new Vector(0.0f, fieldSize.Length), new Vector(0.1f, fieldSize.Length));

            // Create scalar field instance and fill it with data.
            field = new ScalarField(grid);
            int sliceSize = grid.Size.Product() / slice.GetNumTimeSlices();

            // Get data.
            ncState = NetCDF.nc_get_vara_float(_fileID, (int)slice.GetVariable(), offsets.Data, sizeInFile, field.Data);
            Debug.Assert(ncState == NetCDF.ResultCode.NC_NOERR);

            //HACK!
            field.InvalidValue = field[0];

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
            private int _timeSlices;

            public int GetDimensionID(int index) { return (int)_presentDims[index]; }
            public int[] GetOffsets() { return _dimOffsets; }
            public RedSea.Variable GetVariable() { return _var; }
            public bool IsTimeDependent() { return _timeSlices > 1; }
            public int GetNumTimeSlices() { return _timeSlices; }

            public SliceRange(Loader file, RedSea.Variable var, int numTimeSlices = 1)
            {
                _var = var;
                _timeSlices = numTimeSlices;

                // Query number of dimensions of variable.
                int numDims;
                NetCDF.ResultCode ncState = NetCDF.nc_inq_varndims(file.GetID(), (int)var, out numDims);
                Debug.Assert(ncState == NetCDF.ResultCode.NC_NOERR);
                int[] dimIDs = new int[numDims];

                // Query relevant dimensions.
                ncState = NetCDF.nc_inq_vardimid(file.GetID(), (int)var, dimIDs);
                Debug.Assert(ncState == NetCDF.ResultCode.NC_NOERR);

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
