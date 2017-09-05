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
        /// The length of each vector returned.
        /// </summary>
        public abstract int VectorLength { get; protected set; }
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

        public abstract float[] GetData();

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
        public int ArrayLength { get { return _data?.Length ?? 0; } }

        public VectorDataArray(DataType[] data)
        {
#if DEBUG
            int vecLength = data[0].VectorLength;
            int length = data[0].Length;
            foreach (DataType buff in data)
                Debug.Assert(buff.Length == length && buff.VectorLength == vecLength, "Different dimensions in given data blocks.");
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

        public VectorDataArray() { }

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

        public override int VectorLength { get { return _data[0].VectorLength; } protected set { } }

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

        public override float[] GetData()
        {
            throw new NotImplementedException();
        }
    }

    class VectorDataUnsteady<DataType> : VectorData where DataType : VectorData, new()
    {
        /// <summary>
        /// Data. We only append a 1 if accessed.
        /// </summary>
        private VectorDataArray<DataType> _data;
        public float TimeScale = 1.0f;
        public int ArrayLength { get { return _data.ArrayLength; } }

        public VectorDataUnsteady(VectorDataArray<DataType> data)
        {
            _data = data;
            MinValue = VectorRef.ToUnsteady(data.MinValue);
            MaxValue = VectorRef.ToUnsteady(data.MaxValue);
        }

        public VectorDataUnsteady(DataType[] data)
        {
            _data = new VectorDataArray<DataType>(data);
            MinValue = VectorRef.ToUnsteady(_data.MinValue);
            MaxValue = VectorRef.ToUnsteady(_data.MaxValue);
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

        public override int VectorLength { get { return _data.VectorLength + 1; } protected set { } }

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
            return /*new VectorDataUnsteady<DataType>*/(_data.GetSliceInLastDimension(index, size) as DataType);
        }

        public override void SetSize(int numElements, int vectorLength)
        {
            _data = new VectorDataArray<DataType>();
            _data.SetSize(numElements, vectorLength - 1);
        }

        public override float[] GetData()
        {
            throw new NotImplementedException();
        }
    }
}
