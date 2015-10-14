using SlimDX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace FlowSharp
{
    class RedSea
    {
        /// <summary>
        /// Relevant variables of Read Sea file.
        /// </summary>
        public enum Variable : int
        {
            TIME = 3,
            GRID_X = 5,
            CENTER_X = 6,
            GRID_Y = 7,
            CENTER_Y = 8,
            GRID_Z = 9,
            CENTER_Z = 10,
            SALINITY = 11,
            TEMPERATURE = 12,
            VELOCITY_X = 13,
            VELOCITY_Y = 14,
            SURFACE_HEIGHT = 15
        }

        public enum Dimension : int
        {
            MEMBER = 2,
            TIME = 3,
            GRID_X = 8,
            CENTER_X = 9,
            GRID_Y = 10,
            CENTER_Y = 11,
            GRID_Z = 12,
            CENTER_Z = 13
        }

        public enum Display : int
        {
            NONE,
            CP_TRACKING
        }

        public enum DisplayLines : int
        {
            LINE,
            POINTS_2D_LENGTH
        }
        public static VectorField.PositionToColor[] DisplayLineFunctions = new VectorField.PositionToColor[]
        {
            null,
            (f, world, x) => new Vector3(f.Sample((Vec3)x, world).ToVec2().LengthEuclidean() * 10)
        };

        private static RedSea _instance;
        public static RedSea Singleton {
            get
            {
                if (_instance == null)
                    _instance = new RedSea();
                return _instance;
            } }

        // Depending on display and slice0 setting.
        protected DisplaySet[] _displayPresets;
        protected Renderable _slice1;
        protected Display _preset;

        public void SetPresets(DisplaySet[] presets)
        {
            _displayPresets = presets;
        }

        //protected List<Renderable> _currentDisplay;


        public void SetPreset(Display preset, int slice0, DisplayLines lineSetting)
        {
            Debug.Assert(Renderer.Singleton.Initialized);
            Renderer.Singleton.ClearRenderables();
            if (_displayPresets[(int)preset] != null)
            {
                Renderer.Singleton.AddRenderables(_displayPresets[(int)preset].CreateRenderables(slice0, lineSetting).ToList());
                if (_slice1 != null)
                    Renderer.Singleton.AddRenderable(_slice1);
                _preset = preset;
            }
        }
        public void SetPreset(int slice1)
        {
            if(_slice1 != null)
                _slice1.Active = false;
            _slice1 = new FieldPlane(_displayPresets[(int)_preset].Plane, _displayPresets[(int)_preset].GetField(slice1), FieldPlane.RenderEffect.LIC);
            Renderer.Singleton.AddRenderable(_slice1);
        }
    }

    class DisplaySet
    {
        public class FieldData
        {
            public VectorField Field;
            public CriticalPointSet2D[] Points;
            public LineSet[] Lines;
            
            public FieldData(VectorField field, CriticalPointSet2D[] points, LineSet[] lines)
            {
                Field = field;
                Points = points;
                Lines = lines;
            }

            public FieldData(VectorField field)
            {
                Field = field;
            }
        }
        private Renderable[] _staticRenderables; // Containing field plane etc.
        private FieldData[] _rawData;
        public Plane Plane;
        public VectorField Field;

        public VectorField GetField(int index) { return _rawData[Math.Min(_rawData.Length-1, index)].Field; }

        public DisplaySet(FieldData[] data, Plane plane, VectorField field)
        {
            _rawData = data;
            _staticRenderables = new Renderable[0];
            Plane = plane;
            Field = field;
        }

        public Renderable[] CreateRenderables(int field, RedSea.DisplayLines lineSetting, FieldPlane.RenderEffect effect = FieldPlane.RenderEffect.LIC)
        {
            FieldData data = _rawData[field];
            Renderable[] allObjects = new Renderable[1 + _staticRenderables.Length + data.Lines.Length + data.Points.Length];

            for(int p = 0; p < data.Points.Length; ++p)
            {
                allObjects[p] = new PointCloud<CriticalPoint2D>(Plane, data.Points[p]);
            }

            // Depending on Settings, create a different renderable.
            for(int l = 0; l < data.Lines.Length; ++l)
            {
                Renderable line;
                if(lineSetting != RedSea.DisplayLines.LINE)
                {
                    PointSet<Point> points = Field.ColorCodeArbitrary(data.Lines[l], RedSea.DisplayLineFunctions[(int)lineSetting]);
                    line = new PointCloud(Plane, points);
                }
                else
                {
                    line = new LineBall(Plane, data.Lines[l]);
                }
                allObjects[data.Points.Length + l] = line;
            }

            // Simply copy static renderables.
            Array.Copy(_staticRenderables, 0, allObjects, data.Points.Length + data.Lines.Length, _staticRenderables.Length);

            allObjects[allObjects.Length - 1] = new FieldPlane(Plane, data.Field, effect);

            return allObjects;

        }
    }
}
