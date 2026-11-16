namespace Game50033
{
    using System;
    using UnityEngine;
    using UnityEngine.Audio;

    // what an attack tells its hit effect; the effect never feeds anything back into gameplay.
    public readonly struct HitEffectRequest
    {
        public readonly Vector3 Point;
        public readonly Vector3 Direction;
        public readonly float Damage;
        public readonly bool Big;

        public HitEffectRequest(Vector3 point, Vector3 direction, float damage, bool big)
        {
            Point = point;
            Direction = direction;
            Damage = damage;
            Big = big;
        }
    }

    /// <summary>
    /// Presentation played where a hit lands: impact sprite, particles, damage number and sound.
    /// One asset per weapon under Assets/Tuning. Shared by every player using that weapon, so it holds no
    /// per-player state; spawned objects belong to the active (encounter) scene and remove themselves.
    /// Each section has a normal and a big-hit style; an empty big style falls back to the normal one.
    /// </summary>
    [CreateAssetMenu(menuName = "Game50033/Tuning/Hit Effect", fileName = "HitEffect")]
    public sealed class HitEffect : ScriptableObject
    {
        public RenderingSettings rendering = new();
        public ImpactSpriteSettings impactSprite = new();
        public ParticleSettings particles = new();
        public DamageNumberSettings damageNumbers = new();
        public SoundSettings sound = new();

        [Serializable]
        public sealed class RenderingSettings
        {
            [Tooltip("Unlit, vertex-coloured sprite material shared by impact sprites and damage numbers.")]
            public Material material;
            [Tooltip("Layer the spawned sprites render on. PixelWorldVFX is the project's layer for pixelated transparent effects.")]
            public string layer = "PixelWorldVFX";
            [Min(1f)] public float pixelsPerUnit = 50f;
        }

        [Serializable]
        public struct ImpactSpriteStyle
        {
            [Tooltip("Horizontal strip of equally wide frames, played left to right once.")]
            public Texture2D frameStrip;
            [Min(1)] public int frameCount;
            [Min(0.01f)] public float frameDuration;
            [Min(0.01f)] public float scale;
            public Color color;
        }

        [Serializable]
        public sealed class ImpactSpriteSettings
        {
            public ImpactSpriteStyle normal = new() { frameCount = 4, frameDuration = 0.04f, scale = 1f, color = Color.white };
            public ImpactSpriteStyle big = new() { frameCount = 4, frameDuration = 0.05f, scale = 1f, color = Color.white };
        }

        [Serializable]
        public sealed class ParticleSettings
        {
            [Tooltip("Played once at the contact point, facing along the hit direction. Leave empty for none.")]
            public ParticleSystem normalPrefab;
            public ParticleSystem bigPrefab;
        }

        [Serializable]
        public struct DamageNumberStyle
        {
            public Color color;
            [Min(0.01f)] public float scale;
            [Min(0f)] public float riseDistance;
            [Min(0.01f)] public float lifetime;
        }

        [Serializable]
        public sealed class DamageNumberSettings
        {
            public bool show = true;
            [Tooltip("Horizontal strip of equally wide glyphs, in the order given by Glyph Order.")]
            public Texture2D glyphStrip;
            public string glyphOrder = "0123456789.";
            [Tooltip("Pixels between glyphs (can be negative to overlap outlines).")]
            public int glyphSpacing = -1;
            public string format = "0.#";
            [Tooltip("Spawn height above the contact point.")]
            public float heightOffset = 0.35f;
            [Tooltip("Random sideways spread so numbers from simultaneous hits do not stack exactly.")]
            [Min(0f)] public float jitter = 0.12f;
            public DamageNumberStyle normal = new() { color = Color.white, scale = 1f, riseDistance = 0.35f, lifetime = 0.6f };
            public DamageNumberStyle big = new() { color = new Color(1f, 0.82f, 0.3f), scale = 1.6f, riseDistance = 0.5f, lifetime = 0.8f };
        }

        [Serializable]
        public struct SoundStyle
        {
            [Tooltip("One is picked at random per hit.")]
            public AudioClip[] clips;
            [Range(0f, 1f)] public float volume;
            [Tooltip("Random pitch range (min, max).")]
            public Vector2 pitch;
        }

        [Serializable]
        public sealed class SoundSettings
        {
            [Tooltip("Optional mixer group. The project has no Audio Mixer yet.")]
            public AudioMixerGroup output;
            public SoundStyle normal = new() { volume = 0.8f, pitch = new Vector2(0.95f, 1.05f) };
            public SoundStyle big = new() { volume = 1f, pitch = new Vector2(0.95f, 1.05f) };
        }

        // derived from the textures at runtime; rebuilt if the texture or frame layout changes.
        [NonSerialized] private SpriteStripCache _normalFrames;
        [NonSerialized] private SpriteStripCache _bigFrames;
        [NonSerialized] private SpriteStripCache _glyphs;

        public void Play(in HitEffectRequest hit)
        {
            var layer = LayerMask.NameToLayer(rendering.layer);
            if (layer < 0) layer = 0;
            var facing = hit.Direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(hit.Direction) : Quaternion.identity;

            PlayImpactSprite(hit, layer);

            var particlePrefab = hit.Big && particles.bigPrefab != null ? particles.bigPrefab : particles.normalPrefab;
            if (particlePrefab != null)
            {
                var instance = Instantiate(particlePrefab, hit.Point, facing);
                var main = instance.main;
                main.stopAction = ParticleSystemStopAction.Destroy;
                instance.Play(true);
            }

            if (damageNumbers.show && damageNumbers.glyphStrip != null && rendering.material != null)
            {
                var style = hit.Big ? damageNumbers.big : damageNumbers.normal;
                var glyphs = Strip(ref _glyphs, damageNumbers.glyphStrip, Mathf.Max(1, damageNumbers.glyphOrder.Length));
                var jitter = UnityEngine.Random.insideUnitCircle * damageNumbers.jitter;
                var position = hit.Point + Vector3.up * damageNumbers.heightOffset + new Vector3(jitter.x, 0f, jitter.y);
                DamageNumber.Spawn(position, hit.Damage.ToString(damageNumbers.format), damageNumbers.glyphOrder, glyphs,
                    damageNumbers.glyphSpacing / rendering.pixelsPerUnit, style, rendering.material, layer);
            }

            var sounds = hit.Big && HasClips(sound.big) ? sound.big : sound.normal;
            if (HasClips(sounds)) PlaySound(sounds);
        }

        private void PlayImpactSprite(in HitEffectRequest hit, int layer)
        {
            if (rendering.material == null) return;
            var style = hit.Big && impactSprite.big.frameStrip != null ? impactSprite.big : impactSprite.normal;
            if (style.frameStrip == null) return;
            var frames = hit.Big && style.frameStrip == impactSprite.big.frameStrip
                ? Strip(ref _bigFrames, style.frameStrip, style.frameCount)
                : Strip(ref _normalFrames, style.frameStrip, style.frameCount);
            HitSpriteAnimation.Spawn(hit.Point, frames, style.frameDuration, style.scale, style.color, rendering.material, layer);
        }

        private void PlaySound(in SoundStyle style)
        {
            var clip = style.clips[UnityEngine.Random.Range(0, style.clips.Length)];
            if (clip == null) return;
            var go = new GameObject($"{name} sound");
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = style.volume;
            source.pitch = Mathf.Max(0.01f, UnityEngine.Random.Range(style.pitch.x, style.pitch.y));
            source.spatialBlend = 0f;
            source.outputAudioMixerGroup = sound.output;
            source.Play();
            Destroy(go, clip.length / source.pitch + 0.1f);
        }

        private static bool HasClips(in SoundStyle style) => style.clips != null && style.clips.Length > 0;

        private Sprite[] Strip(ref SpriteStripCache cache, Texture2D texture, int count)
        {
            if (cache.Texture != texture || cache.Sprites == null || cache.Sprites.Length != count)
                cache = SpriteStripCache.Build(texture, count, rendering.pixelsPerUnit);
            return cache.Sprites;
        }

        private struct SpriteStripCache
        {
            public Texture2D Texture;
            public Sprite[] Sprites;

            public static SpriteStripCache Build(Texture2D texture, int count, float pixelsPerUnit)
            {
                var width = texture.width / count;
                var sprites = new Sprite[count];
                for (var i = 0; i < count; i++)
                {
                    sprites[i] = Sprite.Create(texture, new Rect(i * width, 0, width, texture.height),
                        new Vector2(0.5f, 0.5f), pixelsPerUnit, 0, SpriteMeshType.FullRect);
                    sprites[i].name = $"{texture.name}_{i}";
                }
                return new SpriteStripCache { Texture = texture, Sprites = sprites };
            }
        }
    }
}
