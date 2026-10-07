using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>A 2D affine transform: x' = a x + b y + tx, y' = c x + d y + ty.</summary>
    public struct Affine2
    {
        public float A, B, C, D, Tx, Ty;

        public static Affine2 Identity => new() { A = 1f, D = 1f };

        public static Affine2 Translate(Vector2 t) => new() { A = 1f, D = 1f, Tx = t.x, Ty = t.y };

        /// <summary>Scale <paramref name="scale"/> and turn <paramref name="degrees"/> clockwise (y down) around <paramref name="pivot"/>.</summary>
        public static Affine2 AroundPivot(Vector2 pivot, float scale, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians) * scale, sin = Mathf.Sin(radians) * scale;
            // y down, so a positive angle turns clockwise on screen.
            var m = new Affine2 { A = cos, B = -sin, C = sin, D = cos };
            m.Tx = pivot.x - (m.A * pivot.x + m.B * pivot.y);
            m.Ty = pivot.y - (m.C * pivot.x + m.D * pivot.y);
            return m;
        }

        /// <summary><paramref name="a"/> after <paramref name="b"/>: applies b, then a.</summary>
        public static Affine2 operator *(Affine2 a, Affine2 b) => new()
        {
            A = a.A * b.A + a.B * b.C,
            B = a.A * b.B + a.B * b.D,
            C = a.C * b.A + a.D * b.C,
            D = a.C * b.B + a.D * b.D,
            Tx = a.A * b.Tx + a.B * b.Ty + a.Tx,
            Ty = a.C * b.Tx + a.D * b.Ty + a.Ty,
        };

        public Vector2 Apply(Vector2 p) => new(A * p.x + B * p.y + Tx, C * p.x + D * p.y + Ty);

        public Vector2 ApplyVector(Vector2 v) => new(A * v.x + B * v.y, C * v.x + D * v.y);

        public float Determinant => A * D - B * C;

        public Affine2 Inverse
        {
            get
            {
                float det = Determinant;
                if (Mathf.Abs(det) < 1e-12f) return new Affine2();
                float inv = 1f / det;
                var m = new Affine2 { A = D * inv, B = -B * inv, C = -C * inv, D = A * inv };
                m.Tx = -(m.A * Tx + m.B * Ty);
                m.Ty = -(m.C * Tx + m.D * Ty);
                return m;
            }
        }

        /// <summary>How long a unit along x and along y comes out, on average: the scale it draws at.</summary>
        public float ScaleFactor => Mathf.Sqrt(Mathf.Abs(Determinant));
    }
}
