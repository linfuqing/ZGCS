using System;
using UnityEngine;

namespace ZG
{
    public struct Qef
    {
        public struct Data
        {
            public Matrix3 ata;
            public Vector3 atb;
            public float btb;
            public Vector3 massPoint;
            public int numPoints;

            public Data(Matrix3 ata, Vector3 atb, float btb, Vector3 massPoint, int numPoints)
            {
                this.ata = ata;
                this.atb = atb;
                this.btb = btb;
                this.massPoint = massPoint;
                this.numPoints = numPoints;
            }

            public Data(Vector3 point, Vector3 normal)
            {
                normal.Normalize();

                ata.m00 = normal.x * normal.x;
                ata.m01 = normal.x * normal.y;
                ata.m02 = normal.x * normal.z;
                ata.m11 = normal.y * normal.y;
                ata.m12 = normal.y * normal.z;
                ata.m22 = normal.z * normal.z;

                float dot = Vector3.Dot(normal, point);

                atb.x = dot * normal.x;
                atb.y = dot * normal.y;
                atb.z = dot * normal.z;

                btb = dot * dot;

                massPoint.x = point.x;
                massPoint.y = point.y;
                massPoint.z = point.z;

                numPoints = 1;
            }

            public static Data operator +(Data x, Data y)
            {
                return new Data(
                    x.ata + y.ata,
                    x.atb + y.atb,
                    x.btb + y.btb,
                    x.massPoint + y.massPoint,
                    x.numPoints + y.numPoints);
            }
        }

        public struct Matrix3
        {
            public float m00, m01, m02, m11, m12, m22;

            public float off
            {
                get
                {
                    return Mathf.Sqrt(2.0f * ((m01 * m01) + (m02 * m02) + (m12 * m12)));
                }
            }

            public float fnorm
            {
                get
                {
                    return Mathf.Sqrt((m00 * m00) + (m01 * m01) + (m02 * m02)
                        + (m01 * m01) + (m11 * m11) + (m12 * m12)
                        + (m02 * m02) + (m12 * m12) + (m22 * m22));
                }
            }
            
            public float this[int y, int x]
            {
                get
                {
                    switch(y)
                    {
                        case 0:
                            switch(x)
                            {
                                case 0:
                                    return m00;
                                case 1:
                                    return m01;
                                case 2:
                                    return m02;
                                default:
                                    throw new IndexOutOfRangeException();
                            }
                        case 1:
                            switch(x)
                            {
                                case 1:
                                    return m11;
                                case 2:
                                    return m12;
                                default:
                                    throw new IndexOutOfRangeException();
                            }
                        case 2:
                            if (x == 2)
                                return m22;
                            else
                                throw new IndexOutOfRangeException();
                        default:
                            throw new IndexOutOfRangeException();
                    }
                }

                set
                {
                    switch (y)
                    {
                        case 0:
                            switch (x)
                            {
                                case 0:
                                    m00 = value;
                                    break;
                                case 1:
                                    m01 = value;
                                    break;
                                case 2:
                                    m02 = value;
                                    break;
                                default:
                                    throw new IndexOutOfRangeException();
                            }

                            break;
                        case 1:
                            switch (x)
                            {
                                case 1:
                                    m11 = value;
                                    break;
                                case 2:
                                    m12 = value;
                                    break;
                                default:
                                    throw new IndexOutOfRangeException();
                            }

                            break;
                        case 2:
                            if (x == 2)
                            {
                                m22 = value;

                                break;
                            }
                            else
                                throw new IndexOutOfRangeException();
                        default:
                            throw new IndexOutOfRangeException();
                    }
                }
            }

            public static Matrix3 operator +(Matrix3 x, Matrix3 y)
            {
                return new Matrix3(
                    x.m00 + y.m00,
                    x.m01 + y.m01,
                    x.m02 + y.m02,

                    x.m11 + y.m11,
                    x.m12 + y.m12,

                    x.m22 + y.m22);
            }

