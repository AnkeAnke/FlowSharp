using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using SlimDX;
using System.Runtime.InteropServices;

namespace FlowSharp
{
    class FlowMapMapper : SelectionMapper
    {
        private FieldPlane _currentState;
        private FlowMapUncertain _flowMap;
        private VectorFieldUnsteady _velocity;

        public FlowMapMapper(Loader.SliceRange[] uv, Plane plane, VectorFieldUnsteady velocity) : base(plane, velocity.Size.ToInt2())
        {
            _flowMap = new FlowMapUncertain(_startPoint, uv, 0, 9);
            _algorithm = _flowMap;
            Plane = plane;
            _subrangePlane = Plane;
            Mapping = GetCurrentMap;
            _velocity = velocity;
            _maxPlane = velocity.Size.ToInt2();
        }

        /// <summary>
        /// If different planes were chosen, load new fields.
        /// </summary>
        /// <returns></returns>
        public List<Renderable> GetCurrentMap()
        {
            if (_lastSetting == null ||
                _currentSetting.SliceTimeMain != _lastSetting.SliceTimeMain)
            {
                if (_lastSetting == null || _currentSetting.SliceTimeMain < _flowMap.CurrentTime)
                {
                    _flowMap.SetupPoint(_startPoint, _currentSetting.SliceTimeMain);
                }

                // Integrate to the desired time step.
                while (_flowMap.CurrentTime < _currentSetting.SliceTimeMain)
                    _flowMap.Step(_currentSetting.StepSize);

                //.GetPlane(Plane);
                //_currentState = _flowMap.GetPlane(Plane);

            }
            if (_lastSetting == null ||
                _currentSetting.StepSize != _lastSetting.StepSize)
            {
            }
            if (_lastSetting == null ||
                _currentSetting.Colormap != _lastSetting.Colormap ||
                _currentSetting.Shader != _lastSetting.Shader ||
                _currentSetting.SliceTimeMain != _lastSetting.SliceTimeMain)
            {
                RefreshPlane();
                _currentState.LowerBound = 0;
                _currentState.UpperBound = _currentSetting.WindowWidth;
            }
            if (_lastSetting == null ||
                _lastSetting.WindowWidth != _currentSetting.WindowWidth)
            {
                _currentState.LowerBound = 0;
                _currentState.UpperBound = _currentSetting.WindowWidth;
            }
            List<Renderable> list = new List<Renderable>(1);
            list.Add(_currentState);
            return list;
        }

        private void RefreshPlane()
        {
            switch (_currentSetting.Shader)
            {
                case FieldPlane.RenderEffect.LIC:
                case FieldPlane.RenderEffect.LIC_LENGTH:
                    var tmp = _velocity.GetTimeSlice(_currentSetting.SliceTimeMain);
                    tmp.TimeSlice = null;
                    _currentState = new FieldPlane(_subrangePlane, tmp, _currentSetting.Shader, _currentSetting.Colormap);
                    _currentState.AddScalar(_flowMap.FlowMap);
                    break;
                default:
                    _currentState = _flowMap.GetPlane(_subrangePlane);
                    _currentState.UsedMap = _currentSetting.Colormap;
                    _currentState.SetRenderEffect(_currentSetting.Shader);
                    break;
            }
        }

        public override bool IsUsed(Setting.Element element)
        {
            switch (element)
            {
                case Setting.Element.Colormap:
                case Setting.Element.WindowWidth:
                //return _currentSetting.Shader == FieldPlane.RenderEffect.COLORMAP || _currentSetting.Shader == FieldPlane.RenderEffect.DEFAULT;
                case Setting.Element.SliceTimeMain:
                case Setting.Element.Shader:
                case Setting.Element.StepSize:
                    return true;
                default:
                    return false;
            }
        }

        public override void EndSelection(Vector2[] points)
        {
            base.EndSelection(points);

            _velocity = _flowMap.LoadMeanField();
            RefreshPlane();

            int size = _velocity.Size.ToInt2().Product();
            float fill, mean, sd;
            _velocity.ScalarsAsSFU[0].GetTimeSlice(0).ComputeStatistics(out fill, out mean, out sd);
            Console.WriteLine("Region:\n\tRegion: " + size + "\n\tValid Part: " + fill + "\n\tValid Cells: " + fill * size);

        }

        public override void ClickSelection(Vector2 point)
        {
            base.ClickSelection(point);
            _flowMap.SetupPoint(_startPoint, _currentSetting.SliceTimeMain);
            RefreshPlane();
        }
    }
    class DiffusionMapper : SelectionMapper
    {
        private FieldPlane _dataMap;
        private LocalDiffusion _diffusionMap;
        private VectorFieldUnsteady _velocity;

