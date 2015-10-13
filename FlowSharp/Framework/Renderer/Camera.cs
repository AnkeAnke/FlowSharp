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

namespace FlowSharp
{
    class Camera
    {
        protected Buffer _globalConstants;
        public Buffer ConstantBuffer { get { return _globalConstants; } }
        /// <summary>
        /// Were the globals ever touched since the last update?
        /// </summary>
        protected bool _changed = false;
        protected Constants _globals;
        public Matrix View
        {
            get { return _globals.View; }
            set { _globals.View = value;  _changed = true; }
        }

        public Matrix Projection
        {
            get { return _globals.Projection; }
            set { _globals.Projection = value; _changed = true; }
        }

        public Camera(Device device, float aspectRatio)
        {
            _globals = new Constants();
            View = Matrix.LookAtLH(new Vector3(0, 0, -20f), Vector3.Zero, Vector3.UnitY);
            Projection = Matrix.PerspectiveFovLH(1.3f, aspectRatio, 0.0001f, 100000);

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

        private Vector3 viewDirection = new Vector3(0, 0, 1);
        private Vector3 position = new Vector3(0, 0, -20);

        private Keyboard keyboard;
        private Mouse mouse;
        /// <summary>
        /// Updates the Camera 
        /// </summary>
        public void Update(float passedTimeSinceLastFrame, Device device)
        {
            // Map mouse movement to angles.
            UpdateThetaPhiFromMouse(passedTimeSinceLastFrame);

            // COmpute view direction from angles.
            viewDirection = new Vector3((float)(System.Math.Cos(phi) * System.Math.Sin(theta)),
                                        (float)(System.Math.Cos(theta)),
                                        (float)(System.Math.Sin(phi) * System.Math.Sin(theta)));
            // Compute up vector.
            float theta2 = (float)theta + (float)System.Math.PI / 2.0f;
            Vector3 upVec = new Vector3((float)(System.Math.Cos(phi) * System.Math.Sin(theta2)),
                                        (float)(System.Math.Cos(theta2)),
                                        (float)(System.Math.Sin(phi) * System.Math.Sin(theta2)));
            Vector3 Right = Vector3.Cross(upVec, viewDirection);


            {
                KeyboardState state = keyboard.GetCurrentState();
                // Forward movement.
                float forward = (state.IsPressed(Key.W) ? 1.0f : 0.0f) - (state.IsPressed(Key.S) ? 1.0f : 0.0f);
                position += forward * forwardSpeed * viewDirection;

                // Side movement.
                float side = (state.IsPressed(Key.D) ? 1.0f : 0.0f) - (state.IsPressed(Key.A) ? 1.0f : 0.0f);
                position += side * sideSpeed * Right;

                // Upward movement.
                float up = state.IsPressed(Key.Space) ? 1.0f : 0.0f;
                position += up * upSpeed * upVec;
            }


            // Compute view matrix.
            View = Matrix.LookAtLH(position, position + viewDirection, upVec);
            UpdateResources(device);
        }

        /// <summary>
        /// intern helper to update view angles by mouse
        /// </summary>
        protected void UpdateThetaPhiFromMouse(float passedTimeSinceLastFrame)
        {
            MouseState stateMouse = mouse.GetCurrentState();
            KeyboardState stateKeyboard = keyboard.GetCurrentState();
            if (stateMouse.IsPressed(0))
            {
                // mouse movement
                double deltaX = Cursor.Position.X - lastMouseX;
                double deltaY = Cursor.Position.Y - lastMouseY;
                phi -= deltaX * rotationSpeed;
                theta -= deltaY * rotationSpeed;
            }
            else
            {
                theta += (stateKeyboard.IsPressed(Key.UpArrow) ? rotationSpeed * passedTimeSinceLastFrame * 0.3f : 0.0f);
                theta -= (stateKeyboard.IsPressed(Key.DownArrow) ? rotationSpeed * passedTimeSinceLastFrame * 0.3f : 0.0f);
                phi -= (stateKeyboard.IsPressed(Key.RightArrow) ? rotationSpeed * passedTimeSinceLastFrame * 0.3f : 0.0f);
                phi += (stateKeyboard.IsPressed(Key.LeftArrow) ? rotationSpeed * passedTimeSinceLastFrame * 0.3f : 0.0f);
            }


            lastMouseX = Cursor.Position.X;
            lastMouseY = Cursor.Position.Y;
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
