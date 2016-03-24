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
        List<Point> _selections;
        int _lastSelection = 0;
        VectorFieldUnsteady _velocity;
        FieldPlane _bg;

        public PlaygroundMapper(Plane plane) : base(plane, new Int2(NUM_CELLS))
        {
            Mapping = Map;
            _velocity = Tests.CreateBowl(new Vec2(NUM_CELLS/2, 0), NUM_CELLS, new Vec2(-10, 0), 11, 200);
        }

        public List<Renderable> Map()
        {
            List<Renderable> result = new List<Renderable>(10);

            #region BackgroundPlane
            if (_bg == null)
                _bg = new FieldPlane(Plane, _velocity.GetTimeSlice(0), Shader, Colormap);

            _bg.SetRenderEffect(Shader);
            _bg.UsedMap = Colormap;
            _bg.LowerBound = WindowStart;
            _bg.UpperBound = WindowStart + WindowWidth;

            result.Add(_bg);
            #endregion BackgroundPlane

            if(_lastSelection != null && LineXChanged)
            {

                int diff = _lastSetting.LineX - LineX;
                if (diff > 0)
                {
                    _selections.RemoveRange(0, diff);
                }

            }

            return result;
        }

        public override void ClickSelection(Vector2 point)
        {
            if(LineX > 0)
            {
                _lastSelection = (_lastSelection + 1) % LineX;
                if(_velocity.Grid.InGrid(new Vec3((Vec2)point, 0)))
                {
                    _selections.Add(new Point(new Vector3(point, 0)));
                }
                else
                {

                }
            }
        }

        public override bool IsUsed(Setting.Element element)
        {
            return true;
        }
    }
}
