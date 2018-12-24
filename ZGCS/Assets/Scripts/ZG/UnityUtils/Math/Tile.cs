using System;

[Serializable]
public struct Tile<T>
{
    public T x;
    public T y;
    public T z;
    public T w;

    public T this[int index]
    {
        get
        {
            switch (index)
            {
                case 0:
                    return x;
                case 1:
                    return y;
                case 2:
                    return z;
                case 3:
                    return w;
            }

            throw new IndexOutOfRangeException();
        }

        set
        {
            switch (index)
            {
                case 0:
                    x = value;
                    break;
                case 1:
                    y = value;
                    break;
                case 2:
                    z = value;
                    break;
                case 3:
                    w = value;
                    break;
                default:
                    throw new IndexOutOfRangeException();
            }
        }
    }

    public Tile(T x, T y, T z, T w)
    {
        this.x = x;
        this.y = y;
        this.z = z;
        this.w = w;
    }

    public bool IsAll<U>(U value) where U : IEquatable<T>
    {
        return value.Equals(x) && value.Equals(y) && value.Equals(z) && value.Equals(w);
    }

    public bool IsAny<U>(U value) where U : IEquatable<T>
    {
        return value.Equals(x) || value.Equals(y) || value.Equals(z) || value.Equals(w);
    }
}

