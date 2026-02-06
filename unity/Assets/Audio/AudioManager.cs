using UnityEngine;
using System;
using System.Collections.Generic;

namespace Floodline.Client
{
    /// <summary>
    /// SFX event types that can be triggered during gameplay.
    /// Each event type corresponds to a specific sound or cue.
    /// </summary>
    public enum SFXEventType
    {
        // Piece interactions
        PieceLock,
        PieceHardDrop,
        PieceSoftDropTick,
        PieceRotate,

        // World interactions
        WorldRotate,      // Tilt/gravity change
        BlockCollapse,
        BlockShift,

        // Water/environment
        WaterSettle,
        WaterFlow,

        // Drain/special
        DrainTick,
        DrainRemove,

        // Freeze
        FreezeApply,
        FreezeThaw,

        // Wind
        WindGustWhoosh,
    }

    /// <summary>
    /// Data structure for an SFX event request.
    /// </summary>
    public struct SFXEvent
    {
        public SFXEventType Type;
        public Vector3 WorldPosition;  // Position to play sound at (for spatial audio)
        public float VolumeScale;       // Relative volume (0-1); 1.0 is default
        public float PitchVariation;    // Pitch shift (-1 to +1); 0 is no variation

        public SFXEvent(SFXEventType type, Vector3 position = default, float volume = 1f, float pitch = 0f)
        {
            Type = type;
            WorldPosition = position;
            VolumeScale = Mathf.Clamp01(volume);
            PitchVariation = Mathf.Clamp(pitch, -1f, 1f);
        }
    }

    /// <summary>
    /// AudioManager: Central coordinator for SFX playback and audio routing.
    /// - Maintains a pool of AudioSources for efficient sound spawning
    /// - Routes SFX events to appropriate clip selection and playback
    /// - Provides feedback (e.g., wind gust screen nudge)
    /// - Never modifies Core simulation state
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        [SerializeField]
        private int audioSourcePoolSize = 12;

        [SerializeField]
        private float masterVolume = 1f;

        [SerializeField]
        private GameObject audioSourcePrefab;

        private Queue<AudioSource> audioSourcePool;
        private Dictionary<SFXEventType, AudioClip> sfxClips;
        private List<AudioSource> activeAudioSources;

        public event Action<SFXEventType, Vector3> OnSFXTriggered;

        private void Awake()
        {
            InitializeAudioPool();
            LoadSFXClips();
        }

        private void InitializeAudioPool()
        {
            audioSourcePool = new Queue<AudioSource>(audioSourcePoolSize);
            activeAudioSources = new List<AudioSource>(audioSourcePoolSize);

            for (int i = 0; i < audioSourcePoolSize; i++)
            {
                GameObject audioSourceObj;
                if (audioSourcePrefab != null)
                {
                    audioSourceObj = Instantiate(audioSourcePrefab, transform);
                }
                else
                {
                    audioSourceObj = new GameObject($"AudioSource_{i}");
                    audioSourceObj.transform.parent = transform;
                }

                AudioSource audioSource = audioSourceObj.GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = audioSourceObj.AddComponent<AudioSource>();
                }

                audioSource.playOnAwake = false;
                audioSourcePool.Enqueue(audioSource);
            }
        }

        private void LoadSFXClips()
        {
            sfxClips = new Dictionary<SFXEventType, AudioClip>();

            // Load placeholder clips from Resources folder
            // In production, these would point to actual audio assets
            foreach (SFXEventType eventType in System.Enum.GetValues(typeof(SFXEventType)))
            {
                string clipName = $"SFX/{eventType}";
                AudioClip clip = Resources.Load<AudioClip>(clipName);
                // If clip not found, silently continue (allows silent placeholders)
                if (clip != null)
                    sfxClips[eventType] = clip;
            }
        }

        /// <summary>
        /// Triggers an SFX event: plays the appropriate sound and fires callbacks.
        /// </summary>
        public void PlaySFX(SFXEvent sfxEvent)
        {
            if (!sfxClips.TryGetValue(sfxEvent.Type, out AudioClip clip))
            {
                // No clip found; silently ignore (allows partial audio setup)
                return;
            }

            AudioSource audioSource = GetAudioSource();
            if (audioSource == null)
            {
                Debug.LogWarning("[Audio] Audio source pool exhausted");
                return;
            }

            audioSource.clip = clip;
            audioSource.volume = masterVolume * sfxEvent.VolumeScale;
            audioSource.pitch = 1f + sfxEvent.PitchVariation * 0.1f;
            audioSource.spatialBlend = 1f;  // 3D audio
            audioSource.transform.position = sfxEvent.WorldPosition;
            audioSource.Play();

            OnSFXTriggered?.Invoke(sfxEvent.Type, sfxEvent.WorldPosition);
        }

        /// <summary>
        /// Gets an available audio source from the pool or creates a temporary one.
        /// </summary>
        private AudioSource GetAudioSource()
        {
            if (audioSourcePool.Count > 0)
            {
                return audioSourcePool.Dequeue();
            }

            // Pool exhausted; create temporary audio source
            GameObject tempObj = new GameObject("AudioSource_Temp");
            tempObj.transform.parent = transform;
            AudioSource tempSource = tempObj.AddComponent<AudioSource>();
            tempSource.playOnAwake = false;
            return tempSource;
        }

        /// <summary>
        /// Returns an audio source to the pool after playback completes.
        /// </summary>
        public void ReturnAudioSource(AudioSource audioSource)
        {
            if (audioSource != null)
            {
                audioSource.Stop();
                audioSource.clip = null;
                if (audioSourcePool.Count < audioSourcePoolSize)
                {
                    audioSourcePool.Enqueue(audioSource);
                }
                else
                {
                    Destroy(audioSource.gameObject);
                }
            }
        }

        public void SetMasterVolume(float volume)
        {
            masterVolume = Mathf.Clamp01(volume);
        }
    }
}