            public static implicit operator Matrix3x3(Matrix3 matrix)
            {
                Matrix3x3 result;
                result.m00 = matrix.m00;
                result.m01 = matrix.m01;
                result.m02 = matrix.m02;

                result.m10 = matrix.m01;

                result.m11 = matrix.m11;
                result.m12 = matrix.m12;

                result.m20 = matrix.m02;
                result.m21 = matrix.m12;

                result.m22 = matrix.m22;

                return result;
            }

            public Matrix3(float m00, float m01, float m02, float m11, float m12, float m22)
            {
                this.m00 = m00;
                this.m01 = m01;
                this.m02 = m02;

                this.m11 = m11;
                this.m12 = m12;
                this.m22 = m22;
            }
            
            public Matrix3x3 Rotate(Matrix3x3 v, int row, int column)
            {
                if (Mathf.Approximately(this[row, column], 0.0f))
                    return v;

                float x = this[row, row], y = this[column, column], a = this[row, column] , c, s;
                CalcSymmetricGivensCoefficients(x, a, y, out c, out s);

                Qef.Rotate(c, s, ref x, ref y, ref a);
                this[row, row] = x;
                this[column, column] = y;
                this[row, column] = a;

                x = this[0, 3 - column];
                y = this[1 - row, 2];

                Qef.Rotate(c, s, ref x, ref y);

                this[0, 3 - column] = x;
                this[1 - row, 2] = y;

                this[row, column] = 0.0f;

                x = v[0, row];
                y = v[0, column];

                Qef.Rotate(c, s, ref x, ref y);

                v[0, row] = x;
                v[0, column] = y;

                x = v[1, row];
                y = v[1, column];

                Qef.Rotate(c, s, ref x, ref y);

                v[1, row] = x;
                v[1, column] = y;

                x = v[2, row];
                y = v[2, column];

                Qef.Rotate(c, s, ref x, ref y);

                v[2, row] = x;
                v[2, column] = y;

                return v;
            }

            public void Rotate01(out float c, out float s)
            {
                CalcSymmetricGivensCoefficients(m00, m01, m11, out c, out s);

                Matrix3 temp = this;

                float cc = c * c;
                float ss = s * s;
                float mix = 2.0f * c * s * temp.m01;

                m00 = cc * temp.m00 - mix + ss * temp.m11;
                m01 = 0.0f;
                m02 = c * temp.m02 - s * temp.m12;

                m11 = ss * temp.m00 + mix + cc * temp.m11;
                m12 = s * temp.m02 + c * temp.m12;
            }

            public void Rotate02(out float c, out float s)
            {
                CalcSymmetricGivensCoefficients(m00, m02, m22, out c, out s);

                Matrix3 temp = this;

                float cc = c * c;
                float ss = s * s;
                float mix = 2.0f * c * s * temp.m02;

                m00 = cc * temp.m00 - mix + ss * temp.m22;
                m01 = c * temp.m01 - s * temp.m12;
                m02 = 0.0f;

                m12 = s * temp.m01 + c * temp.m12;
                m22 = ss * temp.m00 + mix + cc * temp.m22;
            }

            public void Rotate12(out float c, out float s)
            {
                CalcSymmetricGivensCoefficients(m11, m12, m22, out c, out s);

                Matrix3 temp = this;

                float cc = c * c;
                float ss = s * s;
                float mix = 2.0f * c * s * temp.m12;

                m01 = c * temp.m01 - s * temp.m02;
                m02 = s * temp.m01 + c * temp.m02;

                m11 = cc * temp.m11 - mix + ss * temp.m22;
                m12 = 0.0f;
                m22 = ss * temp.m11 + mix + cc * temp.m22;
            }

