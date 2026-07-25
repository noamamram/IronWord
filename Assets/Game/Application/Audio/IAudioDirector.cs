namespace TypingDefense.Application.Audio
{
    /// <summary>Phase D stub — SFX / music bus.</summary>
    public interface IAudioDirector
    {
        void PlayOneShot(string cueId, float volume = 1f);
        void SetMusicState(string stateId);
    }

    public sealed class NullAudioDirector : IAudioDirector
    {
        public void PlayOneShot(string cueId, float volume = 1f) { }
        public void SetMusicState(string stateId) { }
    }
}
