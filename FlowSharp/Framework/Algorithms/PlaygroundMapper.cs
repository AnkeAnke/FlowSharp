using SlimDX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlowSharp
{
    class PlaygroundMapper : SelectionMapper
    {
        private static int NUM_CELLS = 200;
        Vector2[] _selections;
        VectorFieldUnsteady _velocity;
        FieldPlane _bg;

        public PlaygroundMapper(Plane plane) : base(plane, new Int2(NUM_CELLS))
        {
            Mapping = Map;

            _velocity = Tests.CreateCircle(new Vec2(150, 100), NUM_CELLS, new Vec2(-10, 0), 11, 1);
        }

        public List<Renderable> Map()
        {
            List<Renderable> result = new List<Renderable>(10);
            if (_bg == null)
                _bg = new FieldPlane(Plane, _velocity, Shader, Colormap);

            _bg.SetRenderEffect(Shader);
            _bg.LowerBound = WindowStart;
            _bg.UpperBound = WindowStart + WindowWidth;

            result.Add(_bg);

            return result;
        }

        public override bool IsUsed(Setting.Element element)
        {
            return true;
        }
    }
}
