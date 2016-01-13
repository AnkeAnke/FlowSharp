using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace RescaleRawData
{
    class Program
    {
        static float ScaleFactorMult;
        static int TotalDone = 0;
        static void Main(string[] args)
        {
            ComputeScaleFactor(0, 6, 0, 0, 0.04f);


            string[] uPaths = Directory.GetFiles("E:/Anke/Dev/Data/Shaheen_8", "U.0000*.data", SearchOption.AllDirectories);
            string[] vPaths = Directory.GetFiles("E:/Anke/Dev/Data/Shaheen_8", "V.0000*.data", SearchOption.AllDirectories);
            string[] wPaths = Directory.GetFiles("E:/Anke/Dev/Data/Shaheen_8", "W.0000*.data", SearchOption.AllDirectories);

            string[] sPaths = Directory.GetFiles("E:/Anke/Dev/Data/Shaheen_8", "S.0000*.data", SearchOption.AllDirectories);
            string[] tPaths = Directory.GetFiles("E:/Anke/Dev/Data/Shaheen_8", "T.0000*.data", SearchOption.AllDirectories);
            string[] hPaths = Directory.GetFiles("E:/Anke/Dev/Data/Shaheen_8", "Eta.0000*.data", SearchOption.AllDirectories);

            //foreach (string s in uPaths)
            //    ScaleFile(s);
            //foreach (string s in vPaths)
            //    ScaleFile(s);
            foreach (string s in wPaths)
                ScaleFile(s);

            //foreach (string s in sPaths)
            //    TurnFile(s);
            //foreach (string s in tPaths)
            //    TurnFile(s);
            //foreach (string s in hPaths)
            //    TurnFile(s);

            return;
        }

        static void ComputeScaleFactor(int days, int hours, int minutes, int seconds, float degreeStep)
        {
            int totalSeconds = seconds + (minutes + (hours + days * 24) * 60) * 60;
            float metersAt20N40E = 100.0f * degreeStep * 1000; // Approximation. 

            ScaleFactorMult = totalSeconds / metersAt20N40E;
        }

        static void ScaleFile(string dir)
        {
            using (FileStream fs = File.Open(@dir, FileMode.Open))
            {
                using (BinaryReader reader = new BinaryReader(fs))
                {
                    // Read in all floats.
                    int length = (int)reader.BaseStream.Length;
                    int lengthF = length / 4;
                    Debug.Assert(length % 4 == 0);
                    byte[] data = reader.ReadBytes(length);
                    Array.Reverse(data);

                    // Rewrite the data in memory.
                    unsafe
                    {
                        fixed (byte* bytePtr = data)
                        {
                            float* floatPtr = (float*)bytePtr;

                            // Swap beginning and ending float after multiplication.
                            for (int i = 0; i < lengthF; ++i)
                            {
                                float tmp = floatPtr[i];
                                floatPtr[i] = floatPtr[lengthF - i - 1] * ScaleFactorMult;
                                floatPtr[lengthF - i - 1] = tmp * ScaleFactorMult;
                            }
                        }
                    }

                    // Write to file.
                    using (FileStream writeStream = File.Open(@dir + "_scaled_end", FileMode.Create))
                    {
                        writeStream.Write(data, 0, length);
                    }
                }
            }
            Console.WriteLine("Done: {0}", ++TotalDone);
        }

        static void TurnFile(string dir)
        {
            using (FileStream fs = File.Open(@dir, FileMode.Open))
            {
                using (BinaryReader reader = new BinaryReader(fs))
                {
                    // Read in all floats.
                    int length = (int)reader.BaseStream.Length;
                    int lengthF = length / 4;
                    Debug.Assert(length % 4 == 0);
                    byte[] data = reader.ReadBytes(length);
                    Array.Reverse(data);

                    // Rewrite the data in memory.
                    unsafe
                    {
                        fixed (byte* bytePtr = data)
                        {
                            float* floatPtr = (float*)bytePtr;

                            // Swap beginning and ending float after multiplication.
                            for (int i = 0; i < lengthF; ++i)
                            {
                                float tmp = floatPtr[i];
                                floatPtr[i] = floatPtr[lengthF - i - 1] ;
                                floatPtr[lengthF - i - 1] = tmp;
                            }
                        }
                    }

                    // Write to file.
                    using (FileStream writeStream = File.Open(@dir + "_scaled_end", FileMode.Create))
                    {
                        writeStream.Write(data, 0, length);
                    }
                }
            }
            Console.WriteLine("Done: {0}", ++TotalDone);
        }
    }
}
