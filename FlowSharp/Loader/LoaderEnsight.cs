using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;

namespace FlowSharp
{
    class LoaderEnsight
    {
        //Aneurysm.Variable _variable;
        private Aneurysm.GeometryPart _part;
        public static int[] NumVerticesPerPart;
        public LoaderEnsight(Aneurysm.GeometryPart part)//Aneurysm.Variable var = Aneurysm.Variable.velocity)
        {
            _part = part;
            //_variable = var;
        }
        public ScalarFieldUnsteady LoadTimeSlices(int starttime = -1, int timelength = -1)
        {
            return new ScalarFieldUnsteady();
        }

        public void LoadGridSizes()
        {
            if (NumVerticesPerPart != null)
                return;

            // List<int> numVerticesPerPart = new List<int>();
            Dictionary<int, int> numVerticesPerPart = new Dictionary<int, int>();
            int maxIdx = 0;

            string filename = Aneurysm.Singleton.GridFilename;

            Debug.Assert(File.Exists(filename));

            using (FileStream fs = File.Open(@filename, FileMode.Open))
            {
                // Velocity
                // part
                // 1 int
                // coordinate
                using (BinaryReader reader = new BinaryReader(fs))
                {
                    // Read in all floats.
                    Debug.Assert(reader.BaseStream.Length > 8 * 80 + 2, "Less bytes than a header requires.");

                    string l0_type = ReadBlock(reader);
                    Debug.Assert(l0_type.Equals("C Binary"), "Not the expected file type format.");

                    string l1_desc = ReadBlock(reader);
                    string l2_desc = ReadBlock(reader);
                    // Are vertex IDs given or just incremenal?
                    string l3_n_id = ReadBlock(reader);
                    bool containsVertexIDs = l3_n_id.Contains("given");
                    if (containsVertexIDs)
                        throw new NotImplementedException("Non-incremental vertex IDs not yet considered.");

                    string l4_e_id = ReadBlock(reader);
                    bool containsIndexIDs = l4_e_id.Contains("given");
                    if (containsIndexIDs)
                        throw new NotImplementedException("Non-incremental index IDs not yet considered.");

                    string l5_poss_ext = ReadBlock(reader);

                    while (reader.BaseStream.Position < reader.BaseStream.Length)
                    {
                        string l6_part;
                        if (l5_poss_ext.Equals("part"))
                        {
                            l6_part = l5_poss_ext;
                        }
                        else
                        {
                            l6_part = ReadBlock(reader);
                            Debug.Assert(l6_part.Equals("part"), "Expected \'part\' identifier");
                        }

                        int l7_part_num = reader.ReadInt32();
                        string l8_desc = ReadBlock(reader);

                        Console.WriteLine(l8_desc + ", part nr. " + l7_part_num);
                        string l9_cord = ReadBlock(reader);
                        Debug.Assert(l9_cord.Equals("coordinates"), "Expected \'coordinates\' identifier.");

                        // Read vertex positions.
                        int numVerts = reader.ReadInt32();

                        reader.BaseStream.Position += numVerts * 4 * 3;

                        //string elementType = ReadBlock(reader);
                        //Console.WriteLine("Element Type: " + elementType);
                        //if (!elementType.Equals("hexa8"))
                        //    throw new NotImplementedException("Only hexagonal grids so far");

                        //int numVerticesPerCell = 8;
                        string elementType = ReadBlock(reader);
                        int numIdxPerCell = elementType[elementType.Length-1] - '0';
                        //if (!elementType.Equals("hexa8"))
                        //    throw new NotImplementedException("Only hexagonal grids so far");
                        int numIdxs = reader.ReadInt32();
                        reader.BaseStream.Position += numIdxs * 4 * numIdxPerCell;

                        l5_poss_ext = "";

                        numVerticesPerPart.Add(l7_part_num, numVerts);
                        maxIdx = Math.Max(maxIdx, l7_part_num);
                    }
                }
            }

            NumVerticesPerPart = new int[maxIdx];
            for (int i = 0; i < maxIdx; ++i)
                NumVerticesPerPart[i] = numVerticesPerPart.ContainsKey(i) ? numVerticesPerPart[i] : -1;
        }

