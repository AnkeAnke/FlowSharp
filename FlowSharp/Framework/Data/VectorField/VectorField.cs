using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using SlimDX;

namespace FlowSharp
{
    /// <summary>
    /// Class for acessing vector fields. Memory management will differ.
    /// </summary>
    partial class VectorField ///<DataType> where DataType : VectorData, new()
    {
        #region VF
        public VectorData Data { get; set; }
        public FieldGrid Grid { get; protected set; }
        public Index Size { get { return Grid.Size; } }

        #region Delegates
        /// <summary>
        /// Function to compute a new field based on an old one, point wise.
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        public delegate Vector VFFunction(VectorRef v);

        public delegate Vector VFJFunction(VectorRef v, SquareMatrix J);

        public delegate Vector3 PositionToColor(VectorField field, Vector4 position);
        #endregion Delegates

        #region Access
        /// <summary>
        /// Number of dimensions per vector.
        /// </summary>
        public virtual int NumVectorDimensions { get { return Data.VectorLength; } }
        public int NumDimensions { get { return Size.Length; } }

        public virtual float? InvalidValue { get; set; }
        public virtual float? TimeSlice { get; set; }

        /// <summary>
        /// Access field by scalar index.
        /// </summary>
        public virtual VectorRef this[int index] { get { return Data[index]; } protected set { Data[index] = value; } }
        /// <summary>
        /// Access field by N-dimensional index.
        /// </summary>
        public VectorRef this[Index gridPosition]
        {
            get
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

                return this[index];
            }
            protected set
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

