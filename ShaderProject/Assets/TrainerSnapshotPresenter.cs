using UnityEngine;
using UnityEngine.Rendering;

// Reproduces EFT's post-processing: capture first, then present that cached image.
// EFT's supersampling path captures at AfterForwardAlpha; postprocessing consumes the copy.
[ExecuteAlways]
public sealed class TrainerSnapshotPresenter : MonoBehaviour
{
    private Camera _camera;
    private RenderTexture _snapshot;
    private CommandBuffer _capture;
    public int PresentedFrames { get; private set; }
    public RenderTexture Snapshot => _snapshot;

    private void OnEnable()
    {
        _camera = GetComponent<Camera>();
        _snapshot = new RenderTexture(_camera.pixelWidth, _camera.pixelHeight, 0, RenderTextureFormat.ARGB32);
        _snapshot.Create();
        _capture = new CommandBuffer { name = "Regression: capture before final presentation" };
        _capture.Blit(BuiltinRenderTextureType.CameraTarget, _snapshot);
        _camera.AddCommandBuffer(CameraEvent.AfterForwardAlpha, _capture);
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        PresentedFrames++;
        Graphics.Blit(_snapshot, destination);
    }

    private void OnDisable()
    {
        _camera.RemoveCommandBuffer(CameraEvent.AfterForwardAlpha, _capture);
        _capture.Release();
        _snapshot.Release();
        DestroyImmediate(_snapshot);
    }
}
