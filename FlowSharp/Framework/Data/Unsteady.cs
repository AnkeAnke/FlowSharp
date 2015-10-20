using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using SlimDX;

namespace FlowSharp
{
    class VectorFieldUnsteady : VectorField
    {
        private ScalarFieldUnsteady[] _scalars;
        public override Field[] Scalars { get { return _scalars; } }
        /// <summary>
        /// Number of dimensions per vector. Including one time dimension.
        /// </summary>
        public override int NumVectorDimensions { get { return Scalars.Length + 1; } }

        public VectorFieldUnsteady(ScalarFieldUnsteady[] fields) : base()
        {
            _scalars = fields;
        }

        /// <summary>
        /// Access field by scalar index.
        /// </summary>
        public override Vector Sample(int index)
        {
            Debug.Assert(index >= 0 && index < Size.Product(), "Index out of bounds: " + index + " not within [0, " + Size.Product() + ").");
            Vector vec = new Vector(NumVectorDimensions);
            for (int dim = 0; dim < _scalars.Length; ++dim)
                vec[dim] = Scalars[dim][index];

            // Unsteady!
            vec[NumVectorDimensions - 1] = 1;

            return vec;
        }

        /// <summary>
        /// Access field by N-dimensional index.
        /// </summary>
        public override Vector Sample(Index gridPosition)
        {
            Debug.Assert(gridPosition < Size && gridPosition.IsPositive());

            int offsetScale = 1;
            int index = 0;

            // Have last dimension running fastest.
            for (int dim = 0; dim < NumVectorDimensions; ++dim)
            {
                index += offsetScale * gridPosition[dim];
                offsetScale *= Size[dim];
            }

            return Sample(index);
        }

        public override Vector Sample(Vector position, bool worldPosition = true)
        {
            Vector result = Grid.Sample(this, position, worldPosition);
            // Work against the small numerical deviations here.
            Debug.Assert(Math.Abs(result.T - 1) < 0.1f);
            result[NumVectorDimensions - 1] = 1;
            return result;
        }

        public VectorField GetTimeSlice(int slice)
        {
            ScalarField[] slices = new ScalarField[Scalars.Length];
            for (int scalar = 0; scalar < Scalars.Length; ++scalar)
            {
                slices[scalar] = _scalars[scalar].GetTimeSlice(slice);
            }

            return new VectorField(slices);
        }

        public override VectorField GetSlice(int slice) { return (VectorField)GetTimeSlice(slice); }
    }
    class ScalarFieldUnsteady : Field
    {
        public override int NumDimensions { get { return Size.Length - 1; } }
        public override float? InvalidValue
        {
            get
            {
                return _slices[0].InvalidValue;
            }

            set
            {
                _slices[0].InvalidValue = value;
            }
        }

        private ScalarField[] _slices;
        public ScalarField[] TimeSlices { get { return _slices; } }
        public ScalarField GetTimeSlice(int slice) { return _slices[slice]; }
        private FieldGrid _sliceGrid { get { return _slices[0].Grid; } }
        public override float? TimeSlice
        {
            get
            {
                return Grid.TimeOrigin;
            }

            set
            {
                Grid.TimeOrigin = value;
            }
        }
        public override float this[int index]
        {
            get
            {
                int inSliceIndex = index % _sliceGrid.Size.Product();
                int sliceIndex = index / _sliceGrid.Size.Product();
                return _slices[sliceIndex][inSliceIndex];
            }
            set
            {
                int inSliceIndex = index % _sliceGrid.Size.Product();
                int sliceIndex = index / _sliceGrid.Size.Product();
                _slices[sliceIndex][inSliceIndex] = value;
            }
        }

        public int NumTimeSlices { get { return _slices.Length; } }

        public ScalarFieldUnsteady(ScalarField[] fields, float timeStart = 0, float timeStep = 1.0f) : base()
        {
            _slices = fields;
            Grid = _sliceGrid.GetAsTimeGrid(fields.Length, timeStart, timeStep);
            for (int slice = 0; slice < _slices.Length; ++slice)
            {
                _slices[slice].TimeSlice = timeStart + timeStep * slice;
            }
        }

        public override float Sample(Index gridPosition)
        {
            int slice = gridPosition[gridPosition.Length - 1];
            Debug.Assert(slice >= 0 && slice < _slices.Length);

            Index slicePos = new Index(gridPosition.Length - 1);
            Array.Copy(gridPosition.Data, slicePos.Data, slicePos.Length);

            return _slices[slice].Sample(slicePos);
        }

        public override float Sample(Vector position, bool worldSpace = true)
        {
            float time = position[position.Length - 1];
            Vector samplePos = position;
            if (worldSpace)
            {
                samplePos = Grid.ToGridPosition(position);
            }
            Debug.Assert(time >= 0 && time < _slices.Length);

            Vector slicePos = new Vector(samplePos.Length - 1);
            Array.Copy(samplePos.Data, slicePos.Data, slicePos.Length);

            float valueT = _slices[(int)time].Sample(slicePos, false);
            float valueTNext = _slices[Math.Min((int)time + 1, NumTimeSlices-1)].Sample(slicePos, false);
            float t = time - (int)time;
            return (1 - t) * valueT + t * valueTNext;
        }

        public override Vector SampleDerivative(Vector position, bool worldSpace = true)
        {
            float time = position.T;

            // Get spacial sample position in grid space.
            Vector samplePos = position;
            if (worldSpace)
            {
                samplePos = Grid.ToGridPosition(position);
            }
            Debug.Assert(time >= 0 && time < _slices.Length);

            Vector slicePos = new Vector(samplePos.Length - 1);
            Array.Copy(samplePos.Data, slicePos.Data, slicePos.Length);

            // Sample data in current and next time slice.
            Vector valueT = _slices[(int)time].SampleDerivative(slicePos, false);
            Vector valueTNext = _slices[Math.Min((int)time + 1, NumTimeSlices - 1)].SampleDerivative(slicePos, false);
            float t = time - (int)time;
            Vector spaceGrad = (1 - t) * valueT + t * valueTNext;

            // Add derivative in last dimension - always 0.
            Vector gradient = new Vector(spaceGrad.Length + 1);
            Array.Copy(spaceGrad.Data, gradient.Data, spaceGrad.Length);
            gradient[spaceGrad.Length] = 1;

            return gradient;
        }

        public override DataStream GetDataStream()
        {
            DataStream stream = new DataStream(Size.Product(), true, true);
            for (int slice = 0; slice < _slices.Length; ++slice)
            {
                stream.WriteRange<float>(_slices[slice].Data);
            }
            return stream;
        }

        public override bool IsUnsteady()
        {
            return true;
        }
    }
}
