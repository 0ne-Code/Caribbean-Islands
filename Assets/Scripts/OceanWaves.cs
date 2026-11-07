using UnityEngine;

// Builds a flat grid of water and makes it wave using sine math.
// Put this on an empty GameObject called "OceanWater".
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class OceanWaves : MonoBehaviour
{
    [Header("Size")]
    public int resolution = 100;     // grid squares per side (100 is fine; don't go above 250)
    public float size = 300f;        // ocean width/length in Unity units (meters)

    [Header("Waves")]
    public float waveHeight = 0.6f;  // how tall the waves are
    public float waveLength = 14f;   // distance from one wave crest to the next
    public float waveSpeed = 1.2f;   // how fast waves move

    Mesh mesh;
    Vector3[] baseVerts;  // original flat positions
    Vector3[] verts;      // positions we move every frame

    void Start()
    {
        BuildGrid();
    }

    void BuildGrid()
    {
        int count = resolution + 1;
        baseVerts = new Vector3[count * count];
        verts = new Vector3[count * count];
        Vector2[] uvs = new Vector2[count * count];

        // Place points in a flat grid centered on this object
        for (int z = 0; z < count; z++)
        {
            for (int x = 0; x < count; x++)
            {
                float px = ((float)x / resolution - 0.5f) * size;
                float pz = ((float)z / resolution - 0.5f) * size;
                int i = z * count + x;
                baseVerts[i] = new Vector3(px, 0f, pz);
                uvs[i] = new Vector2((float)x / resolution, (float)z / resolution);
            }
        }

        // Connect points into triangles (2 triangles per grid square)
        int[] tris = new int[resolution * resolution * 6];
        int t = 0;
        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int i = z * count + x;
                tris[t++] = i;
                tris[t++] = i + count;
                tris[t++] = i + 1;
                tris[t++] = i + 1;
                tris[t++] = i + count;
                tris[t++] = i + count + 1;
            }
        }

        mesh = new Mesh();
        mesh.name = "OceanMesh";
        mesh.MarkDynamic();              // tells Unity we change this mesh often
        mesh.vertices = baseVerts;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        GetComponent<MeshFilter>().mesh = mesh;

        // Water doesn't need shadows; turning them off saves performance
        MeshRenderer r = GetComponent<MeshRenderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    void Update()
    {
        if (mesh == null) return;

        float time = Time.time;
        for (int i = 0; i < baseVerts.Length; i++)
        {
            Vector3 p = baseVerts[i];
            // Wave height depends on the point's position + time
            p.y = WaveFunction(p.x + transform.position.x, p.z + transform.position.z, time);
            verts[i] = p;
        }
        mesh.vertices = verts;
        mesh.RecalculateNormals(); // keeps lighting correct as waves move
    }

    // Two sine waves added together look more natural than one.
    float WaveFunction(float x, float z, float time)
    {
        float k = 2f * Mathf.PI / waveLength;
        float w1 = Mathf.Sin(x * k + time * waveSpeed);
        float w2 = Mathf.Sin((x * 0.6f + z * 0.8f) * k * 1.3f + time * waveSpeed * 1.3f);
        return (w1 + 0.5f * w2) * waveHeight;
    }

    // We'll use this next step to make ships float.
    public float GetHeight(float worldX, float worldZ)
    {
        return transform.position.y + WaveFunction(worldX, worldZ, Time.time);
    }
}