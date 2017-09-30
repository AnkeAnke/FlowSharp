using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace FlowSharp
{
    class BinaryFile
    {
        public static void WriteFile(string filename, VectorData data, FileMode mode = FileMode.Create)
        {
            WriteFileArray(filename, data.GetData(), mode);
        }

        public static VectorBuffer ReadFile(string filename, int vectorLength)
        {
            float[] data = ReadFileArray<float>(filename);
            if (data == null)
                return null;
            return new VectorBuffer(data, vectorLength);
        }

        public static void WriteFileArray<T>(string filename, T[] data, FileMode mode = FileMode.Create)
        {
            using (FileStream fs = File.Open(@filename, mode))
            {
                BinaryFormatter formatter = new BinaryFormatter();
                try
                {
                    formatter.Serialize(fs, data);
                }
                catch (SerializationException e)
                {
                    Console.WriteLine("Failed to serialize. Reason: " + e.Message);
                    throw;
                }
                finally
                {
                    fs.Close();
                }
            }
        }




        public static T[] ReadFileArray<T>(string filename)
        {
            if (!File.Exists(filename))
                return null;

            using (FileStream fs = File.Open(@filename, FileMode.Open))
            {
                BinaryFormatter formatter = new BinaryFormatter();
                try
                {
                    return (T[])formatter.Deserialize(fs);
                }
                catch (SerializationException e)
                {
                    Console.WriteLine("Failed to serialize. Reason: " + e.Message);
                    throw;
                }
                finally
                {
                    fs.Close();
                }
            }
        }

        public static T[] ReadAllFileArrays<T>(string filename)
        {
            if (!File.Exists(filename))
                return null;

            using (FileStream fs = File.Open(@filename, FileMode.Open))
            {
                BinaryFormatter formatter = new BinaryFormatter();
                try
                {
                    List<T[]> arrays = new List<T[]>(4);
                    while(fs.Position < fs.Length)
                    {
                        arrays.Add((T[])formatter.Deserialize(fs));
                    }
                    if (arrays.Count <= 1)
                        return arrays?[0];

                    // Sum up lengths.
                    int lengthSum = 0;
                    foreach (T[] data in arrays)
                        lengthSum += data.Length;

                    // Concatenate the arrays.
                    T[] concat = new T[lengthSum];
                    lengthSum = 0;
                    foreach(T[] data in arrays)
                    {
                        Array.Copy(data, 0, concat, lengthSum, data.Length);
                        lengthSum += data.Length;
                    }

                    return concat;
                }
                catch (SerializationException e)
                {
                    Console.WriteLine("Failed to serialize. Reason: " + e.Message);
                    throw;
                }
                finally
                {
                    fs.Close();
                }
            }
        }
    }
}
