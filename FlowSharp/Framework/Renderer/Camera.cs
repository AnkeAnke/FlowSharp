using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SlimDX;
using SlimDX.Direct3D11;
using Buffer = SlimDX.Direct3D11.Buffer;
using Device = SlimDX.Direct3D11.Device;
using System.Runtime.InteropServices;
using SlimDX.DirectInput;
using System.Windows.Forms;
using WPFHost;

namespace FlowSharp
{
#if true
    using Context = Aneurysm;
#endif

    class Camera
    {
        protected Buffer _globalConstants;
        public Buffer ConstantBuffer { get { return _globalConstants; } }
        /// <summary>
        /// Were the globals ever touched since the last update?
        /// </summary>
        protected bool _changed = false;
        protected Constants _globals;
        protected Device _device;
        public Matrix View
        {
            get { return _globals.View; }
            set { _globals.View = value;
                if (_device != null)
                    UpdateResources(_device); }//_changed = true; }
        }

        public Matrix Projection
        {
            get { return _globals.Projection; }
            set { _globals.Projection = value;
                if (_device != null)
                    UpdateResources(_device); }//_changed = true; }
        }
        public bool Active = true;

        private float _fov;
        /// <summary>
        /// Width / Height.
        /// </summary>
        private float _aspect;

        public Camera(Device device, float aspectRatio)
        {
            _globals = new Constants();
            _aspect = aspectRatio;
            _fov = 1.3f;
            View = Matrix.LookAtLH(new Vector3(0, 0, -10f), Vector3.Zero, Vector3.UnitY);
            SetPerspective();


            _device = device;
            //Projection = Matrix.PerspectiveFovLH(_fov, aspectRatio, 0.0001f, 100000);
            //Projection = Matrix.OrthoLH(10 * _aspect, 10, 0.0001f, 100000);

            var data = new DataStream(Marshal.SizeOf(typeof(Constants)), true, true);
            data.Write(_globals);
            data.Position = 0;

            _globalConstants = new Buffer(device, //Device
                data, //Stream
                Marshal.SizeOf(typeof(Constants)), // Size
                ResourceUsage.Default,
                BindFlags.ConstantBuffer,
                CpuAccessFlags.None,
                ResourceOptionFlags.None,
                64);

            device.ImmediateContext.PixelShader.SetConstantBuffer(_globalConstants, 0);


            DirectInput input = new DirectInput();
            keyboard = new Keyboard(input);
            mouse = new Mouse(input);
            keyboard.Acquire();
            mouse.Acquire();
            lastMouseX = Cursor.Position.X;
            lastMouseY = Cursor.Position.Y;

            ResetCamera();
        }

        /// <summary>
        /// Updates the constants on GPU side.
        /// </summary>
        /// <param name="device"></param>
        public void UpdateResources(Device device)
        {
            var data = new DataStream(Marshal.SizeOf(typeof(Constants)), true, true);
            data.Write(_globals);
            data.Position = 0;

            device.ImmediateContext.UpdateSubresource(new DataBox(0, 0, data), _globalConstants, 0);
            //device.ImmediateContext.VertexShader.SetConstantBuffer(_globalConstants, 0);
        }

        public void SetOrthographic()
        {
            Projection = Matrix.OrthoLH(20 * _aspect, 20, 0.0001f, 100000);//4
        }
        public void SetPerspective()
        { 
            Projection = Matrix.PerspectiveFovLH(_fov, _aspect, 0.0001f, 100000);
        }

        // Movement factors.
        private readonly float rotationSpeed = 0.001f;
        private readonly float forwardSpeed = 0.3f;
        private readonly float sideSpeed = 0.3f;
        private readonly float upSpeed = 0.2f;

        // Intern camera state variables.
        private double phi = -Math.PI / 2;
        private double theta = -Math.PI / 2;
        private double lastMouseX = 0;
        private double lastMouseY = 0;

        private Vector2? _setMouseRelative = null;
        private Vector2 _releaseMouseRelative = new Vector2();

        private Vector3 viewDirection = new Vector3(0, 0, 1);
        private Vector3 position = new Vector3(0, 0, -20);

        // Save each time when computing.
        private Vector3 upVec, rightVec;

        private Keyboard keyboard;
        private Mouse mouse;
        /// <summary>
        /// Updates the Camera 
        /// </summary>
        public void Update(float passedTimeSinceLastFrame, Device device, DPFCanvas canvas)
        {
            KeyboardState state = keyboard.GetCurrentState();
            if (state.IsPressed(Key.R))
                ResetCamera();

            if (state.IsPressed(Key.P))
            {

                Context.Singleton.WPFWindow.Screenshot(Context.Singleton.SnapFileName + "Snape_" + Context.Singleton.EnsightFilename + ".png");
            }

            // Map mouse movement to angles.
            UpdateThetaPhiFromMouse(passedTimeSinceLastFrame, canvas);

            // Compute view direction from angles.
            viewDirection = new Vector3((float)(System.Math.Cos(phi) * System.Math.Sin(theta)),
                                        (float)(System.Math.Cos(theta)),
                                        (float)(System.Math.Sin(phi) * System.Math.Sin(theta)));
            // Compute up vector.
            float theta2 = (float)theta + (float)System.Math.PI / 2.0f;
            upVec = new Vector3((float)(System.Math.Cos(phi) * System.Math.Sin(theta2)),
                                (float)(System.Math.Cos(theta2)),
                                (float)(System.Math.Sin(phi) * System.Math.Sin(theta2)));
            rightVec = Vector3.Cross(upVec, viewDirection);

            // Update.
            // Forward movement.
            float forward = (state.IsPressed(Key.W) ? 1.0f : 0.0f) - (state.IsPressed(Key.S) ? 1.0f : 0.0f);
            position += forward * forwardSpeed * viewDirection;

            // Side movement.
            float side = (state.IsPressed(Key.D) ? 1.0f : 0.0f) - (state.IsPressed(Key.A) ? 1.0f : 0.0f);
            position += side * sideSpeed * rightVec;

            // Upward movement.
            float up = state.IsPressed(Key.Space) ? 1.0f : 0.0f - (state.IsPressed(Key.LeftAlt) ? 1.0f : 0.0f);
            position += up * upSpeed * upVec;


            // Compute view matrix.
            View = Matrix.LookAtLH(position, position + viewDirection, upVec);
            UpdateResources(device);
        }