            public Matrix3x3 Rotate01(Matrix3x3 x)
            {
                if (Mathf.Approximately(m01, 0.0f))
                    return x;

                float c, s;
                Rotate01(out c, out s);
                //c = 0.0f;
                //s = 0.0f;
                x.Rotate01(c, s);

                return x;
            }

            public Matrix3x3 Rotate02(Matrix3x3 x)
            {
                if (Mathf.Approximately(m02, 0.0f))
                    return x;

                float c, s;
                Rotate02(out c, out s);
                //c = 0.0f;
                //s = 0.0f;
                x.Rotate02(c, s);

                return x;
            }

            public Matrix3x3 Rotate12(Matrix3x3 x)
            {
                if (Mathf.Approximately(m12, 0.0f))
                    return x;

                float c, s;
                Rotate12(out c, out s);
                //c = 0.0f;
                //s = 0.0f;
                x.Rotate12(c, s);

                return x;
            }

            public Matrix3x3 Pseudoinverse(Matrix3x3 v/*, float tol*/)
            {
                float d0 = Mathf.Approximately(m00, 0.0f) ? 0.0f : 1.0f / m00, //Pinv(m00, tol),
                    d1 = Mathf.Approximately(m11, 0.0f) ? 0.0f : 1.0f / m11, // Pinv(m11, tol),
                    d2 = Mathf.Approximately(m22, 0.0f) ? 0.0f : 1.0f / m22;// Pinv(m22, tol);

                return new Matrix3x3(
                    v.m00 * d0 * v.m00 + v.m01 * d1 * v.m01 + v.m02 * d2 * v.m02,
                    v.m00 * d0 * v.m10 + v.m01 * d1 * v.m11 + v.m02 * d2 * v.m12,
                    v.m00 * d0 * v.m20 + v.m01 * d1 * v.m21 + v.m02 * d2 * v.m22,
                    v.m10 * d0 * v.m00 + v.m11 * d1 * v.m01 + v.m12 * d2 * v.m02,
                    v.m10 * d0 * v.m10 + v.m11 * d1 * v.m11 + v.m12 * d2 * v.m12,
                    v.m10 * d0 * v.m20 + v.m11 * d1 * v.m21 + v.m12 * d2 * v.m22,
                    v.m20 * d0 * v.m00 + v.m21 * d1 * v.m01 + v.m22 * d2 * v.m02,
                    v.m20 * d0 * v.m10 + v.m21 * d1 * v.m11 + v.m22 * d2 * v.m12,
                    v.m20 * d0 * v.m20 + v.m21 * d1 * v.m21 + v.m22 * d2 * v.m22);
            }
            
            public Matrix3x3 GetSymmetricSvd(/*float tol, */int maxSweeps)
            {
                Matrix3x3 result = Matrix3x3.identity;
                //float delta = tol * fnorm;

                for (int i = 0; i < maxSweeps/* && off > delta*/; ++i)
                {
                    result = Rotate01(result);
                    result = Rotate02(result);
                    result = Rotate12(result);
                    /*result = Rotate(result, 0, 1);
                    result = Rotate(result, 0, 2);
                    result = Rotate(result, 1, 2);*/
                }

                return result;
            }

            public Vector3 SolveSymmetric(/*float svdTol, */int svdSweeps, /*float pinvTol, */Vector3 b)
            {
                Matrix3 temp = this;
                Matrix3x3 v = temp.GetSymmetricSvd(/*svdTol, */svdSweeps);
                Matrix3x3 pinv = temp.Pseudoinverse(v/*, pinvTol*/);

                return pinv.Multiply(b);
            }

            public Vector3 Multiply(Vector3 vector)
            {
                return new Vector3(
                    (m00 * vector.x) + (m01 * vector.y) + (m02 * vector.z),
                    (m01 * vector.x) + (m11 * vector.y) + (m12 * vector.z),
                    (m02 * vector.x) + (m12 * vector.y) + (m22 * vector.z));
            }