        public HexGrid LoadGrid()
        {
            string filename = Aneurysm.Singleton.GridFilename;

            VectorBuffer vertices;
            IndexArray indices;

            using (FileStream fs = File.Open(@filename, FileMode.Open))
            {

                //byte[] block = new byte[80];
                //fs.Read(block, 0, 80);
                // Read in the data you need.
                using (BinaryReader reader = new BinaryReader(fs))
                {
                    // Read in all floats.
                    Debug.Assert(reader.BaseStream.Length > 8*80 + 2, "Less bytes than a header requires.");

                    string l0_type = ReadBlock(reader);
                    Debug.Assert(l0_type.Equals("C Binary"), "Not the expected file type format.");

                    string l1_desc = ReadBlock(reader);
                    string l2_desc = ReadBlock(reader);
                    // Are vertex IDs given or just incremenal?
                    string l3_n_id = ReadBlock(reader);
                    bool containsVertexIDs = l3_n_id.Contains("given");
                    if (containsVertexIDs)
                        throw new NotImplementedException("Non-incremental vertex IDs not yet considered.");

                    string l4_e_id = ReadBlock(reader);
                    bool containsIndexIDs = l4_e_id.Contains("given");
                    if (containsIndexIDs)
                        throw new NotImplementedException("Non-incremental index IDs not yet considered.");

                    string l5_poss_ext = ReadBlock(reader);
                    string l6_part;
                    if (l5_poss_ext.Equals("part"))
                    {
                        l6_part = l5_poss_ext;
                    }
                    else
                    {
                        l6_part = ReadBlock(reader);
                        Debug.Assert(l6_part.Equals("part"), "Expected \'part\' identifier");
                    }

                    int l7_part_num = reader.ReadInt32();
                    string l8_desc = ReadBlock(reader);
                    string l9_cord = ReadBlock(reader);
                    Debug.Assert(l9_cord.Equals("coordinates"), "Expected \'coordinates\' identifier.");

                    // Read vertex positions.
                    int numVerts = reader.ReadInt32();

                    //numVerts = 8;
                    vertices = new VectorBuffer(numVerts, 3);
                    //vertices = new Vector[numVerts];
                    //for (int i = 0; i < numVerts; ++i)
                    //{
                    //    vertices[i] = new Vector(3);
                    //}

                    //Vector minPos = new Vector(float.MaxValue, 3);
                    //Vector maxPos = new Vector(float.MinValue, 3);

                    for (int dim = 0; dim < 3; ++dim)
                        for (int v = 0; v < numVerts; ++v)
                        {
                            vertices[v][dim] = reader.ReadSingle();
                            //if (v < 8)
                            //{
                            //    minPos[dim] = Math.Min(minPos[dim], vertices[v][dim]);
                            //    maxPos[dim] = Math.Max(maxPos[dim], vertices[v][dim]);
                            //}
                        }

                    //Vector extent = maxPos - minPos;
                    //float maxEx = Math.Max(Math.Max(extent[0], extent[1]), extent[2]);

                    //for (int v = 0; v < numVerts; ++v)
                    //    vertices[v] = (vertices[v] - minPos) / maxEx;


                    // Read indices.
                    string elementType = ReadBlock(reader);
                    Console.WriteLine("Element Type: " + elementType);
                    if (!elementType.Equals("hexa8"))
                        throw new NotImplementedException("Only hexagonal grids so far");

                    int numVerticesPerCell = 8;
                    int numIdxs = reader.ReadInt32();
                    indices = new IndexArray(numIdxs, numVerticesPerCell);
                    for (int i = 0; i < numIdxs; ++i)
                    {
                        indices[i] = new Index(numVerticesPerCell);
                        for (int v = 0; v < numVerticesPerCell; ++v)
                        {
                            Debug.Assert(reader.BaseStream.Position < reader.BaseStream.Length, "Reached End of file.");
                            indices[i][v] = reader.ReadInt32() - 1;
                        }
                    }
                }
            }
            Console.WriteLine("Finished loading grid.");
            return new HexGrid(vertices, indices);
        }

        public VectorBuffer LoadAttribute(Aneurysm.Variable variable, int timeslice)
        {
            LoadGridSizes();

            string filename = Aneurysm.Singleton.EnsightVariableFileName(variable, timeslice);
            VectorBuffer vertices;

            Debug.Assert(File.Exists(filename));

            using (FileStream fs = File.Open(@filename, FileMode.Open))
            {
                // Velocity
                // part
                // 1 int
                // coordinate
                using (BinaryReader reader = new BinaryReader(fs))
                {
                    // Read in all floats.
                    Debug.Assert(reader.BaseStream.Length > (3 * 80 + 2*4) * (int)_part, "Less bytes than a header requires.");
                    int numDims = variable == Aneurysm.Variable.velocity ? 3 : 1;
                    int numVerts = 0;
                    int currentPart = -1;


                    // reader.BaseStream.Position += (80 * 3 + 1) * 4;
                    string l0_attrib = ReadBlock(reader);
                    Debug.Assert(l0_attrib.Equals(Aneurysm.Singleton.VariableName(variable)), "Not the expected attribute.");

                    while (currentPart != (int)_part)
                    {
                        string l1_poss_ext = ReadBlock(reader);
                        Debug.Assert(l1_poss_ext.Equals("part"));

                        currentPart = reader.ReadInt32();

                        string l3_coord = ReadBlock(reader);
                        Debug.Assert(l3_coord.Equals("coordinates"), "Expected \'coordinates\' identifier.");

                        // Read vertex positions.
                        numVerts = NumVerticesPerPart[(int)currentPart];

                        // Skip if not of interest.
                        if (currentPart != (int)_part)
                        {
                            reader.BaseStream.Position += numVerts * sizeof(float) * numDims;
                        }
                    }

                    //vertices = new VectorBuffer(numVerts, numDims);
                    vertices = new VectorBuffer(reader.ReadBytes(numVerts * numDims * sizeof(float)), numDims);
                    Debug.Assert(vertices.Length == numVerts);
                    //for (int dim = 0; dim < numDims; ++dim)
                    //    for (int v = 0; v < numVerts; ++v)
                    //    {
                    //        vertices[v][dim] = reader.ReadSingle();
                    //        //if (v < 8)
                    //        //{
                    //        //    minPos[dim] = Math.Min(minPos[dim], vertices[v][dim]);
                    //        //    maxPos[dim] = Math.Max(maxPos[dim], vertices[v][dim]);
                    //        //}
                    //    }
                    
                }
            }
            Console.WriteLine("Finished loading " + variable);
            return vertices;
        }
        //public HexGrid LoadGrid()
        //{
        //    string filename = Aneurysm.Singleton.GridFilename;

