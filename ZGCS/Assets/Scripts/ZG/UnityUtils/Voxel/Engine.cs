using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZG.Voxel
{
    public enum Axis
    {
        X,
        Y,
        Z,

        Unkown
    }

    [Flags]
    public enum Boundary
    {
        None = 0x00,

        Left = 0x01,
        Right = 0x02,

        Lower = 0x04,
        Upper = 0x08,

        Back = 0x10,
        Front = 0x20,

        LeftLowerBack = Left | Lower | Back,
        RightUpperFront = Right | Upper | Front,

        All = LeftLowerBack | RightUpperFront
    }

    public struct Vertex : IEquatable<Vertex>
    {
        public int count;

        public Vector3 position;
        public Vector3 normal;

        public Qef qef;

        public Vertex(Vector3 position, Vector3 normal, Qef qef)
        {
            this.count = 1;
            this.position = position;
            this.normal = normal;
            this.qef = qef;
        }

        public bool Equals(Vertex vertex)
        {
            return position == vertex.position && normal == vertex.normal;
        }

        public static Vertex operator +(Vertex x, Vertex y)
        {
            x.count += y.count;
            x.position += y.position;
            x.normal += y.normal;
            x.qef += y.qef;

            return x;
        }

        public static Vertex Solve(Vertex x, Vertex y, /*Vector3 min, Vector3 max, */int svdSweeps)
        {
            x.qef += y.qef;
            x.normal += y.normal;
            x.position = (x.position + y.position) * 0.5f;

            /*if (x.position.x < min.x || x.position.y < min.y || x.position.z < min.z || x.position.x > max.x || x.position.y > max.y || x.position.z > max.z)
                x.position = x.qef.massPoint;*/

            return x;
        }

        public static implicit operator MeshData<Vector3>.Vertex(Vertex vertex)
        {
            MeshData<Vector3>.Vertex result;
            result.position = vertex.position / vertex.count;
            result.data = vertex.normal.normalized;

            return result;
        }
    }

    public struct Face
    {
        public int depth;

        public Axis axis;

        public Vector3Int sizeDelta;

        public Vector3Int indices;
    }

    public interface IEngine
    {
        bool Check(Vector3Int world);

        bool Destroy(Vector3Int world);
    }

    public interface IEngineSampler
    {
        float GetDensity(Vector3 position);
    }

    public interface IEngineBuilder
    {
        bool Create(Vector3Int world);

        bool Set(BoundsInt bounds);

        bool Set(Vector3Int position);
    }

    public interface IEngineProcessor<T> where T : IEngine
    {
        int depth { get; }

        Vector3 scale { get; }

        Vector3 offset { get; }

        bool Create(Boundary boundary, int sweeps, float threshold, Vector3Int world, T parent);

        bool Build(
            Boundary boundary, 
            Func<Face, IReadOnlyList<Vertex>, int> subMeshHandler, 
            out MeshData<Vector3> meshData);
    }
    
    public static class EngineUtility
    {
        public static Vector3 ApproximateZeroCrossingPosition(this IEngineSampler sampler, Vector3 x, Vector3 y, float increment)
        {
            if (sampler == null)
                return (x + y) * 0.5f;

            // approximate the zero crossing by finding the min value along the edge
            float density, minValue = int.MaxValue, result = 0.0f, t = 0.0f;
            while (t <= 1.0f)
            {
                density = Mathf.Abs(sampler.GetDensity(x + (y - x) * t));
                if (density < minValue)
                {
                    minValue = density;

                    result = t;
                }

                t += increment;
            }

            return x + (y - x) * result;
        }

        public static Vector3 CalculateSurfaceNormal(this IEngineSampler sampler, Vector3 point, Vector3 scale, float increment)
        {
            if (sampler == null)
                return Vector3.zero;

            //Vector3 x = new Vector3(__scale.x, 0.0f, 0.0f), y = new Vector3(0.0f, __scale.y, 0.0f), z = new Vector3(0.0f, 0.0f, __scale.z);
            Vector3 x = new Vector3(increment * scale.x, 0.0f, 0.0f), y = new Vector3(0.0f, increment * scale.y, 0.0f), z = new Vector3(0.0f, 0.0f, increment * scale.z);

            return new Vector3(
                sampler.GetDensity(point + x) - sampler.GetDensity(point - x),
                sampler.GetDensity(point + y) - sampler.GetDensity(point - y),
                sampler.GetDensity(point + z) - sampler.GetDensity(point - z)).normalized;
        }

    }
}