            public Matrix3x3 Jacobian(out Vector3 determinant)
            {
                int i, j, k, l;
                float temp, g, h, t, c, s, tau, theta, tresh;
                Vector3 b = new Vector3(m00, m11, m22), z = Vector3.zero;
                Matrix3x3 a = this, v = Matrix3x3.identity;

                determinant = b;
                for (i = 1; i <=50; ++i)
                {
                    temp = Mathf.Abs(a.m01) + Mathf.Abs(a.m02) + Mathf.Abs(a.m12);

                    if(Mathf.Approximately(temp, 0.0f))
                    {
                        v = v.transpose;
                        if(Mathf.Abs(determinant.x) < Mathf.Abs(determinant.y))
                        {
                            temp = determinant.x;
                            determinant.x = determinant.y;
                            determinant.y = temp;

                            temp = v.m00;
                            v.m00 = v.m10;
                            v.m10 = temp;

                            temp = v.m01;
                            v.m01 = v.m11;
                            v.m11 = temp;

                            temp = v.m02;
                            v.m02 = v.m12;
                            v.m12 = temp;
                        }

                        if (Mathf.Abs(determinant.y) < Mathf.Abs(determinant.z))
                        {
                            temp = determinant.y;
                            determinant.y = determinant.z;
                            determinant.z = temp;

                            temp = v.m10;
                            v.m10 = v.m20;
                            v.m20 = temp;

                            temp = v.m11;
                            v.m11 = v.m21;
                            v.m21 = temp;

                            temp = v.m12;
                            v.m12 = v.m22;
                            v.m22 = temp;
                        }

                        if (Mathf.Abs(determinant.x) < Mathf.Abs(determinant.y))
                        {
                            temp = determinant.x;
                            determinant.x = determinant.y;
                            determinant.y = temp;

                            temp = v.m00;
                            v.m00 = v.m10;
                            v.m10 = temp;

                            temp = v.m01;
                            v.m01 = v.m11;
                            v.m11 = temp;

                            temp = v.m02;
                            v.m02 = v.m12;
                            v.m12 = temp;
                        }

                        return v;
                    }

                    tresh = i < 4 ? temp * 0.2f / 9.0f : 0.0f;
                    for(j = 0; j < 2; ++j)
                    {
                        for(k = j + 1; k < 3; ++k)
                        {
                            temp = a[j, k];
                            g = /*100.0f * */Mathf.Abs(temp);
                            if (i > 4 && 
                                Mathf.Approximately(Mathf.Abs(determinant[j]) + g, Mathf.Abs(determinant[j])) &&
                                Mathf.Approximately(Mathf.Abs(determinant[k]) + g, Mathf.Abs(determinant[k])))
                                a[j, k] = 0.0f;
                            else if(Mathf.Abs(temp) > tresh)
                            {
                                h = determinant[k] - determinant[j];
                                if (Mathf.Approximately(Mathf.Abs(h) + g, Mathf.Abs(h)))
                                    t = temp / h;
                                else
                                {
                                    theta = 0.5f * h / temp;
                                    t = 1.0f / (Mathf.Abs(theta) + Mathf.Sqrt(1.0f + theta * theta));
                                    if (theta < 0.0f)
                                        t = -t;
                                }

                                c = 1.0f / Mathf.Sqrt(1.0f + t * t);
                                s = t * c;
                                tau = s / (1.0f + c);
                                h = t * temp;

                                z[j] -= h;
                                z[k] += h;

                                determinant[j] -= h;
                                determinant[k] += h;

                                a[j, k] = 0.0f;
                                for(l = 0; l <= j - 1; ++l)
                                {
                                    a.Rotate(l, j, l, k, tau, s);
                                }

                                for(l = j + 1; l <= k - 1; ++l)
                                {
                                    a.Rotate(j, l, l, k, tau, s);
                                }

                                for(l = k + 1; l < 3; ++l)
                                {
                                    a.Rotate(j, l, k, l, tau, s);
                                }

                                for(l = 0; l < 3; ++l)
                                {
                                    v.Rotate(l, j, l, k, tau, s);
                                }
                            }
                        }
                    }
                    
                    b += z;
                    determinant = b;
                    z = Vector3.zero;
                }

                throw new Exception("too many iterations in jacobi");
            }

