namespace SamLib.Math
{
    using System;

    public struct Vec2
    {
        public float X;
        public float Y;

        public Vec2(float x, float y)
        {
            X = x;
            Y = y;
        }

        // Statics 
        public static Vec2 Zero => new Vec2(0f, 0f);
        public static Vec2 One => new Vec2(1f, 1f);
        public static Vec2 UnitX => new Vec2(1f, 0f);
        public static Vec2 UnitY => new Vec2(0f, 1f);
        #region Methods

        /// <summary>
        /// Returns the total magnitude (hypotenuse) of the vector using Pythagoras.
        /// </summary>
        public float Length => (float)Math.Sqrt(X * X + Y * Y);

        /// <summary>
        /// Resizes the vector so its length is exactly 1, but keeps the direction.
        /// </summary>
        public void Normalize()
        {
            float len = Length;
            if (len > 0)
            {
                X /= len;
                Y /= len;
            }
        }

        /// <summary>
        /// Returns a NEW Resized vector so its length is exactly 1, but keeps the direction.
        /// </summary>
        /// <returns></returns>
        public Vec2 Normalized()
        {
            float len = Length;
            if (len == 0) return new Vec2(0, 0);
            return new Vec2(X / len, Y / len);
        }

        /// <summary>
        /// Straight line distance between 2 vectors
        /// </summary>
        /// <param name="value1"></param>
        /// <param name="value2"></param>
        /// <returns></returns>
        public static float Distance(Vec2 value1, Vec2 value2)
        {
            float v1 = value1.X - value2.X;
            float v2 = value1.Y - value2.Y;
            return (float)Math.Sqrt((v1 * v1) + (v2 * v2));
        }

        /// <summary>
        /// Non rooted distance
        /// </summary>
        /// <param name="value1"></param>
        /// <param name="value2"></param>
        /// <returns></returns>
        public static float DistanceSquared(Vec2 value1, Vec2 value2)
        {
            float v1 = value1.X - value2.X;
            float v2 = value1.Y - value2.Y;
            return (v1 * v1) + (v2 * v2);
        }

        /// <summary>
        /// Multiplies components to return a single number representing directional alignment.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns> Result > 0: They point in the same general direction. Result = 0: They are perfectly perpendicular (90°). Result< 0: They point away from each other.</returns>
        public static float Dot(Vec2 a, Vec2 b)
        {
            return (a.X * b.X) + (a.Y * b.Y);
        }

        /// <summary>
        /// Bounces a vector off a surface based on that surface's "Normal" (face direction).
        /// </summary>
        /// <param name="vector"></param>
        /// <param name="normal"></param>
        /// <returns></returns>
        public static Vec2 Reflect(Vec2 vector, Vec2 normal)
        {
            float dot = Dot(vector, normal);
            return vector - (2 * dot * normal);
        }

        /// <summary>
        /// Linearly interpolates between two points based on a percentage (0 to 1).
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="amount"></param>
        /// <returns></returns>
        public static Vec2 Lerp(Vec2 start, Vec2 end, float amount)
        {
            return new Vec2(
                start.X + (end.X - start.X) * amount,
                start.Y + (end.Y - start.Y) * amount
            );
        }

        /// <summary>
        /// Forces the X and Y values to stay within a defined minimum and maximum range.
        /// </summary>
        /// <param name="min"></param>
        /// <param name="max"></param>
        public void Clamp(Vec2 min, Vec2 max)
        {
            X = Math.Max(min.X, Math.Min(max.X, X));
            Y = Math.Max(min.Y, Math.Min(max.Y, Y));
        }
        
        /// <summary>
        /// Normalised forward direction based on rotation
        /// </summary>
        /// <param name="rotation">Rotation in rads</param>
        /// <returns></returns>
        public Vec2 Forward(float rotation)
        {
            return new Vec2((float)Math.Cos(rotation), (float)Math.Sin(rotation));
        }
            
        public Vec2 Back(float rotation)
        {
            return Forward(rotation + (float)Math.PI);
        }

        public Vec2 Right(float rotation)
        {
            return Forward(rotation + (float)Math.PI / 2f);
        }

        public Vec2 Left(float rotation)
        {
            return Forward(rotation - (float)Math.PI / 2f);
        }
        #endregion

        #region Operators

        /// <summary>
        /// Addition: Vector + Vector
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static Vec2 operator +(Vec2 a, Vec2 b)
            => new Vec2(a.X + b.X, a.Y + b.Y);

        /// <summary>
        /// Subtraction: Vector - Vector
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static Vec2 operator -(Vec2 a, Vec2 b)
            => new Vec2(a.X - b.X, a.Y - b.Y);

        /// <summary>
        /// Multiplication: Vector * Scalar (float)
        /// </summary>
        /// <param name="a"></param>
        /// <param name="d"></param>
        /// <returns></returns>
        public static Vec2 operator *(Vec2 a, float d)
            => new Vec2(a.X * d, a.Y * d);

        /// <summary>
        /// Multiplication: Scalar * Vector (for commutativity: 2.0f * vector)
        /// </summary>
        /// <param name="d"></param>
        /// <param name="a"></param>
        /// <returns></returns>
        public static Vec2 operator *(float d, Vec2 a)
            => new Vec2(a.X * d, a.Y * d);

        /// <summary>
        /// Division: Vector / Scalar
        /// </summary>
        /// <param name="a"></param>
        /// <param name="d"></param>
        /// <returns></returns>
        /// <exception cref="DivideByZeroException"></exception>
        public static Vec2 operator /(Vec2 a, float d)
        {
            if (d == 0) throw new DivideByZeroException();
            return new Vec2(a.X / d, a.Y / d);
        }

        /// <summary>
        /// Unary Negation: -Vector
        /// </summary>
        /// <param name="a"></param>
        /// <returns></returns>
        public static Vec2 operator -(Vec2 a)
            => new Vec2(-a.X, -a.Y);

        #endregion
    }
}
