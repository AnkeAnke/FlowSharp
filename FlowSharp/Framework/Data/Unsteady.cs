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
        private ScalarFieldUnsteady[] _scalarsUnsteady;
        public override Field[] Scalars { get { return _scalarsUnsteady; } }
        public ScalarFieldUnsteady[] ScalarsAsSFU { get { return _scalarsUnsteady; } }

        /// <summary>
        /// Number of dimensions per vector. Including one time dimension.
        /// </summary>
        public override int NumVectorDimensions { get { return Scalars.Length + 1; } }

        public VectorFieldUnsteady(ScalarFieldUnsteady[] fields) : base()
        {
            _scalarsUnsteady = fields;
        }

        public virtual void ScaleToGrid(Vector scale)
        {
            Debug.Assert(Scalars.Length == scale.Length);
            for (int dim = 0; dim < Scalars.Length; ++dim)
            {
                ScalarFieldUnsteady field = _scalarsUnsteady[dim];
                field.ScaleToGrid(scale[dim]);
            }
            SpreadInvalidValue();
        }

        public override bool IsValid(Vector pos)
        {
            return ScalarsAsSFU[0].TimeSlices[0].Grid.InGrid(pos) && Scalars[0].IsValid(pos);
        }

        /// <summary>
        /// Access field by scalar index.
        /// </summary>
        public override Vector Sample(int index)
        {
            Debug.Assert(index >= 0 && index < Size.Product(), "Index out of bounds: " + index + " not within [0, " + Size.Product() + ").");
            Vector vec = new Vector(NumVectorDimensions);
            for (int dim = 0; dim < _scalarsUnsteady.Length; ++dim)
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

        public override Vector Sample(Vector position)
        {
            Vector result = Grid.Sample(this, position);
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
                slices[scalar] = _scalarsUnsteady[scalar].GetTimeSlice(slice);
            }

            return new VectorField(slices);
        }

        public override VectorField GetSlice(int slice) { return (VectorField)GetTimeSlice(slice); }

        public VectorFieldUnsteady(VectorFieldUnsteady field, VFJFunction function, int outputDim)
        {
            int scalars = outputDim;
            FieldGrid gridCopy = field._scalarsUnsteady[0].TimeSlices[0].Grid.Copy();
            _scalarsUnsteady = new ScalarFieldUnsteady[outputDim];

            // Reserve the space.
            for (int comp = 0; comp < outputDim; ++comp)
            {
                ScalarField[] fields = new ScalarField[field.Size.T]; //(field.Grid);

                for (int t = 0; t < field.Size.T; ++t)
                    fields[t] = new ScalarField(gridCopy);

                _scalarsUnsteady[comp] = new ScalarFieldUnsteady(fields);
                _scalarsUnsteady[comp].InvalidValue = field.InvalidValue;
            }
            this.InvalidValue = field.InvalidValue;

            // Since the time component is in the grid size as well, we do not need to account for time specially.
            GridIndex indexIterator = new GridIndex(field.Size);
            foreach (GridIndex index in indexIterator)
            {
                Vector v = field.Sample((int)index);

                if (v[0] == InvalidValue)
                {
                    for (int dim = 0; dim < Scalars.Length; ++dim)
                        _scalarsUnsteady[dim][(int)index] = (float)InvalidValue;
                    continue;
                }

                SquareMatrix J = field.SampleDerivative(index);
                Vector funcValue = function(v, J);

                for (int dim = 0; dim < Scalars.Length; ++dim)
                {
                    Scalars[dim][(int)index] = funcValue[dim];
                }
            }
        }

        public override VectorField GetSlicePlanarVelocity(int timeSlice)
        {
            ScalarField[] slices = new ScalarField[Size.Length - 1];

            // Copy the grid - one dimension smaller!
            RectlinearGrid grid = Grid as RectlinearGrid;
            Index newSize = new Index(Size.Length - 1);
            Array.Copy(Size.Data, newSize.Data, newSize.Length);

            FieldGrid sliceGrid = new RectlinearGrid(newSize);
            for (int i = 0; i < Size.Length - 1; ++i)
            {
                slices[i] = this._scalarsUnsteady[i].GetTimeSlice(timeSlice);
                
                slices[i].TimeSlice = timeSlice;
            }
            return new VectorField(slices);
        }
    }

    class VectorFieldUnsteadyAnalytical : VectorFieldUnsteady
    {
        //delegate Vector Evaluate(Vector inVec);
        public delegate Vector Evaluate(Vector inVec, SquareMatrix inJ);

        protected Evaluate _evaluate;
        //protected int _numVectorDimensions = -1;
        protected FieldGrid _outGrid;

        public VectorFieldUnsteadyAnalytical(Evaluate func, VectorFieldUnsteady field, FieldGrid outGrid, bool useJacobian = false) : base(field.ScalarsAsSFU)
        {
            _evaluate = func;
           // _numVectorDimensions = outDimensions;
        }

        public override int NumVectorDimensions
        {
            get
            {
                return _outGrid.Size.Length;
            }
        }

        public override void ScaleToGrid(Vector scale)
        {
            base.ScaleToGrid(scale);
        }

        public override VectorField GetSlice(int slice)
        {
            return base.GetSlice(slice);
        }

        public override Field[] Scalars
        {
            get
            {
                return base.Scalars;
            }
        }

    }

    //class ScalarFieldUnsteadyAnalytical : ScalarFieldUnsteady
    //{
    //    public ScalarFieldUnsteadyAnalytical() : base(null)
    //    {
    //        throw new NotImplementedException();
    //    }
    //}

    //class ScalarFieldUnsteadyAnalytical : ScalarFieldUnsteady
    //{
    //    public ScalarFieldUnsteadyAnalytical() : base(null)
    //    {
    //        throw new NotImplementedException();
    //    }
    //}


    class ScalarFieldUnsteady : Field
    {
        public override int NumDimensions { get { return Size.Length - 1; } }
        public override FieldGrid Grid { get { return _sliceGrid; } set { _sliceGrid = value; } }
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
        private FieldGrid _sliceGrid;
        protected bool _operationsAllowed = false;

        public override bool IsValid(Vector pos)
        {
            float[] weights;
            int[] neighbors = _slices[0].Grid.FindAdjacentIndices(pos, out weights);
            foreach (int neighbor in neighbors)
                if (this[neighbor] == InvalidValue)
                    return false;
            return true;
        }

        public override float this[int index]
        {
            get
            {
                Debug.Assert(_operationsAllowed, "The field data is not scaled to its grid yet.");
                int inSliceIndex = index % (_sliceGrid.Size.Product() / _sliceGrid.Size.T);
                int sliceIndex = index / (_sliceGrid.Size.Product() / _sliceGrid.Size.T);
                return _slices[sliceIndex][inSliceIndex];
            }
            set
            {
                int inSliceIndex = index % (_sliceGrid.Size.Product() / _sliceGrid.Size.T);
                int sliceIndex = index / (_sliceGrid.Size.Product() / _sliceGrid.Size.T);
                _slices[sliceIndex][inSliceIndex] = value;
            }
        }

        public int NumTimeSlices { get { return _slices.Length; } }

        public ScalarFieldUnsteady(ScalarField[] fields, float timeStart = 0, float timeStep = 1.0f) : base()
        {
            _slices = fields;
            _sliceGrid = fields[0].Grid.GetAsTimeGrid(fields.Length, timeStart, timeStep);
            for (int slice = 0; slice < _slices.Length; ++slice)
            {
                _slices[slice].TimeSlice = timeStart + timeStep * slice;
            }
        }

        public void ScaleToGrid(float dimwiseScale)
        {
            //TODO!!
            for (int time = 0; time < _slices.Length; ++time)
                for (int index = 0; index < Size.Product() / Size.T; ++index)
                    if(_slices[time][index] != InvalidValue)
                        _slices[time][index] *= dimwiseScale;
            _operationsAllowed = true;
        }

        public override float Sample(Index gridPosition)
        {
            Debug.Assert(_operationsAllowed, "The field data is not scaled to its grid yet.");
            int slice = gridPosition[gridPosition.Length - 1];
            Debug.Assert(slice >= 0 && slice < _slices.Length);

            Index slicePos = new Index(gridPosition.Length - 1);
            Array.Copy(gridPosition.Data, slicePos.Data, slicePos.Length);

            return _slices[slice].Sample(slicePos);
        }

        public override float Sample(Vector position)
        {
            Debug.Assert(_operationsAllowed, "The field data is not scaled to its grid yet.");
            float time = position[position.Length - 1];
            Vector samplePos = position;

            Debug.Assert(time >= 0 && time < _slices.Length);

            Vector slicePos = new Vector(samplePos.Length - 1);
            Array.Copy(samplePos.Data, slicePos.Data, slicePos.Length);

            float valueT = _slices[(int)time].Sample(slicePos);
            float valueTNext = _slices[Math.Min((int)time + 1, NumTimeSlices-1)].Sample(slicePos);
            float t = time - (int)time;
            return (1 - t) * valueT + t * valueTNext;
        }

        public override Vector SampleDerivative(Vector position)
        {
            Debug.Assert(_operationsAllowed, "The field data is not scaled to its grid yet.");
            float time = position.T;

            // Get spacial sample position in grid space.
            Vector samplePos = position;

            Debug.Assert(time >= 0 && time < _slices.Length);

            Vector slicePos = new Vector(samplePos.Length - 1);
            Array.Copy(samplePos.Data, slicePos.Data, slicePos.Length);

            // Sample data in current and next time slice.
            Vector valueT = _slices[(int)time].SampleDerivative(slicePos);
            Vector valueTNext = _slices[Math.Min((int)time + 1, NumTimeSlices - 1)].SampleDerivative(slicePos);
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
            Debug.Assert(_operationsAllowed, "The field data is not scaled to its grid yet.");
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

        public override void ChangeEndian()
        {
            foreach (ScalarField field in this.TimeSlices)
                field.ChangeEndian();
        }
    }
}
