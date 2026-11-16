using Unity.Cinemachine;
using UnityEngine;

public sealed class SertaoCoinImpact : CinemachineExtension
{
    private const float Duration = 0.19f;
    private float started = float.NegativeInfinity;

    public static void Play()
    {
        Camera camera = Camera.main;
        var brain = camera != null ? camera.GetComponent<CinemachineBrain>() : null;
        if (brain == null || !(brain.ActiveVirtualCamera is CinemachineVirtualCameraBase shot)) return;

        var impact = shot.GetComponent<SertaoCoinImpact>();
        if (impact == null) impact = shot.gameObject.AddComponent<SertaoCoinImpact>();
        impact.started = Time.time;
    }

    protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Finalize) return;
        float age = Time.time - started;
        if (age < 0f || age >= Duration) return;

        float decay = 1f - age / Duration;
        float beat = age * 83f;
        Vector3 offset = new Vector3(Mathf.Sin(beat + 1.1f), Mathf.Sin(beat * 0.72f + 2f) * 0.58f, 0f);
        state.PositionCorrection += state.RawOrientation * offset * (0.09f * decay * decay);
    }
}
