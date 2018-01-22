using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ZG
{

    public abstract class NavPath : IEnumerable<Vector3Int>
    {
        public enum Type
        {
            Once,
            Min,
            Max
        }

        private struct Node
        {
            public byte previous;
            public byte next;
            public int depth;
            public int distance;
            public int value;
            public int evaluation;
        }

        private struct Item
        {
            public byte direction;
            public int depth;
            public Vector3Int position;

            public Item(byte direction, int depth, Vector3Int position)
            {
                this.direction = direction;
                this.depth = depth;
                this.position = position;
            }
        }

        private struct Comparer : IComparer<int>
        {
            public int Compare(int x, int y)
            {
                if (x == y)
                    return -1;

                return x - y;
            }
        }

        private struct Enumerator : IEnumerator<Vector3Int>
        {
            private Vector2Int __size;
            private Vector3Int __position;
            private Node[] __nodes;

            public Vector3Int Current
            {
                get
                {
                    return __position;
                }
            }

            public Enumerator(Vector2Int size, Vector3Int position, Node[] nodes)
            {
                __size = size;
                __position = position;
                __nodes = nodes;
            }

            public bool MoveNext()
            {
                int numNodes = __nodes == null ? 0 : __nodes.Length;
                if (numNodes < 1)
                    return false;

                int index = __position.x + __position.y * __size.x + __position.z * __size.y;
                if (index >= numNodes)
                    return false;

                Node node = __nodes[index];
                if (node.next == 0)
                    return false;
                
                __position += Convert(node.next);

                return true;
            }

            void IEnumerator.Reset()
            {
                throw new NotSupportedException();
            }

            void IDisposable.Dispose()
            {

            }

            object IEnumerator.Current
            {
                get
                {
                    return Current;
                }
            }
        }
        
        private int __minDepth;
        private Vector3Int __size;
        private Vector3Int __position;
        private Node[] __nodes;
        private SortedList<int, Vector3Int> __queue;

        public static Vector3Int Convert(byte direction)
        {
            Vector3Int result = new Vector3Int((direction >> 4) & 3, (direction >> 2) & 3, direction & 3);

            if (result.x == 2)
                result.x = -1;

            if (result.y == 2)
                result.y = -1;

            if (result.z == 2)
                result.z = -1;

            return result;
        }

        public static byte Convert(Vector3Int direction)
        {
            direction.x = Mathf.Clamp(direction.x, -1, 1);
            if (direction.x == -1)
                direction.x = 2;

            direction.y = Mathf.Clamp(direction.y, -1, 1);
            if (direction.y == -1)
                direction.y = 2;

            direction.z = Mathf.Clamp(direction.z, -1, 1);
            if (direction.z == -1)
                direction.z = 2;

            return (byte)((direction.x << 4) | (direction.y << 2) | (direction.z));
        }

        public Vector3Int size
        {
            get
            {
                return __size;
            }
        }

        public NavPath(Vector3Int size)
        {
            __minDepth = 1;

            __size = size;
        }

        public abstract int Voluate(Vector3Int from, Vector3Int to);

        public int Search(
            Type type,
            int minEvaluation,
            int maxEvaluation,
            int maxDistance,
            int maxDepth,
            Vector3Int from,
            Vector3Int to)
        {
            if (from.x < 0 || from.y < 0 || from.z < 0)
                return 0;

            int size = __size.x * __size.y,
                count = size * __size.z,
                index = from.x + from.y * __size.x + from.z * size;
            if (index >= count)
                return 0;

            __position = from;

            if (__nodes == null)
                __nodes = new Node[count];

            maxDepth = Mathf.Min(int.MaxValue - 2, maxDepth);
            if (__minDepth >= (int.MaxValue - maxDepth))
            {
                __minDepth = 1;

                for (int i = 0; i < count; ++i)
                    __nodes[i].depth = 0;
            }
            maxDepth = maxDepth > 0 ? __minDepth + maxDepth : int.MaxValue - 1;

            Node node = __nodes[index];
            node.previous = 0;
            node.distance = 0;
            node.value = 0;
            node.evaluation = Voluate(from, to);
            node.depth = __minDepth;

            int evalution;
            switch (type)
            {
                case Type.Min:
                    evalution = node.evaluation;
                    break;
                case Type.Max:
                    evalution = 0;
                    break;
                default:
                    evalution = int.MaxValue;
                    break;
            }

            __nodes[index] = node;
            
            int depth = __Search(
                from,
                to,
                new Vector2Int(__size.x, size),
                evalution,
                minEvaluation,
                maxEvaluation,
                maxDistance,
                maxDepth,
                ref __minDepth);

            if (depth > maxDepth)
                return 0;
            
            return depth - node.depth + 1;
        }

        public int __Search(
            Vector3Int from,
            Vector3Int to,
            Vector2Int size,
            int evaluation,
            int minEvaluation,
            int maxEvaluation,
            int maxDistance,
            int maxDepth,
            ref int minDepth)
        {
            bool result;
            int index = from.x + from.y * size.x + from.z * size.y;
            Node node = __nodes[index], child;
            int outputDepth = maxDepth + 1, depth, temp;
            Vector3Int position, offset;
            do
            {
                node.next = 0;

                depth = node.depth + 1;

                if (node.evaluation > minEvaluation)
                {
                    if (node.value < maxDistance && node.depth < maxDepth)
                    {
                        int i, j, k, childIndex, distance;
                        for (i = -1; i < 2; ++i)
                        {
                            for (j = -1; j < 2; ++j)
                            {
                                for (k = -1; k < 2; ++k)
                                {
                                    offset = new Vector3Int(i, j, k);
                                    position = from + offset;
                                    if (position.x < 0 ||
                                        position.y < 0 ||
                                        position.z < 0 ||
                                        position.x >= __size.x ||
                                        position.y >= __size.y ||
                                        position.z >= __size.z)
                                        continue;

                                    if (from + Convert(node.previous) == position)
                                        continue;

                                    childIndex = position.x + position.y * size.x + position.z * size.y;
                                    child = __nodes[childIndex];

                                    if (child.depth < minDepth)
                                    {
                                        child.previous = Convert(new Vector3Int(-offset.x, -offset.y, -offset.z));
                                        //child.next = 0;
                                        child.depth = depth;
                                        child.distance = Voluate(from, position);
                                        child.value = child.distance + node.value;
                                        child.evaluation = Voluate(position, to);

                                        __nodes[childIndex] = child;

                                        if (child.evaluation > maxEvaluation)
                                            continue;

                                        distance = child.value + child.evaluation;
                                        if (distance > maxDistance)
                                            continue;

                                        if (__queue == null)
                                            __queue = new SortedList<int, Vector3Int>(new Comparer());

                                        __queue.Add(distance, position);
                                    }
                                    else if (depth < node.depth && (node.next == 0 || (outputDepth - node.depth) > node.evaluation))
                                    {
                                        if (child.evaluation > maxEvaluation)
                                            continue;

                                        child.previous = Convert(new Vector3Int(-offset.x, -offset.y, -offset.z));

                                        result = true;
                                        distance = node.value;
                                        temp = node.depth;
                                        while (child.evaluation > minEvaluation)
                                        {
                                            distance = distance + child.distance;
                                            child.value = distance;
                                            child.depth = ++temp;
                                            __nodes[childIndex] = child;

                                            if (child.next == 0)
                                            {
                                                result = false;

                                                break;
                                            }

                                            position += Convert(child.next);

                                            childIndex = position.x + position.y * size.x + position.z * size.y;

                                            child = __nodes[childIndex];
                                        }

                                        if (!result)
                                            continue;

                                        distance += child.evaluation;
                                        if (distance > maxDistance || temp > maxDepth)
                                            continue;

                                        node.next = Convert(offset);
                                        if (distance <= evaluation)
                                        {
                                            __nodes[index] = node;

                                            return temp;
                                        }

                                        maxDistance = distance;
                                        outputDepth = temp;
                                        maxDepth = outputDepth - 1;
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    position = from;
                    Node previous = node;
                    child = previous;
                    while (previous.previous != 0)
                    {
                        offset = Convert(previous.previous);
                        position += offset;

                        temp = position.x + position.y * size.x + position.z * size.y;

                        previous = __nodes[temp];
                        if (previous.depth < minDepth)
                            break;

                        previous.next = Convert(new Vector3Int(-offset.x, -offset.y, -offset.z));

                        __nodes[temp] = previous;
                    }

                    temp = node.value + node.evaluation;
                    if (temp <= evaluation)
                    {
                        minDepth = depth;

                        __nodes[index] = node;

                        return node.depth;
                    }

                    maxDistance = temp;
                    outputDepth = node.depth;
                    maxDepth = outputDepth - 1;
                }

                __nodes[index] = node;

                result = false;

                if (__queue != null)
                {
                    //int outputMinDepth = node.depth + 1;
                    while (__queue.Count > 0)
                    {
                        position = __queue.Values[0];

                        index = position.x + position.y * size.x + position.z * size.y;

                        node = __nodes[index];
                        __queue.RemoveAt(0);

                        if (node.previous != 0)
                        {
                            offset = position + Convert(node.previous);
                            child = __nodes[offset.x + offset.y * size.x + offset.z * size.y];
                            if (child.value < maxDistance && child.depth < maxDepth && child.depth >= minDepth)
                            {
                                /*temp = minDepth;

                                depth = __Search(position, to, size, evaluation, minEvaluation, maxEvaluation, maxDistance, maxDepth, ref temp);

                                if (temp > outputMinDepth)
                                    outputMinDepth = temp;

                                if (depth >= minDepth && depth <= maxDepth)
                                    outputDepth = depth;*/

                                from = position;

                                result = true;

                                break;
                            }
                        }
                    }
                }
            } while (result);

            //minDepth = outputMinDepth;

            return outputDepth;
        }

        public IEnumerator<Vector3Int> GetEnumerator()
        {
            return new Enumerator(new Vector2Int(__size.x, __size.x * __size.y), __position, __nodes);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    public class NavPathEx : NavPath
    {
        public NavPathEx(Vector3Int size) : base(size)
        {

        }

        public override int Voluate(Vector3Int from, Vector3Int to)
        {
            from -= to;

            return Mathf.Abs(from.x) + Mathf.Abs(from.y) + Mathf.Abs(from.z);
        }
        
        public int Search(
            Type type,
            int maxDistance,
            int maxDepth,
            Vector3Int from,
            Vector3Int to)
        {
            return Search(type, 0, int.MaxValue - 1, maxDistance, maxDepth, from, to);
        }
    }
}