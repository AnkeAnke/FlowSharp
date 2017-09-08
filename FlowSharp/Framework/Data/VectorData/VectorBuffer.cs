using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace FlowSharp
{
    class VectorBuffer : VectorData
    {
        public float[] Data { get; private set; }
        public override int VectorLength { get; protected set; }
        public override int Length { get { return Data.Length / VectorLength; } }

        public override VectorRef this[int index]
        {
            get
            {
                Debug.Assert(index >= 0 && index < Length, "Index out of range. " + index + " not in [0," + Length + ']');
                return new VectorBufferElement(this, index * VectorLength);
                //Vector ret = new Vector(VectorLength);
                //for (int l = 0; l < VectorLength; ++l)
                //    ret[l] = Data[index * VectorLength + l];
                //return ret;
            }
            set
            {
                Debug.Assert(value.Length == VectorLength, "Wrong dimensions.");
                Debug.Assert(index >= 0 && index < Length, "Index out of range. " + index + " not in [0," + Length + ']');
                for (int l = 0; l < VectorLength; ++l)
                    Data[index * VectorLength + l] = value[l];
            }
        }

        public VectorBuffer() { }
        public VectorBuffer(int numElements, int vectorLength) { SetSize(numElements, vectorLength); }
        public VectorBuffer(int numElements, int vectorLength, float value)
        {
            SetSize(numElements, vectorLength);
            for (int d = 0; d < Data.Length; ++d)
                Data[d] = value;
        }
        public override void SetSize(int numElements, int vectorLength)
        {
            VectorLength = vectorLength;
            Data = new float[VectorLength * numElements];
        }
        public VectorBuffer(float[] data, int vectorLength)
        {
            Debug.Assert(data.Length % vectorLength == 0);
            Data = data;
            VectorLength = vectorLength;
        }

        public VectorBuffer(byte[] data, int vectorLength)
        {
            Debug.Assert(data.Length % sizeof(float) == 0 && (data.Length / sizeof(float)) % vectorLength == 0);
            Data = new float[data.Length / sizeof(float)];
            Buffer.BlockCopy(data, 0, Data, 0, data.Length);
            VectorLength = vectorLength;
        }

        public VectorBuffer(Vector[] data, int vectorLength = -1)
        {
            VectorLength = vectorLength > 0 ? vectorLength : data[0].Length;
            Data = new float[data.Length * VectorLength];
            for (int i = 0; i < data.Length; ++i)
            {
                Debug.Assert(data[i].Length == VectorLength);
                Buffer.BlockCopy(data[i].Data, 0, Data, i * VectorLength, VectorLength * sizeof(float));
            }
        }

        public VectorBuffer(VectorData data)
        {
            VectorLength = data.VectorLength;
            Data = new float[data.Length * VectorLength];
            for (int i = 0; i < data.Length; ++i)
            {
                for (int v = 0; v < VectorLength; ++v)
                    Data[i * VectorLength + v] = data[i][v];
            }
        }

        public VectorBuffer(VectorData[] data)
        {
            VectorLength = data[0].VectorLength;
            int innerLength = data[0].Length;
            Data = new float[data.Length * innerLength * VectorLength];
            for (int d = 0; d < data.Length; ++d)
            {
                Debug.Assert(innerLength == data[d].Length);
                for (int i = 0; i < innerLength; ++i)
                    for (int v = 0; v < VectorLength; ++v)
                        Data[d * data.Length * VectorLength +
                             i * VectorLength +
                             v] = data[d][i][v];

            }
        }

        public override VectorData GetSliceInLastDimension(int posInLastDimension, Index size)
        {
            Debug.Assert(Length == size.Product(), "Size and number of elements in buffer do not match.");
            Debug.Assert(posInLastDimension >= 0 && posInLastDimension < size.T, "Index out of given range.");
            int newNumElements = size.Product() / size.T;
            float[] newData = new float[newNumElements * VectorLength];

            Array.Copy(Data, newNumElements * posInLastDimension, newData, 0, newNumElements);
            return new VectorBuffer(newData, VectorLength);
        }

        public override void ChangeEndian()
        {
            Util.FlipEndian(Data);
        }

        public override float[] GetChannel(int index)
        {
            Debug.Assert(index >= 0 && index < VectorLength);
            float[] data = new float[Length];
            for (int e = 0; e < Length; ++e)
                data[e] = Data[e * VectorLength + index];
            return data;
        }

        public override float[] GetData()
        {
            return Data;
        }

        class VectorBufferElement : VectorRef
        {
            private VectorBuffer _parent;
            private int _offset;

            public override int Length { get { return _parent.VectorLength; } }

            public override float this[int index]
            {
                get
                {
                    Debug.Assert(index >= 0 && index < Length, "Index out of bounds.");
                    return _parent.Data[_offset + index];
                }
                set
                {
                    Debug.Assert(index >= 0 && index < Length, "Index out of bounds.");
                    _parent.Data[_offset + index] = value;
                }
            }

            public VectorBufferElement(VectorBuffer parent, int offset)
            {
                _parent = parent;
                _offset = offset;
            }

            protected override void SetSize(int size)
            {
                throw new NotImplementedException();
            }
        }
    }
}
