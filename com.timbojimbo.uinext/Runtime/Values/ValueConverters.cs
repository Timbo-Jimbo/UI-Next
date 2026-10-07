using System;
using System.Collections.Generic;
using UnityEngine;

namespace TimboJimbo.UINext
{
    /// <summary>
    /// Turns a value into up to four floats a spring can move, and back, as Compose's TwoWayConverter and SwiftUI's
    /// VectorArithmetic do. A type with no converter does not interpolate: inside a change it snaps (or a drawable
    /// cross-fades it).
    /// </summary>
    public interface IValueConverter<T>
    {
        /// <summary>How many of the four floats it uses.</summary>
        int Dimensions { get; }

        /// <summary>How close to its target, in converted units, a spring has to be to count as there.</summary>
        float Rest { get; }

        Vector4 ToVector(T value);

        T FromVector(Vector4 vector);
    }

    /// <summary>The converters for the value types the stack animates. Add one for a type of your own with <see cref="Register{T}"/>.</summary>
    public static class ValueConverters
    {
        private static readonly Dictionary<Type, object> s_converters = new()
        {
            { typeof(float), new FloatConverter() },
            { typeof(Vector2), new Vector2Converter() },
            { typeof(Vector3), new Vector3Converter() },
            { typeof(Vector4), new Vector4Converter() },
            { typeof(Color), new ColorConverter() },
            { typeof(Rect), new RectConverter() },
            { typeof(Insets), new InsetsConverter() },
            { typeof(CornerRadii), new CornerRadiiConverter() },
        };

        public static void Register<T>(IValueConverter<T> converter) => s_converters[typeof(T)] = converter;

        /// <summary>The converter for <typeparamref name="T"/>, or null when it does not interpolate.</summary>
        public static IValueConverter<T> Get<T>() => s_converters.TryGetValue(typeof(T), out var converter) ? (IValueConverter<T>)converter : null;

        private sealed class FloatConverter : IValueConverter<float>
        {
            public int Dimensions => 1;
            public float Rest => 0.001f;
            public Vector4 ToVector(float value) => new(value, 0f, 0f, 0f);
            public float FromVector(Vector4 vector) => vector.x;
        }

        private sealed class Vector2Converter : IValueConverter<Vector2>
        {
            public int Dimensions => 2;
            public float Rest => 0.01f;
            public Vector4 ToVector(Vector2 value) => new(value.x, value.y, 0f, 0f);
            public Vector2 FromVector(Vector4 vector) => new(vector.x, vector.y);
        }

        private sealed class Vector3Converter : IValueConverter<Vector3>
        {
            public int Dimensions => 3;
            public float Rest => 0.01f;
            public Vector4 ToVector(Vector3 value) => new(value.x, value.y, value.z, 0f);
            public Vector3 FromVector(Vector4 vector) => new(vector.x, vector.y, vector.z);
        }

        private sealed class Vector4Converter : IValueConverter<Vector4>
        {
            public int Dimensions => 4;
            public float Rest => 0.01f;
            public Vector4 ToVector(Vector4 value) => value;
            public Vector4 FromVector(Vector4 vector) => vector;
        }

        private sealed class RectConverter : IValueConverter<Rect>
        {
            public int Dimensions => 4;
            public float Rest => 0.5f;
            public Vector4 ToVector(Rect value) => new(value.x, value.y, value.width, value.height);
            public Rect FromVector(Vector4 vector) => new(vector.x, vector.y, vector.z, vector.w);
        }

        private sealed class InsetsConverter : IValueConverter<Insets>
        {
            public int Dimensions => 4;
            public float Rest => 0.5f;
            public Vector4 ToVector(Insets value) => new(value.Left, value.Right, value.Top, value.Bottom);
            public Insets FromVector(Vector4 vector) => new(vector.x, vector.y, vector.z, vector.w);
        }

        private sealed class CornerRadiiConverter : IValueConverter<CornerRadii>
        {
            public int Dimensions => 4;
            public float Rest => 0.1f;
            public Vector4 ToVector(CornerRadii value) => new(value.TopLeft, value.TopRight, value.BottomRight, value.BottomLeft);
            public CornerRadii FromVector(Vector4 vector) => new(vector.x, vector.y, vector.z, vector.w);
        }

        // Colours move in OkLab, a perceptually even space, in true linear light: a Color holds sRGB values, so they
        // are taken to linear first and back after, as Compose animates colours.
        private sealed class ColorConverter : IValueConverter<Color>
        {
            public int Dimensions => 4;
            public float Rest => 0.002f;
            public Vector4 ToVector(Color value) => OkLab.FromColor(value);
            public Color FromVector(Vector4 vector) => OkLab.ToColor(vector);
        }
    }

    /// <summary>OkLab (Björn Ottosson's perceptual colour space), from and to sRGB colours, through linear light.</summary>
    public static class OkLab
    {
        /// <summary>An sRGB colour as OkLab L, a, b, with its alpha in w.</summary>
        public static Vector4 FromColor(Color color)
        {
            var linear = color.linear;
            float l = 0.4122214708f * linear.r + 0.5363325363f * linear.g + 0.0514459929f * linear.b;
            float m = 0.2119034982f * linear.r + 0.6806995451f * linear.g + 0.1073969566f * linear.b;
            float s = 0.0883024619f * linear.r + 0.2817188376f * linear.g + 0.6299787005f * linear.b;
            l = Cbrt(l);
            m = Cbrt(m);
            s = Cbrt(s);
            return new Vector4(
                0.2104542553f * l + 0.7936177850f * m - 0.0040720468f * s,
                1.9779984951f * l - 2.4285922050f * m + 0.4505937099f * s,
                0.0259040371f * l + 0.7827717662f * m - 0.8086757660f * s,
                color.a);
        }

        /// <summary>OkLab L, a, b (alpha in w) as an sRGB colour, clamped into range.</summary>
        public static Color ToColor(Vector4 lab)
        {
            float l = lab.x + 0.3963377774f * lab.y + 0.2158037573f * lab.z;
            float m = lab.x - 0.1055613458f * lab.y - 0.0638541728f * lab.z;
            float s = lab.x - 0.0894841775f * lab.y - 1.2914855480f * lab.z;
            l = l * l * l;
            m = m * m * m;
            s = s * s * s;
            var linear = new Color(
                Mathf.Clamp01(4.0767416621f * l - 3.3077115913f * m + 0.2309699292f * s),
                Mathf.Clamp01(-1.2684380046f * l + 2.6097574011f * m - 0.3413193965f * s),
                Mathf.Clamp01(-0.0041960863f * l - 0.7034186147f * m + 1.7076147010f * s),
                Mathf.Clamp01(lab.w));
            return linear.gamma;
        }

        private static float Cbrt(float x) => x < 0f ? -Mathf.Pow(-x, 1f / 3f) : Mathf.Pow(x, 1f / 3f);
    }
}
