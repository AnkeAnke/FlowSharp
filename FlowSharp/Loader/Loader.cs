using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Research.ScientificDataSet.NetCDF4;
using System.Diagnostics;

namespace FlowSharp
{
    abstract class Loader
    {

    }

    /// <summary>
    /// Class for loading and accessing data. Currently limited to NetCDF files of the Red Sea, can be exteded later. No error handling so far.
    /// </summary>
    class LoaderNCF : Loader
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

        protected static int _numOpenFiles = 0;

        /// <summary>
        /// Create a Loader object and open a NetCDF file.
        /// </summary>
        /// <param name="file">Path of the file.</param>
        public LoaderNCF(string file)
        {
            Debug.Assert(_numOpenFiles == 0, "Another NetCDF file is still open!");
            NetCDF.ResultCode result = NetCDF.nc_open(file, NetCDF.CreateMode.NC_NOWRITE, out _fileID);
            Debug.Assert(result == NetCDF.ResultCode.NC_NOERR, result.ToString());
            result = NetCDF.nc_inq_nvars(_fileID, out _numVars);
            Debug.Assert(result == NetCDF.ResultCode.NC_NOERR, result.ToString());

            _numOpenFiles++;
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

            //int[] sizeInFile = new int[offsets.Length];
            int[] sizeInFile = slice.GetLengths();
            // Probably has less dimensions.
            int[] sizeField = new int[offsets.Length];
            int numDimsField = 0;
            //int currDimSlice = 0;
            for (int dim = 0; dim < offsets.Length; ++dim)
            {

                if(offsets[dim] != -1 && sizeInFile[dim] > 1)
                {
                    sizeField[numDimsField++] = sizeInFile[dim];
                }
                // Include whole dimension.
                else if(offsets[dim] == -1)
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

            //if (slice.IsTimeDependent())
            //    numDimsField++;

            // Generate size index for field class.
            Index fieldSize = new Index(numDimsField);
            Array.Copy(sizeField, fieldSize.Data, numDimsField);

            // When the field has several time slices, add a time dimension.
            //if (slice.IsTimeDependent())
            //    fieldSize[numDimsField - 1] = slice.GetNumTimeSlices();

            // Change order of dimensions, so that fastest dimension is at the end.
            for(int dim = 0; dim < fieldSize.Length/2; ++dim)
            {
                int tmp = fieldSize[dim];
                fieldSize[dim] = fieldSize[fieldSize.Length - 1 - dim];
                fieldSize[fieldSize.Length - 1 - dim] = tmp;
            }

            // Create a grid descriptor for the field. 
            // TODO: Actually load this data.
            RectlinearGrid grid = new RectlinearGrid(fieldSize);//, new Vector(0.0f, fieldSize.Length), new Vector(0.1f, fieldSize.Length));

            // Create scalar field instance and fill it with data.
            field = new ScalarField(grid);
            int sliceSize = grid.Size.Product();// / slice.GetNumTimeSlices();

            // Get data. x64 dll fails here...
            ncState = NetCDF.nc_get_vara_float(_fileID, (int)slice.GetVariable(), offsets.Data, sizeInFile, field.Data);
            Debug.Assert(ncState == NetCDF.ResultCode.NC_NOERR, ncState.ToString());

            // Read in invalid value.
            float[] invalidval = new float[1];
            ncState = NetCDF.nc_get_att_float(_fileID, (int)slice.GetVariable(), "_FillValue", invalidval);

            field.InvalidValue = invalidval[0];

            //// Scale the data such that a vector (1,1) travels 1 grid cell exactly! Without this, no operations are allowed.
            //switch (slice.GetVariable())
            //{
            //    case RedSea.Variable.VELOCITY_X:
            //    case RedSea.Variable.VELOCITY_Y:
            //        //field.ScaleToGrid(new Vector(0.1f, fieldSize.Length));
            //        break;
            //    default:
            //        field.UseRawData();
            //        break;
            //}

            return field;
        }

        public int GetNumVariables()
        {
            int size;
            NetCDF.nc_inq_nvars(_fileID, out size);
            return size;
        }

        /// <summary>
        /// Close the file.
        /// </summary>
        public void Close()
        {
            NetCDF.nc_close(_fileID);
            _numOpenFiles--;
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
            private int[] _dimLengths;
            private RedSea.Variable _var;

            public int GetDimensionID(int index) { return (int)_presentDims[index]; }
            public int[] GetOffsets() { return _dimOffsets; }
            public int[] GetLengths() { return _dimLengths; }
            public RedSea.Variable GetVariable() { return _var; }

            public SliceRange(LoaderNCF file, RedSea.Variable var)
            {
                _var = var;

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
                _dimLengths = new int[numDims];

                // Fill arrays.
                for (int dim = 0; dim < numDims; ++dim)
                {
                    // Dimensions in correct order.
                    _presentDims[dim] = (RedSea.Dimension)dimIDs[dim];

                    // "Activate" all dimensions.
                    _dimOffsets[dim] = -1;
                    _dimLengths[dim] = -1;
                }
            }

            public SliceRange(SliceRange range)
            {
                _var = range._var;

                int numDims = range._presentDims.Length;
                _presentDims = new RedSea.Dimension[numDims];
                _dimOffsets = new int[numDims];
                _dimLengths = new int[numDims];


                Array.Copy(range._presentDims, _presentDims, numDims);
                Array.Copy(range._dimOffsets, _dimOffsets, numDims);
                Array.Copy(range._dimLengths, _dimLengths, numDims);
            }


            /// <summary>
            /// Only include this slice of the data in this dimension.
            /// </summary>
            /// <param name="dim"></param>
            /// <param name="slice"></param>
            public void SetMember(RedSea.Dimension dim, int slice)
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
                Debug.Assert(dimPos != -1, "Dimension not present, cannot be set!");

                _dimOffsets[dimPos] = slice;
                // We only chose one element.
                _dimLengths[dimPos] = 1;
            }

            public void SetRange(RedSea.Dimension dim, int start, int length)
            {
                // Search for position of dimension in present dimensions.
                int dimPos = -1;
                for (int pos = 0; pos < _presentDims.Length; ++pos)
                {
                    if (_presentDims[pos] == dim)
                    {
                        dimPos = pos;
                        break;
                    }
                }

                // Dimension found?
                Debug.Assert(dimPos != -1, "Dimension not present, cannot be set!");

                _dimOffsets[dimPos] = start;
                _dimLengths[dimPos] = length;
            }

            public void SetToComplete(RedSea.Dimension dim)
            {
                // Search for position of dimension in present dimensions.
                int dimPos = -1;
                for (int pos = 0; pos < _presentDims.Length; ++pos)
                {
                    if (_presentDims[pos] == dim)
                    {
                        dimPos = pos;
                        break;
                    }
                }

                // Dimension found?
                Debug.Assert(dimPos != -1, "Dimension not present, cannot be set!");

                _dimOffsets[dimPos] = -1;
            }
        }

//        public delegate string FilenameFromIndex(int index);
        public static VectorFieldUnsteady LoadTimeSeries(RedSea.FilenameBuilderNCF func, SliceRange[] vars, int starttime, int timelength)
        {
            ScalarField[][] slices = new ScalarField[vars.Length][];
            for (int var = 0; var < vars.Length; ++var)
                slices[var] = new ScalarField[timelength];


            LoaderNCF ncFile;
            for (int time = starttime; time < starttime + timelength; ++time)
            {
                ncFile = func(time);// path + (time + 1) + filename);
                for(int var = 0; var < vars.Length; ++var)
                {
                    slices[var][time] = ncFile.LoadFieldSlice(vars[var]);
                }
                ncFile.Close();
            }

            ScalarFieldUnsteady[] scalars = new ScalarFieldUnsteady[vars.Length];
            for (int var = 0; var < vars.Length; ++var)
                scalars[var] = new ScalarFieldUnsteady(slices[var], starttime);

            return new VectorFieldUnsteady(scalars);
        }
    }
}
