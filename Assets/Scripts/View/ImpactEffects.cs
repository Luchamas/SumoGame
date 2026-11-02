using UnityEngine;

namespace SumoGame
{
    /// <summary>Bursts of sparks and dust where hits land.</summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class ImpactEffects : MonoBehaviour
    {
        const float ContactHeight = 1.5f;

        static readonly Color Spark = new Color(1f, 0.92f, 0.65f);
        static readonly Color Dust = new Color(0.55f, 0.42f, 0.28f);

        public Material Material;

        ParticleSystem particles;

        void Awake()
        {
            particles = GetComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.maxParticles = 200;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.6f;

            var emission = particles.emission;
            emission.enabled = false;
            var shape = particles.shape;
            shape.enabled = false;

            var size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            var material = new Material(Material);
            material.SetTexture("_BaseMap", SoftDot());
            GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
        }

        /// <summary>A round spot that fades to nothing at the edge, so particles are not squares.</summary>
        static Texture2D SoftDot()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var offset = new Vector2(x - size * 0.5f + 0.5f, y - size * 0.5f + 0.5f) / (size * 0.5f);
                    var glow = Mathf.Clamp01(1.4f - offset.magnitude * 1.4f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, 1f) * Mathf.Clamp01(glow));
                }
            }
            texture.Apply();
            return texture;
        }

        public void Burst(SumoImpact impact, Vector2 where)
        {
            var point = new Vector3(where.x, ContactHeight, where.y);
            switch (impact)
            {
                case SumoImpact.Strike:
                    Emit(point, 30, Spark, 4.5f, 0.42f);
                    break;
                case SumoImpact.Grab:
                    Emit(point, 18, Spark, 3f, 0.32f);
                    break;
                case SumoImpact.Clash:
                    Emit(point, 20, Spark, 3.5f, 0.36f);
                    break;
                case SumoImpact.Ducked:
                    Emit(point + Vector3.up * 0.3f, 10, Spark, 2f, 0.2f);
                    break;
                case SumoImpact.Shove:
                    Emit(new Vector3(where.x, 0.1f, where.y), 10, Dust, 1.6f, 0.45f);
                    break;
                case SumoImpact.Drive:
                    Emit(point, 34, Spark, 5f, 0.44f);
                    Emit(new Vector3(where.x, 0.1f, where.y), 14, Dust, 2f, 0.34f);
                    break;
                case SumoImpact.RingOut:
                    Emit(new Vector3(where.x, 0.1f, where.y), 30, Dust, 2.6f, 0.42f);
                    break;
            }
        }

        void Emit(Vector3 position, int count, Color color, float speed, float size)
        {
            for (var i = 0; i < count; i++)
            {
                var emit = new ParticleSystem.EmitParams
                {
                    position = position,
                    velocity = Random.onUnitSphere * (speed * Random.Range(0.4f, 1f)) + Vector3.up,
                    startColor = color,
                    startSize = size * Random.Range(0.6f, 1.2f),
                    startLifetime = Random.Range(0.18f, 0.42f),
                };
                particles.Emit(emit, 1);
            }
        }
    }
}
