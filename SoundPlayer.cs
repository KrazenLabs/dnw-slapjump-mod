using UnityEngine;
using UnityEngine.Audio;

namespace SlapJump
{
    // Adds effects to the actual sounds
    internal static class SoundPlayer
    {
        private const float DefaultLifetime = 3f;

        // Adds a second playback to increase beyond max volume
        public static void Play(AudioResource resource, Vector3 point, float pitch, float extraVolume)
        {
            PlayOnce(resource, point, pitch, 1f);
            if (extraVolume > 0.01f) PlayOnce(resource, point, pitch, Mathf.Min(extraVolume, 1f));
        }

        private static void PlayOnce(AudioResource resource, Vector3 point, float pitch, float volume)
        {
            var go = new GameObject("SlapJumpSFX", typeof(AudioSource));
            go.transform.position = point;
            var source = go.GetComponent<AudioSource>();
            AudioHelper.SetupAudioSourceForSFX(source);
            source.resource = resource;
            source.pitch = pitch;
            source.volume = volume;
            source.Play();
            // Increase length for lower pitches
            float length = source.clip != null ? source.clip.length : DefaultLifetime;
            Object.Destroy(go, length / Mathf.Max(pitch, 0.1f) + 0.1f);
        }
    }
}
