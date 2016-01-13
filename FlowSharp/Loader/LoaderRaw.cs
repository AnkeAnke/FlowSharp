using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Research.ScientificDataSet.NetCDF4;
using System.Diagnostics;
using System.IO;

namespace FlowSharp
{
    /// <summary>
    /// Class for loading and accessing data. Currently limited to NetCDF files of the Red Sea, can be exteded later. No error handling so far.
    /// </summary>
    class LoaderRaw : Loader
    {
        /// <summary>
        /// Number of variables in the file.
        /// </summary>
        protected int[] _dims;
        protected string _fileName;
        public int NumDims { get { return _dims.Length; } }

        protected static int _numOpenFiles = 0;

        /// <summary>
        /// Create a Loader object and open a NetCDF file.
        /// </summary>
        /// <param name="file">Path of the file.</param>
        public LoaderRaw(string file)
        {
            Debug.Assert(_numOpenFiles == 0, "Another file is still open!");
            _fileName = file;
            // nDims = [3];
            // dimList = [
            //   500, 1, 500,
            //   500, 1, 500,
            //    50, 1, 50
            // ];
            // dataprec = ['float32'];
            // nrecords = [1];
            // timeStepNumber = [108];
            //string metadata = System.IO.File.ReadAllText(@file+".meta");
            //int index = metadata.IndexOf("nDims = [   ") + ("nDims = [   ").Length;

            //if (index != -1)
            //{
            //    int index2 = this.Message.IndexOf(",", index);
            //    if (index2 == -1)
            //    {
            //        index2 = this.Message.Length;
            //    }
            //}
            _dims = new int[] { 500, 500 };
        }

        /// <summary>
        /// Load a slice from the file.
        /// </summary>
        /// <param name="slice">Carries variable to load, dimensions in file and what to load.</param>
        /// <returns></returns>
        public ScalarField LoadField(bool rightEndian = true) //SliceRange range)
        {

            // Create a grid descriptor for the field. 
            RectlinearGrid grid = new RectlinearGrid(new Index(_dims));

            // Create scalar field instance and fill it with data.
            ScalarField field = new ScalarField(grid);
            int sliceSize = grid.Size.Product();// / slice.GetNumTimeSlices();

            uint filePos = 0;
            using (FileStream fs = File.Open(@_fileName, FileMode.Open))
            {
                using (BinaryReader reader = new BinaryReader(fs))
                {
                    // Read in all floats.
                    Debug.Assert(reader.BaseStream.Length >= sliceSize * sizeof(float));
                    byte[] data = reader.ReadBytes(sliceSize * sizeof(float));

                    // Change Endian of data.
                    if (!rightEndian)
                    {
                        Array.Reverse(data);
                        for (int i = 0; i < sliceSize; ++i)
                        {
                            field.Data[sliceSize - 1 - i] = BitConverter.ToSingle(data, i * 4);
                        }
                    }
                }
            }

            return field;
        }

        /// <summary>
        /// Close the file.
        /// </summary>

        /// <summary>
        /// Class to define a slice to be loaded. Dimensions may be either included completely, or only one value is taken.
        /// </summary>
        public class SliceRange
        {
            private int[] _dimOffsets;
            private RedSea.Variable _var;
            
            public int[] GetOffsets() { return _dimOffsets; }
            public RedSea.Variable GetVariable() { return _var; }

            public SliceRange(LoaderRaw file, RedSea.Variable var)
            {
                _var = var;

                // Query number of dimensions of variable.
                _dimOffsets = new int[file.NumDims];

                // Fill arrays.
                for (int dim = 0; dim < file.NumDims; ++dim)
                {
                    // "Activate" all dimensions.
                    _dimOffsets[dim] = -1;
                }
            }


            /// <summary>
            /// Only include this slice of the data in this dimension.
            /// </summary>
            /// <param name="dim"></param>
            /// <param name="slice"></param>
            public void SetMember(int dim, int slice)
            {
                _dimOffsets[dim] = slice;
            }
        }
    }
}
