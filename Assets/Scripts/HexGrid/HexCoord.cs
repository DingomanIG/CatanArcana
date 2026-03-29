using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 큐브 좌표계 기반 헥스 좌표 (q + r + s = 0)
/// Pointy-top 헥스 기준
/// </summary>
[System.Serializable]
public readonly struct HexCoord : IEquatable<HexCoord>
{
    public readonly int Q;
    public readonly int R;
    public readonly int S;

    public HexCoord(int q, int r)
    {
        Q = q;
        R = r;
        S = -q - r;
    }

    public HexCoord(int q, int r, int s)
    {
        Q = q;
        R = r;
        S = s;
    }

    /// <summary>6방향 이웃 오프셋 (E, NE, NW, W, SW, SE)</summary>
    public static readonly HexCoord[] Directions = new HexCoord[]
    {
        new(1, 0, -1),   // E
        new(1, -1, 0),   // NE
        new(0, -1, 1),   // NW
        new(-1, 0, 1),   // W
        new(-1, 1, 0),   // SW
        new(0, 1, -1),   // SE
    };

    public static HexCoord Zero => new(0, 0, 0);

    public HexCoord GetNeighbor(int direction)
    {
        var d = Directions[direction];
        return new HexCoord(Q + d.Q, R + d.R, S + d.S);
    }

    public IEnumerable<HexCoord> GetNeighbors()
    {
        for (int i = 0; i < 6; i++)
            yield return GetNeighbor(i);
    }

    public int DistanceTo(HexCoord other)
    {
        return (Math.Abs(Q - other.Q) + Math.Abs(R - other.R) + Math.Abs(S - other.S)) / 2;
    }

    /// <summary>중심으로부터 radius 거리의 링 좌표 목록</summary>
    public static List<HexCoord> Ring(HexCoord center, int radius)
    {
        var results = new List<HexCoord>();
        if (radius == 0)
        {
            results.Add(center);
            return results;
        }

        // SW 방향으로 radius만큼 이동한 지점에서 시작
        var coord = new HexCoord(
            center.Q + Directions[4].Q * radius,
            center.R + Directions[4].R * radius,
            center.S + Directions[4].S * radius);

        for (int dir = 0; dir < 6; dir++)
        {
            for (int step = 0; step < radius; step++)
            {
                results.Add(coord);
                coord = coord.GetNeighbor(dir);
            }
        }

        return results;
    }

    /// <summary>정육각형 영역 좌표 (radius=2 → 19타일)</summary>
    public static List<HexCoord> Hexagon(HexCoord center, int radius)
    {
        var results = new List<HexCoord>();
        for (int r = 0; r <= radius; r++)
        {
            results.AddRange(Ring(center, r));
        }
        return results;
    }

    /// <summary>큐브 좌표 → 월드 좌표 (pointy-top, XZ 평면)</summary>
    public Vector3 ToWorldPosition(float hexSize)
    {
        float x = hexSize * (Mathf.Sqrt(3f) * Q + Mathf.Sqrt(3f) / 2f * R);
        float z = hexSize * 1.5f * R;
        return new Vector3(x, 0f, z);
    }

    /// <summary>6개 꼭짓점 월드 좌표 (pointy-top)</summary>
    public Vector3[] GetCornerPositions(float hexSize)
    {
        var center = ToWorldPosition(hexSize);
        var corners = new Vector3[6];
        for (int i = 0; i < 6; i++)
        {
            float angle = Mathf.Deg2Rad * (60f * i + 30f);
            corners[i] = new Vector3(
                center.x + hexSize * Mathf.Cos(angle),
                0f,
                center.z + hexSize * Mathf.Sin(angle));
        }
        return corners;
    }

    // Equality & Operators
    public bool Equals(HexCoord other) => Q == other.Q && R == other.R;
    public override bool Equals(object obj) => obj is HexCoord other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Q, R);
    public static bool operator ==(HexCoord a, HexCoord b) => a.Equals(b);
    public static bool operator !=(HexCoord a, HexCoord b) => !a.Equals(b);
    public static HexCoord operator +(HexCoord a, HexCoord b) => new(a.Q + b.Q, a.R + b.R, a.S + b.S);
    public static HexCoord operator -(HexCoord a, HexCoord b) => new(a.Q - b.Q, a.R - b.R, a.S - b.S);

    public override string ToString() => $"Hex({Q},{R},{S})";
}
