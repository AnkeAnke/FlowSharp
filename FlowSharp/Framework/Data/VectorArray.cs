using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlowSharp
{
    class VectorArray
    {
        private float[] Data;
        public int VectorLength { get; protected set; }

        public Vector this[int index]
        {
            get
            {
                Vector ret = new Vector(VectorLength);
                for (int l = 0; l < VectorLength; ++l)
                    ret[l] = Data[index * VectorLength + l];
                return ret;
            }
            set
            {
                Debug.Assert(value.Length == VectorLength, "Wrong dimensions.");
                for (int l = 0; l < VectorLength; ++l)
                    Data[index * VectorLength + l] = value[l];
            }
        }

        public VectorArray(string filename)
        {
            using (FileStream fs = File.Open(@filename, FileMode.Open))
            {
                //fs.Read();

            }
        }
    }
}
