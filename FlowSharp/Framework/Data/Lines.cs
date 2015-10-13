using SlimDX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlowSharp
{
    class Line
    {
        public Vector3[] Positions;
        public int Length { get { return Positions.Length; } }
    }

    class LineSet : GeometryData
    {
        protected Line[] _lines;
        
        public Line[] Lines
        {
            get { return _lines; }
            protected set
            {
                _lines = value;
                NumPoints = 0;
                foreach (Line line in _lines)
                {
                    NumPoints += Math.Max(line.Positions.Length, 2);
                    NumExistentPoints += line.Positions.Length;
                }
            }
        }
        public int NumPoints { get; protected set; }
        public int NumExistentPoints { get; protected set; }
        public virtual Vector3 Color { get; set; } = SlimDX.Vector3.UnitY;
        public virtual float Thickness { get; set; } = 0.1f;

        public LineSet(Line[] lines)
        {
            Lines = lines;
            WorldPosition = true;
        }

        public LineSet(Line[] lines, GeometryData data)
        {
            Lines = lines;
            WorldPosition = data.WorldPosition;
            if(!WorldPosition)
            {
                _origin = (Vector3)data.Origin;
                _cellSize = (Vector3)data.CellSize;
            }
        }

        public LineSet(Line[] lines, Vector3 cellSize, Vector3 origin)
        {
            Lines = lines;
            WorldPosition = false;
            _cellSize = cellSize;
            _origin = origin;
        }

        public Line GetWorldLine(int index)
        {
            if (WorldPosition)
                return Lines[index];

            Vector3[] gridPos = Lines[index].Positions;
            Vector3[] positions = new Vector3[gridPos.Length];

            for (int point = 0; point < positions.Length; ++point)
                positions[point] = MathHelper.Mult(gridPos[point], _cellSize) + _origin;

            Line worldLine = new Line()
            {
                Positions = positions
            };

            return worldLine;
        }
    }
}
