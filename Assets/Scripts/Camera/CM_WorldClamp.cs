//Cinemachineのカメラの移動範囲を制限するスクリプト
using UnityEngine;
using Unity.Cinemachine;

[ExecuteAlways]
public class CM_WorldClamp : CinemachineExtension
{
    // 制限したい範囲（ワールド座標）
    public Vector3 min = new Vector3(-90f, -99999f, -99999f);
    public Vector3 max = new Vector3( 99999f,  99999f,  99999f);

    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage,
        ref CameraState state,
        float deltaTime)
    {
        // Body（位置）が決まったあとにクランプする
        if (stage != CinemachineCore.Stage.Body) return;

        var p = state.GetFinalPosition();
        var clamped = new Vector3(
            Mathf.Clamp(p.x, min.x, max.x),
            Mathf.Clamp(p.y, min.y, max.y),
            Mathf.Clamp(p.z, min.z, max.z)
        );

        state.PositionCorrection += (clamped - p);
    }
}
