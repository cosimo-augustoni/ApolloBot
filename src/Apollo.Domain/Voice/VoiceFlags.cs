namespace Apollo.Domain.Voice;

/// <summary>
/// The per-segment flags of a voice session (<c>voice_state_segments</c>, scope §5.1). Server mute and server deaf are
/// not recorded.
/// </summary>
/// <param name="SelfMute">Muted by the user.</param>
/// <param name="SelfDeaf">Deafened by the user.</param>
/// <param name="Streaming">Sharing the screen.</param>
/// <param name="Video">Camera on.</param>
public sealed record VoiceFlags(bool SelfMute, bool SelfDeaf, bool Streaming, bool Video)
{
    public static VoiceFlags None { get; } = new(SelfMute: false, SelfDeaf: false, Streaming: false, Video: false);
}
