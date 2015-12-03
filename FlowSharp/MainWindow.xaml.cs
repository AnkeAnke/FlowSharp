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
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace FlowSharp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private DataMapper _mapper;
        //private RedSea.Display _display;
        //private RedSea.DisplayLines _displayLines;
        //private int _slice0, _slice1;
        //private FieldPlane _slice1Plane;

        private static FrameworkElement[] _windowObjects = new FrameworkElement[Enum.GetValues(typeof(DataMapper.Setting.Element)).Length];


        public MainWindow()
        {
            InitializeComponent();

            DX11Display.Scene = Renderer.Singleton;
            _mapper = new EmptyMapper();

            FSMain.LoadData();
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
            _windowObjects[(int)DataMapper.Setting.Element.IntegrationTime] = IntegrationTimeBlock;

            Renderer.Singleton.SetCanvas(DX11Display);
        }

        private void LoadDisplay(object sender, RoutedEventArgs e)
        {
            DropDownDisplay.ItemsSource = Enum.GetValues(typeof(RedSea.Display)).Cast<RedSea.Display>();
            DropDownDisplay.SelectedIndex = 0;
        }

        private void LoadDisplayLines(object sender, RoutedEventArgs e)
        {
            DropDownDisplayLines.ItemsSource = Enum.GetValues(typeof(RedSea.DisplayLines)).Cast<RedSea.DisplayLines>();
            DropDownDisplayLines.SelectedIndex = 0;
            
        }

        private void LoadSlice0(object sender, RoutedEventArgs e)
        {
            DropDownSlice0.ItemsSource = Enumerable.Range(0, 10);          
            DropDownSlice0.SelectedIndex = 3;
            _mapper.CurrentSetting.SliceTimeMain = DropDownSlice0.SelectedIndex;
        }

        private void LoadSlice1(object sender, RoutedEventArgs e)
        {
            DropDownSlice1.ItemsSource = Enumerable.Range(0, 10);
            DropDownSlice1.SelectedIndex = 5;
            _mapper.CurrentSetting.SliceTimeReference = DropDownSlice1.SelectedIndex;
        }

        private void LoadMember(object sender, RoutedEventArgs e)
        {
            //string[] values = new string[53];
            //values[0] = "Mean";
            //values[1] = "Variance";
            //for (int i = 0; i < values.Length - 2; ++i)
            //    values[i + 2] = "Member " + i;
            //(sender as ComboBox).Da = values;
            (sender as ComboBox).ItemsSource = Enumerable.Range(0, 52);
            (sender as ComboBox).SelectedIndex = 0;
        }

        private void LoadHeight(object sender, RoutedEventArgs e)
        {
            (sender as ComboBox).ItemsSource = Enumerable.Range(0, 50);
            (sender as ComboBox).SelectedIndex = 0;
        }

        private void LoadIntegrator(object sender, RoutedEventArgs e)
        {
            DropDownIntegrator.ItemsSource = Enum.GetValues(typeof(VectorField.Integrator.Type)).Cast<VectorField.Integrator.Type>();
            DropDownIntegrator.SelectedIndex = (int)VectorField.Integrator.Type.EULER; 
        }

        private void LoadMeasure(object sender, RoutedEventArgs e)
        {
            DropDownMeasure.ItemsSource = Enum.GetValues(typeof(RedSea.Measure)).Cast<RedSea.Measure>();
            DropDownMeasure.SelectedIndex = (int)RedSea.Measure.VELOCITY;
        }

        private void LoadTracking(object sender, RoutedEventArgs e)
        {
            DropDownTracking.ItemsSource = Enum.GetValues(typeof(RedSea.DisplayTracking)).Cast<RedSea.DisplayTracking>();
            DropDownTracking.SelectedIndex = (int)RedSea.DisplayTracking.LINE_POINTS;
        }

        private void LoadStepSize(object sender, RoutedEventArgs e)
        {
            //(sender as Slider).Minimum = 0.01;
            (sender as Slider).Value = 1.0;// 0.06;
            _mapper.CurrentSetting.StepSize = (float)(sender as Slider).Value;
        }

        private void LoadShader(object sender, RoutedEventArgs e)
        {
            DropDownShader.ItemsSource = Enum.GetValues(typeof(FieldPlane.RenderEffect)).Cast<FieldPlane.RenderEffect>();
            DropDownShader.SelectedIndex = (int)FieldPlane.RenderEffect.COLORMAP;
            
        }

        private void LoadColormap(object sender, RoutedEventArgs e)
        {
            DropDownColormap.ItemsSource = Enum.GetValues(typeof(Colormap)).Cast<Colormap>();
            DropDownColormap.SelectedIndex = (int)Colormap.Parula;
            
        }

        private void LoadColormapView(object sender, RoutedEventArgs e)
        {
            BitmapImage b = new BitmapImage();
            b.BeginInit();
            string dir = Directory.GetCurrentDirectory();
            b.UriSource = new Uri(dir + "/Framework/Renderer/Resources/Colormap" + "Parula" + ".png"); 
            b.EndInit();

            // ... Get Image reference from sender.
            var image = sender as Image;
            // ... Assign Source.
            image.Source = b;
        }

        #region OnChangeValue
        // ~~~~~~~~~~ On change callbacks ~~~~~~~~~~~ \\
        private void OnChangeDisplay(object sender, RoutedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            DataMapper last = _mapper;
            _mapper = RedSea.Singleton.SetMapper((RedSea.Display)(comboBox.SelectedItem as RedSea.Display?));
            _mapper.CurrentSetting = new DataMapper.Setting(last.CurrentSetting);

            //AlphaStable = (float)alpha.Value;
            //_mapper.CurrentSetting.IntegrationType = (VectorField.Integrator.Type)DropDownIntegrator.SelectedIndex;
            //_mapper.CurrentSetting.LineSetting = (RedSea.DisplayLines)DropDownDisplayLines.SelectedIndex;
            //_mapper.CurrentSetting.SliceTimeMain = DropDownSlice0.SelectedIndex;
            //_mapper.CurrentSetting.SliceTimeReference = DropDownSlice1.SelectedIndex;
            //_mapper.CurrentSetting.StepSize = (float)step.Value / 10f;
            //_display = (RedSea.Display)(comboBox.SelectedItem as RedSea.Display?);
            UpdateRenderer();
        }

        private void OnChangeDisplayLines(object sender, RoutedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            _mapper.CurrentSetting.LineSetting = (RedSea.DisplayLines)(comboBox.SelectedItem as RedSea.DisplayLines?);
            UpdateRenderer();
        }

        private void OnChangeMeasure(object sender, RoutedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            _mapper.CurrentSetting.Measure = (RedSea.Measure)(comboBox.SelectedItem as RedSea.Measure?);
            UpdateRenderer();
        }

        private void OnChangeTracking(object sender, RoutedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            _mapper.CurrentSetting.Tracking = (RedSea.DisplayTracking)(comboBox.SelectedItem as RedSea.DisplayTracking?);
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

        // ~~~~~~~~~~~ Sliders ~~~~~~~~~~~~~ \\
        private void OnChangeStepSize(object sender, RoutedEventArgs e)
        {
            var slider = sender as Slider;
            _mapper.CurrentSetting.StepSize = (float)(slider.Value as double?);
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
        private void OnChangeWindowStart(object sender, RoutedEventArgs e)
        {
            var slider = sender as Slider;
            _mapper.CurrentSetting.WindowStart = (float)slider.Value;
            UpdateRenderer();
        }
        #endregion

        private void UpdateRenderer()
        {
            // Enable/disable GUI elements.
            foreach (DataMapper.Setting.Element element in Enum.GetValues(typeof(DataMapper.Setting.Element)))  // (int element = 0; element < _windowObjects.Length; ++element)
            {
                bool elementActive = _mapper.IsUsed(element);
                _windowObjects[(int)element].Visibility = elementActive ? Visibility.Visible : Visibility.Hidden;
            }

            if (Renderer.Singleton.Initialized && _mapper != null)
                RedSea.Singleton.Update();
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
    }
}