                this[index] = value;
            }
        }
        public bool IsValid(Index pos)
        {
            return Sample(pos)[0] != InvalidValue;
        }

        public bool IsValid(int pos)
        {
            return Sample(pos)[0] != InvalidValue;
        }

        public VectorField(VectorData data, FieldGrid grid)
        {
            Data = data;
            Grid = grid;
            InvalidValue = null;
            //         SpreadInvalidValue();
        }

        protected VectorField() { }

        public VectorField(ScalarField[] scalars)
        {
            VectorBuffer[] raw = new VectorBuffer[scalars.Length];
            for (int d = 0; d < scalars.Length; ++d)
                raw[d] = scalars[d].BufferData;
            Data = new VectorDataArray<VectorBuffer>(raw);
        }
        ///// <summary>
        ///// Access field by scalar index.
        ///// </summary>
        //public override Vector Sample(int index)
        //{
        //    return _data[index];
        //}
        //public VectorField(VectorField field, VFJFunction function, int outputDim, bool needJacobian = true)
        //{
        //    return ConstructVectorField<VectorBuffer>(field, function, outputDim, needJacobian);
        //}

        public VectorField(VectorField field, VFJFunction function, int outputDim, bool needJacobian = true)
        {
            int scalars = outputDim;/*function(field.Sample(0), field.SampleDerivative(new Vector(0, field.Size.Length))).Length;*/
            Data = new VectorBuffer();
            Data.SetSize(field.NumVectorDimensions, field.Size.Product());
            FieldGrid gridCopy = field.Grid.Copy();

            // In case the input field was time dependant, this one is not. Still, we keep the size and origin of the time as new dimension!
            gridCopy.TimeDependant = false;

            InvalidValue = field.InvalidValue;

            GridIndex indexIterator = new GridIndex(field.Size);
            foreach (GridIndex index in indexIterator)
            {
                VectorRef v = field[(int)index];

                if (v[0] == InvalidValue)
                {
                    for (int dim = 0; dim < NumVectorDimensions; ++dim)
                        Data[(int)index][dim] = (float)InvalidValue;
                    continue;
                }

                SquareMatrix J = needJacobian ? field.SampleDerivative(index) : null;
                Vector funcValue = function(v, J);

                Data[(int)index] = funcValue;
            }
        }

        public ScalarField GetChannel(int slice)
        {
            return new ScalarField(new VectorBuffer(Data.GetChannel(slice), 1), Grid.Copy());
        }
        //public static VectorField ConstructVectorField(VectorField field, VFJFunction function, int outputDim, bool needJacobian = true)
        //{
        //    return ConstructVectorField<VectorBuffer>(field, function, outputDim, needJacobian);
        //}

        //public VectorField ConstructVectorField<DataType>(VectorField field, VFJFunction function, int outputDim, bool needJacobian = true) where DataType : VectorData, new()
        //{
        //    int scalars = outputDim;/*function(field.Sample(0), field.SampleDerivative(new Vector(0, field.Size.Length))).Length;*/
        //    VectorField newField = new VectorField();
        //    newField._data = new DataType();
        //    newField._data.SetSize(field.NumVectorDimensions, field.Size.Product());
        //    FieldGrid gridCopy = field.Grid.Copy();

        //    // In case the input field was time dependant, this one is not. Still, we keep the size and origin of the time as new dimension!
        //    gridCopy.TimeDependant = false;

        //    newField.InvalidValue = field.InvalidValue;

        //    GridIndex indexIterator = new GridIndex(field.Size);
        //    foreach (GridIndex index in indexIterator)
        //    {
        //        VectorRef v = field[(int)index];

        //        if (v[0] == newField.InvalidValue)
        //        {
        //            for (int dim = 0; dim < newField.NumVectorDimensions; ++dim)
        //                newField._data[(int)index][dim] = (float)newField.InvalidValue;
        //            continue;
        //        }

        //        SquareMatrix J = needJacobian ? field.SampleDerivative(index) : null;
        //        Vector funcValue = function(v, J);

        //        newField._data[(int)index] = funcValue;
        //        //for (int dim = 0; dim < NumVectorDimensions; ++dim)
        //        //{
        //        //    var vec = Scalars[dim];
        //        //    _data[(int)index][dim] = funcValue[dim];
        //        //}
        //    }
        //    return newField;
        //}

        public bool IsValid(Vector pos, out Vector sample)
        {
            sample = Sample(pos);
            return sample != null && sample[0] != InvalidValue;
            //VectorRef weights;
            //Index neighbors = Grid.FindAdjacentIndices(pos, out weights);
            //return neighbors != null;
            //foreach (int neighbor in neighbors)
            //    for (int n = 0; n < NumVectorDimensions; ++n)
            //        if (Data[neighbor][n] == InvalidValue)
            //            return false;
            //return true;
        }
        /// <summary>
        /// Access field by scalar index.
        /// </summary>
        public VectorRef Sample(int index) { return this[index]; }

        /// <summary>
        /// Access field by N-dimensional index.
        /// </summary>
        public virtual VectorRef Sample(Index gridPosition)
        {
            return this[gridPosition];
        }

        public virtual Vector Sample(Vector position, Vector lastDirection)
        {
            return Sample(position);
        }

        public Vector Sample(Vector position)
        {
            return Grid.Sample(this, position);
        }

        /// <summary>
        /// Get the derivative at a data point. Not checking for InvalidValue.
        /// </summary>
        /// <param name="pos"></param>
        /// <returns></returns>
        public SquareMatrix SampleDerivative(Index pos)
        {
            //Debug.Assert(NumVectorDimensions == Size.Length);
            int size = Math.Max(Size.Length, NumVectorDimensions);
            SquareMatrix jacobian = new SquareMatrix(size);

            // For all dimensions, so please reset each time.
            Index samplePos = new Index(pos);

            for (int dim = 0; dim < Size.Length; ++dim)
            {
                // Just to be sure, check thst no value was overwritten.
                int posCpy = samplePos[dim];

                // See whether a step to the right/left is possible.
                samplePos[dim]++;
                bool rightValid = (samplePos[dim] < Size[dim]) && IsValid(samplePos); //Scalars[0].Sample(samplePos) != InvalidValue;
                samplePos[dim] -= 2;
                bool leftValid = (samplePos[dim] >= 0) && IsValid(samplePos); //Scalars[0].Sample(samplePos) != InvalidValue;
                samplePos[dim]++;

                if (rightValid)
                {
                    if (leftValid)
                    {
                        // Regular case. Interpolate.
                        samplePos[dim]++;
                        jacobian[dim] = this[samplePos].ToVec(size);
                        samplePos[dim] -= 2;
                        jacobian[dim] -= this[samplePos].ToVec(size);
                        jacobian[dim] *= 0.5f;
                        samplePos[dim]++;
                    }
                    else
                    {
                        // Left border.
                        samplePos[dim]++;
                        jacobian[dim] = this[samplePos].ToVec(size);
                        samplePos[dim]--;
                        jacobian[dim] -= this[samplePos].ToVec(size);
                    }
                }
                else
                {
                    if (leftValid)
                    {
                        // Right border.
                        jacobian[dim] = this[samplePos].ToVec(size);
                        samplePos[dim]--;
                        jacobian[dim] -= this[samplePos].ToVec(size);
                        samplePos[dim]++;
                    }
                    else
                    {
                        // Weird case. 
                        jacobian[dim] = new Vector(0, size);
                    }
                }
                Debug.Assert(posCpy == samplePos[dim]);
            }

            return jacobian;
        }

        public SquareMatrix SampleDerivative(Vector position)
        {
            //Debug.Assert(NumVectorDimensions == Size.Length);
            SquareMatrix jacobian = new SquareMatrix(NumVectorDimensions);

            for (int dim = 0; dim < Size.Length; ++dim)
            {
                float stepPos = Math.Min(Size[dim] - 1, position[dim] + 0.5f) - position[dim];
                float stepMin = Math.Max(0, position[dim] - 0.5f) - position[dim];
                Vector samplePos = new Vector(position);
                samplePos[dim] += stepPos;
                jacobian[dim] = Sample(samplePos);

                samplePos[dim] += stepMin - stepPos;
                jacobian[dim] -= Sample(samplePos);

                jacobian[dim] /= (stepPos - stepMin);
            }

            return jacobian;
        }

        #endregion Access
        /// <summary>
        /// Make sure the invalid value is in the first scalar field if it is anywhere.
        /// </summary>
        protected void SpreadInvalidValue()
        {
            for (int pos = 0; pos < Size.Product(); ++pos)
            {
                for (int dim = 1; dim < NumVectorDimensions; ++dim)
                    if (this[pos][dim] == InvalidValue)
                        this[pos][0] = (float)InvalidValue;
            }
        }

        public VectorField GetSlice(int posInLastDimension)
        {
            // Copy the grid - one dimension smaller!
            RectlinearGrid grid = Grid as RectlinearGrid;
            Index newSize = new Index(Size.Length - 1);
            Array.Copy(Size.Data, newSize.Data, newSize.Length);

            FieldGrid sliceGrid = new RectlinearGrid(newSize);

            VectorData newData = Data.GetSliceInLastDimension(posInLastDimension, Size);
            return new VectorField(newData, sliceGrid);
        }

        public VectorField GetSlicePlanarVelocity(int posInLastDimension)
        {
            // Copy the grid - one dimension smaller!
            int lastSize = Size.T;
            RectlinearGrid grid = Grid as RectlinearGrid;
            Index newSize = new Index(Size.Length - 1);

            FieldGrid sliceGrid = new RectlinearGrid(newSize);

            float[] raw = new float[NumVectorDimensions * newSize.Product()];
            foreach (Index idx in new GridIndex(newSize))
            {
                VectorRef vec = Data[(int)idx + posInLastDimension * newSize.Length];
                for (int v = 0; v < newSize.Length; ++v)
                    raw[(int)idx * newSize.Length + v] = vec[v];
            }

            return new VectorField(new VectorBuffer(raw, newSize.Length), sliceGrid);
        }

        public PointSet<Point> ColorCodeArbitrary(LineSet lines, PositionToColor func)
        {
            Point[] points;
            points = new Point[lines.NumExistentPoints];
            int idx = 0;
            foreach (Line line in lines.Lines)
            {
                foreach (Vector4 pos in line.Positions)
                {
                    points[idx] = new Point() { Position = pos, Color = func(this, pos), Radius = lines.Thickness };
                    ++idx;
                }
            }

            return new PointSet<Point>(points);
        }

        public DataStream GetDataStream() { throw new NotImplementedException(); }
        public virtual bool IsUnsteady() { return this.TimeSlice != null; }

        public void ChangeEndian() { Data.ChangeEndian(); }

        public virtual void ScaleToGrid(float dimwiseScale)
        {
            for (int n = 0; n < Data.Length; ++n)
                if (IsValid(n))
                    for (int dim = 0; dim < Data.VectorLength; ++dim)
                        Data[n][dim] *= dimwiseScale;
        }

        public virtual void ScaleToGrid(Vector scale)
        {
            Debug.Assert(Data.VectorLength == scale.Length);
            for (int dim = 0; dim < Data.VectorLength; ++dim)
            {
                for (int n = 0; n < Data.Length; ++n)
                    Data[n][dim] *= scale[dim];
            }
        }

        #endregion VF
    }
}