﻿using AcTools.Render.Base.Utils;
using SlimDX;

namespace AcTools.Render.Base.Cameras {
    public delegate float HeightFunc(float x, float y);

    public class FpsCamera : BaseCamera {
        public FpsCamera(float fov) : base(fov) {}

        public override void Save() {
            throw new System.NotImplementedException();
        }

        public override void Restore() {
            throw new System.NotImplementedException();
        }

        public override BaseCamera Clone() {
            throw new System.NotImplementedException();
        }

        public override void LookAt(Vector3 pos, Vector3 target, Vector3 up) {
            Position = pos;
            Look = Vector3.Normalize(target - pos);
            Right = Vector3.Normalize(Vector3.Cross(up, Look));
            Up = Vector3.Cross(Look, Right);
        }

        public override void Strafe(float d) {
            Position += Right * d;
        }

        public override void Walk(float d) {
            Position += Look * d;
        }

        public override void Pitch(float angle) {
            var r = Matrix.RotationAxis(Right, angle);
            Up = Vector3.TransformNormal(Up, r);
            Look = Vector3.TransformNormal(Look, r);
        }

        public override void Yaw(float angle) {
            var r = Matrix.RotationY(angle);
            Right = Vector3.TransformNormal(Right, r);
            Up = Vector3.TransformNormal(Up, r);
            Look = Vector3.TransformNormal(Look, r);
        }

        public override void Zoom(float dr) {
            FovY = MathF.Clamp(FovY + dr, MathF.PI * 0.05f, MathF.PI * 0.8f);
            SetLens(Aspect);
        }

        public override void UpdateViewMatrix() {
            View = Matrix.LookAtLH(Position, Position + Look, Vector3.UnitY);

            Right = new Vector3(View.M11, View.M21, View.M31);
            Right.Normalize();

            Look = new Vector3(View.M13, View.M23, View.M33);
            Look.Normalize();

            Frustum = Frustum.FromViewProj(ViewProj);
        }
    }
}