using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace FlowSharp
{
    class VectorList : VectorData
    {
        private List<Vector> _data;

        public override int VectorLength { get { return _data[0].Length; } protected set { } }
        public override int Length { get { return _data.Count; } }
        public override VectorRef this[int index]
        {
            get
            {
                Debug.Assert(index >= 0 && index < Length, "Index out of range. " + index + " not in [0," + Length + ']');
                return _data[index];
            }
            set
            {
                Debug.Assert(value.Length == VectorLength, "Wrong dimensions.");
                Debug.Assert(index >= 0 && index < Length, "Index out of range. " + index + " not in [0," + Length + ']');
                _data[index] = new Vector(value);
            }
        }

        public override VectorData GetSliceInLastDimension(int index, Index size)
        {
            throw new NotImplementedException();
        }

        public VectorList() { }
        public VectorList(int length) { _data = new List<Vector>(length); }

        public VectorList(List<Vector> points)
        {
            _data = points;
        }

        public override void SetSize(int numElements, int vectorLength)
        {
            _data = new List<Vector>(numElements);
            for (int n = 0; n < numElements; ++n)
                _data.Add(new Vector(vectorLength));
        }

        public override void ChangeEndian()
        {
            for (int n = 0; n < Length; ++n)
                Util.FlipEndian(_data[n].Data);
        }

        public override float[] GetChannel(int index)
        {
            Debug.Assert(index >= 0 && index < VectorLength);
            float[] data = new float[Length];
            for (int e = 0; e < Length; ++e)
                data[e] = _data[e][index];
            return data;
        }

        public override float[] GetData()
        {
            float[] data = new float[Length * VectorLength];
            for (int l = 0; l < Length; ++l)
                Array.Copy(_data[l].Data, 0, data, l * VectorLength, VectorLength);

            return data;
        }
    }
}
