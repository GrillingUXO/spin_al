// SpineControlServer.cs
using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

public class EnhancedSpineControlServer
{
    private readonly ISpineViewer _viewer;
    private bool isRunning = true;
    
    // 骨骼映射配置
    private readonly Dictionary<string, string> _mediaPipeToSpineMapping = new Dictionary<string, string>
    {
        // 手臂骨骼映射
        { "LEFT_SHOULDER", "upper_arm_l" },
        { "LEFT_ELBOW", "lower_arm_l" },
        { "LEFT_WRIST", "hand_l" },
        { "RIGHT_SHOULDER", "upper_arm_r" },
        { "RIGHT_ELBOW", "lower_arm_r" },
        { "RIGHT_WRIST", "hand_r" },
        
        // 腿部骨骼映射
        { "LEFT_HIP", "upper_leg_l" },
        { "LEFT_KNEE", "lower_leg_l" },
        { "LEFT_ANKLE", "foot_l" },
        { "RIGHT_HIP", "upper_leg_r" },
        { "RIGHT_KNEE", "lower_leg_r" },
        { "RIGHT_ANKLE", "foot_r" },
        
        // 躯干和头部
        { "NOSE", "head" },
        { "CHEST_MID", "chest" }
    };

    public EnhancedSpineControlServer(ISpineViewer viewer)
    {
        _viewer = viewer;
    }

    public async Task StartAsync()
    {
        while (isRunning)
        {
            using (var server = new NamedPipeServerStream("SpineControlPipe"))
            {
                await server.WaitForConnectionAsync();
                
                byte[] buffer = new byte[4096];
                int bytesRead = await server.ReadAsync(buffer, 0, buffer.Length);
                string json = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                
                string response = ProcessCommand(json);
                byte[] responseBytes = Encoding.UTF8.GetBytes(response);
                await server.WriteAsync(responseBytes, 0, responseBytes.Length);
            }
        }
    }

    private string ProcessCommand(string json)
    {
        try
        {
            var cmd = JsonConvert.DeserializeObject<SpineCommand>(json);
            
            switch (cmd.CommandType)
            {
                case "get_structure":
                    return GetSkeletonStructure();
                    
                case "control_bone":
                    return ControlBone(cmd);
                    
                case "set_animation":
                    return SetAnimation(cmd);
                    
                case "set_skin":
                    return SetSkin(cmd);
                    
                case "update_pose_from_mediapipe":
                    return UpdatePoseFromMediaPipe(cmd);
                    
                default:
                    return JsonConvert.SerializeObject(new
                    {
                        status = "error",
                        message = $"Unknown command type: {cmd.CommandType}"
                    });
            }
        }
        catch (Exception ex)
        {
            return JsonConvert.SerializeObject(new
            {
                status = "error",
                message = $"Command processing failed: {ex.Message}"
            });
        }
    }

    private string UpdatePoseFromMediaPipe(SpineCommand cmd)
    {
        try
        {
            if (cmd.MediaPipeLandmarks == null || !cmd.MediaPipeLandmarks.Any())
            {
                return JsonConvert.SerializeObject(new
                {
                    status = "error",
                    message = "No MediaPipe landmarks provided"
                });
            }
            
            // 1. 更新根骨骼位置（身体中心）
            if (cmd.MediaPipeLandmarks.TryGetValue("CHEST_MID", out var chestPoint))
            {
                var rootBone = _viewer.GetBone("root");
                if (rootBone != null)
                {
                    rootBone.X = chestPoint[0];
                    rootBone.Y = chestPoint[1];
                    _viewer.UpdateBoneTransform(rootBone);
                }
            }

            // 2. 计算并应用肢体旋转
            ApplyLimbRotations(cmd.MediaPipeLandmarks);
            
            // 3. 更新骨骼物理
            _viewer.UpdateSkeletonTransforms();
            
            return JsonConvert.SerializeObject(new
            {
                status = "success",
                updated_bones = cmd.MediaPipeLandmarks.Count
            });
        }
        catch (Exception ex)
        {
            return JsonConvert.SerializeObject(new
            {
                status = "error",
                message = $"MediaPipe pose update failed: {ex.Message}"
            });
        }
    }