            public Matrix3x3 Invert(out Vector3 determinant)
            {
                Matrix3x3 u = Jacobian(out determinant);
                UnityEngine.Assertions.Assert.IsFalse(Mathf.Approximately(determinant.x, 0.0f));
                float temp;
                for(int i = 1; i < 3; ++i)
                {
                    temp = determinant[i];
                    determinant[i] = Mathf.Approximately(temp, 0.0f) ? 0.0f : 1.0f / temp;
                }

                determinant.x = 1.0f / determinant.x;

                Matrix3x3 result;

                result.m00 = determinant.x * u.m00 * u.m00 +
                                determinant.y * u.m10 * u.m10 +
                                determinant.z * u.m20 * u.m20;
                result.m01 = determinant.x * u.m00 * u.m01 +
                                determinant.y * u.m10 * u.m11 +
                                determinant.z * u.m20 * u.m21;
                result.m02 = determinant.x * u.m00 * u.m02 +
                                determinant.y * u.m10 * u.m12 +
                                determinant.z * u.m20 * u.m22;
                result.m10 = determinant.x * u.m01 * u.m00 +
                                determinant.y * u.m11 * u.m10 +
                                determinant.z * u.m21 * u.m20;
                result.m11 = determinant.x * u.m01 * u.m01 +
                                determinant.y * u.m11 * u.m11 +
                                determinant.z * u.m21 * u.m21;
                result.m12 = determinant.x * u.m01 * u.m02 +
                                determinant.y * u.m11 * u.m12 +
                                determinant.z * u.m21 * u.m22;
                result.m20 = determinant.x * u.m02 * u.m00 +
                                determinant.y * u.m12 * u.m10 +
                                determinant.z * u.m22 * u.m20;
                result.m21 = determinant.x * u.m02 * u.m01 +
                                determinant.y * u.m12 * u.m11 +
                                determinant.z * u.m22 * u.m21;
                result.m22 = determinant.x * u.m02 * u.m02 +
                                determinant.y * u.m12 * u.m12 +
                                determinant.z * u.m22 * u.m22;

                return result;
            }
        }

        public struct Matrix3x3
        {
            public float m00, m01, m02, m10, m11, m12, m20, m21, m22;

            public static Matrix3x3 identity
            {
                get
                {
                    return new Matrix3x3(
                        1.0f,
                        0.0f,
                        0.0f,

                        0.0f,
                        1.0f,
                        0.0f,

                        0.0f,
                        0.0f,
                        1.0f);
                }
            }

            public Matrix3x3 transpose
            {
                get
                {
                    return new Matrix3x3(m00, m10, m20, m01, m11, m21, m02, m12, m22);
                }
            }

