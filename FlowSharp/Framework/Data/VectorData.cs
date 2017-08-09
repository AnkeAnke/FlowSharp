using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlowSharp
{
    abstract class VectorData : IEnumerable<VectorRef>
    {
        public Vector MinValue;
        public Vector MaxValue;
        public Vector Extent { get { return MaxValue - MinValue; } }
        /// <summary>
        /// THe length of each vector returned.
        /// </summary>
        public abstract int NumVectorDimensions { get; protected set; }
        /// <summary>
        /// Number of vectors stored.
        /// </summary>
        public abstract int Length { get; }
        /// <summary>
        /// Access the indexth vector. Assume copy for setting!
        /// </summary>
        /// <param name="index">Vector index.</param>
        /// <returns></returns>
        public abstract VectorRef this[int index]
        { get; set; }
        /// <summary>
        /// Slice through last space dimension, subset of original data. Vector Length stays constant.
        /// </summary>
        /// <param name="index">Slice index.</param>
        /// <param name="size">Dimension to take for slicing.</param>
        /// <returns></returns>
        public abstract VectorData GetSliceInLastDimension(int index, Index size);
        public abstract float[] GetChannel(int index);
        /// <summary>
        /// Set the inner sizes, create the buffers. Use in combination with new() constraint.
        /// </summary>
        /// <param name="numElements">Number of vectors to be stored.</param>
        /// <param name="vectorLength">Elements per vector.</param>
        public abstract void SetSize(int numElements, int vectorLength);
        /// <summary>
        /// Change endianess (flip bytes) of each vector.
        /// </summary>
        public abstract void ChangeEndian();

        public virtual void ExtractMinMax()
        {
            if (MinValue != null && MaxValue != null)
                return;

            Debug.Assert(Length > 0, "No data.");

            MinValue = new Vector(this[0]);
            MaxValue = new Vector(this[0]);

            foreach (VectorRef v in this)
            {
                MinValue.MinOf(v);
                MaxValue.MaxOf(v);
            }

        }
        #region Enumerator
        public IEnumerator<VectorRef> GetEnumerator()
        {
            return new VectorDataEnumerable(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new VectorDataEnumerable(this);
        }
        #endregion Enumerator
    }

    class VectorDataEnumerable : IEnumerator<VectorRef>
    {
        private VectorData _parent;
        private int _index;

        public VectorRef Current { get { return _parent[_index]; } }

        object IEnumerator.Current {  get { return this; } }

        public VectorDataEnumerable(VectorData parent)
        {
            _parent = parent;
            Reset();
        }

        public bool MoveNext()
        {
            _index++;
            return _index < _parent.Length;
        }

        public void Reset()
        {
            _index = -1;
        }

        public void Dispose() { }
    }

    //class VectorDataEnumerator : IEnumerable<VectorDataEnumerable>
    //{
    //    private VectorData _parent;
    //    private int _index;

    //}

    class VectorDataArray<DataType> : VectorData where DataType : VectorData, new()
    {
        /// <summary>
        /// Stack of data arrays. Assume same size and dimensionality in each.
        /// </summary>
        private DataType[] _data;

        public VectorDataArray(DataType[] data)
        {
#if DEBUG
            int vecLength = data[0].NumVectorDimensions;
            int length = data[0].Length;
            foreach (DataType buff in data)
                Debug.Assert(buff.Length == length && buff.NumVectorDimensions == vecLength, "Different dimensions in given data blocks.");
#endif
            MinValue = data[0].MinValue;
            MaxValue = data[0].MaxValue;
            foreach (DataType d in data)
            {
                if (d.MinValue == null || d.MaxValue == null)
                {
                    MinValue = null;
                    MaxValue = null;
                    break;
                }
                MinValue.MinOf(d.MinValue);
                MaxValue.MaxOf(d.MaxValue);
            }
            _data = data;
        }

        public override VectorRef this[int index]
        {
            get
            {
                int slice = index / _data.Length;
                int restInd = index % _data.Length;
                return _data[slice][restInd];
            }

            set
            {
                int slice = index / _data.Length;
                int restInd = index % _data.Length;
                _data[slice][restInd] = value;
            }
        }

        public override int Length { get { return _data.Length * _data[0].Length; } }

        public override int NumVectorDimensions { get { return _data[0].NumVectorDimensions; } protected set { } }

        public override void ChangeEndian()
        {
            foreach (DataType buff in _data)
                buff.ChangeEndian();
        }

        public override float[] GetChannel(int index)
        {
            float[] raw = new float[Length];
            for (int i = 0; i < _data.Length; ++i)
            {
                float[] slice = _data[i].GetChannel(index);
                Array.Copy(slice, 0, raw, slice.Length * i, slice.Length);
            }

            return raw;
        }

        public override VectorData GetSliceInLastDimension(int index, Index size)
        {
            Debug.Assert(size.T == _data.Length, "Size and array size do not match.");
            return _data[index];
        }

        public override void SetSize(int numElements, int vectorLength)
        {
            _data = new DataType[1];
            _data[0] = new DataType();
            _data[0].SetSize(numElements, vectorLength);
        }
    }

    class VectorDataUnsteady<DataType> : VectorData where DataType : VectorData, new()
    {
        /// <summary>
        /// Data. We only append a 1 if accessed.
        /// </summary>
        private DataType _data;

        public VectorDataUnsteady(DataType data)
        {
            _data = data;
            MinValue = VectorRef.ToUnsteady(data.MinValue);
            MaxValue = VectorRef.ToUnsteady(data.MaxValue);
        }

        public VectorDataUnsteady() { }

        public override VectorRef this[int index]
        {
            get
            {
                return VectorRef.ToUnsteady(_data[index]);
            }

            set
            {
                Debug.Assert(value.Length == _data.Length || value.Length == Length, "Length of vector to set does not agree with data dimension.");
                Vector steady = VectorRef.Subvec(value, _data.Length);
            }
        }

        public override int Length { get { return _data.Length; } }

        public override int NumVectorDimensions { get { return _data.NumVectorDimensions + 1; } protected set { } }

        public override void ChangeEndian()
        {
            _data.ChangeEndian();
        }

        public override float[] GetChannel(int index)
        {
            return _data.GetChannel(index);
        }

        public override VectorData GetSliceInLastDimension(int index, Index size)
        {
            Debug.Assert(size.T == _data.Length, "Size and array size do not match.");
            return new VectorDataUnsteady<DataType>(_data.GetSliceInLastDimension(index, size) as DataType);
        }

        public override void SetSize(int numElements, int vectorLength)
        {
            _data = new DataType();
            _data.SetSize(numElements, vectorLength - 1);
        }
    }
    class VectorList : VectorData
    {
        private Vector[] _data;
        public override int NumVectorDimensions { get { return _data[0].Length; } protected set { } }
        public override int Length { get { return _data.Length; } }
        public override VectorRef this[int index]
        {
            get
            {
                Debug.Assert(index >= 0 && index < Length, "Index out of range. " + index + " not in [0," + Length + ']');
                return _data[index];
            }
            set
            {
                Debug.Assert(value.Length == NumVectorDimensions, "Wrong dimensions.");
                Debug.Assert(index >= 0 && index < Length, "Index out of range. " + index + " not in [0," + Length + ']');
                _data[index] = new Vector(value);
            }
        }

        public override VectorData GetSliceInLastDimension(int index, Index size)
        {
            throw new NotImplementedException();
        }

        public VectorList() { }
        public VectorList(int length) { _data = new Vector[length]; }
        public override void SetSize(int numElements, int vectorLength)
        {
            _data = new Vector[numElements];
            for (int n = 0; n < numElements; ++n)
                _data[n] = new Vector(vectorLength);
        }

        public override void ChangeEndian()
        {
            for (int n = 0; n < Length; ++n)
                Util.FlipEndian(_data[n].Data);
        }

        public override float[] GetChannel(int index)
        {
            Debug.Assert(index >= 0 && index < NumVectorDimensions);
            float[] data = new float[Length];
            for (int e = 0; e < Length; ++e)
                data[e] = _data[e][index];
            return data;
        }
    }

    class VectorBuffer : VectorData
    {
        public float[] Data { get; private set; }
        public override int NumVectorDimensions { get; protected set; }
        public override int Length { get { return Data.Length / NumVectorDimensions; } }

        public override VectorRef this[int index]
        {
            get
            {
                Debug.Assert(index >= 0 && index < Length, "Index out of range. " + index + " not in [0," + Length + ']');
                return new VectorBufferElement(this, index*NumVectorDimensions);
                //Vector ret = new Vector(VectorLength);
                //for (int l = 0; l < VectorLength; ++l)
                //    ret[l] = Data[index * VectorLength + l];
                //return ret;
            }
            set
            {
                Debug.Assert(value.Length == NumVectorDimensions, "Wrong dimensions.");
                Debug.Assert(index >= 0 && index < Length, "Index out of range. " + index + " not in [0," + Length + ']');
                for (int l = 0; l < NumVectorDimensions; ++l)
                    Data[index * NumVectorDimensions + l] = value[l];
            }
        }

        public VectorBuffer() { }
        public VectorBuffer(int numElements, int vectorLength) { SetSize(numElements, vectorLength); }
        public override void SetSize(int numElements, int vectorLength)
        {
            NumVectorDimensions = vectorLength;
            Data = new float[NumVectorDimensions * numElements]; 
        }
        public VectorBuffer(float[] data, int vectorLength)
        {
            Debug.Assert(data.Length % vectorLength == 0);
            Data = data;
            NumVectorDimensions = vectorLength;
        }

        public VectorBuffer(byte[] data, int vectorLength)
        {
            Debug.Assert(data.Length % sizeof(float) == 0 && (data.Length/ sizeof(float)) % vectorLength == 0);
            Data = new float[data.Length / sizeof(float)];
            Buffer.BlockCopy(data, 0, Data, 0, data.Length);
            NumVectorDimensions = vectorLength;
        }

        public VectorBuffer(Vector[] data, int vectorLength = -1)
        {
            NumVectorDimensions = vectorLength > 0 ? vectorLength : data[0].Length;
            Data = new float[data.Length * NumVectorDimensions];
            for (int i = 0; i < data.Length; ++i)
            {
                Debug.Assert(data[i].Length == NumVectorDimensions);
                Buffer.BlockCopy(data[i].Data, 0, Data, i * NumVectorDimensions, NumVectorDimensions * sizeof(float));
            }
        }

        public VectorBuffer(VectorData data)
        {
            NumVectorDimensions = data.NumVectorDimensions;
            Data = new float[data.Length * NumVectorDimensions];
            for (int i = 0; i < data.Length; ++i)
            {
                for (int v = 0; v < NumVectorDimensions; ++v)
                    Data[i * NumVectorDimensions + v] = data[i][v];
            }
        }

        public VectorBuffer(VectorData[] data)
        {
            NumVectorDimensions = data[0].NumVectorDimensions;
            int innerLength = data[0].Length;
            Data = new float[data.Length * innerLength * NumVectorDimensions];
            for (int d = 0; d < data.Length; ++d)
            {
                Debug.Assert(innerLength == data[d].Length);
                for (int i = 0; i < innerLength; ++i)
                    for (int v = 0; v < NumVectorDimensions; ++v)
                        Data[d * data.Length * NumVectorDimensions +
                             i * NumVectorDimensions + 
                             v] = data[d][i][v];

            }
        }

        public override VectorData GetSliceInLastDimension(int posInLastDimension, Index size)
        {
            Debug.Assert(Length == size.Product(), "Size and number of elements in buffer do not match.");
            Debug.Assert(posInLastDimension >= 0 && posInLastDimension < size.T, "Index out of given range.");
            int newNumElements = size.Product() / size.T;
            float[] newData = new float[newNumElements * NumVectorDimensions];

            Array.Copy(Data, newNumElements * posInLastDimension, newData, 0, newNumElements);
            return new VectorBuffer(newData, NumVectorDimensions);
        }

        public override void ChangeEndian()
        {
                Util.FlipEndian(Data);
        }

        public override float[] GetChannel(int index)
        {
            Debug.Assert(index >= 0 && index < NumVectorDimensions);
            float[] data = new float[Length];
            for (int e = 0; e < Length; ++e)
                data[e] = Data[e * NumVectorDimensions + index];
            return data;
        }

        class VectorBufferElement : VectorRef
        {
            private VectorBuffer _parent;
            private int _offset;

            public override int Length {  get { return _parent.NumVectorDimensions; } }

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

    /// <summary>
    /// Vector data where each vector dimension is stored in its own data block.
    /// </summary>
    class VectorChannels : VectorData
    {
        private float[][] _data;
        /// <summary>
        /// Number of slices.
        /// </summary>
        public override int NumVectorDimensions { get { return _data.Length; } protected set { } }
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
                Debug.Assert(value.Length == NumVectorDimensions, "Wrong dimensions.");
                Debug.Assert(index >= 0 && index < Length, "Index out of range. " + index + " not in [0," + Length + ']');
                for (int dim = 0; dim < NumVectorDimensions; ++dim)
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

            float[][] slices = new float[NumVectorDimensions][];
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

        class VectorChannelElement : VectorRef
        {
            private VectorChannels _parent;
            private int _index;

            public override int Length { get { return _parent.NumVectorDimensions; } }

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
