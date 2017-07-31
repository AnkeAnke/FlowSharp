using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace FlowSharp
{
    using System.Diagnostics;
#if true
    using Context = Aneurysm;
#endif
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private DataMapper _mapper;
        private bool _mapperChanged = true;
        //private Context.Display _display;
        //private Context.DisplayLines _displayLines;
        //private int _slice0, _slice1;
        //private FieldPlane _slice1Plane;

        private static FrameworkElement[] _windowObjects = new FrameworkElement[Enum.GetValues(typeof(DataMapper.Setting.Element)).Length];
//        private float startX, startY, endX, endY, dimX, dimY;
        public MainWindow()
        {
            try
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => { LoadUI(); }));
                InitializeComponent();

                DX11Display.Scene = Renderer.Singleton;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                Debug.Assert(false);
            }

            _mapper = new EmptyMapper();

            try // Catching exceptions before they end up in weird WPF errors.
            {
                Context.Singleton.WPFWindow = this;
                FSMain.LoadData();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                Debug.Assert(false);
            }
        }

        public void UpdateNumTimeSlices()
        {
            DropDownSlice0.ItemsSource = Enumerable.Range(0, Context.Singleton.NumTimeSlices);

            DropDownSlice1.ItemsSource = Enumerable.Range(0, Context.Singleton.NumTimeSlices);
        }

        public void LoadUI()
        {

            DropDownDisplay.ItemsSource = Enum.GetValues(typeof(Context.Display)).Cast<Context.Display>();
            DropDownDisplay.SelectedIndex = 0;

            DropDownDisplayLines.ItemsSource = Enum.GetValues(typeof(Context.DisplayLines)).Cast<Context.DisplayLines>();
            DropDownDisplayLines.SelectedIndex = 0;

            DropDownSlice0.ItemsSource = Enumerable.Range(0, Context.Singleton.NumSteps);
            DropDownSlice0.SelectedIndex = 0;

            DropDownSlice1.ItemsSource = Enumerable.Range(0, Context.Singleton.NumSteps);
            DropDownSlice1.SelectedIndex = 0;

            DropDownMember0.ItemsSource = Enumerable.Range(0, 10);
            DropDownMember0.SelectedIndex = 0;

            DropDownMember1.ItemsSource = Enumerable.Range(0, 10);
            DropDownMember1.SelectedIndex = 0;

            DropDownHeight.ItemsSource = Enumerable.Range(0, 10);
            DropDownHeight.SelectedIndex = 0;

            DropDownIntegrator.ItemsSource = Enum.GetValues(typeof(VectorField.Integrator.Type)).Cast<VectorField.Integrator.Type>();
            DropDownIntegrator.SelectedIndex = (int)VectorField.Integrator.Type.RUNGE_KUTTA_4;

            StepSizeSlider.Value = 0.5;
            StepSizeSlider.Minimum = 0.000001;
            integrationTime.Value = 60;
            AlphaSlider.Value = 3;

            DropDownMeasure.ItemsSource = Enum.GetValues(typeof(Context.Measure)).Cast<Context.Measure>();
            DropDownMeasure.SelectedIndex = (int)Context.Measure.velocity;

            DropDownDiffusionMeasure.ItemsSource = Enum.GetValues(typeof(Context.GeometryPart)).Cast<Context.GeometryPart>();
            DropDownDiffusionMeasure.SelectedIndex = (int)Context.GeometryPart.Wall;

            DropDownTracking.ItemsSource = Enum.GetValues(typeof(Context.DisplayTracking)).Cast<Context.DisplayTracking>();
            DropDownTracking.SelectedIndex = (int)Context.DisplayTracking.LINE_POINTS;

            DropDownShader.ItemsSource = Enum.GetValues(typeof(FieldPlane.RenderEffect)).Cast<FieldPlane.RenderEffect>();
            DropDownShader.SelectedIndex = (int)FieldPlane.RenderEffect.COLORMAP;

            DropDownColormap.ItemsSource = Enum.GetValues(typeof(Colormap)).Cast<Colormap>();
            DropDownColormap.SelectedIndex = (int)Colormap.Parula;

            DropDownCore.ItemsSource = Enum.GetValues(typeof(DataMapper.CoreAlgorithm)).Cast<DataMapper.CoreAlgorithm>();
            DropDownColormap.SelectedIndex = 0;

            VarX.ItemsSource = Enum.GetValues(typeof(DataMapper.Setting.Element)).Cast<DataMapper.Setting.Element>();
            VarX.SelectedIndex = 0;
            VarY.ItemsSource = Enum.GetValues(typeof(DataMapper.Setting.Element)).Cast<DataMapper.Setting.Element>();
            VarY.SelectedIndex = 1;

            BitmapImage b = new BitmapImage();
            b.BeginInit();
            string dir = Directory.GetCurrentDirectory();
            b.UriSource = new Uri(dir + "/Framework/Renderer/Resources/Colormap" + "Parula" + ".png");
            b.EndInit();

            ColormapView.Source = b;
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            _windowObjects[(int)DataMapper.Setting.Element.LineSetting] = DropDownDisplayLines;
            _windowObjects[(int)DataMapper.Setting.Element.SliceTimeMain] = DropDownSlice0;
            _windowObjects[(int)DataMapper.Setting.Element.SliceTimeReference] = DropDownSlice1;
            _windowObjects[(int)DataMapper.Setting.Element.MemberMain] = Member0Block;
            _windowObjects[(int)DataMapper.Setting.Element.MemberReference] = DropDownMember1;
            _windowObjects[(int)DataMapper.Setting.Element.SliceHeight] = MemberHeightBlock;
            _windowObjects[(int)DataMapper.Setting.Element.IntegrationType] = DropDownIntegrator;
            _windowObjects[(int)DataMapper.Setting.Element.AlphaStable] = AlphaBlock;
            _windowObjects[(int)DataMapper.Setting.Element.StepSize] = StepSizeBlock;
            _windowObjects[(int)DataMapper.Setting.Element.LineX] = LineXBlock;
            _windowObjects[(int)DataMapper.Setting.Element.WindowWidth] = WindowWidthBlock;
            _windowObjects[(int)DataMapper.Setting.Element.StepSize] = StepSizeBlock;
            _windowObjects[(int)DataMapper.Setting.Element.Shader] = DropDownShader;
            _windowObjects[(int)DataMapper.Setting.Element.Colormap] = DropDownColormap;
            _windowObjects[(int)DataMapper.Setting.Element.Tracking] = DropDownTracking;
            _windowObjects[(int)DataMapper.Setting.Element.WindowStart] = WindowStartBlock;
            _windowObjects[(int)DataMapper.Setting.Element.Measure] = DropDownMeasure;
            _windowObjects[(int)DataMapper.Setting.Element.GeometryPart] = DropDownDiffusionMeasure;
            _windowObjects[(int)DataMapper.Setting.Element.IntegrationTime] = IntegrationTimeBlock;
            _windowObjects[(int)DataMapper.Setting.Element.VarX] = MatrixBox;
            _windowObjects[(int)DataMapper.Setting.Element.VarY] = MatrixBox;
            _windowObjects[(int)DataMapper.Setting.Element.StartX] = MatrixBox;
            _windowObjects[(int)DataMapper.Setting.Element.StartY] = MatrixBox;
            _windowObjects[(int)DataMapper.Setting.Element.EndX] = MatrixBox;
            _windowObjects[(int)DataMapper.Setting.Element.EndY] = MatrixBox;
            _windowObjects[(int)DataMapper.Setting.Element.DimX] = MatrixBox;
            _windowObjects[(int)DataMapper.Setting.Element.DimY] = MatrixBox;
            _windowObjects[(int)DataMapper.Setting.Element.Flat] = DisplayFlat;
            _windowObjects[(int)DataMapper.Setting.Element.Graph] = ShowGraph;
            _windowObjects[(int)DataMapper.Setting.Element.Core] = DropDownCore;

            Renderer.Singleton.SetCanvas(DX11Display);
        }

        #region OnChangeValue
        // ~~~~~~~~~~ On change callbacks ~~~~~~~~~~~ \\
        private void OnChangeDisplay(object sender, RoutedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            DataMapper last = _mapper;
            _mapper = Context.Singleton.SetMapper((Context.Display)(comboBox.SelectedItem as Context.Display?));
            _mapper.CurrentSetting = new DataMapper.Setting(last.CurrentSetting);

            _mapperChanged = true;

            int? start = _mapper.GetStart(DataMapper.Setting.Element.WindowStart);
            int? length = _mapper.GetLength(DataMapper.Setting.Element.WindowStart);
            if (start != null && length != null)
            {
                WindowStart.Minimum = (int)start;
                WindowStart.Maximum = (int)length;
            }

            start = _mapper.GetStart(DataMapper.Setting.Element.WindowWidth);
            length = _mapper.GetLength(DataMapper.Setting.Element.WindowWidth);
            if (start != null && length != null)
            {
                WindowWidth.Minimum = (int)start;
                WindowWidth.Maximum = (int)length;
            }

            UpdateRenderer();
        }

        public void Screenshot(string name)
        {
            RenderTargetBitmap renderTargetBitmap =
            new RenderTargetBitmap((int)DX11Display.Width, (int)DX11Display.Height, 96, 96, PixelFormats.Pbgra32);
            renderTargetBitmap.Render(DX11Display);
            PngBitmapEncoder pngImage = new PngBitmapEncoder();
            pngImage.Frames.Add(BitmapFrame.Create(renderTargetBitmap));
            using (Stream fileStream = File.Create(name))
            {
                pngImage.Save(fileStream);
            }
        }

        //private void OnChangeValue(object sender, RoutedEventArgs e)
        //{
        //    _mapper.CurrentSetting.Measure = (Context.Measure)(DropDownMeasure.SelectedItem as Context.Measure?);
        //    _mapper.CurrentSetting.LineSetting = (Context.DisplayLines)(DropDownDisplayLines.SelectedItem as Context.DisplayLines?);
        //    _mapper.CurrentSetting.SliceTimeMain = (int)(DropDownSlice0.SelectedItem as int?);
        //    _mapper.CurrentSetting.SliceTimeReference = (int)(DropDownSlice1.SelectedItem as int?);
        //    _mapper.CurrentSetting.IntegrationType = (VectorField.Integrator.Type)(DropDownIntegrator.SelectedItem as VectorField.Integrator.Type?);
        //    _mapper.CurrentSetting.StepSize = (float)(StepSizeSlider.Value as double?);
        //    _mapper.CurrentSetting.AlphaStable = (float)(AlphaSlider.Value as double?);
        //    _mapper.CurrentSetting.LineX = (int)(LineSlider.Value as int?);
        //    _mapper.CurrentSetting.MemberMain = (int)(DropDownMember0.SelectedItem as int?);
        //    _mapper.CurrentSetting.MemberReference = (int)(DropDownMember1.SelectedItem as int?);
        //    _mapper.CurrentSetting.Shader = (FieldPlane.RenderEffect)(DropDownShader.SelectedItem as FieldPlane.RenderEffect?);
        //    _mapper.CurrentSetting.Colormap = (Colormap)(DropDownColormap.SelectedItem as Colormap?);
        //    _mapper.CurrentSetting.WindowWidth = (float)(WindowWidth.Value as double?);
        //    _mapper.CurrentSetting.WindowStart = (float)(WindowStart.Value as double?);
        //    _mapper.CurrentSetting.Tracking = (Context.DisplayTracking)(DropDownTracking.SelectedItem as Context.DisplayTracking?);
        //    _mapper.CurrentSetting.SliceHeight = (int)(DropDownHeight.SelectedItem as int?);
        //    _mapper.CurrentSetting.IntegrationTime = (float)(integrationTime.Value as double?);
        //    _mapper.CurrentSetting.DiffusionMeasure = (Context.DiffusionMeasure)(DropDownDiffusionMeasure.SelectedItem as Context.DiffusionMeasure?);
        //    _mapper.CurrentSetting.VarX = (DataMapper.Setting.Element)(VarX.SelectedItem as DataMapper.Setting.Element?);
        //    _mapper.CurrentSetting.VarY = (DataMapper.Setting.Element)(VarY.SelectedItem as DataMapper.Setting.Element?);
        //    _mapper.CurrentSetting.StartX = endX; //(float)(EndX.Value as double?);
        //    _mapper.CurrentSetting.StartY = startY; //(float)(DimX.Value as double?);
        //    _mapper.CurrentSetting.EndX = endX; //(float)(VarX.Value as double?);
        //    _mapper.CurrentSetting.EndY = endY; //(float)(StartY.Value as double?);
        //    _mapper.CurrentSetting.DimX = (int)dimX; //(float)(EndY.Value as double?);
        //    _mapper.CurrentSetting.DimY = (int)dimY; //(float)(DimY.Value as double?);
        //}

        private void OnChangeDisplayLines(object sender, RoutedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            _mapper.CurrentSetting.LineSetting = (Context.DisplayLines)(comboBox.SelectedItem as Context.DisplayLines?);
            UpdateRenderer();
        }

        private void OnChangeMeasure(object sender, RoutedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            _mapper.CurrentSetting.Measure = (Context.Measure)(comboBox.SelectedItem as Context.Measure?);
            UpdateRenderer();
        }

        private void OnChangeDiffusionMeasure(object sender, RoutedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            _mapper.CurrentSetting.GeometryPart = (Context.GeometryPart)(comboBox.SelectedItem as Context.GeometryPart?);
            UpdateRenderer();
        }

        private void OnChangeTracking(object sender, RoutedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            _mapper.CurrentSetting.Tracking = (Context.DisplayTracking)(comboBox.SelectedItem as Context.DisplayTracking?);
            UpdateRenderer();
        }

        private void OnChangeSlice0(object sender, RoutedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            _mapper.CurrentSetting.SliceTimeMain = (int)(comboBox.SelectedItem as int?);
            UpdateRenderer();
        }

        private void OnChangeSlice1(object sender, RoutedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            _mapper.CurrentSetting.SliceTimeReference = (int)(comboBox.SelectedItem as int?);
            UpdateRenderer();
        }

        private void OnChangeMember0(object sender, RoutedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            _mapper.CurrentSetting.MemberMain = (int)(comboBox.SelectedItem as int?);
            UpdateRenderer();
        }

        private void OnChangeMember1(object sender, RoutedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            _mapper.CurrentSetting.MemberReference = (int)(comboBox.SelectedItem as int?);
            UpdateRenderer();
        }

        private void OnChangeHeight(object sender, RoutedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            _mapper.CurrentSetting.SliceHeight = (int)(comboBox.SelectedItem as int?);
            UpdateRenderer();
        }

        private void OnChangeIntegrator(object sender, RoutedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            _mapper.CurrentSetting.IntegrationType = (VectorField.Integrator.Type)(comboBox.SelectedItem as VectorField.Integrator.Type?);
            UpdateRenderer();
        }

        private void OnChangeCore(object sender, RoutedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            _mapper.CurrentSetting.Core = (DataMapper.CoreAlgorithm)(comboBox.SelectedItem as DataMapper.CoreAlgorithm?);
            UpdateRenderer();
        }

        // ~~~~~~~~~~~ Sliders ~~~~~~~~~~~~~ \\
        private void OnChangeStepSize(object sender, RoutedEventArgs e)
        {
            var slider = sender as Slider;
            _mapper.CurrentSetting.StepSize = (float)Math.Max(0.000000001f, (float)(slider.Value as double?));
            UpdateRenderer();
        }
        private void OnChangeIntegrationTime(object sender, RoutedEventArgs e)
        {
            var slider = sender as Slider;
            _mapper.CurrentSetting.IntegrationTime = (float)(slider.Value as double?);
            UpdateRenderer();
        }

        private void OnChangeAlphaFFF(object sender, RoutedEventArgs e)
        {
            var slider = sender as Slider;
            _mapper.CurrentSetting.AlphaStable = (float)(slider.Value as double?);
            UpdateRenderer();
        }

        private void OnChangeVerticalLine(object sender, RoutedEventArgs e)
        {
            var slider = sender as Slider;
            _mapper.CurrentSetting.LineX = (int)((slider.Value as double?) + 0.5);
            UpdateRenderer();
        }

        private void OnChangeShader(object sender, RoutedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            _mapper.CurrentSetting.Shader = (FieldPlane.RenderEffect)(comboBox.SelectedItem as FieldPlane.RenderEffect?);
            UpdateRenderer();
        }

        private void OnChangeColormap(object sender, RoutedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            Colormap map = (Colormap)(comboBox.SelectedItem as Colormap?);
            _mapper.CurrentSetting.Colormap = map;

            // Load a new image. It is already in the VRAM, but small.
            BitmapImage b = new BitmapImage();
            b.BeginInit();
            string dir = Directory.GetCurrentDirectory();
            b.UriSource = new Uri(dir + "/Framework/Renderer/Resources/Colormap" + map.ToString() + ".png");
            b.EndInit();

            // ... Assign Source.
            ColormapView.Source = b;

            UpdateRenderer();
        }

        private void OnChangeWindowWidth(object sender, RoutedEventArgs e)
        {
            var slider = sender as Slider;
            _mapper.CurrentSetting.WindowWidth = (float)slider.Value;
            UpdateRenderer();
        }

        private void OnCheckFlat(object sender, RoutedEventArgs e)
        {
            CheckBox box = (sender as CheckBox);
            _mapper.CurrentSetting.Flat = box.IsChecked ?? true ? Sign.POSITIVE : Sign.NEGATIVE;
            UpdateRenderer();
        }

        private void OnCheckGraph(object sender, RoutedEventArgs e)
        {
            CheckBox box = (sender as CheckBox);
            _mapper.CurrentSetting.Graph = box.IsChecked ?? true ? Sign.POSITIVE : Sign.NEGATIVE;
            UpdateRenderer();
        }


        private void StartX_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void OnChangeWindowStart(object sender, RoutedEventArgs e)
        {
            var slider = sender as Slider;
            _mapper.CurrentSetting.WindowStart = (float)slider.Value;
            UpdateRenderer();
        }

        private void OnChangeMatrix(object sender, RoutedEventArgs e)
        {
            if (_mapper == null) return;

            _mapper.CurrentSetting.StartX = Convert.ToSingle(StartX?.Text ?? "0");
            _mapper.CurrentSetting.StartY = Convert.ToSingle(StartY?.Text ?? "0");
            _mapper.CurrentSetting.EndX = Convert.ToSingle(EndX?.Text ?? "1");
            _mapper.CurrentSetting.EndY = Convert.ToSingle(EndY?.Text ?? "1");
            _mapper.CurrentSetting.DimX = (int)Convert.ToSingle(DimX?.Text ?? "2");
            _mapper.CurrentSetting.DimY = (int)Convert.ToSingle(DimY?.Text ?? "2");
            _mapper.CurrentSetting.VarX = ((VarX?.SelectedValue as DataMapper.Setting.Element?)?? DataMapper.Setting.Element.IntegrationTime);
            _mapper.CurrentSetting.VarY = ((VarY?.SelectedValue as DataMapper.Setting.Element?) ?? DataMapper.Setting.Element.AlphaStable);
        }
        #endregion


        private void UpdateRenderer()
        {
            // Enable/disable GUI elements.
            if(_mapperChanged)
                foreach (DataMapper.Setting.Element element in Enum.GetValues(typeof(DataMapper.Setting.Element)))  // (int element = 0; element < _windowObjects.Length; ++element)
                {
                    bool elementActive = _mapper.IsUsed(element);
                    _windowObjects[(int)element].Visibility = elementActive ? Visibility.Visible : Visibility.Hidden;

                    TextBlock[] text = (_windowObjects[(int)element] as Panel)?.Children.OfType<TextBlock>().ToArray() ;
                    if (text != null && text.Length > 0)
                        text[0].Text = _mapper.GetName(element);
                    else if(_windowObjects[(int)element] as CheckBox != null)
                        (_windowObjects[(int)element] as CheckBox).Content = _mapper.GetName(element);

                    _mapperChanged = false;
                }

            if (Renderer.Singleton.Initialized && _mapper != null)
                Context.Singleton.Update();
        }

        private void ActivateCamera(object sender, MouseEventArgs e)
        {
            Renderer.Singleton.Camera.Active = true;
            //foreach (FrameworkElement elem in _windowObjects)
                DX11Display.Focus();
        }

        private void DeactivateCamera(object sender, MouseEventArgs e)
        {
            Renderer.Singleton.Camera.Active = false;
        }

        private void ChangeProjection(object sender, RoutedEventArgs e)
        {
            bool ortho = orthographic.IsChecked ?? false;
            if (ortho)
                Renderer.Singleton.Camera.SetOrthographic();
            else
                Renderer.Singleton.Camera.SetPerspective();
        }
    }
}