            public float this[int y, int x]
            {
                get
                {
                    switch (y)
                    {
                        case 0:
                            switch (x)
                            {
                                case 0:
                                    return m00;
                                case 1:
                                    return m01;
                                case 2:
                                    return m02;
                                default:
                                    throw new IndexOutOfRangeException();
                            }
                        case 1:
                            switch (x)
                            {
                                case 0:
                                    return m10;
                                case 1:
                                    return m11;
                                case 2:
                                    return m12;
                                default:
                                    throw new IndexOutOfRangeException();
                            }
                        case 2:
                            switch (x)
                            {
                                case 0:
                                    return m20;
                                case 1:
                                    return m21;
                                case 2:
                                    return m22;
                                default:
                                    throw new IndexOutOfRangeException();
                            }
                        default:
                            throw new IndexOutOfRangeException();
                    }
                }

                set
                {
                    switch (y)
                    {
                        case 0:
                            switch (x)
                            {
                                case 0:
                                    m00 = value;
                                    break;
                                case 1:
                                    m01 = value;
                                    break;
                                case 2:
                                    m02 = value;
                                    break;
                                default:
                                    throw new IndexOutOfRangeException();
                            }

                            break;
                        case 1:
                            switch (x)
                            {
                                case 0:
                                    m10 = value;
                                    break;
                                case 1:
                                    m11 = value;
                                    break;
                                case 2:
                                    m12 = value;
                                    break;
                                default:
                                    throw new IndexOutOfRangeException();
                            }

                            break;
                        case 2:
                            switch (x)
                            {
                                case 0:
                                    m20 = value;
                                    break;
                                case 1:
                                    m21 = value;
                                    break;
                                case 2:
                                    m22 = value;
                                    break;
                                default:
                                    throw new IndexOutOfRangeException();
                            }

                            break;
                        default:
                            throw new IndexOutOfRangeException();
                    }
                }
            }

            public static implicit operator Matrix4x4(Matrix3x3 matrix)
            {
                Matrix4x4 result;
                result.m00 = matrix.m00;
                result.m01 = matrix.m01;
                result.m02 = matrix.m02;
                result.m03 = 0.0f;

                result.m10 = matrix.m10;
                result.m11 = matrix.m11;
                result.m12 = matrix.m12;
                result.m13 = 0.0f;

                result.m20 = matrix.m20;
                result.m21 = matrix.m21;
                result.m22 = matrix.m22;
                result.m23 = 0.0f;

                result.m30 = 0.0f;
                result.m31 = 0.0f;
                result.m32 = 0.0f;
                result.m33 = 1.0f;

                return result;
            }

            public static implicit operator Unity.Mathematics.float3x3(Matrix3x3 matrix)
            {
                Unity.Mathematics.float3x3 result;
                result.c0.x = matrix.m00;
                result.c0.y = matrix.m01;
                result.c0.z = matrix.m02;

                result.c1.x = matrix.m10;
                result.c1.y = matrix.m11;
                result.c1.z = matrix.m12;

                result.c2.x = matrix.m20;
                result.c2.y = matrix.m21;
                result.c2.z = matrix.m22;
                
                return result;
            }

            public Matrix3x3(float m00, float m01, float m02, float m10, float m11, float m12, float m20, float m21, float m22)
            {
                this.m00 = m00;
                this.m01 = m01;
                this.m02 = m02;

                this.m10 = m10;
                this.m11 = m11;
                this.m12 = m12;

                this.m20 = m20;
                this.m21 = m21;
                this.m22 = m22;
            }

            public void Rotate(int i, int j, int k, int l, float tau, float s)
            {
                float g = this[i, j], h = this[k, l];

                this[i, j] = g - s * (h + g * tau);
                this[k, l] = h + s * (g - h * tau);
            }

            public void Rotate01(float c, float s)
            {
                Matrix3x3 temp = this;

                m00 = c * temp.m00 - s * temp.m01;
                m01 = s * temp.m00 + c * temp.m01;

                m10 = c * temp.m10 - s * temp.m11;
                m11 = s * temp.m10 + c * temp.m11;

                m20 = c * temp.m20 - s * temp.m21;
                m21 = s * temp.m20 + c * temp.m21;
            }

            public void Rotate02(float c, float s)
            {
                Matrix3x3 temp = this;

                m00 = c * temp.m00 - s * temp.m02;
                m02 = s * temp.m00 + c * temp.m02;

                m10 = c * temp.m10 - s * temp.m12;
                m12 = s * temp.m10 + c * temp.m12;

                m20 = c * temp.m20 - s * temp.m22;
                m22 = s * temp.m20 + c * temp.m22;
            }

