using UnityEngine;

// Builds a flat grid of water and makes it wave using Perlin noise.
// Put this on an empty GameObject called "OceanWater".
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class OceanWaves : MonoBehaviour
{
    [Header("Size")]
    [SerializeField] private int resolution = 100;     // grid squares per side (100 is fine; don't go above 250)
    [SerializeField] private float size = 300f;        // ocean width/length in Unity units (meters)

    [Header("Waves")]
    [SerializeField] private float waveHeight = 0.6f;  // how tall the waves are
    [SerializeField] private float waveLength = 14f;   // size of the main swells in meters
    [SerializeField] private float waveSpeed = 1.2f;   // how fast the main swells travel (m/s)

    [Header("Detail Noise")]
    [SerializeField] private float noiseScale = 2.3f;  // detail size: 2.3 = detail is 2.3x finer than the swells (higher = smaller ripples)
    [SerializeField] private float noiseSpeed = 1.3f;  // detail speed: 1.3 = detail moves 1.3x as fast as the swells

    /// <summary>The ocean in the scene, so ship prefabs don't need a scene reference.</summary>
    public static OceanWaves Instance { get; private set; }

    Mesh mesh;
    Vector3[] baseVerts;  // original flat positions
    Vector3[] verts;      // positions we move every frame

    // The wave pattern is a pure function of (x, z, waveTime). Every machine must use the same waveTime
    // for the water to match. Offline it is just Time.time; online, the network layer calls SetTimeOffset
    // with (serverTime - Time.time) so all clients agree.
    float waveTime;
    float timeOffset;

    public void SetTimeOffset(float seconds)
    {
        timeOffset = seconds;
    }

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

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
        // Sampled once per frame, so physics (GetHeight) and the visible mesh always agree.
        waveTime = Time.time + timeOffset;

        if (mesh == null) return;

        for (int i = 0; i < baseVerts.Length; i++)
        {
            Vector3 p = baseVerts[i];
            p.y = WaveFunction(p.x + transform.position.x, p.z + transform.position.z, waveTime);
            verts[i] = p;
        }
        mesh.vertices = verts;
        mesh.RecalculateNormals(); // keeps lighting correct as waves move
    }

    // Two layers of Perlin noise: big swells + smaller, faster detail.
    float WaveFunction(float x, float z, float time)
    {
        const float offset = 10000f;            // keeps inputs positive to avoid mirroring at 0
        float scale = 1f / waveLength;          // noise units per meter
        float travel = time * waveSpeed;        // meters the swells have moved so far
        float detailTravel = travel * noiseSpeed;

        // Layer 1: main swells
        float n1 = Mathf.PerlinNoise((x + offset - travel) * scale,
                                     (z + offset - travel * 0.3f) * scale);

        // Layer 2: detail, scaled by noiseScale and moved by noiseSpeed
        float n2 = Mathf.PerlinNoise((x + offset + detailTravel * 0.5f) * scale * noiseScale,
                                     (z + offset - detailTravel * 0.8f) * scale * noiseScale);

        // Remap 0..1 to -1..1
        n1 = n1 * 2f - 1f;
        n2 = n2 * 2f - 1f;

        return (n1 + 0.5f * n2) * waveHeight;
    }

    /// <summary>Water surface height (world Y) at a world XZ position, matching what is currently drawn.</summary>
    public float GetHeight(float worldX, float worldZ)
    {
        return transform.position.y + WaveFunction(worldX, worldZ, waveTime);
    }
}