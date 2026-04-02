using Unity.Netcode;
using UnityEngine;

/// <summary>
/// HexCoord의 네트워크 직렬화 래퍼
/// </summary>
public struct HexCoordNet : INetworkSerializable, System.IEquatable<HexCoordNet>
{
    public int Q;
    public int R;

    public HexCoordNet(int q, int r) { Q = q; R = r; }
    public HexCoordNet(HexCoord coord) { Q = coord.Q; R = coord.R; }

    public HexCoord ToHexCoord() => new(Q, R);

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Q);
        serializer.SerializeValue(ref R);
    }

    public bool Equals(HexCoordNet other) => Q == other.Q && R == other.R;
    public override bool Equals(object obj) => obj is HexCoordNet other && Equals(other);
    public override int GetHashCode() => System.HashCode.Combine(Q, R);
    public override string ToString() => $"HexNet({Q},{R})";
}

/// <summary>
/// 5종 자원 고정 배열 (Wood, Brick, Wool, Wheat, Ore 순)
/// </summary>
public struct ResArray : INetworkSerializable
{
    public int Wood;
    public int Brick;
    public int Wool;
    public int Wheat;
    public int Ore;

    public int this[ResourceType type]
    {
        get => type switch
        {
            ResourceType.Wood => Wood,
            ResourceType.Brick => Brick,
            ResourceType.Wool => Wool,
            ResourceType.Wheat => Wheat,
            ResourceType.Ore => Ore,
            _ => 0,
        };
        set
        {
            switch (type)
            {
                case ResourceType.Wood: Wood = value; break;
                case ResourceType.Brick: Brick = value; break;
                case ResourceType.Wool: Wool = value; break;
                case ResourceType.Wheat: Wheat = value; break;
                case ResourceType.Ore: Ore = value; break;
            }
        }
    }

    public int Total => Wood + Brick + Wool + Wheat + Ore;

    public static ResArray FromDict(System.Collections.Generic.Dictionary<ResourceType, int> dict)
    {
        var arr = new ResArray();
        if (dict == null) return arr;
        foreach (var kv in dict)
            arr[kv.Key] = kv.Value;
        return arr;
    }

    public System.Collections.Generic.Dictionary<ResourceType, int> ToDict()
    {
        return new()
        {
            { ResourceType.Wood, Wood },
            { ResourceType.Brick, Brick },
            { ResourceType.Wool, Wool },
            { ResourceType.Wheat, Wheat },
            { ResourceType.Ore, Ore },
        };
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Wood);
        serializer.SerializeValue(ref Brick);
        serializer.SerializeValue(ref Wool);
        serializer.SerializeValue(ref Wheat);
        serializer.SerializeValue(ref Ore);
    }
}

/// <summary>
/// 타일 상태 스냅샷 (보드 동기화용)
/// </summary>
public struct TileSnapshot : INetworkSerializable
{
    public HexCoordNet Coord;
    public ResourceType Resource;
    public int NumberToken;
    public bool HasRobber;

    public TileSnapshot(HexTile tile)
    {
        Coord = new HexCoordNet(tile.Coord);
        Resource = tile.Resource;
        NumberToken = tile.NumberToken;
        HasRobber = tile.HasRobber;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Coord);
        int res = (int)Resource;
        serializer.SerializeValue(ref res);
        Resource = (ResourceType)res;
        serializer.SerializeValue(ref NumberToken);
        serializer.SerializeValue(ref HasRobber);
    }
}

/// <summary>
/// 꼭짓점 상태 스냅샷 (건물 + 항구)
/// </summary>
public struct VertexSnapshot : INetworkSerializable
{
    public int Id;
    public int OwnerPlayerIndex;
    public BuildingType Building;
    public PortType Port;

    public VertexSnapshot(HexVertex vertex)
    {
        Id = vertex.Id;
        OwnerPlayerIndex = vertex.OwnerPlayerIndex;
        Building = vertex.Building;
        Port = vertex.Port;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Id);
        serializer.SerializeValue(ref OwnerPlayerIndex);
        int b = (int)Building;
        serializer.SerializeValue(ref b);
        Building = (BuildingType)b;
        int p = (int)Port;
        serializer.SerializeValue(ref p);
        Port = (PortType)p;
    }
}

/// <summary>
/// 변 상태 스냅샷 (도로)
/// </summary>
public struct EdgeSnapshot : INetworkSerializable
{
    public int Id;
    public int OwnerPlayerIndex;
    public bool HasRoad;

