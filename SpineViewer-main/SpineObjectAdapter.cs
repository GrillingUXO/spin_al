// SpineObjectAdapter.cs
using System.Collections.Generic;
using System.Linq;

public class SpineObjectAdapter : ISpineViewer
{
    private readonly SpineObject _spineObject;

    public SpineObjectAdapter(SpineObject spineObject)
    {
        _spineObject = spineObject;
    }

    public IBone GetBone(string boneName)
    {
        return _spineObject.Skeleton.Bones.TryGetValue(boneName, out var bone) ? 
            new BoneAdapter(bone) : null;
    }

    public void UpdateBoneTransform(IBone bone)
    {
        if (bone is BoneAdapter adapter)
        {
            adapter.Bone.UpdateWorldTransform();
        }
    }

    public void UpdateSkeletonTransforms()
    {
        _spineObject.Skeleton.UpdateWorldTransform();
    }

    public void SetAnimation(string animationName, bool loop)
    {
        _spineObject.AnimationState.SetAnimation(0, animationName, loop);
    }

    public void SetSkin(string skinName)
    {
        foreach (var skin in _spineObject.Data.Skins)
        {
            _spineObject.SetSkinStatus(skin.Name, skin.Name == skinName);
        }
        _spineObject.ReloadSkins();
    }

    public SkeletonStructure GetSkeletonStructure()
    {
        var structure = new SkeletonStructure
        {
            Bones = new Dictionary<string, BoneInfo>(),
            Slots = new Dictionary<string, string[]>(),
            Skins = new List<string>(),
            Animations = new List<string>()
        };

        // 获取骨骼信息
        foreach (var bone in _spineObject.Skeleton.Bones.Values)
        {
            structure.Bones[bone.Data.Name] = new BoneInfo
            {
                Name = bone.Data.Name,
                Parent = bone.Parent?.Data.Name,
                Children = GetChildrenNames(bone),
                Position = new float[] { bone.X, bone.Y },
                Rotation = bone.Rotation,
                Scale = new float[] { bone.ScaleX, bone.ScaleY }
            };
        }

        // 获取插槽和附件
        foreach (var slot in _spineObject.Skeleton.Slots)
        {
            var attachments = new List<string>();
            if (_spineObject.Data.SlotAttachments.TryGetValue(slot.Data.Name, out var slotAttach))
            {
                attachments.AddRange(slotAttach.Keys);
            }
            structure.Slots[slot.Data.Name] = attachments.ToArray();
        }

        // 皮肤
        foreach (var skin in _spineObject.Data.Skins)
        {
            if (skin.Name != "default") structure.Skins.Add(skin.Name);
        }

        // 动画信息
        foreach (var anim in _spineObject.Data.Animations)
        {
            structure.Animations.Add(anim.Name);
        }

        return structure;
    }

    private List<string> GetChildrenNames(IBone bone)
    {
        var children = new List<string>();
        foreach (var child in bone.Children)
        {
            children.Add(child.Data.Name);
        }
        return children;
    }

    private class BoneAdapter : IBone
    {
        public IBone Bone { get; }

        public BoneAdapter(IBone bone)
        {
            Bone = bone;
        }

        public string Name => Bone.Data.Name;
        public IBone Parent => Bone.Parent;
        public float Length => Bone.Data.Length;
        public float X { get => Bone.X; set => Bone.X = value; }
        public float Y { get => Bone.Y; set => Bone.Y = value; }
        public float Rotation { get => Bone.Rotation; set => Bone.Rotation = value; }
        public float ScaleX { get => Bone.ScaleX; set => Bone.ScaleX = value; }
        public float ScaleY { get => Bone.ScaleY; set => Bone.ScaleY = value; }

        public void UpdateWorldTransform() => Bone.UpdateWorldTransform();
        
        public void LocalToWorld(float localX, float localY, out float worldX, out float worldY) 
            => Bone.LocalToWorld(localX, localY, out worldX, out worldY);
        
        public void WorldToLocal(float worldX, float worldY, out float localX, out float localY) 
            => Bone.WorldToLocal(worldX, worldY, out localX, out localY);
    }
}