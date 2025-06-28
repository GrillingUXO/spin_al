// MediaPipeDataProcessor.cs
public class MediaPipeDataProcessor
{
    private readonly ISpineViewer _viewer;
    
    public MediaPipeDataProcessor(ISpineViewer viewer) 
        => _viewer = viewer;

    public void ProcessPoseData(MediaPipeSpineProtocol.PoseData pose)
    {
        // 1. 更新核心骨骼
        UpdateCoreBones(pose.Landmarks);
        
        // 2. 更新肢体链
        UpdateLimbChain("LEFT", pose.Landmarks);
        UpdateLimbChain("RIGHT", pose.Landmarks);
        
        // 3. 更新手指等细节骨骼
        UpdateFingers(pose.Landmarks);
        
        // 4. 应用物理更新
        _viewer.UpdateSkeletonTransforms();
    }

    private void UpdateLimbChain(string side, Dictionary<string, float[]> landmarks)
    {
        // 手臂链
        CalculateLimbRotation(
            landmarks[$"{side}_SHOULDER"],
            landmarks[$"{side}_ELBOW"],
            landmarks[$"{side}_WRIST"],
            $"upper_arm_{side[0]}".ToLower(),
            $"lower_arm_{side[0]}".ToLower()
        );
        
        // 腿部链
        CalculateLimbRotation(
            landmarks[$"{side}_HIP"],
            landmarks[$"{side}_KNEE"],
            landmarks[$"{side}_ANKLE"],
            $"upper_leg_{side[0]}".ToLower(),
            $"lower_leg_{side[0]}".ToLower()
        );
    }
    
    // 优化后的肢体旋转计算
    private void CalculateLimbRotation(
        float[] jointA, 
        float[] jointB, 
        float[] jointC,
        string boneAB,
        string boneBC)
    {
        var bone1 = _viewer.GetBone(boneAB);
        var bone2 = _viewer.GetBone(boneBC);
        if (bone1 == null || bone2 == null) return;

        // 计算骨骼向量
        var vecAB = new Vector2(jointB[0] - jointA[0], jointB[1] - jointA[1]);
        var vecBC = new Vector2(jointC[0] - jointB[0], jointC[1] - jointB[1]);
        
        // 计算世界空间旋转
        bone1.Rotation = vecAB.ToAngleDegrees();
        
        // 计算相对旋转 (bone2相对于bone1)
        bone2.Rotation = vecBC.ToAngleDegrees() - bone1.Rotation;
        
        // 更新骨骼
        _viewer.UpdateBoneTransform(bone1);
        _viewer.UpdateBoneTransform(bone2);
    }
}

// 向量辅助类
public struct Vector2
{
    public float X, Y;
    
    public Vector2(float x, float y) => (X, Y) = (x, y);
    
    public float ToAngleDegrees() 
        => (float)(Math.Atan2(Y, X) * 180 / Math.PI);
}