using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class MenuMusic : MonoBehaviour
{
    [Tooltip("The looping menu track (Assets/Music/game-theme.mp3).")]
    [SerializeField] private AudioClip theme;
    [SerializeField, Range(0f, 1f)] private float volume = 0.45f;

    private void Awake()
    {
        AudioSource source = GetComponent<AudioSource>();
        source.clip = theme;
        source.loop = true;
        source.playOnAwake = true;
        source.spatialBlend = 0f;
        source.volume = volume;
        source.ignoreListenerPause = true;
    }

    private void OnEnable()
    {
        AudioSource source = GetComponent<AudioSource>();
        if (theme != null && !source.isPlaying)
        {
            source.Play();
        }
    }
}
