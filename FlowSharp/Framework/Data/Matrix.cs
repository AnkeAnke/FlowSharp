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
        protected VectorRef[] Columns;
        public int Length { get { return Columns.Length; } }
        public SquareMatrix(Vector[] columns)
        {
#if DEBUG
            foreach (Vector col in columns)
                Debug.Assert(columns.Length == col.Length); // Only sqare matrices allowed.
#endif
            Columns = columns;
        }
        public float Ux { get { return this[0][0]; } set { this[0][0] = value; } }
        public float Uy { get { return this[1][0]; } set { this[1][0] = value; } }
        public float Vx { get { return this[0][1]; } set { this[0][1] = value; } }
        public float Vy { get { return this[1][1]; } set { this[1][1] = value; } }

        public float m00 { get { return this[0][0]; } set { this[0][0] = value; } }
        public float m10 { get { return this[1][0]; } set { this[1][0] = value; } }
        public float m01 { get { return this[0][1]; } set { this[0][1] = value; } }
        public float m11 { get { return this[1][1]; } set { this[1][1] = value; } }

        public SquareMatrix(SquareMatrix m)
        {
#if DEBUG
            foreach (Vector col in m.Columns)
                Debug.Assert(m.Columns.Length == col.Length); // Only sqare matrices allowed.
#endif
            Columns = new Vector[m.Columns.Length];

            for (int c = 0; c < Columns.Length; ++c)
                Columns[c] = new Vector(m.Columns[c]);
        }

        public SquareMatrix(int length)
        {
            Columns = new Vector[length];
            for (int row = 0; row < length; ++row)
                Columns[row] = new Vector(0, length);
        }

        public VectorRef this[int index]
        {
            get { return Columns[index]; }
            set { Debug.Assert(value.Length == Length); Columns[index] = value; }
        }

        public float this[Int2 index]
        {
            get { return this[index.X][index.Y]; }
            set { this[index.X][index.Y] = value; }
        }

        public VectorRef Column(int index)
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

        public void Transpose()
        {
            for(int x = 0; x < Length; ++x)
                for(int y = x+1; y < Length; ++y)
                {
                    float tmp = this[x][y];
                    this[x][y] = this[y][x];
                    this[y][x] = tmp;
                }
        }

        public SquareMatrix Transposed()
        {
            SquareMatrix mat = new SquareMatrix(Length);
            for (int x = 0; x < Length; ++x)
                for (int y = 0; y < Length; ++y)
                {
                    mat[x][y] = this[y][x];
                    //float tmp = this[x][y];
                    //this[x][y] = this[y][x];
                    //this[y][x] = tmp;
                }

            return mat;
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

        public float EuclideanNormSquared()
        {
            float sum = 0;
            foreach (Vector vec in Columns)
                sum += vec.LengthSquared();
            return sum;
        }
        public float EuclideanNorm()
        {
            return (float)Math.Sqrt(EuclideanNormSquared());
        }

        public void Eigenanalysis(out SquareMatrix eigenvalues, out SquareMatrix eigenvectors)
        {
            Debug.Assert(Length == 2, "Only 2D eigenanalysis implemented so far.");
            eigenvectors = new SquareMatrix(2);
            eigenvalues = new SquareMatrix(2);

            float a = this[0][0]; float b = this[1][0]; float c = this[0][1]; float d = this[1][1];
            // Computing eigenvalues.
            float Th = (a + d) * 0.5f;
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

        public VectorRef EigenvaluesReal()
        {
            SquareMatrix vals, vecs;
            Eigenanalysis(out vals, out vecs);

            return vals[0];
        }

        public SquareMatrix Inverse()
        {
            Debug.Assert(Length == 3, "Only implemented 3x3.");
            SquareMatrix inv = new SquareMatrix(Length);

            for (int c = 0; c < 3; ++c)
            {
                for (int r = 0; r < 3; ++r)
                {
                    int col0 = (c + 1) % 3;
                    int col1 = (c + 2) % 3;
                    int row0 = (r + 1) % 3;
                    int row1 = (r + 2) % 3;
                    int sign = (c + r) % 2 == 0 ? 1 : -1;
                    inv[r][c] = (this[col0][row0] * this[col1][row1] - this[col0][row1] * this[col1][row0]);
                }
            }
            // -6.2938e-14
            float det = -this[0][0] * inv[0][0] + this[0][1] * inv[0][1] - this[0][2] * inv[0][2];
            return inv/det;
        }

        public SquareMatrix ToMat2x2()
        {
            SquareMatrix mat = new SquareMatrix(2);
            mat[0] = this[0].ToVec2();
            mat[1] = (this.Length > 1) ? this[1].ToVec2() : new Vec2(0);

            return mat;
        }

        public static SquareMatrix operator + (SquareMatrix a, SquareMatrix b)
        {
            Debug.Assert(a.Length == b.Length);
            SquareMatrix c = new SquareMatrix(a.Length);
            for (int col = 0; col < c.Length; ++col)
                c[col] = a[col] + b[col];

            return c;
        }

        public static SquareMatrix operator -(SquareMatrix a, SquareMatrix b)
        {
            Debug.Assert(a.Length == b.Length);
            SquareMatrix c = new SquareMatrix(a.Length);
            for (int col = 0; col < c.Length; ++col)
                c[col] = a[col] - b[col];

            return c;
        }

        public static SquareMatrix operator *(SquareMatrix a, float b)
        {
            SquareMatrix c = new SquareMatrix(a.Length);
            for (int col = 0; col < c.Length; ++col)
                c[col] = a[col] * b;

            return c;
        }

        public static SquareMatrix operator /(SquareMatrix a, float b)
        {
            SquareMatrix c = new SquareMatrix(a.Length);
            for (int col = 0; col < c.Length; ++col)
                c[col] = a[col] / b;

            return c;
        }

        public static SquareMatrix operator *(float a, SquareMatrix b)
        {
            return b * a;
        }

        public static SquareMatrix operator *(SquareMatrix a, SquareMatrix b)
        {
            Debug.Assert(a.Length == b.Length);
            SquareMatrix prod = new SquareMatrix(a.Length);

            for(int x = 0; x < a.Length; ++x)
            {
                Vector row = a.Row(x);

                for(int y = 0; y < b.Length; ++y)
                {
                    prod[x][y] = Vector.Dot(row, b[y]);
                }
            }
            return prod;
        }

        public static Vector operator *(VectorRef a, SquareMatrix b)
        {
            Debug.Assert(a.Length == b.Length);
            Vector prod = new Vector(a.Length);

            for (int x = 0; x < a.Length; ++x)
            {
                prod[x] = Vector.Dot(a, b[x]);
            }
            return prod;
        }

        public static Vector operator *(SquareMatrix a, VectorRef b)
        {
            Debug.Assert(a.Length == b.Length);
            Vector prod = new Vector(a.Length);

            for (int y = 0; y < a.Length; ++y)
            {
                prod[y] = Vector.Dot(a.Row(y), b);
            }
            return prod;
        }
    }
}
