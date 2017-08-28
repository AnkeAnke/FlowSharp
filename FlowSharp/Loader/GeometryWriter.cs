using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using SlimDX;

namespace FlowSharp
{
    static class GeometryWriter
    {
        public static void WriteToFile(string file, LineSet lines)
        {
            // Open the file. If it already exists, overwrite.
            using (FileStream fs = File.Open(@file, FileMode.Create))
            {
                using (BinaryWriter writer = new BinaryWriter(fs))
                {
                    // Write number of lines.
                    writer.Write(lines.Length);

                    // Write line lengths in order.
                    foreach (Line line in lines.Lines)
                        writer.Write(line.Length);

                    // Write positions.
                    foreach(Line line in lines.Lines)
                    {
                        foreach(Vector4 vec in line.Positions)
                        {
                            writer.Write(vec.X);
                            writer.Write(vec.Y);
                            writer.Write(vec.Z);
                        }
                    }
                    
                }
            }
        }

        public static void WriteHeightCSV(string file, LineSet lines)
        {
            // Open the file. If it already exists, overwrite.
            using (FileStream fs = File.Open(@file, FileMode.Create))
            {
                using (StreamWriter writer = new StreamWriter(fs))
                {
                    // Write positions.
                    foreach (Line line in lines.Lines)
                    {
                        foreach (Vector4 vec in line.Positions)
                        {
                            writer.Write("{0},", vec.Z);
                        }
                        writer.Write('\n');
                    }

                }
            }
        }

        public static void WriteGraphCSV(string file, Graph2D graph)
        {
            try {
                // Open the file. If it already exists, overwrite.
                using (FileStream fs = File.Open(@file, FileMode.Create))
                {
                    using (StreamWriter writer = new StreamWriter(fs))
                    {
                        foreach (float f in graph.X)
                        {
                            writer.Write("{0},", f);
                        }
                        writer.Write('\n');
                        foreach (float f in graph.Fx)
                        {
                            if (!float.IsNaN(f))
                                writer.Write("{0},", f);
                        }
                        writer.Write('\n');

                    }
                }
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        public static void WriteGraphCSV(string file, Graph2D[] graph)
        {
            try { 
                // Open the file. If it already exists, overwrite.
                using (FileStream fs = File.Open(@file, FileMode.Create))
                {
                    using (StreamWriter writer = new StreamWriter(fs))
                    {
                        // X values in first row. Assume equal.
                        foreach (float f in graph[0].X)
                        {
                            writer.Write("{0},", f);
                        }
                        writer.Write('\n');

                        // Fx values subsequent.
                        foreach (Graph2D g in graph)
                        {
                        
                            foreach (float f in g.Fx)
                            {
                                writer.Write("{0},", f);
                            }
                            writer.Write('\n');
                        }
                    }
                }
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
            }
}

        public static void ReadFromFile(string file, out LineSet lineset)
        {
            Line[] lines;

            // Open the file. If it already exists, overwrite.
            using (FileStream fs = File.Open(@file, FileMode.Open))
            {
                using (BinaryReader reader = new BinaryReader(fs))
                {
                    // Write number of lines.
                    lines = new Line[reader.ReadInt32()];

                    // Write line lengths in order.
                    for (int l = 0; l < lines.Length; ++l)
                        lines[l] = new Line() { Positions = new Vector4[reader.ReadInt32()] };

                    // Write positions.
                    float x, y, z;
                    foreach (Line line in lines)
                    {
                        for(int v = 0; v < line.Length; ++v)
                        {
                            x = reader.ReadSingle();
                            y = reader.ReadSingle();
                            z = reader.ReadSingle();
                            line[v] = new Vector4(x, y, z, 0);
                        }
                    }

                }
            }

            lineset = new LineSet(lines);
        }
    }
}
