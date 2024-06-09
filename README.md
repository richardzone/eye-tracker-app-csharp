[![Dotnet Version Badge](https://img.shields.io/badge/CODE-%3C.NET%208.0%3E%20%3CC%23%2012.0%3E-darkblue?style=for-the-badge&labelColor=66ccff)](https://dotnet.microsoft.com/download)
[![Build Release Status Badge](https://img.shields.io/github/actions/workflow/status/richardzone/eye-tracker-app-csharp/build-release.yml?style=for-the-badge&label=Build%20Release)](https://github.com/richardzone/eye-tracker-app-csharp/actions)

# 演示方法

1. 调整计算机分辨率和缩放比例，具体参见下图
    ![调整分辨率和缩放比例](docs/resolution_adjustment.png)
2. 关闭杀毒软件，设置鼠标指针大小为较大（看的更清楚），调整电源计划（避免计算机自动熄屏）
3. 接上视频采集卡
4. 从 [Release页面](https://github.com/richardzone/eye-tracker-app-csharp/releases) 下载 `x64-Release.zip`
5. 解压并运行其中的 `eye_tracker_app_csharp.exe`

## 命令行参数

.\eye_tracker_app_csharp.exe 1 --show-video --video-size 300 200

- `1` 代表摄像头序号，存在多个摄像头/视频采集设备时，提供该参数会直接选择对应摄像头
- `--show-video` 启用显示视频采集卡的输出，用于调试
- `--video-size 300 200` 用于调整视频输出窗口大小
- `--duration 500` 用于调整鼠标指针移动速度，如果不提供该参数则默认是瞬时移动

## 无视频演示卡测试方法

1. 执行上面1和2步骤
2. 下载模拟摄像头软件例如 [WeCam](https://www.e2esoft.cn/wecam/)
3. 将虚拟摄像头设为播放视频，视频在[这里](docs/test_arcode_video.mp4)
4. 执行上面的4和5步骤