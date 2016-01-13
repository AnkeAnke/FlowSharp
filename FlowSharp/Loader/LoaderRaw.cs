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
        protected int[] _dimIDs;
        protected int[] _dimLengths;
        //        protected string _fileName;
        public int NumDims { get { return _dimIDs.Length; } }

        protected static int _numOpenFiles = 0;

        /// <summary>
        /// Create a Loader object and open a NetCDF file.
        /// </summary>
        /// <param name="file">Path of the file.</param>
        //public LoaderRaw(string file)
        //{
        //    Debug.Assert(_numOpenFiles == 0, "Another file is still open!");
        //    _fileName = file;
        //    // nDims = [3];
        //    // dimList = [
        //    //   500, 1, 500,
        //    //   500, 1, 500,
        //    //    50, 1, 50
        //    // ];
        //    // dataprec = ['float32'];
        //    // nrecords = [1];
        //    // timeStepNumber = [108];
        //    //string metadata = System.IO.File.ReadAllText(@file+".meta");
        //    //int index = metadata.IndexOf("nDims = [   ") + ("nDims = [   ").Length;

        //    //if (index != -1)
        //    //{
        //    //    int index2 = this.Message.IndexOf(",", index);
        //    //    if (index2 == -1)
        //    //    {
        //    //        index2 = this.Message.Length;
        //    //    }
        //    //}
        //    _dimIDs = new int[] { (int)RedSea.Variable.GRID_X, (int)RedSea.Variable.GRID_Y, (int)RedSea.Variable.GRID_Z };
        //    _dimLengths = new int[] { 500, 500, 50 };
        //}
        protected int _step;
        protected int _substep;

        public LoaderRaw()
        {
            Debug.Assert(_numOpenFiles == 0, "Another file is still open!");

            _dimIDs = new int[] { (int)RedSea.Dimension.GRID_X, (int)RedSea.Dimension.GRID_Y, (int)RedSea.Dimension.GRID_Z, (int)RedSea.Dimension.MEMBER, (int)RedSea.Dimension.TIME, (int)RedSea.Dimension.SUBTIME };
            _dimLengths = new int[] { 500, 500, 50, 50, 160, 108 };
        }

        /// <summary>
        /// Load a slice from the file.
        /// </summary>
        /// <param name="slice">Carries variable to load, dimensions in file and what to load.</param>
        /// <returns></returns>
        public override ScalarFieldUnsteady LoadTimeSlices(SliceRange slice, int starttime = -1, int timelength = -1)
        { 
            Index offsets = new Index(slice.GetOffsets());
            int spaceDims = 4;

            int[] sizeInFile = slice.GetLengths();
            Debug.Assert(starttime == -1 && timelength == -1, "Ignoring those parameters. Plase specify in the SliceRange instance!");

            // Probably has less dimensions.
            int[] sizeField = new int[spaceDims];
            int numDimsField = 0;

            // Exclude time dimension. It will be treated differently.
            for (int dim = 0; dim < spaceDims; ++dim)
            {

                if (offsets[dim] != -1 && sizeInFile[dim] > 1)
                {
                    sizeField[numDimsField++] = sizeInFile[dim];
                }
                // Include whole dimension.
                else if (offsets[dim] == -1)
                {
                    // Fill size.
                    sizeInFile[dim] = _dimLengths[dim];

                    // Set offset to one. offset = 0, size = size of dimension.
                    offsets[dim] = 0;

                    // Save size in size-vector for the scalar field.
                    sizeField[numDimsField++] = sizeInFile[dim];
                }
            }
            Index fieldSize = new Index(numDimsField);
            Array.Copy(sizeField, fieldSize.Data, numDimsField);

            Debug.Assert(sizeInFile[3] == 1, "How should I load several members into one data block???");

            // Create a grid descriptor for the field. 
            // TODO: Actually load this data.
            RectlinearGrid grid = new RectlinearGrid(fieldSize);

            // Create scalar field instance and fill it with data.
            int sliceSize = grid.Size.Product();

            // For each time and subtime step, run through them.
            ScalarField[] fields = new ScalarField[sizeInFile[4] * sizeInFile[5]];

            int indexTime = 0;
            for (int time = 0; time < sizeInFile[spaceDims]; ++time)
            {
                for (int subtime = 0; subtime < sizeInFile[spaceDims + 1]; ++subtime)
                {

                    // Now, load one single file.
                    string filename = RedSea.Singleton.GetFilename(offsets[spaceDims] + time, offsets[spaceDims + 1] + subtime, offsets[3], slice.GetVariable());

                    using (FileStream fs = File.Open(@filename, FileMode.Open))
                    {
                        // Read in the data you need.
                        using (BinaryReader reader = new BinaryReader(fs))
                        {
                            // Read in all floats.
                            Debug.Assert(reader.BaseStream.Length >= sliceSize * sizeof(float));

                            fields[indexTime] = new ScalarField(grid);
                            int indexSpace = 0;
                            for (int z = offsets[2]; z < offsets[2] + sizeInFile[2]; ++z)
                            {
                                // Set file reader position to right start point.
                                reader.BaseStream.Seek(z * _dimLengths[0] * _dimLengths[1] + offsets[1] * _dimLengths[0] + offsets[0], SeekOrigin.Begin);
                                for (int y = offsets[1]; z < offsets[1] + sizeInFile[1]; ++y)
                                {
                                    for (int x = offsets[0]; x < offsets[0] + sizeInFile[0]; ++x)
                                    {
                                        fields[indexTime][indexSpace++] = reader.ReadSingle();
                                    }
                                    // Advance one line.
                                    reader.BaseStream.Seek((_dimLengths[0] - sizeInFile[0]) * sizeof(float), SeekOrigin.Current);
                                }
                            }
                        }
                    }
                    // Go on to next file.
                    indexTime++;
                }

            }

            return new ScalarFieldUnsteady(fields, offsets[spaceDims] * _dimLengths[spaceDims+1] + offsets[spaceDims+1]); 
        }

        public override ScalarField LoadFieldSlice(SliceRange slice)
        {
            Index offsets = new Index(slice.GetOffsets());
            int spaceDims = 4;

            int[] sizeInFile = slice.GetLengths();
            // Probably has less dimensions.
            int[] sizeField = new int[spaceDims];
            int numDimsField = 0;

            // Exclude time dimension. It will be treated differently.
            for (int dim = 0; dim < spaceDims; ++dim)
            {

                if (offsets[dim] != -1 && sizeInFile[dim] > 1)
                {
                    sizeField[numDimsField++] = sizeInFile[dim];
                }
                // Include whole dimension.
                else if (offsets[dim] == -1)
                {
                    // Fill size.
                    sizeInFile[dim] = _dimLengths[dim];

                    // Set offset to one. offset = 0, size = size of dimension.
                    offsets[dim] = 0;

                    // Save size in size-vector for the scalar field.
                    sizeField[numDimsField++] = sizeInFile[dim];
                }
            }
            Index fieldSize = new Index(numDimsField);
            Array.Copy(sizeField, fieldSize.Data, numDimsField);

            Debug.Assert(sizeInFile[3] == 1, "How should I load several members into one data block???");

            // Create a grid descriptor for the field. 
            // TODO: Actually load this data.
            RectlinearGrid grid = new RectlinearGrid(fieldSize);

            // Create scalar field instance and fill it with data.
            int sliceSize = grid.Size.Product();

            // For each time and subtime step, run through them.
            ScalarField field = new ScalarField(grid);

            int indexTime = 0;

            Debug.Assert(sizeInFile[spaceDims] == 0 && sizeInFile[spaceDims + 1] == 0, "Define a single timestep, else use the method for ScalarFieldUnsteady.");
            // Now, load one single file.
            string filename = RedSea.Singleton.GetFilename(offsets[spaceDims], offsets[spaceDims + 1], offsets[3], slice.GetVariable());

            using (FileStream fs = File.Open(@filename, FileMode.Open))
            {
                // Read in the data you need.
                using (BinaryReader reader = new BinaryReader(fs))
                {
                    // Read in all floats.
                    Debug.Assert(reader.BaseStream.Length >= sliceSize * sizeof(float));

                    int indexSpace = 0;
                    for (int z = offsets[2]; z < offsets[2] + sizeInFile[2]; ++z)
                    {
                        // Set file reader position to right start point.
                        reader.BaseStream.Seek(z * _dimLengths[0] * _dimLengths[1] + offsets[1] * _dimLengths[0] + offsets[0], SeekOrigin.Begin);
                        for (int y = offsets[1]; z < offsets[1] + sizeInFile[1]; ++y)
                        {
                            for (int x = offsets[0]; x < offsets[0] + sizeInFile[0]; ++x)
                            {
                                field[indexSpace++] = reader.ReadSingle();
                            }
                            // Advance one line.
                            reader.BaseStream.Seek((_dimLengths[0] - sizeInFile[0]) * sizeof(float), SeekOrigin.Current);
                        }
                    }
                    //// Change Endian of data.
                    //if (!rightEndian)
                    //{
                    //    Array.Reverse(data);
                    //    for (int i = 0; i < sliceSize; ++i)
                    //    {
                    //        field.Data[sliceSize - 1 - i] = BitConverter.ToSingle(data, i * 4);
                    //    }
                    //}

                    // Write to scalar field!
                }
            }

            return field;
        }

        public override VectorFieldUnsteady LoadTimeVectorField(SliceRange[] slices, int starttime, int timelength)
        {
            ScalarFieldUnsteady[] fields = new ScalarFieldUnsteady[slices.Length];

            for (int i = 0; i < slices.Length; ++i)
                fields[i] = LoadTimeSlices(slices[i]);

            return new VectorFieldUnsteady(fields);
        }

    }
}
