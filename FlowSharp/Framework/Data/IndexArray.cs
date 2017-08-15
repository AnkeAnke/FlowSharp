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
    abstract class IndexData : IEnumerable<Index>
    {
        public abstract int IndexLength { get; protected set; }
        public abstract int Length { get; }
        public abstract Index this[int index]
        { get; set; }
        #region Enumerator
        public IEnumerator<Index> GetEnumerator()
        {
            return new IndexDataEnumerable(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new IndexDataEnumerable(this);
        }
        #endregion Enumerator
    }

    class IndexDataEnumerable : IEnumerator<Index>
    {
        private IndexData _parent;
        private int _index;

        public Index Current { get { return _parent[_index]; } }

        object IEnumerator.Current {  get { return this; } }

        public IndexDataEnumerable(IndexData parent)
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
    class BaseIndexArray : IndexData
    {
        private Index[] _data;
        public override int IndexLength { get { return _data[0].Length; } protected set { } }
        public override int Length { get { return _data.Length; } }
        public override Index this[int index]
        {
            get
            {
                Debug.Assert(index >= 0 && index < Length, "Index out of range. " + index + " not in [0," + Length + ']');
                Index ret = new Index(IndexLength);
                return _data[index];
            }
            set
            {
                Debug.Assert(value.Length == IndexLength, "Wrong dimensions.");
                _data[index] = value;
            }
        }

        public BaseIndexArray(Index[] data) { _data = data; }
        //implicit operator Index[]() { }
    }
    class IndexArray : IndexData
    {
        public int[] Data { get; protected set; }
        public override int IndexLength { get; protected set; }
        public override int Length { get { return Data.Length / IndexLength; } }

        public override Index this[int index]
        {
            get
            {
                Debug.Assert(index >= 0 && index < Length, "Index out of range. " + index + " not in [0," + Length + ']');
                Index ret = new Index(IndexLength);
                for (int l = 0; l < IndexLength; ++l)
                    ret[l] = Data[index * IndexLength + l];
                return ret;
            }
            set
            {
                Debug.Assert(value.Length == IndexLength, "Wrong dimensions.");
                for (int l = 0; l < IndexLength; ++l)
                    Data[index * IndexLength + l] = value[l];
            }
        }

        public IndexArray(int[] data, int indexLength)
        {
            Debug.Assert(data.Length % indexLength == 0);
            Data = data;
            IndexLength = indexLength;
        }

        public IndexArray(int numElements, int indexLength)
        {
            Data = new int[numElements * indexLength];
            IndexLength = indexLength;
        }

        public IndexArray(byte[] data, int indexLength)
        {
            Debug.Assert(data.Length % sizeof(int) == 0 && (data.Length/ sizeof(int)) % indexLength == 0);
            Data = new int[data.Length / sizeof(int)];
            Buffer.BlockCopy(data, 0, Data, 0, data.Length);
            IndexLength = indexLength;
        }

        public IndexArray(Index[] data, int indexLength = -1)
        {
            IndexLength = indexLength > 0 ? indexLength : data[0].Length;
            Data = new int[data.Length * IndexLength];
            for (int i = 0; i < data.Length; ++i)
            {
                Debug.Assert(data[i].Length == IndexLength);
                Buffer.BlockCopy(data[i].Data, 0, Data, i * IndexLength * sizeof(int), IndexLength * sizeof(int));
            }
        }

        public void SetBlock(int intOffset, int[] data)
        {
            Debug.Assert(intOffset + data.Length <= Length * IndexLength);
            Buffer.BlockCopy(data, 0, Data, intOffset * sizeof(int), data.Length * sizeof(int));
        }
    }
}