    // 计算并应用所有肢体旋转
    private void ApplyLimbRotations(Dictionary<string, float[]> landmarks)
    {
        // 左手臂旋转计算
        ApplyArmRotation(
            shoulderPoint: landmarks["LEFT_SHOULDER"],
            elbowPoint: landmarks["LEFT_ELBOW"],
            wristPoint: landmarks["LEFT_WRIST"],
            upperArmBoneName: "upper_arm_l",
            lowerArmBoneName: "lower_arm_l"
        );

        // 右手臂旋转计算
        ApplyArmRotation(
            shoulderPoint: landmarks["RIGHT_SHOULDER"],
            elbowPoint: landmarks["RIGHT_ELBOW"],
            wristPoint: landmarks["RIGHT_WRIST"],
            upperArmBoneName: "upper_arm_r",
            lowerArmBoneName: "lower_arm_r"
        );

        // 左腿旋转计算
        ApplyLegRotation(
            hipPoint: landmarks["LEFT_HIP"],
            kneePoint: landmarks["LEFT_KNEE"],
            anklePoint: landmarks["LEFT_ANKLE"],
            upperLegBoneName: "upper_leg_l",
            lowerLegBoneName: "lower_leg_l"
        );

        // 右腿旋转计算
        ApplyLegRotation(
            hipPoint: landmarks["RIGHT_HIP"],
            kneePoint: landmarks["RIGHT_KNEE"],
            anklePoint: landmarks["RIGHT_ANKLE"],
            upperLegBoneName: "upper_leg_r",
            lowerLegBoneName: "lower_leg_r"
        );
    }

    // 手臂旋转计算
    private void ApplyArmRotation(
        float[] shoulderPoint,
        float[] elbowPoint,
        float[] wristPoint,
        string upperArmBoneName,
        string lowerArmBoneName)
    {
        var upperArm = _viewer.GetBone(upperArmBoneName);
        var lowerArm = _viewer.GetBone(lowerArmBoneName);
        
        if (upperArm == null || lowerArm == null) return;
        
        // 计算上臂向量 (从肩部到肘部)
        float upperArmVecX = elbowPoint[0] - shoulderPoint[0];
        float upperArmVecY = elbowPoint[1] - shoulderPoint[1];
        
        // 计算上臂角度 (世界坐标系)
        float upperArmRotation = (float)(Math.Atan2(upperArmVecY, upperArmVecX) * (180 / Math.PI));
        
        // 计算前臂向量 (从肘部到手腕)
        float lowerArmVecX = wristPoint[0] - elbowPoint[0];
        float lowerArmVecY = wristPoint[1] - elbowPoint[1];
        
        // 计算前臂相对于上臂的角度
        float relativeAngle = CalculateRelativeAngle(
            parentRotation: upperArmRotation,
            childVecX: lowerArmVecX,
            childVecY: lowerArmVecY
        );
        
        // 应用旋转
        upperArm.Rotation = upperArmRotation;
        lowerArm.Rotation = relativeAngle;
        
        _viewer.UpdateBoneTransform(upperArm);
        _viewer.UpdateBoneTransform(lowerArm);
    }

    // 腿部旋转计算
    private void ApplyLegRotation(
        float[] hipPoint,
        float[] kneePoint,
        float[] anklePoint,
        string upperLegBoneName,
        string lowerLegBoneName)
    {
        ApplyArmRotation(
            shoulderPoint: hipPoint,
            elbowPoint: kneePoint,
            wristPoint: anklePoint,
            upperArmBoneName: upperLegBoneName,
            lowerArmBoneName: lowerLegBoneName
        );
    }