        /// <summary>
        /// intern helper to update view angles by mouse
        /// </summary>
        protected void UpdateThetaPhiFromMouse(float passedTimeSinceLastFrame, DPFCanvas canvas)
        {
            MouseState stateMouse = mouse.GetCurrentState();
            KeyboardState stateKeyboard = keyboard.GetCurrentState();

            // Left button pressed. Perform plane intersection.
            if (stateMouse.IsPressed(0))
            {
                System.Windows.Point mouse = System.Windows.Input.Mouse.GetPosition(canvas);
                _releaseMouseRelative.X = (float)((2 * mouse.X - canvas.ActualWidth) / canvas.ActualWidth);
                _releaseMouseRelative.Y = (float)((2 * mouse.Y - canvas.ActualHeight) / canvas.ActualHeight);

                //Console.WriteLine(_releaseMouseRelative.ToString());
                if (_setMouseRelative == null)
                {
                    _setMouseRelative = _releaseMouseRelative;
                }

                Context.Singleton.UpdateSelection();
            }
            // Mouse wheel pressed. Move camera.
            else if (_setMouseRelative != null)
            {
                Context.Singleton.EndSelection();
                _setMouseRelative = null;
            }

            if (stateMouse.IsPressed(2))
            {
                // mouse movement
                double deltaX = Cursor.Position.X - lastMouseX;
                double deltaY = Cursor.Position.Y - lastMouseY;
                phi -= deltaX * rotationSpeed;
                theta -= deltaY * rotationSpeed;
            }
            else
            {
                //theta += (stateKeyboard.IsPressed(Key.UpArrow) ? rotationSpeed * passedTimeSinceLastFrame * 0.3f : 0.0f);
                //theta -= (stateKeyboard.IsPressed(Key.DownArrow) ? rotationSpeed * passedTimeSinceLastFrame * 0.3f : 0.0f);
                //phi -= (stateKeyboard.IsPressed(Key.RightArrow) ? rotationSpeed * passedTimeSinceLastFrame * 0.3f : 0.0f);
                //phi += (stateKeyboard.IsPressed(Key.LeftArrow) ? rotationSpeed * passedTimeSinceLastFrame * 0.3f : 0.0f);
            }

            lastMouseX = Cursor.Position.X;
            lastMouseY = Cursor.Position.Y;
        }

        public void UpdateInactive()
        {
            lastMouseX = Cursor.Position.X;
            lastMouseY = Cursor.Position.Y;
        }

        protected void ResetCamera()
        {
            phi = -Math.PI / 2;
            theta = -Math.PI / 2;
            lastMouseX = 0;
            lastMouseY = 0;

            viewDirection = new Vector3(0, 0, 1);
            position = new Vector3(0, 6.8f, -19);
        }

        public Vector2[] IntersectPlane(Plane plane)
        {
            if (_setMouseRelative == null || _releaseMouseRelative.X < -1 || _releaseMouseRelative.X > 1 || _releaseMouseRelative.Y < -1 || _releaseMouseRelative.Y > 1)
                return null;
            return new Vector2[] { IntersectPlane(plane, (Vector2)_setMouseRelative), IntersectPlane(plane, _releaseMouseRelative) };
        }

        public Vector2 IntersectPlane(Plane plane, Vector2 relativeMousePos)
        {
            // Relative screen position.
            float xRayView = relativeMousePos.X / Projection.M11;
            float yRayView = relativeMousePos.Y / Projection.M22;
            viewDirection.Normalize();
            Vector3 ray = viewDirection
                + rightVec * xRayView
                - upVec * yRayView;
            Vector4 pointVec = new Vector4(position - plane.Origin, 0);

            Matrix dirMat = new Matrix();
            Matrix test = Matrix.Translation(1, 1, 1);
            //var x = test.get_Rows(3);
            dirMat.set_Rows(0, new Vector4(plane.XAxis, 0));
            dirMat.set_Rows(1, new Vector4(plane.YAxis, 0));
            dirMat.set_Rows(2, new Vector4(-ray, 0));
            dirMat.set_Rows(3, Vector4.UnitW);
            dirMat.Invert();

            Vector4 res = Vector4.Transform(pointVec, dirMat);
            Vector2 xy = new Vector2(Vector4.Dot(dirMat.get_Rows(0), pointVec), Vector4.Dot(dirMat.get_Rows(1), pointVec));
            return new Vector2(res.X, res.Y);
        }



        [StructLayout(LayoutKind.Explicit)]
        public struct Constants
        {
            [FieldOffset(0)]
            public Matrix View;
            [FieldOffset(64)]
            public Matrix Projection;
        }
    }
}