        public DiffusionMapper(VectorFieldUnsteady velocity, Plane plane) : base(plane, velocity.Size.ToInt2())
        {
            _diffusionMap = new LocalDiffusion(velocity, 0, 0.3f);
            _algorithm = _diffusionMap;
            Plane = plane;
            _subrangePlane = Plane;
            Mapping = GetCurrentMap;
            _velocity = velocity;
            _maxPlane = velocity.Size.ToInt2();
        }

        /// <summary>
        /// If different planes were chosen, load new fields.
        /// </summary>
        /// <returns></returns>
        public List<Renderable> GetCurrentMap()
        {
            if (_lastSetting == null ||
                _currentSetting.SliceTimeMain != _lastSetting.SliceTimeMain ||
                _currentSetting.AlphaStable != _lastSetting.AlphaStable)
            {
                _diffusionMap.SetupMap(_startPoint, _currentSetting.SliceTimeMain, 0.5f);

                // Integrate to the desired time step.
                while (_diffusionMap.CurrentTime < _diffusionMap.EndTime)
                    _diffusionMap.Step(_currentSetting.StepSize, _currentSetting.AlphaStable);
            }
            if (_lastSetting == null ||
                _currentSetting.StepSize != _lastSetting.StepSize)
            {
            }
            if (_lastSetting == null ||
                _currentSetting.Colormap != _lastSetting.Colormap ||
                _currentSetting.Shader != _lastSetting.Shader ||
                _currentSetting.SliceTimeMain != _lastSetting.SliceTimeMain)
            {
                RefreshPlane();
                _dataMap.LowerBound = 0;
                _dataMap.UpperBound = _currentSetting.WindowWidth;
            }
            if (_lastSetting == null ||
                _lastSetting.WindowWidth != _currentSetting.WindowWidth)
            {
                _dataMap.LowerBound = 0;
                _dataMap.UpperBound = _currentSetting.WindowWidth;
            }
            List<Renderable> list = new List<Renderable>(1);
            list.Add(_dataMap);
            return list;
        }

        private void RefreshPlane()
        {
            switch (_currentSetting.Shader)
            {
                case FieldPlane.RenderEffect.LIC:
                case FieldPlane.RenderEffect.LIC_LENGTH:
                    var tmp = _velocity.GetTimeSlice(_currentSetting.SliceTimeMain);
                    tmp.TimeSlice = null;
                    _dataMap = new FieldPlane(_subrangePlane, tmp, _currentSetting.Shader, _currentSetting.Colormap);
                    if(_currentSetting.Shader == FieldPlane.RenderEffect.LIC)
                        _dataMap.AddScalar(_diffusionMap.SelectionMap);
                    break;
                default:
                    _dataMap = _diffusionMap.GetPlane(_subrangePlane);
                    _dataMap.UsedMap = _currentSetting.Colormap;
                    _dataMap.SetRenderEffect(_currentSetting.Shader);
                    break;
            }
            _dataMap.LowerBound = _currentSetting.WindowStart;
            _dataMap.UpperBound = _currentSetting.WindowWidth + _currentSetting.WindowStart;
        }

        public override bool IsUsed(Setting.Element element)
        {
            switch (element)
            {
                case Setting.Element.Colormap:
                case Setting.Element.WindowWidth:
                //return _currentSetting.Shader == FieldPlane.RenderEffect.COLORMAP || _currentSetting.Shader == FieldPlane.RenderEffect.DEFAULT;
                case Setting.Element.SliceTimeMain:
                case Setting.Element.Shader:
                case Setting.Element.StepSize:
                case Setting.Element.AlphaStable:
                    return true;
                default:
                    return false;
            }
        }

        public override void EndSelection(Vector2[] points)
        {
            //base.EndSelection(points);

            //_velocity = _velocity.GetTimeSlice()
            //RefreshPlane();

            //int size = _velocity.Size.ToInt2().Product();
            //float fill, mean, sd;
            //_velocity.ScalarsAsSFU[0].GetTimeSlice(0).ComputeStatistics(out fill, out mean, out sd);
            //Console.WriteLine("Region:\n\tRegion: " + size + "\n\tValid Part: " + fill + "\n\tValid Cells: " + fill * size);
        }

        public override void ClickSelection(Vector2 point)
        {
            base.ClickSelection(point);
            //_flowMap.SetupPoint(_startPoint, _currentSetting.SliceTimeMain);
            //RefreshPlane();
            _diffusionMap.SetupMap(_startPoint, _currentSetting.SliceTimeMain, 0.5f);

            // Integrate to the desired time step.
            while (_diffusionMap.CurrentTime < _diffusionMap.EndTime)
                _diffusionMap.Step(_currentSetting.StepSize, _currentSetting.AlphaStable);

            RefreshPlane();

            Console.WriteLine("Clicked: " + _startPoint);
        }
    }
}
