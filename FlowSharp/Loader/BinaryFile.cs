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
            //using (FileStream fs = File.Open(@filename, mode))
            //{
            //    BinaryFormatter formatter = new BinaryFormatter();
            //    try
            //    {
            //        formatter.Serialize(fs, data.GetData());
            //    }
            //    catch (SerializationException e)
            //    {
            //        Console.WriteLine("Failed to serialize. Reason: " + e.Message);
            //        throw;
            //    }
            //    finally
            //    {
            //        fs.Close();
            //    }
            //}
        }

        public static VectorBuffer ReadFile(string filename, int vectorLength)
        {
            float[] data = ReadFileArray<float>(filename);
            if (data == null)
                return null;
            return new VectorBuffer(data, vectorLength);
            //if (!File.Exists(filename))
            //    return null;

            //using (FileStream fs = File.Open(@filename, FileMode.Open))
            //{
            //    BinaryFormatter formatter = new BinaryFormatter();
            //    try
            //    {
            //        float[] data = (float[])formatter.Deserialize(fs);
            //        return new VectorBuffer(data, vectorLength);
            //    }
            //    catch (SerializationException e)
            //    {
            //        Console.WriteLine("Failed to serialize. Reason: " + e.Message);
            //        throw;
            //    }
            //    finally
            //    {
            //        fs.Close();
            //    }
            //}
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
    }
}
