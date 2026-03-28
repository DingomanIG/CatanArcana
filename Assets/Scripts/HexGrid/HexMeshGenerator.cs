using UnityEngine;

/// <summary>
/// 프로시저럴 헥스 메시 생성기
/// </summary>
public static class HexMeshGenerator
{
    /// <summary>flat-top 헥스 메시 생성 (XZ 평면, 위에서 보는 방향)</summary>
    public static Mesh CreateFlatHexMesh(float size)
    {
        var mesh = new Mesh();
        mesh.name = "HexMesh";

        var vertices = new Vector3[7];
        var triangles = new int[18];
        var uv = new Vector2[7];

        // 중심 꼭짓점
        vertices[0] = Vector3.zero;
        uv[0] = new Vector2(0.5f, 0.5f);

        // 6개 코너 (flat-top)
        for (int i = 0; i < 6; i++)
        {
            float angle = Mathf.Deg2Rad * (60f * i);
            vertices[i + 1] = new Vector3(
                size * Mathf.Cos(angle),
                0f,
                size * Mathf.Sin(angle));

            uv[i + 1] = new Vector2(
                0.5f + 0.5f * Mathf.Cos(angle),
                0.5f + 0.5f * Mathf.Sin(angle));
        }

        // 삼각형 팬 (CCW winding, 위에서 보이도록)
        for (int i = 0; i < 6; i++)
        {
            int t = i * 3;
            triangles[t] = 0;
            triangles[t + 1] = (i + 1) % 6 + 1;
            triangles[t + 2] = i + 1;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uv;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }
}
