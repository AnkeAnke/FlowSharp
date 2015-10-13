using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace FlowSharp
{
    class SquareMatrix
    {
        protected Vector[] Columns;
        public int Length { get { return Columns.Length; } }
        public SquareMatrix(Vector[] columns)
        {
#if DEBUG
            foreach(Vector col in columns)
                Debug.Assert(columns.Length == col.Length); // Only sqare matrices allowed.
#endif
            Columns = columns;
        }

        public SquareMatrix(int length)
        {
            Columns = new Vector[length];
            for (int row = 0; row < length; ++row)
                Columns[row] = new Vector(0, length);
        }

        public Vector this[int index]
        {
            get { return Columns[index]; }
            set { Debug.Assert(value.Length == Length); Columns[index] = value; }
        }

        public Vector Column(int index)
        {
            return this[index];
        }

        public Vector Row(int index)
        {
            Vector row = new Vector(Length);
            for (int x = 0; x < Length; ++x)
                row[x] = this[x][index];
            return row;
        }

        public float Determinant()
        {
            SquareMatrix tmp;
            return Lagrange(out tmp);
        }

        private float Lagrange(out SquareMatrix clipped)
        {
            if(Length == 2)
            {
                clipped = null;
                return this[0][0] * this[1][1] - this[0][1] * this[1][0];
            }
            else
            {
                throw new NotImplementedException();
            }
        }
    }
}
