using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace FlowSharp
{
    class LoaderVTU : Loader
    {
        public Aneurysm.GeometryPart Part { get; protected set; }
        public UnstructuredGeometry Grid { get; protected set; }

        public LoaderVTU(Aneurysm.GeometryPart part)
        {
            Part = part;
        }

        public UnstructuredGeometry LoadGeometry()
        {
            int numPoints = 0, numCells = 0;
            VectorBuffer vertices = null;
            IndexArray indices = null;
            bool readNumberPointsAndCells = false;

            string rawString = null;
            byte[] rawData = null;

            Stopwatch watch = new Stopwatch();
            watch.Start();

            using (XmlReader reader = XmlReader.Create(Aneurysm.Singleton.VtuCompleteFilename(0, Part)))
            {
                while (reader.Read())
                {
                    // Only detect start elements.
                    if (reader.IsStartElement())
                    {
                        // Get element name and switch on it.
                        switch (reader.Name)
                        {
                            // Read number of geometric elements.
                            // < Piece NumberOfPoints = "2827" NumberOfCells = "5512" >
                            case "Piece":
                                Debug.Assert(reader.HasAttributes, "No attributes found.");
                                numPoints = int.Parse(reader.GetAttribute("NumberOfPoints"));
                                numCells = int.Parse(reader.GetAttribute("NumberOfCells"));
                                readNumberPointsAndCells = true;
                                break;
                            // Read vertex positions.
                            // <Points>
                            // <DataArray type = "Float32" Name="Points" NumberOfComponents="3" format="binary" RangeMin="0.33136009462" RangeMax="0.33290702085">
                            case "Points":
                                Debug.Assert(readNumberPointsAndCells, "No size read yet.");

                                // Assert all attributes are given as expected.
                                do
                                {
                                    reader.Read();
                                } while (!reader.IsStartElement());

                                Debug.Assert(reader.Name.Equals("DataArray") && reader.HasAttributes, "No data here.");
                                Debug.Assert(reader.GetAttribute("type").Equals("Float32"), "Not 32 bit floats as expected, but " + reader.GetAttribute("type"));
                                Debug.Assert(reader.GetAttribute("Name").Equals("Points"), "Not the Points data array, but " + reader.GetAttribute("Name"));
                                Debug.Assert(reader.GetAttribute("NumberOfComponents").Equals("3"), "Not 3 components, but " + reader.GetAttribute("NumberOfComponents"));
                                Debug.Assert(reader.GetAttribute("format").Equals("binary"), "Not binary data, but " + reader.GetAttribute("format"));
                                // Read range of positions.
                                float rangeMin = float.Parse(reader.GetAttribute("RangeMin"));
                                float rangeMax = float.Parse(reader.GetAttribute("RangeMax"));

                                // Next element should be a binary block.
                                reader.Read();
                                Debug.Assert(!reader.IsStartElement(), "Expected binary data block now");
                                
                                // Read in positions float by float. 
                                vertices = new VectorBuffer(numPoints, 3);

                               // byte[] rawData = new byte[numPoints * 4 * 3];
                                //char[] raw64Data = new char[12 + numPoints * 4 * 4];
                                //char[] raw64Data = new char[12];
                                // Skip the spaces this way.
                                //reader.ReadValueChunk(raw64Data, 0, 12);
                                rawString = reader.ReadContentAsString();
                                //int actualSize = reader.ReadContentAsBase64(rawData, 0, rawData.Length);
                                //reader.ReadValueChunk(raw64Data, 0, raw64Data.Length);
                                rawData = Convert.FromBase64String(rawString);
                                Int64 someNumber = BitConverter.ToInt64(rawData, 0);


                                Buffer.BlockCopy(rawData, sizeof(Int64), vertices.Data, 0, 4 * 3 * numPoints);
                                //Array.Reverse(rawData);
                                //for (int v = 0; v < numPoints; ++v)
                                //{
                                //    vertices[v] = new Vec3();
                                //    Buffer.BlockCopy(rawData, v*4*3 + sizeof(Int64), vertices[v].Data, 0, 4*3);

                                //    //Debug.Assert(vertices[v][0] <= rangeMax && vertices[v][0] >= rangeMin, "First value out of read range.");
                                //    //Debug.Assert(vertices[v][1] <= rangeMax && vertices[v][1] >= rangeMin, "Second value out of read range.");
                                //    //Debug.Assert(vertices[v][2] <= rangeMax && vertices[v][2] >= rangeMin, "Third value out of read range.");
                                //}

                                break;

                            //<Cells>
                            //<DataArray type = "Int64" Name="connectivity" format="binary" RangeMin="0" RangeMax="2826">
                            case "Cells":
                                Debug.Assert(readNumberPointsAndCells, "No size read yet.");

                                // Assert all attributes are given as expected.
                                do
                                {
                                    reader.Read();
                                } while (!reader.IsStartElement());

                                Debug.Assert(reader.Name.Equals("DataArray") && reader.HasAttributes, "No data here.");
                                Debug.Assert(reader.GetAttribute("type").Equals("Int64"), "Not 642 bit int as expected, but " + reader.GetAttribute("type"));
                                Debug.Assert(reader.GetAttribute("Name").Equals("connectivity"), "Not the connectivity data array, but " + reader.GetAttribute("Name"));
                                Debug.Assert(reader.GetAttribute("format").Equals("binary"), "Not binary data, but " + reader.GetAttribute("format"));
                                // Read range of positions.
                                int idxMin = int.Parse(reader.GetAttribute("RangeMin"));
                                int idxMax = int.Parse(reader.GetAttribute("RangeMax"));
                                Debug.Assert(idxMin == 0 && idxMax == numPoints - 1,
                                    "Cell indices not matching the number of points, instead " + numPoints + "Points, but indices in [" + idxMin + ',' + idxMax + "].");

                                // Next element should be a binary block.
                                reader.Read();
                                Debug.Assert(!reader.IsStartElement(), "Expected binary data block now");

                                rawString = reader.ReadContentAsString();
                                //int actualSize = reader.ReadContentAsBase64(rawData, 0, rawData.Length);
                                //reader.ReadValueChunk(raw64Data, 0, raw64Data.Length);
                                rawData = Convert.FromBase64String(rawString);

                                // ==================== Read Offsets ================== \\

                                // <DataArray type="Int64" Name="offsets" format="binary" RangeMin="3" RangeMax="16536">
                                // Assert all attributes are given as expected.
                                do
                                {
                                    reader.Read();
                                } while (!reader.IsStartElement());

                                Debug.Assert(reader.Name.Equals("DataArray") && reader.HasAttributes, "No data here.");
                                Debug.Assert(reader.GetAttribute("type").Equals("Int64"), "Not 642 bit int as expected, but " + reader.GetAttribute("type"));
                                Debug.Assert(reader.GetAttribute("Name").Equals("offsets"), "Not the connectivity offset data array, but " + reader.GetAttribute("Name"));
                                Debug.Assert(reader.GetAttribute("format").Equals("binary"), "Not binary data, but " + reader.GetAttribute("format"));
                                // Read range of positions.
                                int offMin = int.Parse(reader.GetAttribute("RangeMin"));
                                int offMax = int.Parse(reader.GetAttribute("RangeMax"));

                                // Next element should be a binary block.
                                reader.Read();
                                Debug.Assert(!reader.IsStartElement(), "Expected binary data block now");

                                // Read and convert Base64.
                                rawString = reader.ReadContentAsString();
                                byte[] rawDataOffsets = Convert.FromBase64String(rawString);
                                Int64[] offsets = new Int64[numCells];

                                Int64 bytesForCells = BitConverter.ToInt64(rawDataOffsets, 0);
                                Debug.Assert(bytesForCells == numCells * 8, "The first number is not the number of bytes used for cell indices.");

                                // Old variant: We allowed for different primitives in one field.

                                //Int64 lastOffset = 0;
                                //for (int o = 0; o < numCells; ++o)
                                //{
                                //    Int64 offset = BitConverter.ToInt64(rawDataOffsets, (o+1) * sizeof(Int64) );
                                //    int size = (int)(offset - lastOffset);
                                //    indices[o] = new Index(size);
                                //    Int64[] data = new Int64[size];

                                //    // Convert to int indices.
                                //    Buffer.BlockCopy(rawData, (int)(lastOffset + 1) * sizeof(Int64), data, 0, size * sizeof(Int64));
                                //    for (int i = 0; i < size; ++i)
                                //    {
                                //        indices[o][i] = (int)data[i];
                                //        Debug.Assert(data[i] >= 0 && data[i] < numPoints, "Index out of bounds.");
                                //    }
                                //    lastOffset = offset;
                                //    //Debug.Assert(vertices[v][0] <= rangeMax && vertices[v][0] >= rangeMin, "First value out of read range.");
                                //    //Debug.Assert(vertices[v][1] <= rangeMax && vertices[v][1] >= rangeMin, "Second value out of read range.");
                                //    //Debug.Assert(vertices[v][2] <= rangeMax && vertices[v][2] >= rangeMin, "Third value out of read range.");
                                //}

                                // Read the length of first element - assume all will be the same length.
                                int firstElementSize = (int)BitConverter.ToInt64(rawDataOffsets, (1) * sizeof(Int64));
                                Debug.Assert(firstElementSize * numCells == rawData.Length / sizeof(Int64) - 1, "Assuming all cells have the same number of points.");
                                int[] croppedData = new int[numCells * firstElementSize];
                                for (int n = 0; n < numCells * firstElementSize; ++n)
                                    croppedData[n] = BitConverter.ToInt32(rawData, (n + 1) * 8);

                                //int[] croppedData = new int[numCells * firstElementSize];
                                //Debug.Assert(sizeof(int) == sizeof(Int64));
                                //Buffer.BlockCopy(rawData, sizeof(Int64), croppedData, 0, numCells * firstElementSize * sizeof(Int64));


                                indices = new IndexArray(croppedData, firstElementSize);

                                break;
                            default:
                                break;
                        }
                    }
                }
            }

            Grid = new UnstructuredGeometry(vertices, indices);

            watch.Stop();
            Console.WriteLine("Geometry loading took {0}m {1}s", (int)watch.Elapsed.TotalMinutes, watch.Elapsed.Seconds);

            return Grid;
        }

        public override ScalarField LoadFieldSlice(SliceRange slice)
        {
            throw new NotImplementedException();
        }

        public override ScalarFieldUnsteady LoadTimeSlices(SliceRange slices, int starttime, int timelength)
        {
            throw new NotImplementedException();
        }

        public override VectorFieldUnsteady LoadTimeVectorField(SliceRange[] slices, int starttime, int timelength)
        {
            throw new NotImplementedException();
        }
    }
}