        //    Vector[] vertices;
        //    Index[] indices;

        //    using (FileStream fs = File.Open(@filename, FileMode.Open))
        //    {

        //        //byte[] block = new byte[80];
        //        //fs.Read(block, 0, 80);
        //        // Read in the data you need.
        //        using (BinaryReader reader = new BinaryReader(fs))
        //        {
        //            // Read in all floats.
        //            Debug.Assert(reader.BaseStream.Length > 8 * 80 + 2, "Less bytes than a header requires.");

        //            string l0_type = ReadBlock(reader);
        //            Debug.Assert(l0_type.Equals("C Binary"), "Not the expected file type format.");

        //            string l1_desc = ReadBlock(reader);
        //            string l2_desc = ReadBlock(reader);
        //            // Are vertex IDs given or just incremenal?
        //            string l3_n_id = ReadBlock(reader);
        //            bool containsVertexIDs = l3_n_id.Contains("given");
        //            if (containsVertexIDs)
        //                throw new NotImplementedException("Non-incremental vertex IDs not yet considered.");

        //            string l4_e_id = ReadBlock(reader);
        //            bool containsIndexIDs = l4_e_id.Contains("given");
        //            if (containsIndexIDs)
        //                throw new NotImplementedException("Non-incremental index IDs not yet considered.");

        //            string l5_poss_ext = ReadBlock(reader);
        //            string l6_part;
        //            if (l5_poss_ext.Equals("part"))
        //            {
        //                l6_part = l5_poss_ext;
        //            }
        //            else
        //            {
        //                l6_part = ReadBlock(reader);
        //                Debug.Assert(l6_part.Equals("part"), "Expected \'part\' identifier");
        //            }

        //            int l7_part_num = reader.ReadInt32();
        //            string l8_desc = ReadBlock(reader);
        //            string l9_cord = ReadBlock(reader);
        //            Debug.Assert(l9_cord.Equals("coordinates"), "Expected \'coordinates\' identifier.");

        //            // Read vertex positions.
        //            int numVerts = reader.ReadInt32();

        //            //numVerts = 8;

        //            vertices = new Vector[numVerts];
        //            for (int i = 0; i < numVerts; ++i)
        //            {
        //                vertices[i] = new Vector(3);
        //            }

        //            Vector minPos = new Vector(float.MaxValue, 3);
        //            Vector maxPos = new Vector(float.MinValue, 3);

        //            for (int dim = 0; dim < 3; ++dim)
        //                for (int v = 0; v < numVerts; ++v)
        //                {
        //                    vertices[v][dim] = reader.ReadSingle();
        //                    if (v < 8)
        //                    {
        //                        minPos[dim] = Math.Min(minPos[dim], vertices[v][dim]);
        //                        maxPos[dim] = Math.Max(maxPos[dim], vertices[v][dim]);
        //                    }
        //                }

        //            Vector extent = maxPos - minPos;
        //            float maxEx = Math.Max(Math.Max(extent[0], extent[1]), extent[2]);

        //            for (int v = 0; v < numVerts; ++v)
        //                vertices[v] = (vertices[v] - minPos) / maxEx;


        //            // Read indices.
        //            string elementType = ReadBlock(reader);
        //            Console.WriteLine("Element Type: " + elementType);
        //            if (!elementType.Equals("hexa8"))
        //                throw new NotImplementedException("Only hexagonal grids so far");

        //            int numVerticesPerCell = 8;
        //            int numIdxs = reader.ReadInt32();
        //            indices = new Index[numIdxs];
        //            for (int i = 0; i < numIdxs; ++i)
        //            {
        //                indices[i] = new Index(numVerticesPerCell);
        //                for (int v = 0; v < numVerticesPerCell; ++v)
        //                {
        //                    Debug.Assert(reader.BaseStream.Position < reader.BaseStream.Length, "Reached End of file.");
        //                    indices[i][v] = reader.ReadInt32() - 1;
        //                }
        //            }
        //        }
        //    }
        //    Console.WriteLine("Finished loading grid.");
        //    return new HexGrid(vertices, indices);
        //}

        private string ReadBlock(BinaryReader reader)
        {
            char[] block = reader.ReadChars(80);
            int b = 0;
            for (; b < 80; ++b)
                if (block[b] == '\0')
                    break;
            return new string(block, 0, b);
        }
    }
}