            public void Rotate12(float c, float s)
            {
                Matrix3x3 temp = this;

                m01 = c * temp.m01 - s * temp.m02;
                m02 = s * temp.m01 + c * temp.m02;

                m11 = c * temp.m11 - s * temp.m12;
                m12 = s * temp.m11 + c * temp.m12;

                m21 = c * temp.m21 - s * temp.m22;
                m22 = s * temp.m21 + c * temp.m22;
            }

            public float CalcError(Vector3 x, Vector3 b)
            {
                return (b - Multiply(x)).sqrMagnitude;
            }

            public Vector3 Multiply(Vector3 vector)
            {
                return new Vector3(
                    (m00 * vector.x) + (m01 * vector.y) + (m02 * vector.z),
                    (m10 * vector.x) + (m11 * vector.y) + (m12 * vector.z),
                    (m20 * vector.x) + (m21 * vector.y) + (m22 * vector.z));
            }

            public Vector3 MultiplyTS(Vector3 vector)
            {
                return new Vector3(
                    (m00 * vector.x) + (m10 * vector.y) + (m20 * vector.z),
                    (m01 * vector.x) + (m11 * vector.y) + (m21 * vector.z),
                    (m02 * vector.x) + (m12 * vector.y) + (m22 * vector.z));
            }
        }

        private Data __data;

        public Data data
        {
            get
            {
                return __data;
            }
        }

        public Vector3 massPoint
        {
            get
            {
                return __data.massPoint / __data.numPoints;
            }
        }

        public static Qef operator +(Qef x, Qef y)
        {
            x.__data += y.__data;

            return x;
        }

        public static void CalcSymmetricGivensCoefficients(
            float pp,
            float pq,
            float qq,
            out float c,
            out float s)
        {
            if (Mathf.Approximately(pq, 0.0f))
            {
                c = 1.0f;
                s = 0.0f;

                return;
            }

            float tau = (qq - pp) / (2.0f * pq);
            float stt = Mathf.Sqrt(1.0f + tau * tau);
            float tan = 1.0f / ((tau >= 0.0f) ? (tau + stt) : (tau - stt));
            c = 1.0f / Mathf.Sqrt(1.0f + tan * tan);
            s = tan * c;
        }

        public static void Rotate(float c, float s, ref float x, ref float y, ref float a)
        {
            float cc = c * c; float ss = s * s;

            float mx = 2.0f * c * s * a;

            float u = x;
            float v = y;

            x = cc * u - mx + ss * v;

            y = ss * u + mx + cc * v;
        }

        public static void Rotate(float c, float s, ref float x, ref float y)
        {
            float u = x; float v = y;

            x = c * u - s * v;

            y = s * u + c * v;
        }

        /*public static float Pinv(float x, float tol)
        {
            return (Mathf.Abs(x) < tol || Mathf.Abs(1.0f / x) < tol) ? 0.0f : (1.0f / x);
        }*/

        public void Add(Data data)
        {
            __data += data;
        }
        
        public Vector3 Solve()
        {
            Vector3 massPoint = this.massPoint;

            Vector3 determinant;
            Matrix3x3 result = __data.ata.Invert(out determinant).transpose;
            //Matrix3x3 result = __data.ata.Invert(out determinant);
            //Unity.Mathematics.float3x3 result = (Matrix3x3)__data.ata;
            //result = Unity.Mathematics.math.inverse(result);

            return result.Multiply(__data.atb - __data.ata.Multiply(massPoint)) + massPoint;
        }

        public Vector3 Solve(/*float svdTol, */int svdSweeps/*, float pinvTol*/)
        {
            return Solve();

            Vector3 massPoint = this.massPoint;
            return __data.ata.SolveSymmetric(/*svdTol, */svdSweeps, /*pinvTol, */__data.atb - __data.ata.Multiply(massPoint)) + massPoint;
        }

        public float GetError(Vector3 position)
        {
            return Vector3.Dot(position, __data.ata.Multiply(position)) - 2.0f * Vector3.Dot(position, __data.atb) + __data.btb;
        }
    }
}