    // 计算子骨骼相对于父骨骼的旋转角度
    private float CalculateRelativeAngle(float parentRotation, float childVecX, float childVecY)
    {
        // 将子向量旋转到父骨骼的局部坐标系
        float cos = (float)Math.Cos(-parentRotation * Math.PI / 180);
        float sin = (float)Math.Sin(-parentRotation * Math.PI / 180);
        float rotatedX = childVecX * cos - childVecY * sin;
        float rotatedY = childVecX * sin + childVecY * cos;
        
        // 计算局部坐标系中的角度
        return (float)(Math.Atan2(rotatedY, rotatedX) * (180 / Math.PI));
    }   

    private string GetSkeletonStructure()
    {
        try
        {
            return JsonConvert.SerializeObject(new
            {
                status = "success",
                data = _viewer.GetSkeletonStructure()
            });
        }
        catch (Exception ex)
        {
            return JsonConvert.SerializeObject(new
            {
                status = "error",
                message = $"Failed to get skeleton structure: {ex.Message}"
            });
        }
    }

    private string ControlBone(SpineCommand cmd)
    {
        try
        {
            var bone = _viewer.GetBone(cmd.BoneName);
            if (bone == null)
            {
                return JsonConvert.SerializeObject(new
                {
                    status = "error",
                    message = $"Bone not found: {cmd.BoneName}"
                });
            }
            
            float x = cmd.X;
            float y = cmd.Y;
            float rotation = cmd.Rotation;
            
            if (cmd.Normalize)
            {
                // 获取骨骼长度作为归一化基准
                float boneLength = bone.Length > 0 ? bone.Length : 10f;
                
                // 位置归一化
                if (bone.Parent != null)
                {
                    bone.Parent.LocalToWorld(0, 0, out float px, out float py);
                    x = px + x * boneLength;
                    y = py + y * boneLength;
                    
                    // 转换为局部坐标
                    bone.WorldToLocal(x, y, out x, out y);
                }
                
                // 旋转归一化
                rotation = rotation * 360f;
            }
            
            // 应用变换
            bone.X = x;
            bone.Y = y;
            bone.Rotation = rotation;
            
            if (cmd.ScaleX.HasValue) bone.ScaleX = cmd.ScaleX.Value;
            if (cmd.ScaleY.HasValue) bone.ScaleY = cmd.ScaleY.Value;
            
            _viewer.UpdateBoneTransform(bone);
            _viewer.UpdateSkeletonTransforms();
            
            return JsonConvert.SerializeObject(new
            {
                status = "success",
                bone = cmd.BoneName,
                appliedValues = new
                {
                    x = bone.X,
                    y = bone.Y,
                    rotation = bone.Rotation,
                    scaleX = bone.ScaleX,
                    scaleY = bone.ScaleY
                }
            });
        }
        catch (Exception ex)
        {
            return JsonConvert.SerializeObject(new
            {
                status = "error",
                message = $"Bone control failed: {ex.Message}"
            });
        }
    }

    private string SetAnimation(SpineCommand cmd)
    {
        try
        {
            _viewer.SetAnimation(cmd.AnimationName, cmd.Loop);
            return JsonConvert.SerializeObject(new
            {
                status = "success",
                animation = cmd.AnimationName
            });
        }
        catch (Exception ex)
        {
            return JsonConvert.SerializeObject(new
            {
                status = "error",
                message = $"Animation set failed: {ex.Message}"
            });
        }
    }

    private string SetSkin(SpineCommand cmd)
    {
        try
        {
            _viewer.SetSkin(cmd.SkinName);
            return JsonConvert.SerializeObject(new
            {
                status = "success",
                skin = cmd.SkinName
            });
        }
        catch (Exception ex)
        {
            return JsonConvert.SerializeObject(new
            {
                status = "error",
                message = $"Skin set failed: {ex.Message}"
            });
        }
    }
}

public class SpineCommand
{
    public string CommandType { get; set; }
    public string BoneName { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Rotation { get; set; }
    public float? ScaleX { get; set; }
    public float? ScaleY { get; set; }
    public bool Normalize { get; set; } = true;
    public string AnimationName { get; set; }
    public bool Loop { get; set; } = true;
    public string SkinName { get; set; }
    
    // MediaPipe数据接口
    public Dictionary<string, float[]> MediaPipeLandmarks { get; set; }
}