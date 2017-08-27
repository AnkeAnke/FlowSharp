using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace FlowSharp
{
    /// <summary>
    /// Vector data where each vector dimension is stored in its own data block.
    /// </summary>
    class VectorChannels : VectorData
    {
        private float[][] _data;
        /// <summary>
        /// Number of slices.
        /// </summary>
        public override int VectorLength { get { return _data.Length; } protected set { } }
        /// <summary>
        /// Size of first slice.
        /// </summary>
        public override int Length { get { return _data[0].Length; } }
        public override VectorRef this[int index]
        {
            get
            {
                Debug.Assert(index >= 0 && index < Length, "Index out of range. " + index + " not in [0," + Length + ']');
                return new VectorChannelElement(this, index);
                //Vector ret = new Vector(VectorLength);
                //for (int dim = 0; dim < VectorLength; ++dim)
                //    ret[dim] = _data[dim][index];
                //return ret;
            }
            set
            {
                Debug.Assert(value.Length == VectorLength, "Wrong dimensions.");
                Debug.Assert(index >= 0 && index < Length, "Index out of range. " + index + " not in [0," + Length + ']');
                for (int dim = 0; dim < VectorLength; ++dim)
                    _data[dim][index] = value[dim];
            }
        }

        public VectorChannels(float[][] data)
        {
            _data = data;
        }
        public VectorChannels() { }
        public VectorChannels(int numElements, int vectorLength) { SetSize(numElements, vectorLength); }
        public override void SetSize(int numElements, int vectorLength)
        {
            _data = new float[vectorLength][];
            for (int dim = 0; dim < vectorLength; ++dim)
                _data[dim] = new float[numElements];
        }

        public override VectorData GetSliceInLastDimension(int posInLastDimension, Index size)
        {
            Debug.Assert(Length == size.Product(), "Size and number of elements in buffer do not match.");
            Debug.Assert(posInLastDimension >= 0 && posInLastDimension < size.T, "Index out of given range.");

            float[][] slices = new float[VectorLength][];
            int newNumElements = size.Product() / size.T;
            for (int i = 0; i < slices.Length; ++i)
            {

                slices[i] = new float[newNumElements];
                Array.Copy(_data[i], newNumElements * posInLastDimension, slices[i], 0, newNumElements);
            }
            return new VectorChannels(slices);
        }

        public override void ChangeEndian()
        {
            for (int n = 0; n < Length; ++n)
                Util.FlipEndian(_data[n]);
        }

        public override float[] GetChannel(int index)
        {
            return _data[index];
        }

        public override float[] GetData()
        {
            if (VectorLength == 1)
                return _data[0];
            throw new NotImplementedException();
        }

        class VectorChannelElement : VectorRef
        {
            private VectorChannels _parent;
            private int _index;

            public override int Length { get { return _parent.VectorLength; } }

            public override float this[int index]
            {
                get
                {
                    Debug.Assert(index >= 0 && index < Length, "Index out of bounds.");
                    return _parent._data[index][_index];
                }
                set
                {
                    _parent._data[index][_index] = value;
                }
            }

            public VectorChannelElement(VectorChannels parent, int offset)
            {
                _parent = parent;
                _index = offset;
            }

            protected override void SetSize(int size)
            {
                throw new NotImplementedException();
            }
        }
    }
}