    public EdgeSnapshot(HexEdge edge)
    {
        Id = edge.Id;
        OwnerPlayerIndex = edge.OwnerPlayerIndex;
        HasRoad = edge.HasRoad;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Id);
        serializer.SerializeValue(ref OwnerPlayerIndex);
        serializer.SerializeValue(ref HasRoad);
    }
}

/// <summary>
/// 플레이어 공개 정보 스냅샷 (재접속 동기화용)
/// </summary>
public struct PlayerPublicSnapshot : INetworkSerializable
{
    public int PlayerIndex;
    public int VP;
    public int TotalResourceCount;
    public int KnightsPlayed;
    public int RoadsRemaining;
    public int SettlementsRemaining;
    public int CitiesRemaining;

    public PlayerPublicSnapshot(PlayerState ps)
    {
        PlayerIndex = ps.PlayerIndex;
        VP = ps.VictoryPoints;
        TotalResourceCount = ps.TotalResourceCount;
        KnightsPlayed = ps.KnightsPlayed;
        RoadsRemaining = ps.RoadsRemaining;
        SettlementsRemaining = ps.SettlementsRemaining;
        CitiesRemaining = ps.CitiesRemaining;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref PlayerIndex);
        serializer.SerializeValue(ref VP);
        serializer.SerializeValue(ref TotalResourceCount);
        serializer.SerializeValue(ref KnightsPlayed);
        serializer.SerializeValue(ref RoadsRemaining);
        serializer.SerializeValue(ref SettlementsRemaining);
        serializer.SerializeValue(ref CitiesRemaining);
    }
}

/// <summary>
/// 전체 보드 스냅샷 (게임 시작/재접속 시 일괄 동기화)
/// Netcode의 INetworkSerializable 배열은 고정크기 써야 안전하므로 직접 길이 관리
/// </summary>
public struct BoardSnapshot : INetworkSerializable
{
    public TileSnapshot[] Tiles;
    public VertexSnapshot[] Vertices;
    public EdgeSnapshot[] Edges;
    public PlayerPublicSnapshot[] Players;

    // 게임 상태
    public int TurnNumber;
    public int CurrentPlayerIndex;
    public int FirstPlayerIndex;
    public int CurrentPhase;         // GamePhase as int
    public int CurrentBuildMode;     // BuildMode as int
    public HexCoordNet RobberPosition;
    public int DevCardDeckRemaining;
    public int LongestRoadHolder;
    public int LargestArmyHolder;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        // 게임 상태
        serializer.SerializeValue(ref TurnNumber);
        serializer.SerializeValue(ref CurrentPlayerIndex);
        serializer.SerializeValue(ref FirstPlayerIndex);
        serializer.SerializeValue(ref CurrentPhase);
        serializer.SerializeValue(ref CurrentBuildMode);
        serializer.SerializeValue(ref RobberPosition);
        serializer.SerializeValue(ref DevCardDeckRemaining);
        serializer.SerializeValue(ref LongestRoadHolder);
        serializer.SerializeValue(ref LargestArmyHolder);

        // 타일
        int tileCount = Tiles?.Length ?? 0;
        serializer.SerializeValue(ref tileCount);
        if (serializer.IsReader) Tiles = new TileSnapshot[tileCount];
        for (int i = 0; i < tileCount; i++)
            serializer.SerializeValue(ref Tiles[i]);

        // 꼭짓점
        int vertexCount = Vertices?.Length ?? 0;
        serializer.SerializeValue(ref vertexCount);
        if (serializer.IsReader) Vertices = new VertexSnapshot[vertexCount];
        for (int i = 0; i < vertexCount; i++)
            serializer.SerializeValue(ref Vertices[i]);

        // 변
        int edgeCount = Edges?.Length ?? 0;
        serializer.SerializeValue(ref edgeCount);
        if (serializer.IsReader) Edges = new EdgeSnapshot[edgeCount];
        for (int i = 0; i < edgeCount; i++)
            serializer.SerializeValue(ref Edges[i]);

        // 플레이어
        int playerCount = Players?.Length ?? 0;
        serializer.SerializeValue(ref playerCount);
        if (serializer.IsReader) Players = new PlayerPublicSnapshot[playerCount];
        for (int i = 0; i < playerCount; i++)
            serializer.SerializeValue(ref Players[i]);
    }
}
