import cv2
import mediapipe as mp
import json
import time
import numpy as np

# MediaPipe姿势检测初始化
mp_pose = mp.solutions.pose
pose = mp_pose.Pose(
    min_detection_confidence=0.5,
    min_tracking_confidence=0.5,
    model_complexity=1
)

# 导入绘图工具
mp_drawing = mp.solutions.drawing_utils
mp_drawing_styles = mp.solutions.drawing_styles

# 定义与C#服务器对应的关键点映射
MEDIAPIPE_TO_SPINE_MAPPING = {
    "LEFT_SHOULDER": mp_pose.PoseLandmark.LEFT_SHOULDER,
    "LEFT_ELBOW": mp_pose.PoseLandmark.LEFT_ELBOW,
    "LEFT_WRIST": mp_pose.PoseLandmark.LEFT_WRIST,
    "RIGHT_SHOULDER": mp_pose.PoseLandmark.RIGHT_SHOULDER,
    "RIGHT_ELBOW": mp_pose.PoseLandmark.RIGHT_ELBOW,
    "RIGHT_WRIST": mp_pose.PoseLandmark.RIGHT_WRIST,
    "LEFT_HIP": mp_pose.PoseLandmark.LEFT_HIP,
    "LEFT_KNEE": mp_pose.PoseLandmark.LEFT_KNEE,
    "LEFT_ANKLE": mp_pose.PoseLandmark.LEFT_ANKLE,
    "RIGHT_HIP": mp_pose.PoseLandmark.RIGHT_HIP,
    "RIGHT_KNEE": mp_pose.PoseLandmark.RIGHT_KNEE,
    "RIGHT_ANKLE": mp_pose.PoseLandmark.RIGHT_ANKLE,
    "NOSE": mp_pose.PoseLandmark.NOSE
}

# 虚拟画布尺寸（用于坐标缩放）
CANVAS_WIDTH = 1000
CANVAS_HEIGHT = 1000

def process_frame(image):
    """处理帧并返回MediaPipe检测结果"""
    results = pose.process(cv2.cvtColor(image, cv2.COLOR_BGR2RGB))
    return results.pose_landmarks if results.pose_landmarks else None

def convert_to_spine_coordinates(landmarks, image_shape):
    """将MediaPipe关键点转换为Spine服务器所需的格式"""
    spine_points = {}
    
    # 提取所需关键点
    for spine_name, mediapipe_idx in MEDIAPIPE_TO_SPINE_MAPPING.items():
        landmark = landmarks.landmark[mediapipe_idx]
        # 转换为绝对坐标（翻转Y轴并缩放到虚拟画布）
        x = landmark.x * CANVAS_WIDTH
        y = (1 - landmark.y) * CANVAS_HEIGHT  # 翻转Y轴
        spine_points[spine_name] = [x, y]
    
    # 计算胸部中心点（左右肩膀的中点）
    if "LEFT_SHOULDER" in spine_points and "RIGHT_SHOULDER" in spine_points:
        left_shoulder = spine_points["LEFT_SHOULDER"]
        right_shoulder = spine_points["RIGHT_SHOULDER"]
        chest_mid = [
            (left_shoulder[0] + right_shoulder[0]) / 2,
            (left_shoulder[1] + right_shoulder[1]) / 2
        ]
        spine_points["CHEST_MID"] = chest_mid
    
    return spine_points

def send_to_spine_server(landmarks):
    """构造并发送姿态数据到Spine服务器"""
    command = {
        "CommandType": "update_pose_from_mediapipe",
        "MediaPipeLandmarks": landmarks
    }
    
    try:
        # 打印传输的JSON数据
        json_data = json.dumps(command, indent=2)
        print("Sending JSON to Spine server:")
        print(json_data)
        
        # 在Windows上使用命名管道通信
        with open(r'\\.\pipe\SpineControlPipe', 'w') as pipe:
            pipe.write(json_data)
            pipe.flush()
        return True
    except Exception as e:
        print(f"管道通信错误: {e}")
        return False

def main():
    cap = cv2.VideoCapture(0)  # 打开默认摄像头
    
    while cap.isOpened():
        success, image = cap.read()
        if not success:
            continue
        
        # 创建黑色背景
        black_image = np.zeros(image.shape, dtype=np.uint8)
        
        # 处理帧并获取关键点
        landmarks = process_frame(image)
        
        if landmarks:
            # 在黑色背景上绘制骨骼
            mp_drawing.draw_landmarks(
                black_image,
                landmarks,
                mp_pose.POSE_CONNECTIONS,
                landmark_drawing_spec=mp_drawing_styles.get_default_pose_landmarks_style()
            )
            
            # 转换坐标格式
            spine_points = convert_to_spine_coordinates(landmarks, image.shape)
            
            # 发送到Spine服务器
            if send_to_spine_server(spine_points):
                # 在图像上显示状态
                cv2.putText(black_image, "Sending to Spine", (10, 30),
                           cv2.FONT_HERSHEY_SIMPLEX, 1, (0, 255, 0), 2)
        
        # 显示结果（黑色背景上的骨骼）
        cv2.imshow('MediaPipe Spine Control - Skeleton View', black_image)
        
        # 退出条件
        if cv2.waitKey(5) & 0xFF == 27:
            break
    
    # 清理资源
    cap.release()
    cv2.destroyAllWindows()

if __name__ == "__main__":
    main()