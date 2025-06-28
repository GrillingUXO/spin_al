// ISpineViewer.cs
using System.Collections.Generic;

public interface ISpineViewer
{
    IBone GetBone(string boneName);
    void UpdateBoneTransform(IBone bone);
    void UpdateSkeletonTransforms();
    void SetAnimation(string animationName, bool loop);
    void SetSkin(string skinName);
    
    SkeletonStructure GetSkeletonStructure();
}

public interface IBone
{
    string Name { get; }
    IBone Parent { get; }
    float Length { get; }
    float X { get; set; }
    float Y { get; set; }
    float Rotation { get; set; }
    float ScaleX { get; set; }
    float ScaleY { get; set; }
    void UpdateWorldTransform();
    void LocalToWorld(float localX, float localY, out float worldX, out float worldY);
    void WorldToLocal(float worldX, float worldY, out float localX, out float localY);
}

public class SkeletonStructure
{
    public Dictionary<string, BoneInfo> Bones { get; set; }
    public Dictionary<string, string[]> Slots { get; set; }
    public List<string> Skins { get; set; }
    public List<string> Animations { get; set; }
}

public class BoneInfo
{
    public string Name { get; set; }
    public string Parent { get; set; }
    public List<string> Children { get; set; }
    public float[] Position { get; set; }
    public float Rotation { get; set; }
    public float[] Scale { get; set; }
}