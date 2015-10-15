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
            foreach (Vector col in columns)
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
            if (Length == 2)
            {
                clipped = null;
                return this[0][0] * this[1][1] - this[0][1] * this[1][0];
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public void Eigenanalysis(out SquareMatrix eigenvalues, out SquareMatrix eigenvectors)
        {
            eigenvectors = new SquareMatrix(2);
            eigenvalues = new SquareMatrix(2);

            float a = this[0][0]; float b = this[1][0]; float c = this[0][1]; float d = this[1][1];
            // Computing eigenvalues.
            float Th = (a - d) * 0.5f;
            float D = a * d - b * c;
            float root = Th * Th - D;

            float complex = 0;
            if (root < 0)
            {
                complex = -root;
                root = 0;
            }

            root = (float)Math.Sqrt(root);
            float l0 = Th + root;
            float l1 = Th - root;

            // Save directional information.
            eigenvalues[0] = new Vec2(l0, complex);
            eigenvalues[1] = new Vec2(l1, -complex);

            // Computing eigenvectors.
            if (c != 0)
            {
                eigenvectors[0] = new Vec2(l0 - d, c);
                eigenvectors[1] = new Vec2(l1 - d, c);
            }
            else if (b != 0)
            {
                eigenvectors[0] = new Vec2(b, l0 - a);
                eigenvectors[1] = new Vec2(b, l1 - a);
            }
            else
            {
                eigenvectors[0] = new Vec2(1, 0);
                eigenvectors[1] = new Vec2(0, 1);
            }
        }
    }
}
